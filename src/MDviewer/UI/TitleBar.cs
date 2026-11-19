using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MDviewer.UI
{
    /// <summary>The custom-drawn window title bar: app mark, name, current file and drag support.</summary>
    public sealed class TitleBar : Control
    {
        public string FileName { get; set; }
        public string FileSubtitle { get; set; }
        public int FileTextLeft { get; set; }
        public int FileTextRight { get; set; }

        public TitleBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            FileTextLeft = 480;
            FileTextRight = 600;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Theme.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var bg = new SolidBrush(t.ChromeBg))
                g.FillRectangle(bg, ClientRectangle);

            using (var pen = new Pen(t.Border))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            int logoSize = Ui.S(this, 26);
            Rectangle logo = new Rectangle(Ui.S(this, 12), (Height - logoSize) / 2, logoSize, logoSize);
            Bitmap artwork = AppResources.GetLogoBitmap(logoSize);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(artwork, logo);

            int titleX = logo.Right + Ui.S(this, 10);
            using (var font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular))
            using (var brush = new SolidBrush(t.Text))
            {
                g.DrawString("MDviewer", font, brush, titleX, (Height - font.Height) / 2f - 1);
            }

            // Current file name, shown between the toolbar and the window buttons.
            if (!string.IsNullOrEmpty(FileName) && FileTextRight > FileTextLeft + 40)
            {
                int divX = FileTextLeft - Ui.S(this, 14);
                using (var pen = new Pen(t.Border))
                    g.DrawLine(pen, divX, Ui.S(this, 12), divX, Height - Ui.S(this, 12));

                var rect = new Rectangle(FileTextLeft, 0, Math.Max(10, FileTextRight - FileTextLeft), Height);
                using (var font = new Font("Segoe UI", 9f))
                using (var brush = new SolidBrush(t.TextMuted))
                {
                    TextRenderer.DrawText(g, FileName, font, rect, t.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                        TextFormatFlags.SingleLine);
                }
                // Draw a subtle "modified"/folder hint.
                if (!string.IsNullOrEmpty(FileSubtitle))
                {
                    using (var font = new Font("Segoe UI", 8f))
                    {
                        SizeF sz = g.MeasureString(FileName, font);
                        // Subtitle intentionally omitted for clarity; reserved for future use.
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            Form form = FindForm();
            if (form == null) return;

            if (e.Clicks == 2)
            {
                form.WindowState = form.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
                return;
            }

            Native.ReleaseCapture();
            Native.SendMessage(form.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            Form form = FindForm();
            if (form == null) return;
            form.WindowState = form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }
    }
}
