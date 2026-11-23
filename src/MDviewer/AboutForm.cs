using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using MDviewer.UI;
using MdIcon = MDviewer.UI.Icon;

namespace MDviewer
{
    /// <summary>Simple, themed "About" dialog describing the app and its features.</summary>
    public sealed class AboutForm : Form
    {
        private const int LW = 560;
        private const int LH = 596;
        private const string RepoUrl = "https://github.com/Anyiran/MDviewer";
        private const string WebView2Url = "https://developer.microsoft.com/microsoft-edge/webview2/";

        private readonly string _version;
        private readonly float _scale;
        private Rectangle _closeRect;
        private Rectangle _linkRect;
        private Rectangle _repoRect;
        private Rectangle _okRect;
        private Rectangle _defaultRect;
        private bool _closeHover;
        private bool _linkHover;
        private bool _repoHover;
        private bool _okHover;
        private bool _defaultHover;

        public AboutForm()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            object[] info = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
            if (info != null && info.Length > 0)
            {
                _version = ((AssemblyInformationalVersionAttribute)info[0]).InformationalVersion;
            }
            else
            {
                Version v = asm.GetName().Version;
                _version = string.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build);
            }

            using (Graphics g = CreateGraphics())
                _scale = Math.Max(1f, g.DpiX / 96f);

            // Never let the dialog overflow small screens (e.g. 1366x768 at 125%).
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            float fit = Math.Min((work.Width - 24f) / LW, (work.Height - 24f) / LH);
            if (fit > 0 && fit < _scale)
                _scale = Math.Max(0.6f, fit);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            ClientSize = new Size((int)Math.Round(LW * _scale), (int)Math.Round(LH * _scale));
            DoubleBuffered = true;
            BackColor = Theme.Current.WindowBg;
            Font = new Font("Segoe UI", 9f);
            Text = "关于 MDviewer";

            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private Point ToLogical(Point p)
        {
            return new Point((int)Math.Round(p.X / _scale), (int)Math.Round(p.Y / _scale));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(_scale, _scale);

            using (var bg = new SolidBrush(t.WindowBg))
                g.FillRectangle(bg, 0, 0, LW, LH);

            using (var pen = new Pen(t.Border))
                g.DrawRectangle(pen, 0, 0, LW - 1, LH - 1);

            int pad = 28;
            int y = 20;

            // App mark
            Rectangle logo = new Rectangle(pad, y, 52, 52);
            Bitmap artwork = AppResources.GetLogoBitmap(52);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(artwork, logo);

            // Title
            int textX = logo.Right + 18;
            using (var font = new Font("Segoe UI Semibold", 19f, FontStyle.Regular))
            using (var brush = new SolidBrush(t.Text))
                g.DrawString("MDviewer", font, brush, textX, y + 1);

            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(t.TextMuted))
                g.DrawString("版本 " + _version + "  ·  MIT License", font, brush, textX + 2, y + 34);

            y = logo.Bottom + 20;

            // Close button (top-right)
            _closeRect = new Rectangle(LW - 44, 14, 30, 30);
            if (_closeHover)
                Ui.Fill(this, g, Theme.Blend(t.Hover, t.Danger, 0.12), _closeRect, 6);
            Icons.Draw(g, MdIcon.Close, new Rectangle(_closeRect.X + 8, _closeRect.Y + 8, 14, 14),
                _closeHover ? t.Danger : t.TextMuted);

            // Description
            using (var font = new Font("Segoe UI", 10f))
            using (var brush = new SolidBrush(t.Text))
                g.DrawString("一个轻量、顺手的 Markdown 阅读器，完全离线运行。", font, brush, pad, y);
            y += 34;

            // Feature rows
            string[][] features = new string[][]
            {
                new[] { "可折叠侧边栏", "自动提取章节标题，点击即可跳转定位，滚动时实时高亮当前章节。" },
                new[] { "日间 / 夜间模式", "一键切换，配色参照 GitHub，长时间阅读更舒适。" },
                new[] { "字体缩放", "工具栏按钮、Ctrl + 滚轮或 Ctrl +/- 调整正文大小，并自动记忆。" },
                new[] { "GitHub 风格排版", "表格、任务列表、引用、代码高亮等常见语法完整支持。" },
                new[] { "右键手势", "按住鼠标右键向上 / 向下拖动，快速回到文档开头或结尾。" },
                new[] { "广泛兼容", "基于 .NET Framework + WebView2，支持 Windows 7 SP1 及更高版本。" }
            };

            foreach (string[] f in features)
            {
                using (var dot = new SolidBrush(t.Accent))
                    g.FillEllipse(dot, pad + 1, y + 7, 6, 6);

                using (var font = new Font("Segoe UI Semibold", 9.5f))
                using (var brush = new SolidBrush(t.Text))
                    g.DrawString(f[0], font, brush, pad + 16, y);

                using (var font = new Font("Segoe UI", 8.5f))
                using (var brush = new SolidBrush(t.TextMuted))
                {
                    RectangleF r = new RectangleF(pad + 16, y + 18, LW - pad * 2 - 16, 24);
                    g.DrawString(f[1], font, brush, r);
                }
                y += 48;
            }

            // Divider
            using (var pen = new Pen(t.Border))
                g.DrawLine(pen, pad, y + 4, LW - pad, y + 4);
            y += 20;

            using (var font = new Font("Segoe UI", 8.5f))
            using (var brush = new SolidBrush(t.TextMuted))
            {
                g.DrawString("作者：Anyiran    界面：.NET Framework WinForms", font, brush, pad, y);
                y += 20;
                g.DrawString("渲染内核：Microsoft Edge WebView2", font, brush, pad, y);
                y += 28;
            }

            _repoRect = new Rectangle(pad, y, 320, 20);
            using (var font = new Font("Segoe UI", 8.75f, FontStyle.Underline))
            using (var brush = new SolidBrush(_repoHover ? t.AccentHover : t.Accent))
                g.DrawString("GitHub：github.com/Anyiran/MDviewer  ›", font, brush, _repoRect.X, _repoRect.Y);
            y += 24;

            _linkRect = new Rectangle(pad, y, 240, 20);
            using (var font = new Font("Segoe UI", 8.75f, FontStyle.Underline))
            using (var brush = new SolidBrush(_linkHover ? t.AccentHover : t.Accent))
                g.DrawString("获取 WebView2 运行时  ›", font, brush, _linkRect.X, _linkRect.Y);

            // Default-app button (bottom, outline style)
            Rectangle def = new Rectangle(LW - pad - 88 - 10 - 192, LH - 48, 192, 30);
            if (_defaultHover)
                Ui.Fill(this, g, Theme.Blend(t.WindowBg, t.Accent, 0.10), def, 6);
            Ui.Stroke(this, g, t.BorderStrong, def, 6, 1f);
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(t.Text))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("设为默认 Markdown 应用", font, brush, def, sf);
            }
            _defaultRect = def;

            // Close button (bottom)
            Rectangle ok = new Rectangle(LW - pad - 88, LH - 48, 88, 30);
            Color btn = _okHover ? t.AccentHover : t.Accent;
            Ui.Fill(this, g, btn, ok, 6);
            using (var font = new Font("Segoe UI Semibold", 9f))
            using (var brush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("关闭", font, brush, ok, sf);
            }
            _okRect = ok;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = ToLogical(e.Location);
            bool ch = _closeRect.Contains(p);
            bool lh = _linkRect.Contains(p);
            bool rh = _repoRect.Contains(p);
            bool oh = _okRect.Contains(p);
            bool dh = _defaultRect.Contains(p);
            if (ch != _closeHover || lh != _linkHover || rh != _repoHover || oh != _okHover || dh != _defaultHover)
            {
                _closeHover = ch;
                _linkHover = lh;
                _repoHover = rh;
                _okHover = oh;
                _defaultHover = dh;
                Cursor = (ch || lh || rh || oh || dh) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _closeHover = _linkHover = _repoHover = _okHover = _defaultHover = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            Point p = ToLogical(e.Location);
            if (_closeRect.Contains(p)) { Close(); return; }
            if (_okRect.Contains(p)) { Close(); return; }
            if (_defaultRect.Contains(p)) { SetDefaultApp(); return; }
            if (_repoRect.Contains(p)) { OpenUrl(RepoUrl); return; }
            if (_linkRect.Contains(p)) { OpenUrl(WebView2Url); return; }

            // Drag the dialog by its background.
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close();
        }

        private void SetDefaultApp()
        {
            string message;
            try { message = MDviewer.Core.FileAssociation.Register(true); }
            catch (Exception ex) { message = "设置失败：" + ex.Message; }

            MessageBox.Show(this, message, "默认打开方式",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
