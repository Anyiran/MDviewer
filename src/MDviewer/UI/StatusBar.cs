using System;
using System.Drawing;
using System.Windows.Forms;

namespace MDviewer.UI
{
    /// <summary>Bottom status strip: file path on the left, document facts on the right.</summary>
    public sealed class StatusBarView : Control
    {
        public string LeftText { get; set; }
        public string MiddleText { get; set; }
        public string RightText { get; set; }

        public StatusBarView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            LeftText = "";
            MiddleText = "";
            RightText = "";
        }

        public void ApplyTheme()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var bg = new SolidBrush(t.ChromeBg))
                g.FillRectangle(bg, ClientRectangle);
            using (var pen = new Pen(t.Border))
                g.DrawLine(pen, 0, 0, Width, 0);

            int pad = Ui.S(this, 12);
            int rightW = 0;

            if (!string.IsNullOrEmpty(RightText))
            {
                using (var font = new Font("Segoe UI", 8.25f))
                {
                    Size sz = TextRenderer.MeasureText(RightText, font);
                    rightW = sz.Width;
                    var rect = new Rectangle(Width - pad - sz.Width, 0, sz.Width, Height);
                    TextRenderer.DrawText(g, RightText, font, rect, t.TextMuted,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }

            if (!string.IsNullOrEmpty(MiddleText))
            {
                using (var font = new Font("Segoe UI", 8.25f))
                {
                    Size sz = TextRenderer.MeasureText(MiddleText, font);
                    int mx = Width - pad - rightW - Ui.S(this, 18) - sz.Width;
                    var rect = new Rectangle(mx, 0, sz.Width, Height);
                    TextRenderer.DrawText(g, MiddleText, font, rect, t.TextFaint,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }

            if (!string.IsNullOrEmpty(LeftText))
            {
                int maxW = Math.Max(0, Width - pad * 2 - rightW - Ui.S(this, 160));
                var rect = new Rectangle(pad, 0, maxW, Height);
                using (var font = new Font("Segoe UI", 8.25f))
                {
                    TextRenderer.DrawText(g, LeftText, font, rect, t.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                        TextFormatFlags.SingleLine);
                }
            }
        }
    }
}
