using System;
using System.Drawing;
using System.Windows.Forms;

namespace MDviewer.UI
{
    /// <summary>Floating find bar with match counter and previous/next navigation.</summary>
    public sealed class FindBar : Panel
    {
        private readonly TextBox _input;
        private readonly ChromeButton _prev;
        private readonly ChromeButton _next;
        private readonly ChromeButton _close;
        private readonly Label _counter;
        private readonly Label _hint;

        public event EventHandler QueryChanged;
        public event EventHandler NextRequested;
        public event EventHandler PreviousRequested;
        public event EventHandler CloseRequested;

        public FindBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 46;
            Padding = new Padding(0);
            Visible = false;

            _input = new TextBox();
            _input.BorderStyle = BorderStyle.FixedSingle;
            _input.Font = new Font("Segoe UI", 10f);
            _input.TextChanged += delegate { if (QueryChanged != null) QueryChanged(this, EventArgs.Empty); };
            _input.KeyDown += OnInputKeyDown;
            Controls.Add(_input);

            _counter = new Label();
            _counter.AutoSize = false;
            _counter.TextAlign = ContentAlignment.MiddleLeft;
            _counter.Font = new Font("Segoe UI", 8.5f);
            Controls.Add(_counter);

            _hint = new Label();
            _hint.AutoSize = false;
            _hint.TextAlign = ContentAlignment.MiddleLeft;
            _hint.Text = "Enter 下一个  ·  Shift+Enter 上一个  ·  Esc 关闭";
            _hint.Font = new Font("Segoe UI", 8f);
            Controls.Add(_hint);

            _prev = MakeButton(Icon.ChevronUp, "上一个");
            _prev.Click += delegate { if (PreviousRequested != null) PreviousRequested(this, EventArgs.Empty); };
            Controls.Add(_prev);

            _next = MakeButton(Icon.ChevronDown, "下一个");
            _next.Click += delegate { if (NextRequested != null) NextRequested(this, EventArgs.Empty); };
            Controls.Add(_next);

            _close = MakeButton(Icon.Close, "关闭");
            _close.Click += delegate { if (CloseRequested != null) CloseRequested(this, EventArgs.Empty); };
            Controls.Add(_close);

            ApplyTheme();
        }

        private ChromeButton MakeButton(Icon icon, string tip)
        {
            var b = new ChromeButton();
            b.Icon = icon;
            b.IconSize = 15;
            b.Size = new Size(30, 30);
            var tt = new ToolTip();
            tt.SetToolTip(b, tip);
            return b;
        }

        public string Query
        {
            get { return _input.Text; }
        }

        public TextBox Input { get { return _input; } }

        public void FocusInput()
        {
            _input.Focus();
            _input.SelectAll();
        }

        public void SetResult(int count, int index)
        {
            if (count <= 0)
            {
                _counter.Text = _input.Text.Length == 0 ? "" : "无结果";
            }
            else
            {
                _counter.Text = string.Format("{0}/{1}", index + 1, count);
            }
        }

        public void Reset()
        {
            _counter.Text = "";
        }

        public void ApplyTheme()
        {
            Theme t = Theme.Current;
            BackColor = t.ChromeBg;
            ForeColor = t.Text;
            _input.BackColor = t.InputBg;
            _input.ForeColor = t.Text;
            _counter.ForeColor = t.TextMuted;
            _hint.ForeColor = t.TextFaint;
            _prev.Invalidate();
            _next.Invalidate();
            _close.Invalidate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            using (var bg = new SolidBrush(t.ChromeBg))
                e.Graphics.FillRectangle(bg, ClientRectangle);
            using (var pen = new Pen(t.Border))
            {
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
        }

        public void LayoutChildren()
        {
            if (_input == null || _prev == null || _next == null || _close == null ||
                _counter == null || _hint == null) return;

            int s = Ui.S(this, 1);
            int pad = 14 * s;
            int h = 30 * s;
            int y = (Height - h) / 2;

            int right = Width - pad;
            _close.SetBounds(right - 30 * s, y, 30 * s, h); right -= 34 * s;
            _next.SetBounds(right - 30 * s, y, 30 * s, h); right -= 34 * s;
            _prev.SetBounds(right - 30 * s, y, 30 * s, h); right -= 40 * s;
            _counter.SetBounds(right - 70 * s, y, 68 * s, h);

            int inputW = Math.Min(320 * s, Math.Max(120 * s, right - pad - 110 * s));
            _input.SetBounds(pad, y, inputW, h);
            _hint.SetBounds(pad + inputW + 12 * s, y, Math.Max(0, right - 70 * s - (pad + inputW + 12 * s) - 8 * s), h);
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (e.Shift) { if (PreviousRequested != null) PreviousRequested(this, EventArgs.Empty); }
                else { if (NextRequested != null) NextRequested(this, EventArgs.Empty); }
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }
    }
}
