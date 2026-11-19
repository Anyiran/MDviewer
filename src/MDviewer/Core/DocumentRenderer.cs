using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MDviewer.Core
{
    /// <summary>
    /// Turns markdown into a complete, self-sufficient HTML document that is written
    /// to the cache folder and loaded through a virtual host in the WebView.
    /// </summary>
    public sealed class DocumentRenderer
    {
        public const string AssetHost = "mdviewer.app";
        public const string DocHost = "mdviewer.doc";
        public const string ResHost = "mdviewer.res";

        private static readonly Regex ScriptRx = new Regex(
            @"<script\b[^>]*>[\s\S]*?</script\s*>", RegexOptions.IgnoreCase);

        private static readonly Regex EventAttrRx = new Regex(
            @"\s+on[a-zA-Z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase);

        private static readonly Regex JsUrlRx = new Regex(
            @"(href|src)\s*=\s*(?:""\s*javascript:[^""]*""|'\s*javascript:[^']*'|javascript:[^\s>]+)",
            RegexOptions.IgnoreCase);

        private static readonly Regex ImgSrcRx = new Regex(
            @"(<img\b[^>]*?\bsrc\s*=\s*"")([^""]*)("")", RegexOptions.IgnoreCase);

        public string Render(string markdown, string sourcePath, bool darkTheme,
            double fontScale, out List<Heading> headings)
        {
            var parser = new MarkdownParser(true);
            string body = parser.ToHtml(markdown ?? string.Empty, out headings);
            body = Sanitize(body);
            body = RewriteImageSources(body, sourcePath);

            string title = Path.GetFileNameWithoutExtension(sourcePath ?? "MDviewer");
            string themeAttr = darkTheme ? " data-theme=\"dark\"" : "";
            string scale = fontScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            string bg = darkTheme ? "#0d1117" : "#ffffff";

            var sb = new StringBuilder(body.Length + 2048);
            sb.Append("<!DOCTYPE html>\n<html lang=\"zh-CN\"").Append(themeAttr)
              .Append(" style=\"--mdv-font-scale:").Append(scale).Append("\">\n<head>\n");
            sb.Append("<meta charset=\"utf-8\">\n");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            sb.Append("<title>").Append(MarkdownParser.Escape(title)).Append("</title>\n");
            sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"")
              .Append("default-src 'none'; ")
              .Append("img-src https: http: data: blob:; ")
              .Append("style-src 'unsafe-inline' https://").Append(AssetHost).Append("; ")
              .Append("script-src https://").Append(AssetHost).Append("; ")
              .Append("font-src data:;\">\n");
            sb.Append("<link rel=\"stylesheet\" href=\"https://").Append(AssetHost).Append("/github.css\">\n");
            sb.Append("<link rel=\"stylesheet\" href=\"https://").Append(AssetHost).Append("/theme-dark.css\">\n");
            sb.Append("<style>html,body{background-color:").Append(bg).Append("}</style>\n");
            sb.Append("</head>\n<body>\n");
            sb.Append("<article class=\"markdown-body\">\n");
            sb.Append(body);
            sb.Append("</article>\n");
            sb.Append("<div id=\"loading\"></div>\n");
            sb.Append("<script src=\"https://").Append(AssetHost).Append("/runtime.js\"></script>\n");
            sb.Append("</body>\n</html>\n");
            return sb.ToString();
        }

        /// <summary>Welcome screen shown when no file is open.</summary>
        public string RenderEmptyState(bool darkTheme, double fontScale)
        {
            string themeAttr = darkTheme ? " data-theme=\"dark\"" : "";
            string scale = fontScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            string bg = darkTheme ? "#0d1117" : "#ffffff";

            var sb = new StringBuilder(2048);
            sb.Append("<!DOCTYPE html>\n<html lang=\"zh-CN\"").Append(themeAttr)
              .Append(" style=\"--mdv-font-scale:").Append(scale).Append("\">\n<head>\n");
            sb.Append("<meta charset=\"utf-8\">\n<title>MDviewer</title>\n");
            sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src https: data:; style-src 'unsafe-inline' https://")
              .Append(AssetHost).Append("; script-src https://").Append(AssetHost).Append("; font-src data:;\">\n");
            sb.Append("<link rel=\"stylesheet\" href=\"https://").Append(AssetHost).Append("/github.css\">\n");
            sb.Append("<link rel=\"stylesheet\" href=\"https://").Append(AssetHost).Append("/theme-dark.css\">\n");
            sb.Append("<style>html,body{background-color:").Append(bg).Append("}</style>\n");
            sb.Append("</head>\n<body>\n<article class=\"markdown-body\">\n");
            sb.Append("<div class=\"empty-state\">\n");
            sb.Append("<div style=\"width:64px;height:64px;border-radius:16px;background:var(--accent-emphasis);color:#fff;")
              .Append("display:flex;align-items:center;justify-content:center;font-weight:700;font-size:30px;margin-bottom:18px\">M</div>\n");
            sb.Append("<h2>欢迎使用 MDviewer</h2>\n");
            sb.Append("<p>打开一个 Markdown 文件开始阅读，或将文件拖入窗口。</p>\n");
            sb.Append("<p style=\"margin-top:16px\"><span class=\"kbd\">Ctrl</span> + <span class=\"kbd\">O</span> 打开文件&nbsp;&nbsp;·&nbsp;&nbsp;")
              .Append("<span class=\"kbd\">Ctrl</span> + <span class=\"kbd\">B</span> 侧边栏&nbsp;&nbsp;·&nbsp;&nbsp;")
              .Append("<span class=\"kbd\">Ctrl</span> + <span class=\"kbd\">F</span> 查找</p>\n");
            sb.Append("<p style=\"margin-top:8px\">支持 .md / .markdown / .mdown / .mkd / .txt</p>\n");
            sb.Append("</div>\n</article>\n");
            sb.Append("<script src=\"https://").Append(AssetHost).Append("/runtime.js\"></script>\n");
            sb.Append("</body>\n</html>\n");
            return sb.ToString();
        }

        public static string EscapeJson(string value)
        {
            if (value == null) return "\"\"";
            var sb = new StringBuilder(value.Length + 16);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == '\u2028' || c == '\u2029')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string Sanitize(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;

            // Never allow executable script from document content.
            html = ScriptRx.Replace(html, "");
            html = EventAttrRx.Replace(html, "");
            html = JsUrlRx.Replace(html, "$1=\"#\"");
            return html;
        }

        /// <summary>
        /// Relative image paths are resolved through a virtual host that maps the
        /// markdown file's folder, so local images display without file:// access.
        /// </summary>
        private static string RewriteImageSources(string html, string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath)) return html;
            string baseDir;
            try { baseDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath)); }
            catch { return html; }
            if (string.IsNullOrEmpty(baseDir)) return html;

            return ImgSrcRx.Replace(html, delegate(Match m)
            {
                string src = m.Groups[2].Value;
                if (!IsRelativeUrl(src)) return m.Value;

                string clean = src;
                int q = clean.IndexOfAny(new[] { '?', '#' });
                string suffix = "";
                if (q >= 0)
                {
                    suffix = clean.Substring(q);
                    clean = clean.Substring(0, q);
                }

                string unescaped = Uri.UnescapeDataString(clean);
                string full;
                try { full = Path.GetFullPath(Path.Combine(baseDir, unescaped.Replace('/', '\\'))); }
                catch { return m.Value; }

                string rel;
                try
                {
                    string root = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return m.Value;
                    rel = full.Substring(root.Length).Replace('\\', '/');
                }
                catch { return m.Value; }

                var encoded = new StringBuilder();
                string[] parts = rel.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) encoded.Append('/');
                    encoded.Append(Uri.EscapeDataString(parts[i]));
                }

                return m.Groups[1].Value + "https://" + ResHost + "/" + encoded + suffix + m.Groups[3].Value;
            });
        }

        public static bool IsRelativeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string u = url.Trim();
            if (u.StartsWith("#", StringComparison.Ordinal)) return true; // in-document anchor
            if (u.StartsWith("//", StringComparison.Ordinal)) return false;
            if (u.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
            if (u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;
            if (Regex.IsMatch(u, @"^[a-zA-Z][a-zA-Z0-9+.-]*:")) return false; // has a scheme
            return true;
        }

        /// <summary>Reads a file, auto-detecting UTF-8/UTF-16 and falling back to GB18030.</summary>
        public static string ReadTextFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            if (IsValidUtf8(bytes))
                return Encoding.UTF8.GetString(bytes);

            try
            {
                Encoding gb = Encoding.GetEncoding(936); // GBK / GB18030 common on Chinese Windows
                return gb.GetString(bytes);
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private static bool IsValidUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];
                if (b < 0x80) { i++; continue; }

                int extra;
                if ((b & 0xE0) == 0xC0) extra = 1;
                else if ((b & 0xF0) == 0xE0) extra = 2;
                else if ((b & 0xF8) == 0xF0) extra = 3;
                else return false;

                if (i + extra >= bytes.Length) return false;
                for (int k = 1; k <= extra; k++)
                {
                    if ((bytes[i + k] & 0xC0) != 0x80) return false;
                }
                i += extra + 1;
            }
            return true;
        }
    }
}
