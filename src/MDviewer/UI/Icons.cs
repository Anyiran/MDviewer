using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MDviewer.UI
{
    public enum Icon
    {
        Sidebar,
        Sun,
        Moon,
        ZoomIn,
        ZoomOut,
        Folder,
        Reload,
        Find,
        Info,
        Minimize,
        Maximize,
        Restore,
        Close,
        ChevronDown,
        ChevronUp,
        ChevronRight,
        Check
    }

    /// <summary>Vector icons drawn with GDI+ so they stay crisp and theme-aware.</summary>
    public static class Icons
    {
        public static void Draw(Graphics g, Icon icon, Rectangle bounds, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float s = Math.Min(bounds.Width, bounds.Height);
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float u = s / 16f; // icon designed on a 16x16 grid

            using (var pen = new Pen(color, Math.Max(1.1f, 1.35f * u)))
            using (var brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                switch (icon)
                {
                    case Icon.Sidebar:
                        // Three lines with a left panel hint.
                        DrawLine(g, pen, cx - 6.5f * u, cy - 4.5f * u, cx + 6.5f * u, cy - 4.5f * u);
                        DrawLine(g, pen, cx - 6.5f * u, cy, cx + 6.5f * u, cy);
                        DrawLine(g, pen, cx - 6.5f * u, cy + 4.5f * u, cx + 6.5f * u, cy + 4.5f * u);
                        DrawLine(g, pen, cx - 2.6f * u, cy - 7.2f * u, cx - 2.6f * u, cy + 7.2f * u);
                        break;

                    case Icon.Sun:
                        g.DrawEllipse(pen, cx - 3.2f * u, cy - 3.2f * u, 6.4f * u, 6.4f * u);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = Math.PI / 4 * i;
                            float x1 = cx + (float)Math.Cos(a) * 5.2f * u;
                            float y1 = cy + (float)Math.Sin(a) * 5.2f * u;
                            float x2 = cx + (float)Math.Cos(a) * 7.0f * u;
                            float y2 = cy + (float)Math.Sin(a) * 7.0f * u;
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;

                    case Icon.Moon:
                        {
                            // Crescent: alternate-fill a circle minus an offset circle.
                            // The cutout sits up-right so the remaining lit shape is a
                            // clear crescent opening to the right.
                            using (var path = new GraphicsPath())
                            {
                                path.FillMode = FillMode.Alternate;
                                path.AddEllipse(cx - 6.4f * u, cy - 6.4f * u, 12.8f * u, 12.8f * u);
                                path.AddEllipse(cx - 3.6f * u, cy - 8.2f * u, 12.0f * u, 12.0f * u);
                                g.FillPath(brush, path);
                            }
                        }
                        break;

                    case Icon.ZoomIn:
                    case Icon.ZoomOut:
                        g.DrawEllipse(pen, cx - 6.0f * u, cy - 6.6f * u, 8.6f * u, 8.6f * u);
                        g.DrawLine(pen, cx + 2.4f * u, cy + 2.4f * u, cx + 6.4f * u, cy + 6.4f * u);
                        DrawLine(g, pen, cx - 4.0f * u, cy - 2.3f * u, cx - 1.2f * u, cy - 2.3f * u);
                        if (icon == Icon.ZoomIn)
                            DrawLine(g, pen, cx - 2.6f * u, cy - 3.7f * u, cx - 2.6f * u, cy - 0.9f * u);
                        break;

                    case Icon.Folder:
                        {
                            using (var path = new GraphicsPath())
                            {
                                float w = 13.0f * u, h = 10.5f * u;
                                float x = cx - w / 2, y = cy - h / 2 + 0.5f * u;
                                path.StartFigure();
                                path.AddLine(x, y + 2.2f * u, x + 4.5f * u, y + 2.2f * u);
                                path.AddLine(x + 5.6f * u, y, x + w, y);
                                path.AddLine(x + w, y + h, x, y + h);
                                path.CloseFigure();
                                g.DrawPath(pen, path);
                            }
                        }
                        break;

                    case Icon.Reload:
                        {
                            float r = 5.4f * u;
                            g.DrawArc(pen, cx - r, cy - r, r * 2f, r * 2f, 55, 260);

                            // Arrowhead at the end of the arc, aligned with its tangent.
                            double endAngle = (55 + 260) * Math.PI / 180.0;
                            float ex = cx + r * (float)Math.Cos(endAngle);
                            float ey = cy + r * (float)Math.Sin(endAngle);
                            float tx = -(float)Math.Sin(endAngle);
                            float ty = (float)Math.Cos(endAngle);
                            float nx = (float)Math.Cos(endAngle);
                            float ny = (float)Math.Sin(endAngle);

                            PointF tip = new PointF(ex + tx * 3.8f * u, ey + ty * 3.8f * u);
                            PointF b1 = new PointF(ex - tx * 0.6f * u + nx * 2.9f * u, ey - ty * 0.6f * u + ny * 2.9f * u);
                            PointF b2 = new PointF(ex - tx * 0.6f * u - nx * 2.9f * u, ey - ty * 0.6f * u - ny * 2.9f * u);
                            using (var arrow = new GraphicsPath())
                            {
                                arrow.AddPolygon(new PointF[] { tip, b1, b2 });
                                g.FillPath(brush, arrow);
                            }
                        }
                        break;

                    case Icon.Find:
                        g.DrawEllipse(pen, cx - 6.2f * u, cy - 6.2f * u, 8.4f * u, 8.4f * u);
                        g.DrawLine(pen, cx + 1.4f * u, cy + 1.4f * u, cx + 6.4f * u, cy + 6.4f * u);
                        break;

                    case Icon.Info:
                        g.DrawEllipse(pen, cx - 6.4f * u, cy - 6.4f * u, 12.8f * u, 12.8f * u);
                        g.FillEllipse(brush, cx - 0.9f * u, cy - 4.0f * u, 1.8f * u, 1.8f * u);
                        DrawLine(g, pen, cx, cy - 1.0f * u, cx, cy + 4.2f * u);
                        break;

                    case Icon.Minimize:
                        DrawLine(g, pen, cx - 6f * u, cy + 0.5f * u, cx + 6f * u, cy + 0.5f * u);
                        break;

                    case Icon.Maximize:
                        g.DrawRectangle(pen, cx - 5.6f * u, cy - 5.6f * u, 11.2f * u, 11.2f * u);
                        break;

                    case Icon.Restore:
                        g.DrawRectangle(pen, cx - 5.8f * u, cy - 4.2f * u, 9.2f * u, 9.2f * u);
                        DrawLine(g, pen, cx - 3.2f * u, cy - 4.2f * u, cx - 3.2f * u, cy - 6.2f * u);
                        DrawLine(g, pen, cx - 3.2f * u, cy - 6.2f * u, cx + 3.6f * u, cy - 6.2f * u);
                        DrawLine(g, pen, cx + 3.6f * u, cy - 6.2f * u, cx + 3.6f * u, cy - 1.0f * u);
                        break;

                    case Icon.Close:
                        DrawLine(g, pen, cx - 5.2f * u, cy - 5.2f * u, cx + 5.2f * u, cy + 5.2f * u);
                        DrawLine(g, pen, cx + 5.2f * u, cy - 5.2f * u, cx - 5.2f * u, cy + 5.2f * u);
                        break;

                    case Icon.ChevronDown:
                        DrawLine(g, pen, cx - 4.6f * u, cy - 1.6f * u, cx, cy + 3.0f * u);
                        DrawLine(g, pen, cx, cy + 3.0f * u, cx + 4.6f * u, cy - 1.6f * u);
                        break;

                    case Icon.ChevronUp:
                        DrawLine(g, pen, cx - 4.6f * u, cy + 1.6f * u, cx, cy - 3.0f * u);
                        DrawLine(g, pen, cx, cy - 3.0f * u, cx + 4.6f * u, cy + 1.6f * u);
                        break;

                    case Icon.ChevronRight:
                        DrawLine(g, pen, cx - 1.6f * u, cy - 4.6f * u, cx + 3.0f * u, cy);
                        DrawLine(g, pen, cx + 3.0f * u, cy, cx - 1.6f * u, cy + 4.6f * u);
                        break;

                    case Icon.Check:
                        DrawLine(g, pen, cx - 5.4f * u, cy + 0.2f * u, cx - 1.6f * u, cy + 4.0f * u);
                        DrawLine(g, pen, cx - 1.6f * u, cy + 4.0f * u, cx + 5.6f * u, cy - 4.4f * u);
                        break;
                }
            }
        }

        private static void DrawLine(Graphics g, Pen pen, float x1, float y1, float x2, float y2)
        {
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }
}
