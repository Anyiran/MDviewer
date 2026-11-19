using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace MDviewer
{
    public static class AppResources
    {
        private const string ResourceName = "MDviewer.app.ico";
        private static Icon _icon;
        private static byte[] _iconBytes;
        private static readonly Dictionary<int, Bitmap> LogoCache =
            new Dictionary<int, Bitmap>();

        /// <summary>Loads the multi-resolution icon embedded in the executable.</summary>
        public static Icon CreateAppIcon()
        {
            if (_icon != null) return _icon;

            try
            {
                Assembly asm = typeof(AppResources).Assembly;
                using (Stream stream = asm.GetManifestResourceStream(ResourceName))
                {
                    if (stream != null)
                    {
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            _iconBytes = ms.ToArray();
                        }
                        _icon = new Icon(new MemoryStream(_iconBytes));
                        return _icon;
                    }
                }
            }
            catch { }

            _icon = DrawFallbackIcon();
            return _icon;
        }

        /// <summary>Returns the icon artwork at the requested size (for title bar / About).</summary>
        public static Bitmap GetLogoBitmap(int size)
        {
            if (size <= 0) size = 64;

            Bitmap cached;
            if (LogoCache.TryGetValue(size, out cached)) return cached;

            try
            {
                if (_iconBytes == null) CreateAppIcon();
                if (_iconBytes != null)
                {
                    using (var ico = new Icon(new MemoryStream(_iconBytes), new Size(256, 256)))
                    using (Bitmap source = ico.ToBitmap())
                    {
                        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(result))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.DrawImage(source, new Rectangle(0, 0, size, size));
                        }
                        LogoCache[size] = result;
                        return result;
                    }
                }
            }
            catch { }

            Bitmap fallback = DrawFallbackLogo(size);
            LogoCache[size] = fallback;
            return fallback;
        }

        private static Icon DrawFallbackIcon()
        {
            try
            {
                using (Bitmap bmp = DrawFallbackLogo(64))
                {
                    IntPtr handle = bmp.GetHicon();
                    return Icon.FromHandle(handle);
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private static Bitmap DrawFallbackLogo(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Rectangle r = new Rectangle(1, 1, size - 2, size - 2);
                using (var path = UI.Ui.RoundRect(r, Math.Max(4, size / 5)))
                using (var brush = new LinearGradientBrush(r,
                           Color.FromArgb(92, 143, 255), Color.FromArgb(67, 56, 202), 45f))
                {
                    g.FillPath(brush, path);
                }

                using (var font = new Font("Segoe UI", Math.Max(8f, size * 0.52f), FontStyle.Bold))
                using (var white = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("M", font, white, new RectangleF(0, -size * 0.04f, size, size), sf);
                }
            }
            return bmp;
        }
    }
}
