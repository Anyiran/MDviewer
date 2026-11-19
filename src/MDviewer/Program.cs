using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MDviewer.UI;

namespace MDviewer
{
    internal static class Program
    {
        private const string MutexName = "MDviewer.SingleInstance.Mutex";
        private const string PipeName = "MDviewer.InstancePipe";

        [STAThread]
        private static void Main(string[] args)
        {
            ConfigureNativeLoader();

            string initialPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-", StringComparison.Ordinal))
                {
                    initialPath = args[i];
                    break;
                }
            }

            // Integration helpers used by the installer and manual setup.
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--register" || a == "--register-silent")
                {
                    string msg = Core.FileAssociation.Register(a == "--register");
                    if (a == "--register")
                        MessageBox.Show(msg, "MDviewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (a == "--unregister" || a == "--unregister-silent")
                {
                    string msg = Core.FileAssociation.Unregister();
                    if (a == "--unregister")
                        MessageBox.Show(msg, "MDviewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    ForwardToRunningInstance(initialPath);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                var form = new MainForm(initialPath);
                StartPipeServer(form);

                try
                {
                    Application.Run(form);
                }
                finally
                {
                    GC.KeepAlive(mutex);
                }
            }
        }

        /// <summary>
        /// Points the native loader at the correct WebView2Loader.dll for this process
        /// bitness, so a single AnyCPU build runs on both 32-bit and 64-bit Windows.
        /// </summary>
        private static void ConfigureNativeLoader()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string arch = Environment.Is64BitProcess ? "x64" : "x86";
                string candidate = Path.Combine(baseDir, "WebView2Loader", arch);
                if (Directory.Exists(candidate))
                {
                    Native.SetDllDirectory(candidate);
                }
            }
            catch { }
        }

        private static void ForwardToRunningInstance(string path)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(1200);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(path ?? "");
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // The other instance may still be starting up. Nothing more we can do.
            }
        }

        private static void StartPipeServer(MainForm form)
        {
            var thread = new Thread(delegate()
            {
                while (!form.IsDisposed)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(
                            PipeName, PipeDirection.In, 1,
                            PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            using (var reader = new StreamReader(server, new UTF8Encoding(false)))
                            {
                                string line = reader.ReadLine();
                                if (!string.IsNullOrEmpty(line))
                                {
                                    try { form.OpenFromExternalInstance(line); }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch
                    {
                        Thread.Sleep(300);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowError(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowError(e.ExceptionObject as Exception);
        }

        private static void ShowError(Exception ex)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDviewer");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                    (ex == null ? "null" : ex.ToString()) + "\r\n\r\n");
            }
            catch { }

            try
            {
                MessageBox.Show(
                    "MDviewer 遇到一个错误：\n\n" + (ex == null ? "未知错误" : ex.Message),
                    "MDviewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
