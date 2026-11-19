using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MDviewer.Core;
using MDviewer.UI;
using MdIcon = MDviewer.UI.Icon;

namespace MDviewer
{
    public sealed class MainForm : Form
    {
        // ------------------------------------------------------------- state

        private readonly Settings _settings;
        private readonly DocumentRenderer _renderer = new DocumentRenderer();

        private string _currentPath;
        private List<Heading> _headings = new List<Heading>();
        private bool _webReady;
        private bool _pageReady;
        private bool _sidebarVisible;
        private int _sidebarLogicalWidth;
        private double _pendingScrollY;
        private string _pendingAnchor;

        private static readonly double[] ZoomSteps = new double[]
        {
            0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.25, 1.4, 1.6, 1.8, 2.0, 2.25, 2.5, 2.75, 3.0
        };
        private int _zoomIndex = 4;

        private string _lastFindText;
        private bool _findOpen;

        private readonly string _cacheDir;
        private readonly string _webRoot;
        private readonly string _userDataDir;

        // --------------------------------------------------------- controls

        private readonly TitleBar _titleBar = new TitleBar();
        private readonly Panel _sidebar = new Panel();
        private readonly Panel _sideHeader = new Panel();
        private readonly Label _sideTitle = new Label();
        private readonly ChromeButton _sideCollapse = new ChromeButton();
        private readonly OutlineList _outline = new OutlineList();
        private readonly Panel _splitter = new Panel();
        private readonly Panel _content = new Panel();
        private readonly WebView2 _web = new WebView2();
        private readonly FindBar _findBar = new FindBar();
        private readonly StatusBarView _status = new StatusBarView();

        private ChromeButton _btnSidebar;
        private ChromeButton _btnZoomOut;
        private ChromeButton _btnZoomLabel;
        private ChromeButton _btnZoomIn;
        private ChromeButton _btnTheme;
        private ChromeButton _btnReload;
        private ChromeButton _btnFind;
        private ChromeButton _btnAbout;
        private ChromeButton _btnOpen;
        private ChromeButton _btnMin;
        private ChromeButton _btnMax;
        private ChromeButton _btnClose;

        private bool _draggingSidebar;
        private int _dragStartX;
        private int _dragStartWidth;
        private bool _uiReady;

        // ---------------------------------------------------------- lifecycle

        public MainForm(string initialPath)
        {
            _settings = Settings.Load();
            Theme.Current = Theme.For(_settings.Theme == "dark" ? ThemeMode.Dark : ThemeMode.Light);
            _sidebarVisible = _settings.SidebarVisible;
            _sidebarLogicalWidth = Math.Max(180, Math.Min(520, _settings.SidebarWidth));
            _zoomIndex = NearestZoomIndex(_settings.FontScale);

            _cacheDir = Path.Combine(LocalAppData(), "MDviewer", "cache");
            _userDataDir = Path.Combine(LocalAppData(), "MDviewer", "WebView2");
            _webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web");
            try
            {
                if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                if (!Directory.Exists(_userDataDir)) Directory.CreateDirectory(_userDataDir);
            }
            catch { }

            SetupWindow();
            BuildUi();
            ApplyTheme();
            RestoreWindowBounds();

            Shown += delegate
            {
                InitializeWebViewAsync(initialPath);
            };
        }

        private static string LocalAppData()
        {
            string p = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(p)) p = Path.GetTempPath();
            return p;
        }

        private void SetupWindow()
        {
            Text = "MDviewer";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            MinimumSize = new Size(640, 420);
            AllowDrop = true;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f);
            BackColor = Theme.Current.WindowBg;
            Icon = AppResources.CreateAppIcon();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        private void RestoreWindowBounds()
        {
            int w = Math.Max(640, _settings.WindowWidth);
            int h = Math.Max(420, _settings.WindowHeight);

            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            int x = _settings.WindowX;
            int y = _settings.WindowY;
            if (x == int.MinValue || y == int.MinValue ||
                x < virtualScreen.Left - 100 || y < virtualScreen.Top - 20 ||
                x + 100 > virtualScreen.Right || y + 50 > virtualScreen.Bottom)
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(
                    work.Left + Math.Max(0, (work.Width - w) / 2),
                    work.Top + Math.Max(0, (work.Height - h) / 2));
                Size = new Size(w, h);
            }
            else
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(x, y);
                Size = new Size(w, h);
            }

            if (_settings.WindowMaximized)
                WindowState = FormWindowState.Maximized;
        }

        private void BuildUi()
        {
            // -------- title bar buttons
            _btnSidebar = MakeIconButton(MdIcon.Sidebar, "侧边栏（Ctrl+B）", true);
            _btnSidebar.Toggled = _sidebarVisible;
            _btnSidebar.Click += delegate { ToggleSidebar(); };

            _btnZoomOut = new ChromeButton();
            _btnZoomOut.Caption = "−";
            _btnZoomOut.IconSize = 0;
            _btnZoomOut.CaptionFont = new Font("Segoe UI Semibold", 13f);
            _btnZoomOut.Size = new Size(Ui.S(this, 34), Ui.S(this, 30));
            var zoomOutTip = new ToolTip();
            zoomOutTip.SetToolTip(_btnZoomOut, "缩小字号（Ctrl+-）");
            _btnZoomOut.Click += delegate { ZoomBy(-1); };

            _btnZoomLabel = new ChromeButton();
            _btnZoomLabel.Caption = "100%";
            _btnZoomLabel.IconSize = 0;
            _btnZoomLabel.Size = new Size(Ui.S(this, 52), Ui.S(this, 28));
            _btnZoomLabel.Font = new Font("Segoe UI", 8.5f);
            var zoomTip = new ToolTip();
            zoomTip.SetToolTip(_btnZoomLabel, "重置为 100%（Ctrl+0）");
            _btnZoomLabel.Click += delegate { ResetZoom(); };

            _btnZoomIn = new ChromeButton();
            _btnZoomIn.Caption = "+";
            _btnZoomIn.IconSize = 0;
            _btnZoomIn.CaptionFont = new Font("Segoe UI Semibold", 13f);
            _btnZoomIn.Size = new Size(Ui.S(this, 34), Ui.S(this, 30));
            var zoomInTip = new ToolTip();
            zoomInTip.SetToolTip(_btnZoomIn, "放大字号（Ctrl++）");
            _btnZoomIn.Click += delegate { ZoomBy(1); };

            _btnTheme = MakeIconButton(MdIcon.Moon, "切换夜间模式（Ctrl+T）", true);
            _btnTheme.Click += delegate { ToggleTheme(); };

            _btnReload = MakeIconButton(MdIcon.Reload, "重新载入（F5）", false);
            _btnReload.Click += delegate { ReloadCurrent(true); };

            _btnFind = MakeIconButton(MdIcon.Find, "在文档中查找（Ctrl+F）", true);
            _btnFind.Click += delegate { ShowFind(); };

            _btnAbout = MakeIconButton(MdIcon.Info, "关于 MDviewer（F1）", false);
            _btnAbout.Click += delegate { ShowAbout(); };

            _btnOpen = new ChromeButton();
            _btnOpen.Caption = "打开";
            _btnOpen.Icon = MdIcon.Folder;
            _btnOpen.IconSize = 15;
            _btnOpen.Size = new Size(Ui.S(this, 74), Ui.S(this, 28));
            _btnOpen.Font = new Font("Segoe UI", 9f);
            var openTip = new ToolTip();
            openTip.SetToolTip(_btnOpen, "打开 Markdown 文件（Ctrl+O）");
            _btnOpen.Click += delegate { ShowOpenDialog(); };

            _btnMin = MakeWindowButton(MdIcon.Minimize, "最小化");
            _btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };

            _btnMax = MakeWindowButton(MdIcon.Maximize, "最大化");
            _btnMax.Click += delegate { ToggleMaximize(); };

            _btnClose = MakeWindowButton(MdIcon.Close, "关闭");
            _btnClose.IsCloseButton = true;
            _btnClose.Click += delegate { Close(); };

            _titleBar.Controls.AddRange(new Control[] {
                _btnSidebar, _btnZoomOut, _btnZoomLabel, _btnZoomIn,
                _btnTheme, _btnReload, _btnFind, _btnAbout, _btnOpen,
                _btnMin, _btnMax, _btnClose
            });

            // -------- sidebar
            _sidebar.BackColor = Theme.Current.SidebarBg;

            _sideHeader.BackColor = Theme.Current.SidebarBg;
            _sideHeader.Paint += OnSideHeaderPaint;
            _sideHeader.DragEnter += OnDragEnter;
            _sideHeader.DragDrop += OnDragDrop;

            _sideTitle.Text = "目录";
            _sideTitle.AutoSize = false;
            _sideTitle.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            _sideTitle.TextAlign = ContentAlignment.MiddleLeft;
            _sideTitle.BackColor = Color.Transparent;
            _sideTitle.ForeColor = Theme.Current.TextMuted;
            _sideHeader.Controls.Add(_sideTitle);

            _sideCollapse.Icon = MdIcon.Sidebar;
            _sideCollapse.IconSize = 15;
            _sideCollapse.Size = new Size(Ui.S(this, 26), Ui.S(this, 24));
            var collapseTip = new ToolTip();
            collapseTip.SetToolTip(_sideCollapse, "收起侧边栏");
            _sideCollapse.Click += delegate { ToggleSidebar(); };
            _sideHeader.Controls.Add(_sideCollapse);

            _outline.BackColor = Theme.Current.SidebarBg;
            _outline.HeadingActivated += delegate(object s, HeadingEventArgs e)
            {
                NavigateToHeading(e.Heading);
            };

            _sidebar.Controls.Add(_outline);
            _sidebar.Controls.Add(_sideHeader);

            // -------- splitter
            _splitter.BackColor = Theme.Current.Border;
            _splitter.Cursor = Cursors.SizeWE;
            _splitter.MouseDown += OnSplitterMouseDown;
            _splitter.MouseMove += OnSplitterMouseMove;
            _splitter.MouseUp += OnSplitterMouseUp;

            // -------- content / web
            _content.BackColor = Theme.Current.WindowBg;
            _web.Dock = DockStyle.None;
            _web.AllowExternalDrop = false;
            _web.DragEnter += OnDragEnter;
            _web.DragDrop += OnDragDrop;
            _web.CoreWebView2InitializationCompleted += OnWebInitialized;

            _findBar.QueryChanged += delegate
            {
                _lastFindText = null;
                RunFind(true);
            };
            _findBar.NextRequested += delegate { RunFind(true); };
            _findBar.PreviousRequested += delegate { RunFind(false); };
            _findBar.CloseRequested += delegate { HideFind(); };

            _content.Controls.Add(_findBar);
            _content.Controls.Add(_web);
            _findBar.BringToFront();

            // -------- status
            _status.ApplyTheme();

            Controls.Add(_titleBar);
            Controls.Add(_sidebar);
            Controls.Add(_splitter);
            Controls.Add(_content);
            Controls.Add(_status);

            _web.DragEnter += OnDragEnter;
            _web.DragDrop += OnDragDrop;

            UpdateZoomLabel();
            UpdateStatus();
            _uiReady = true;
            DoLayout();
        }

        private ChromeButton MakeIconButton(MdIcon icon, string tip, bool toggle)
        {
            var b = new ChromeButton();
            b.Icon = icon;
            b.IconSize = 18;
            b.Size = new Size(Ui.S(this, 34), Ui.S(this, 30));
            if (toggle) b.Radius = 6;
            var tt = new ToolTip();
            tt.SetToolTip(b, tip);
            return b;
        }

        private ChromeButton MakeWindowButton(MdIcon icon, string tip)
        {
            var b = new ChromeButton();
            b.Icon = icon;
            b.IconSize = 16;
            b.IsWindowControl = true;
            b.Radius = 0;
            b.Size = new Size(Ui.S(this, 46), Ui.S(this, Ui.BaseTitleHeight));
            var tt = new ToolTip();
            tt.SetToolTip(b, tip);
            return b;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW

                // A borderless window without these styles is ignored by Aero Snap
                // (drag-to-edge, Win+Left/Right). Keep a real thick frame/caption so
                // the shell treats the window as resizable, then hide the non-client
                // area in WM_NCCALCSIZE below.
                const int WS_CAPTION = 0x00C00000;
                const int WS_THICKFRAME = 0x00040000;
                const int WS_SYSMENU = 0x00080000;
                const int WS_MINIMIZEBOX = 0x00020000;
                const int WS_MAXIMIZEBOX = 0x00010000;
                cp.Style |= WS_CAPTION | WS_THICKFRAME | WS_SYSMENU |
                            WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                return cp;
            }
        }

        // ------------------------------------------------------------- layout

        private void DoLayout()
        {
            if (!_uiReady) return;
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int s = Math.Max(1, Ui.S(this, 1));
            int titleH = Ui.S(this, Ui.BaseTitleHeight);
            int statusH = Ui.S(this, Ui.BaseStatusHeight);
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            _titleBar.SetBounds(0, 0, w, titleH);
            _status.SetBounds(0, h - statusH, w, statusH);

            int contentH = h - titleH - statusH;
            if (contentH < 0) contentH = 0;

            int sideW = _sidebarVisible ? Ui.S(this, _sidebarLogicalWidth) : 0;
            int splitW = _sidebarVisible ? Math.Max(3, Ui.S(this, 4)) : 0;

            _sidebar.Visible = _sidebarVisible;
            _splitter.Visible = _sidebarVisible;

            if (_sidebarVisible)
            {
                _sidebar.SetBounds(0, titleH, sideW, contentH);
                _splitter.SetBounds(sideW, titleH, splitW, contentH);

                int headerH = Ui.S(this, 34);
                _sideHeader.SetBounds(0, 0, sideW, headerH);
                int pad = Ui.S(this, 4);
                _sideTitle.SetBounds(Ui.S(this, 14), 0, sideW - Ui.S(this, 54), headerH);
                _sideCollapse.SetBounds(sideW - Ui.S(this, 36), Utils.Center(headerH, _sideCollapse.Height), _sideCollapse.Width, _sideCollapse.Height);
                _outline.SetBounds(Ui.S(this, 4), headerH + Ui.S(this, 4), sideW - Ui.S(this, 8), Math.Max(0, contentH - headerH - Ui.S(this, 8)));
            }

            int contentX = sideW + splitW;
            _content.SetBounds(contentX, titleH, Math.Max(0, w - contentX), contentH);

            int findH = _findBar.Visible ? Ui.S(this, 46) : 0;
            if (findH > 0)
            {
                _findBar.SetBounds(0, 0, _content.Width, findH);
                _findBar.LayoutChildren();
            }
            _web.SetBounds(0, findH, _content.Width, Math.Max(0, _content.Height - findH));

            LayoutTitleButtons(titleH);
        }

        private void LayoutTitleButtons(int titleH)
        {
            int btnH = Ui.S(this, 30);
            int y = (titleH - btnH) / 2;
            int bw = Ui.S(this, 34);
            int ww = Ui.S(this, 46);
            int minLeft = ClientSize.Width - ww * 3;

            // Window controls, flush to the top-right corner.
            _btnClose.SetBounds(ClientSize.Width - ww, 0, ww, titleH);
            _btnMax.SetBounds(ClientSize.Width - ww * 2, 0, ww, titleH);
            _btnMin.SetBounds(ClientSize.Width - ww * 3, 0, ww, titleH);

            bool showAbout = true;
            bool showFind = true;
            bool showReload = true;
            bool showLabel = true;
            bool compactOpen = false;
            int rightEdge = Ui.S(this, 126);

            // On narrow windows progressively drop secondary buttons instead of
            // overlapping the window controls.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = Ui.S(this, 126);
                _btnSidebar.Visible = true;
                _btnSidebar.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 4);
                x += Ui.S(this, 6);

                _btnZoomOut.Visible = true;
                _btnZoomOut.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 2);

                _btnZoomLabel.Visible = showLabel;
                if (showLabel)
                {
                    _btnZoomLabel.SetBounds(x, y, Ui.S(this, 52), btnH);
                    x += Ui.S(this, 54);
                }

                _btnZoomIn.Visible = true;
                _btnZoomIn.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 8);
                x += Ui.S(this, 6);

                _btnTheme.Visible = true;
                _btnTheme.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 2);

                _btnReload.Visible = showReload;
                if (showReload) { _btnReload.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 2); }

                _btnFind.Visible = showFind;
                if (showFind) { _btnFind.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 2); }

                _btnAbout.Visible = showAbout;
                if (showAbout) { _btnAbout.SetBounds(x, y, bw, btnH); x += bw + Ui.S(this, 8); }

                int openW = compactOpen ? bw : Ui.S(this, 74);
                _btnOpen.Caption = compactOpen ? "" : "打开";
                _btnOpen.IconSize = compactOpen ? 18 : 15;
                _btnOpen.SetBounds(x, y, openW, btnH);
                x += openW + Ui.S(this, 12);

                rightEdge = x;
                if (rightEdge <= minLeft - Ui.S(this, 8)) break;

                if (!compactOpen)
                {
                    compactOpen = true;
                    continue;
                }
                if (showAbout) { showAbout = false; continue; }
                if (showFind) { showFind = false; continue; }
                if (showReload) { showReload = false; continue; }
                if (showLabel) { showLabel = false; continue; }
                break;
            }

            _titleBar.FileTextLeft = rightEdge;
            _titleBar.FileTextRight = Math.Max(rightEdge, minLeft - Ui.S(this, 12));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_uiReady)
            {
                _btnMax.Icon = WindowState == FormWindowState.Maximized
                    ? MdIcon.Restore : MdIcon.Maximize;
                _btnMax.Invalidate();
                ApplyWindowCorners();
            }
            DoLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DoLayout();
        }

        // --------------------------------------------------------------- theme

        private void ApplyTheme()
        {
            Theme t = Theme.Current;
            BackColor = t.WindowBg;
            _titleBar.Invalidate();
            _status.ApplyTheme();
            _status.Invalidate();
            _sidebar.BackColor = t.SidebarBg;
            _sideHeader.BackColor = t.SidebarBg;
            _sideHeader.Invalidate();
            _sideTitle.ForeColor = t.TextMuted;
            _outline.ApplyTheme();
            _splitter.BackColor = t.Border;
            _content.BackColor = t.WindowBg;
            _findBar.ApplyTheme();

            _btnTheme.Icon = t.IsDark ? MdIcon.Sun : MdIcon.Moon;

            foreach (Control c in _titleBar.Controls)
            {
                ChromeButton cb = c as ChromeButton;
                if (cb != null) cb.Invalidate();
            }

            try
            {
                _web.DefaultBackgroundColor = t.IsDark
                    ? Color.FromArgb(13, 17, 23)
                    : Color.White;
            }
            catch { }

            ApplyWindowCorners();

            if (_webReady)
            {
                FireScript("MDviewer.setTheme(" + DocumentRenderer.EscapeJson(t.IsDark ? "dark" : "light") + ")");
            }

            UpdateStatus();
        }

        private void ApplyWindowCorners()
        {
            try
            {
                // 2 = round, 1 = square. Keep square corners when maximized so the
                // edges sit flush against the screen work area.
                int preference = WindowState == FormWindowState.Maximized ? 1 : 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
            }
            catch { }
        }

        private void ToggleTheme()
        {
            _settings.Theme = Theme.Current.IsDark ? "light" : "dark";
            Theme.Current = Theme.For(_settings.Theme == "dark" ? ThemeMode.Dark : ThemeMode.Light);
            ApplyTheme();
            _settings.Save();
        }

        // ---------------------------------------------------------------- zoom

        private void ZoomBy(int direction)
        {
            int target = _zoomIndex + (direction > 0 ? 1 : -1);
            target = Math.Max(0, Math.Min(ZoomSteps.Length - 1, target));
            if (target == _zoomIndex) return;
            _zoomIndex = target;
            ApplyZoom();
        }

        private void ResetZoom()
        {
            _zoomIndex = NearestZoomIndex(1.0);
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            _settings.FontScale = ZoomSteps[_zoomIndex];
            UpdateZoomLabel();
            UpdateStatus();
            if (_webReady)
            {
                FireScript("MDviewer.setFontScale(" +
                    _settings.FontScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")");
            }
            _settings.Save();
        }

        private void UpdateZoomLabel()
        {
            _btnZoomLabel.Caption = Math.Round(ZoomSteps[_zoomIndex] * 100).ToString() + "%";
            _btnZoomLabel.Invalidate();
        }

        private static int NearestZoomIndex(double scale)
        {
            int best = 4;
            double bestDiff = double.MaxValue;
            for (int i = 0; i < ZoomSteps.Length; i++)
            {
                double d = Math.Abs(ZoomSteps[i] - scale);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------- sidebar

        private void ToggleSidebar()
        {
            SetSidebarVisible(!_sidebarVisible);
        }

        private void SetSidebarVisible(bool visible)
        {
            _sidebarVisible = visible;
            _settings.SidebarVisible = visible;
            _btnSidebar.Toggled = visible;
            DoLayout();
            _settings.Save();
        }

        private void OnSideHeaderPaint(object sender, PaintEventArgs e)
        {
            Theme t = Theme.Current;
            Graphics g = e.Graphics;
            using (var bg = new SolidBrush(t.SidebarBg))
                g.FillRectangle(bg, _sideHeader.ClientRectangle);
            using (var pen = new Pen(t.Border))
                g.DrawLine(pen, 0, _sideHeader.Height - 1, _sideHeader.Width, _sideHeader.Height - 1);
        }

        private void OnSplitterMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _draggingSidebar = true;
            _dragStartX = Cursor.Position.X;
            _dragStartWidth = Ui.S(this, _sidebarLogicalWidth);
            _splitter.Capture = true;
        }

        private void OnSplitterMouseMove(object sender, MouseEventArgs e)
        {
            if (!_draggingSidebar) return;
            int delta = Cursor.Position.X - _dragStartX;
            int newWidth = _dragStartWidth + delta;
            float scale = Ui.DpiScale(this);
            int logical = (int)Math.Round(newWidth / (scale <= 0 ? 1 : scale));
            if (logical < 180) logical = 180;
            if (logical > 520) logical = 520;
            if (logical == _sidebarLogicalWidth) return;
            _sidebarLogicalWidth = logical;
            DoLayout();
        }

        private void OnSplitterMouseUp(object sender, MouseEventArgs e)
        {
            if (!_draggingSidebar) return;
            _draggingSidebar = false;
            _splitter.Capture = false;
            _settings.SidebarWidth = _sidebarLogicalWidth;
            _settings.Save();
        }

        private void NavigateToHeading(Heading heading)
        {
            if (heading == null) return;
            _pendingAnchor = heading.Id;
            if (_webReady && _pageReady)
            {
                FireScript("MDviewer.scrollToId(" + DocumentRenderer.EscapeJson(heading.Id) + ")");
            }
            _web.Focus();
        }

        // ---------------------------------------------------------------- web

        private async void InitializeWebViewAsync(string initialPath)
        {
            try
            {
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                    null, _userDataDir, null);
                await _web.EnsureCoreWebView2Async(env);

                CoreWebView2 core = _web.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsSwipeNavigationEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;

                core.SetVirtualHostNameToFolderMapping(
                    DocumentRenderer.AssetHost, _webRoot, CoreWebView2HostResourceAccessKind.Allow);
                core.SetVirtualHostNameToFolderMapping(
                    DocumentRenderer.DocHost, _cacheDir, CoreWebView2HostResourceAccessKind.Allow);

                core.WebMessageReceived += OnWebMessage;
                core.NewWindowRequested += OnNewWindowRequested;
                core.NavigationCompleted += OnNavigationCompleted;

                _webReady = true;

                if (!string.IsNullOrEmpty(initialPath) && File.Exists(initialPath))
                    OpenPath(initialPath, false);
                else
                    ShowEmptyState();
            }
            catch (Exception ex)
            {
                ShowWebViewFailure(ex);
            }
        }

        private void OnWebInitialized(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            // Handled after explicit EnsureCoreWebView2Async in InitializeWebViewAsync.
        }

        private void ShowWebViewFailure(Exception ex)
        {
            string message =
                "无法初始化页面渲染组件（WebView2）。\n\n" +
                "MDviewer 需要系统安装 “Microsoft Edge WebView2 运行时”。\n" +
                "Windows 10/11 通常已自带；Windows 7 需要手动安装。\n\n" +
                "是否现在打开下载页面？\n\n" +
                "技术信息：" + ex.Message;

            DialogResult r = MessageBox.Show(this, message, "MDviewer",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://developer.microsoft.com/microsoft-edge/webview2/",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            OpenExternalUrl(e.Uri);
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;
            _pageReady = false;
        }

        private async void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json;
            try { json = e.WebMessageAsJson; }
            catch { return; }
            if (string.IsNullOrEmpty(json)) return;

            string type = JsonHelper.GetString(json, "type");
            if (string.IsNullOrEmpty(type)) return;

            switch (type)
            {
                case "ready":
                    await PushPageStateAsync();
                    break;

                case "activeHeading":
                    {
                        string id = JsonHelper.GetString(json, "id");
                        _outline.SyncActive(id ?? "");
                        break;
                    }

                case "openLink":
                    {
                        string href = JsonHelper.GetString(json, "href");
                        HandleLink(href);
                        break;
                    }

                case "zoomDelta":
                    {
                        double d = JsonHelper.GetNumber(json, "delta");
                        ZoomBy(d >= 0 ? 1 : -1);
                        break;
                    }

                case "findResult":
                    {
                        int count = (int)JsonHelper.GetNumber(json, "count");
                        int index = (int)JsonHelper.GetNumber(json, "index");
                        _findBar.SetResult(count, index);
                        break;
                    }

                case "rendered":
                    _pageReady = true;
                    break;

                case "shortcut":
                    HandleShortcut(JsonHelper.GetString(json, "action"));
                    break;
            }
        }

        private async Task PushPageStateAsync()
        {
            if (!_webReady) return;

            await ExecuteScriptAsync("MDviewer.setTheme(" +
                DocumentRenderer.EscapeJson(Theme.Current.IsDark ? "dark" : "light") + ")");
            await ExecuteScriptAsync("MDviewer.setFontScale(" +
                _settings.FontScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")");
            await ExecuteScriptAsync("MDviewer.afterRender()");

            if (!string.IsNullOrEmpty(_pendingAnchor))
            {
                await ExecuteScriptAsync("MDviewer.scrollToId(" +
                    DocumentRenderer.EscapeJson(_pendingAnchor) + ")");
                _pendingAnchor = null;
            }
            else if (_pendingScrollY > 0.5)
            {
                double y = _pendingScrollY;
                _pendingScrollY = 0;
                await ExecuteScriptAsync("window.scrollTo(0, " +
                    y.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ")");
            }

            _pageReady = true;
            UpdateStatus();
        }

        private Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                if (_webReady && _web.CoreWebView2 != null)
                    return _web.ExecuteScriptAsync(script);
            }
            catch { }
            return Task.FromResult<string>("");
        }

        private void FireScript(string script)
        {
            Task<string> t = ExecuteScriptAsync(script);
            if (t == null) return;
        }

        // ------------------------------------------------------------ documents

        public void OpenPath(string path, bool preserveScroll)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full))
                {
                    MessageBox.Show(this, "找不到文件：\n" + full, "MDviewer",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _currentPath = full;
                _settings.LastFolder = Path.GetDirectoryName(full);
                _settings.Save();
                ReloadCurrent(preserveScroll);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开文件失败：\n" + ex.Message, "MDviewer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ReloadCurrent(bool preserveScroll)
        {
            if (!_webReady || string.IsNullOrEmpty(_currentPath)) return;

            double scrollY = 0;
            if (preserveScroll && _pageReady)
            {
                try
                {
                    string result = await _web.ExecuteScriptAsync("String(window.scrollY || 0)");
                    double.TryParse(result.Trim('"'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out scrollY);
                }
                catch { }
            }

            try
            {
                UseWaitCursor = true;
                string markdown = DocumentRenderer.ReadTextFile(_currentPath);
                List<Heading> headings;
                string html = _renderer.Render(markdown, _currentPath,
                    Theme.Current.IsDark, _settings.FontScale, out headings);

                _headings = headings;
                _outline.SetHeadings(headings);

                string docPath = Path.Combine(_cacheDir, "document.html");
                File.WriteAllText(docPath, html, new UTF8Encoding(false));

                UpdateResourceMapping(Path.GetDirectoryName(_currentPath));

                _pendingScrollY = scrollY;
                _pageReady = false;
                _web.CoreWebView2.Navigate(
                    "https://" + DocumentRenderer.DocHost + "/document.html?v=" +
                    DateTime.Now.Ticks.ToString());

                _titleBar.FileName = Path.GetFileName(_currentPath);
                Text = Path.GetFileName(_currentPath) + " - MDviewer";
                _titleBar.Invalidate();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "渲染文档失败：\n" + ex.Message, "MDviewer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void ShowEmptyState()
        {
            try
            {
                string html = _renderer.RenderEmptyState(Theme.Current.IsDark, _settings.FontScale);
                string docPath = Path.Combine(_cacheDir, "document.html");
                File.WriteAllText(docPath, html, new UTF8Encoding(false));
                _headings = new List<Heading>();
                _outline.SetHeadings(_headings);
                _titleBar.FileName = "";
                _titleBar.Invalidate();
                Text = "MDviewer";
                _pageReady = false;
                _web.CoreWebView2.Navigate(
                    "https://" + DocumentRenderer.DocHost + "/document.html?v=" +
                    DateTime.Now.Ticks.ToString());
                UpdateStatus();
            }
            catch { }
        }

        private void UpdateResourceMapping(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            try
            {
                if (_webReady && _web.CoreWebView2 != null)
                {
                    _web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        DocumentRenderer.ResHost, folder, CoreWebView2HostResourceAccessKind.Allow);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------- commands

        /// <summary>Shortcuts forwarded by the page because WebView2 consumes its own keys.</summary>
        private void HandleShortcut(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            switch (action)
            {
                case "open": ShowOpenDialog(); break;
                case "sidebar": ToggleSidebar(); break;
                case "theme": ToggleTheme(); break;
                case "find": ShowFind(); break;
                case "resetZoom": ResetZoom(); break;
                case "zoomIn": ZoomBy(1); break;
                case "zoomOut": ZoomBy(-1); break;
                case "reload": ReloadCurrent(true); break;
                case "about": ShowAbout(); break;
                case "escape":
                    if (_findOpen) HideFind();
                    break;
            }
        }

        private void ShowOpenDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "打开 Markdown 文件";
                dlg.Filter =
                    "Markdown 文件 (*.md;*.markdown;*.mdown;*.mkd)|*.md;*.markdown;*.mdown;*.mkd|" +
                    "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dlg.CheckFileExists = true;
                dlg.Multiselect = false;
                if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
                    dlg.InitialDirectory = _settings.LastFolder;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                    OpenPath(dlg.FileName, false);
            }
        }

        private void ShowAbout()
        {
            using (var about = new AboutForm())
            {
                Theme.Current = Theme.For(_settings.Theme == "dark" ? ThemeMode.Dark : ThemeMode.Light);
                about.ShowDialog(this);
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private void HandleLink(string href)
        {
            if (string.IsNullOrEmpty(href)) return;

            if (href.StartsWith("#", StringComparison.Ordinal))
            {
                string id = Uri.UnescapeDataString(href.Substring(1));
                _pendingAnchor = id;
                FireScript("MDviewer.scrollToId(" + DocumentRenderer.EscapeJson(id) + ")");
                return;
            }

            if (href.StartsWith("https://" + DocumentRenderer.ResHost + "/", StringComparison.OrdinalIgnoreCase))
            {
                // Local image opened as a link: reveal in Explorer.
                RevealLocalResource(href);
                return;
            }

            if (DocumentRenderer.IsRelativeUrl(href))
            {
                string baseDir = Path.GetDirectoryName(_currentPath ?? "");
                if (!string.IsNullOrEmpty(baseDir))
                {
                    string candidate;
                    try { candidate = Path.GetFullPath(Path.Combine(baseDir, href.Replace('/', '\\'))); }
                    catch { return; }

                    if (File.Exists(candidate))
                    {
                        string ext = Path.GetExtension(candidate).ToLowerInvariant();
                        if (ext == ".md" || ext == ".markdown" || ext == ".mdown" || ext == ".mkd")
                        {
                            OpenPath(candidate, false);
                            return;
                        }
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = candidate,
                                UseShellExecute = true
                            });
                            return;
                        }
                        catch { }
                    }
                }
                return;
            }

            OpenExternalUrl(href);
        }

        private void RevealLocalResource(string href)
        {
            try
            {
                string rel = href.Substring(("https://" + DocumentRenderer.ResHost + "/").Length);
                int q = rel.IndexOfAny(new[] { '?', '#' });
                if (q >= 0) rel = rel.Substring(0, q);
                string decoded = Uri.UnescapeDataString(rel).Replace('/', '\\');
                string baseDir = Path.GetDirectoryName(_currentPath ?? "");
                if (string.IsNullOrEmpty(baseDir)) return;
                string full = Path.GetFullPath(Path.Combine(baseDir, decoded));
                if (File.Exists(full))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + full + "\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void OpenExternalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return;
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps &&
                    uri.Scheme != Uri.UriSchemeMailto && uri.Scheme != "file")
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ---------------------------------------------------------------- find

        private void ShowFind()
        {
            _findOpen = true;
            _findBar.Visible = true;
            _findBar.BringToFront();
            DoLayout();
            _findBar.FocusInput();
        }

        private void HideFind()
        {
            _findOpen = false;
            _findBar.Visible = false;
            _findBar.Reset();
            FireScript("MDviewer.findClear()");
            DoLayout();
            _web.Focus();
        }

        private void RunFind(bool forward)
        {
            if (!_webReady) return;

            string query = _findBar.Query;
            if (string.IsNullOrEmpty(query))
            {
                FireScript("MDviewer.findClear()");
                _findBar.Reset();
                _lastFindText = null;
                return;
            }

            if (query != _lastFindText)
            {
                _lastFindText = query;
                FireScript("MDviewer.findHighlight(" + DocumentRenderer.EscapeJson(query) + ", false)");
            }
            else
            {
                FireScript("MDviewer.findNext(" + (forward ? "1" : "-1") + ")");
            }
        }

        // --------------------------------------------------------------- status

        private void UpdateStatus()
        {
            string left = string.IsNullOrEmpty(_currentPath) ? "未打开文件" : _currentPath;
            string middle = _headings.Count > 0
                ? _headings.Count.ToString() + " 个标题"
                : "";
            string right = string.Format("{0}%  ·  {1}",
                Math.Round(ZoomSteps[_zoomIndex] * 100),
                Theme.Current.IsDark ? "夜间" : "日间");

            _status.LeftText = left;
            _status.MiddleText = middle;
            _status.RightText = right;
            _status.Invalidate();
            _titleBar.Invalidate();
        }

        // ------------------------------------------------------------- drag/drop

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && IsMarkdownFile(files[0]))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            if (IsMarkdownFile(files[0]))
                OpenPath(files[0], false);
        }

        private static bool IsMarkdownFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".md" || ext == ".markdown" || ext == ".mdown" || ext == ".mkd" ||
                   ext == ".txt" || ext == ".text";
        }

        // -------------------------------------------------------------- input

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys keys = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;

            if (ctrl)
            {
                switch (keys)
                {
                    case Keys.O:
                        ShowOpenDialog();
                        return true;
                    case Keys.B:
                        ToggleSidebar();
                        return true;
                    case Keys.T:
                        ToggleTheme();
                        return true;
                    case Keys.F:
                        ShowFind();
                        return true;
                    case Keys.D0:
                    case Keys.NumPad0:
                        ResetZoom();
                        return true;
                    case Keys.Oemplus:
                    case Keys.Add:
                        ZoomBy(1);
                        return true;
                    case Keys.OemMinus:
                    case Keys.Subtract:
                        ZoomBy(-1);
                        return true;
                }
            }

            switch (keys)
            {
                case Keys.F5:
                    ReloadCurrent(true);
                    return true;
                case Keys.F1:
                    ShowAbout();
                    return true;
                case Keys.Escape:
                    if (_findOpen) { HideFind(); return true; }
                    break;
                case Keys.E:
                    if (ctrl) { ShowOpenDialog(); return true; }
                    break;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------ nchittest

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Let the client area cover the entire window so the native frame,
                // caption and thick border remain invisible while still enabling
                // Aero Snap and DWM shadows.
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == Native.WM_GETMINMAXINFO)
            {
                // Borderless windows otherwise maximize over the taskbar. Constrain
                // the maximized size to the current monitor's work area.
                Screen screen = Screen.FromHandle(Handle);
                Rectangle work = screen.WorkingArea;
                Rectangle bounds = screen.Bounds;

                var info = (Native.MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(
                    m.LParam, typeof(Native.MINMAXINFO));
                info.ptMaxPosition.X = work.Left - bounds.Left;
                info.ptMaxPosition.Y = work.Top - bounds.Top;
                info.ptMaxSize.X = work.Width;
                info.ptMaxSize.Y = work.Height;
                info.ptMinTrackSize.X = MinimumSize.Width;
                info.ptMinTrackSize.Y = MinimumSize.Height;
                System.Runtime.InteropServices.Marshal.StructureToPtr(
                    info, m.LParam, true);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == Native.WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1) // HTCLIENT
                {
                    int screenX = (short)(m.LParam.ToInt32() & 0xFFFF);
                    int screenY = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                    Point p = PointToClient(new Point(screenX, screenY));
                    int grip = Math.Max(4, Ui.S(this, 6));
                    bool left = p.X <= grip;
                    bool right = p.X >= ClientSize.Width - grip;
                    bool top = p.Y <= grip;
                    bool bottom = p.Y >= ClientSize.Height - grip;

                    if (top && left) m.Result = (IntPtr)Native.HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)Native.HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)Native.HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)Native.HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)Native.HTLEFT;
                    else if (right) m.Result = (IntPtr)Native.HTRIGHT;
                    else if (top) m.Result = (IntPtr)Native.HTTOP;
                    else if (bottom) m.Result = (IntPtr)Native.HTBOTTOM;
                }
                return;
            }

            base.WndProc(ref m);
        }

        // -------------------------------------------------------------- closing

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                _settings.WindowX = bounds.X;
                _settings.WindowY = bounds.Y;
                _settings.WindowWidth = bounds.Width;
                _settings.WindowHeight = bounds.Height;
                _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
                _settings.SidebarVisible = _sidebarVisible;
                _settings.SidebarWidth = _sidebarLogicalWidth;
                _settings.FontScale = ZoomSteps[_zoomIndex];
                _settings.Save();
            }
            catch { }
        }

        // ------------------------------------------------------------- bridge

        /// <summary>Called when a second instance forwards a file path.</summary>
        public void OpenFromExternalInstance(string path)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OpenFromExternalInstance), path);
                return;
            }
            if (string.IsNullOrEmpty(path)) return;

            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            OpenPath(path, false);
        }
    }

    // ---------------------------------------------------------------- helpers

    internal static class Utils
    {
        public static int Center(int outer, int inner)
        {
            return Math.Max(0, (outer - inner) / 2);
        }
    }

    /// <summary>Minimal JSON field reader for the fixed messages sent by runtime.js.</summary>
    internal static class JsonHelper
    {
        public static string GetString(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            if (!m.Success) return null;
            return Unescape(m.Groups[1].Value);
        }

        public static double GetNumber(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)");
            double d;
            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out d))
                return d;
            return 0;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i + 1, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out code))
                            {
                                sb.Append((char)code);
                                i += 4;
                                break;
                            }
                        }
                        sb.Append("\\u");
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }
    }
}
