using System;
using System.Drawing;
using System.Windows.Forms;

namespace MDviewer.UI
{
    /// <summary>Flat, theme-aware toolbar / title-bar button.</summary>
    public class ChromeButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private bool _toggled;

        public Icon Icon { get; set; }
        public string Caption { get; set; }
        public Font CaptionFont { get; set; }
        public int IconSize { get; set; }
        public int Radius { get; set; }
        public bool IsWindowControl { get; set; }
        public bool IsCloseButton { get; set; }

        public event EventHandler ToggledChanged;

        public ChromeButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = true;
            Radius = 6;
            IconSize = 18;
            Size = new Size(34, 30);
            Cursor = Cursors.Hand;
        }

        public bool Toggled
        {
            get { return _toggled; }
            set
            {
                if (_toggled == value) return;
                _toggled = value;
                Invalidate();
                if (ToggledChanged != null) ToggledChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int pad = 2;
            Rectangle r = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);
            int radius = Ui.S(this, Radius);

            Color bg = Color.Empty;
            if (IsCloseButton && (_hover || _pressed))
            {
                bg = _pressed ? Theme.Blend(t.Danger, Color.Black, 0.15) : t.Danger;
            }
            else if (_pressed)
            {
                bg = t.Active;
            }
            else if (_hover)
            {
                bg = t.Hover;
            }
            else if (Toggled)
            {
                bg = t.Active;
            }

            if (bg != Color.Empty)
                Ui.Fill(this, g, bg, r, radius);

            Color fg = t.Text;
            if (IsCloseButton && (_hover || _pressed)) fg = Color.White;
            else if (Toggled) fg = t.Accent;
            else if (!Enabled) fg = t.TextFaint;

            if (string.IsNullOrEmpty(Caption))
            {
                int iconSize = Ui.S(this, IconSize);
                var ib = new Rectangle((Width - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize);
                Icons.Draw(g, Icon, ib, fg);
            }
            else
            {
                int iconSize = Ui.S(this, IconSize);
                int gap = Ui.S(this, 6);
                Font font = CaptionFont;
                bool disposeFont = false;
                if (font == null)
                {
                    font = Ui.UiFont(this, 9f, FontStyle.Regular);
                    disposeFont = true;
                }
                try
                {
                    SizeF ts = g.MeasureString(Caption, font);
                    int contentW = (int)ts.Width + (IconSize > 0 ? iconSize + gap : 0);
                    int x = (Width - contentW) / 2;
                    if (IconSize > 0)
                    {
                        var ib = new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize);
                        Icons.Draw(g, Icon, ib, fg);
                        x += iconSize + gap;
                    }
                    using (var brush = new SolidBrush(fg))
                    {
                        g.DrawString(Caption, font, brush, x, (Height - ts.Height) / 2f - 1);
                    }
                }
                finally
                {
                    if (disposeFont) font.Dispose();
                }
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle fr = r;
                fr.Inflate(-2, -2);
                Ui.Stroke(this, g, Theme.Blend(t.Accent, t.ChromeBg, 0.35), fr, radius, 1f);
            }
        }

    }

    /// <summary>A thin vertical separator used in the toolbar.</summary>
    public class ToolbarSeparator : Control
    {
        public ToolbarSeparator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Width = 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            using (var pen = new Pen(t.Border))
            {
                int x = Width / 2;
                int pad = Height / 5;
                e.Graphics.DrawLine(pen, x, pad, x, Height - pad);
            }
        }
    }
}
