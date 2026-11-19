using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MDviewer.UI
{
    public enum ThemeMode
    {
        Light,
        Dark
    }

    /// <summary>
    /// Central palette for the native window chrome. The markdown surface itself is
    /// themed inside the WebView via CSS; these colours keep toolbar, sidebar and
    /// status bar visually consistent with it.
    /// </summary>
    public sealed class Theme
    {
        public static Theme Current = Light();

        public ThemeMode Mode;
        public Color WindowBg;
        public Color ChromeBg;
        public Color ChromeBgAlt;
        public Color Border;
        public Color BorderStrong;
        public Color Text;
        public Color TextMuted;
        public Color TextFaint;
        public Color Hover;
        public Color Active;
        public Color Accent;
        public Color AccentHover;
        public Color AccentText;
        public Color Danger;
        public Color SidebarBg;
        public Color SidebarSelected;
        public Color InputBg;
        public Color ScrollThumb;
        public Color ScrollThumbHover;

        public bool IsDark { get { return Mode == ThemeMode.Dark; } }

        public static Theme Light()
        {
            var t = new Theme();
            t.Mode = ThemeMode.Light;
            t.WindowBg = Color.FromArgb(255, 255, 255);
            t.ChromeBg = Color.FromArgb(246, 248, 250);
            t.ChromeBgAlt = Color.FromArgb(255, 255, 255);
            t.Border = Color.FromArgb(209, 217, 224);
            t.BorderStrong = Color.FromArgb(191, 200, 209);
            t.Text = Color.FromArgb(31, 35, 40);
            t.TextMuted = Color.FromArgb(89, 99, 110);
            t.TextFaint = Color.FromArgb(139, 148, 158);
            t.Hover = Color.FromArgb(234, 238, 242);
            t.Active = Color.FromArgb(224, 229, 234);
            t.Accent = Color.FromArgb(9, 105, 218);
            t.AccentHover = Color.FromArgb(8, 95, 198);
            t.AccentText = Color.White;
            t.Danger = Color.FromArgb(207, 34, 46);
            t.SidebarBg = Color.FromArgb(252, 253, 254);
            t.SidebarSelected = Color.FromArgb(221, 244, 255);
            t.InputBg = Color.FromArgb(255, 255, 255);
            t.ScrollThumb = Color.FromArgb(209, 217, 224);
            t.ScrollThumbHover = Color.FromArgb(183, 191, 199);
            return t;
        }

        public static Theme Dark()
        {
            var t = new Theme();
            t.Mode = ThemeMode.Dark;
            t.WindowBg = Color.FromArgb(13, 17, 23);
            t.ChromeBg = Color.FromArgb(22, 27, 34);
            t.ChromeBgAlt = Color.FromArgb(13, 17, 23);
            t.Border = Color.FromArgb(48, 54, 61);
            t.BorderStrong = Color.FromArgb(61, 68, 77);
            t.Text = Color.FromArgb(230, 237, 243);
            t.TextMuted = Color.FromArgb(145, 152, 161);
            t.TextFaint = Color.FromArgb(110, 118, 129);
            t.Hover = Color.FromArgb(33, 38, 45);
            t.Active = Color.FromArgb(42, 48, 56);
            t.Accent = Color.FromArgb(68, 147, 248);
            t.AccentHover = Color.FromArgb(88, 166, 255);
            t.AccentText = Color.FromArgb(13, 17, 23);
            t.Danger = Color.FromArgb(248, 81, 73);
            t.SidebarBg = Color.FromArgb(13, 17, 23);
            t.SidebarSelected = Color.FromArgb(18, 40, 66);
            t.InputBg = Color.FromArgb(13, 17, 23);
            t.ScrollThumb = Color.FromArgb(61, 68, 77);
            t.ScrollThumbHover = Color.FromArgb(77, 85, 96);
            return t;
        }

        public static Theme For(ThemeMode mode)
        {
            return mode == ThemeMode.Dark ? Dark() : Light();
        }

        public static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>Small DPI helper for the hand-drawn chrome.</summary>
    public static class Ui
    {
        public const int BaseTitleHeight = 46;
        public const int BaseStatusHeight = 26;

        public static float DpiScale(Control c)
        {
            try
            {
                using (Graphics g = c.CreateGraphics())
                {
                    return g.DpiX / 96f;
                }
            }
            catch
            {
                return 1f;
            }
        }

        public static int S(Control c, float value)
        {
            return (int)Math.Round(value * DpiScale(c));
        }

        public static Font UiFont(Control c, float size, FontStyle style)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        /// <summary>Rounded rectangle path with per-corner radius support.</summary>
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Fill(Control c, Graphics g, Color color, Rectangle r, int radius)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void Stroke(Control c, Graphics g, Color color, Rectangle r, int radius, float w)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (var pen = new Pen(color, w))
                g.DrawPath(pen, p);
        }

        /// <summary>Turns a native scrollbar into a thin, theme-coloured one (Win10+).</summary>
        public static void StyleScrollBar(Control c)
        {
            try
            {
                if (!c.IsHandleCreated) return;
                // uxtheme: SetWindowTheme(handle, "DarkMode_Explorer", null) darkens scrollbars.
                Native.SetWindowTheme(c.Handle, Theme.Current.IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
        }
    }
}
