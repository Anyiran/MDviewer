using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using MDviewer.UI;

namespace MDviewer.Core
{
    /// <summary>
    /// Registers MDviewer as a Markdown handler for the current user.
    /// Windows 8+ does not allow silently taking over an existing file association,
    /// so we register the ProgId/capabilities and open the Default Apps settings.
    /// Windows 7 honours a per-user association written directly to HKCU.
    /// </summary>
    public static class FileAssociation
    {
        public const string ProgId = "MDviewer.Markdown";
        public const string AppName = "MDviewer";

        public static readonly string[] Extensions =
            new string[] { ".md", ".markdown", ".mdown", ".mkd" };

        /// <summary>Registers the app and returns a user-facing status message.</summary>
        public static string Register(bool openSettings)
        {
            string exe = Application.ExecutablePath;
            string openWith = "\"" + exe + "\" \"%1\"";
            string icon = "\"" + exe + "\",0";
            string appKeyName = Path.GetFileName(exe); // e.g. MDviewer.exe

            using (RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
            {
                if (classes == null) return "无法写入注册表，请检查权限。";

                // ProgId: document type + icon + open command.
                using (RegistryKey prog = classes.CreateSubKey(ProgId))
                {
                    prog.SetValue(null, "Markdown 文档", RegistryValueKind.String);
                    prog.SetValue("FriendlyTypeName", "Markdown 文档", RegistryValueKind.String);
                    using (RegistryKey iconKey = prog.CreateSubKey("DefaultIcon"))
                        iconKey.SetValue(null, icon, RegistryValueKind.String);
                    using (RegistryKey cmd = prog.CreateSubKey(@"shell\open\command"))
                        cmd.SetValue(null, openWith, RegistryValueKind.String);
                }

                foreach (string ext in Extensions)
                {
                    // "Open with" list entry.
                    using (RegistryKey owp = classes.CreateSubKey(ext + @"\OpenWithProgids"))
                        owp.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                }

                // Applications\MDviewer.exe (enables Open With + Default Programs list).
                using (RegistryKey app = classes.CreateSubKey(@"Applications\" + appKeyName))
                {
                    app.SetValue("FriendlyAppName", AppName, RegistryValueKind.String);
                    using (RegistryKey iconKey = app.CreateSubKey("DefaultIcon"))
                        iconKey.SetValue(null, icon, RegistryValueKind.String);
                    using (RegistryKey cmd = app.CreateSubKey(@"shell\open\command"))
                        cmd.SetValue(null, openWith, RegistryValueKind.String);
                    using (RegistryKey supported = app.CreateSubKey("SupportedTypes"))
                    {
                        foreach (string ext in Extensions)
                            supported.SetValue(ext, "", RegistryValueKind.String);
                    }
                }

                // Windows 7: per-user default association still works.
                if (IsWindows7OrEarlier())
                {
                    foreach (string ext in Extensions)
                    {
                        using (RegistryKey extKey = classes.CreateSubKey(ext))
                            extKey.SetValue(null, ProgId, RegistryValueKind.String);
                    }
                }
            }

            // Default Programs capabilities.
            using (RegistryKey caps = Registry.CurrentUser.CreateSubKey(@"Software\" + AppName + @"\Capabilities"))
            {
                if (caps != null)
                {
                    caps.SetValue("ApplicationName", AppName, RegistryValueKind.String);
                    caps.SetValue("ApplicationDescription",
                        "轻量、离线的 GitHub 风格 Markdown 阅读器", RegistryValueKind.String);
                    caps.SetValue("ApplicationIcon", icon, RegistryValueKind.String);
                    using (RegistryKey fa = caps.CreateSubKey("FileAssociations"))
                    {
                        foreach (string ext in Extensions)
                            fa.SetValue(ext, ProgId, RegistryValueKind.String);
                    }
                }
            }

            using (RegistryKey registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                if (registered != null)
                    registered.SetValue(AppName, @"Software\" + AppName + @"\Capabilities",
                        RegistryValueKind.String);
            }

            NotifyShell();

            if (IsWindows7OrEarlier())
            {
                return "已将 MDviewer 设置为 .md / .markdown / .mdown / .mkd 文件的默认打开方式。";
            }

            if (openSettings) OpenDefaultAppsSettings();
            return "已注册为 Markdown 文件的候选应用。\n\n" +
                   "Windows 8 及更高版本出于安全限制，不允许程序直接修改默认应用。\n" +
                   "已在“默认应用”设置页面中为你打开相关选项，请在 “.md” 一栏选择 MDviewer。";
        }

        public static string Unregister()
        {
            try
            {
                using (RegistryKey classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true))
                {
                    if (classes != null)
                    {
                        foreach (string ext in Extensions)
                        {
                            using (RegistryKey owp = classes.OpenSubKey(ext + @"\OpenWithProgids", true))
                            {
                                if (owp != null) owp.DeleteValue(ProgId, false);
                            }
                        }
                        DeleteSubKeyTreeSafe(classes, ProgId);
                        DeleteSubKeyTreeSafe(classes, @"Applications\MDviewer.exe");
                    }
                }

                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
                {
                    if (reg != null) reg.DeleteValue(AppName, false);
                }
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + AppName, false);
                NotifyShell();
                return "已移除 MDviewer 的文件关联注册信息。";
            }
            catch (Exception ex)
            {
                return "移除失败：" + ex.Message;
            }
        }

        private static void DeleteSubKeyTreeSafe(RegistryKey parent, string name)
        {
            try
            {
                parent.DeleteSubKeyTree(name, false);
            }
            catch { }
        }

        /// <summary>Opens the Windows "Default apps" page (best effort).</summary>
        public static void OpenDefaultAppsSettings()
        {
            try
            {
                // Windows 11 supports deep-linking to a registered app; fall back to
                // the general page when this exact URI is unavailable.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:defaultapps",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static bool IsWindows7OrEarlier()
        {
            return Environment.OSVersion.Version.Major == 6 &&
                   Environment.OSVersion.Version.Minor <= 1;
        }

        private static void NotifyShell()
        {
            try
            {
                Native.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }
    }
}
