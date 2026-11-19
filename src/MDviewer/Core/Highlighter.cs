using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MDviewer.Core
{
    /// <summary>
    /// Lightweight, dependency-free syntax highlighter.
    /// Produces span-wrapped HTML compatible with the theme's token colours.
    /// </summary>
    public static class Highlighter
    {
        private static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "c", "h", "cpp", "c++", "cc", "hpp", "cs", "csharp", "java", "js", "javascript", "jsx",
            "ts", "typescript", "tsx", "python", "py", "ruby", "rb", "go", "rust", "rs", "php",
            "sql", "css", "scss", "less", "html", "xml", "json", "yaml", "yml", "bash", "sh",
            "shell", "powershell", "ps1", "bat", "cmd", "ini", "toml"
        };

        public static bool CanHighlight(string lang)
        {
            return !string.IsNullOrEmpty(lang) && Supported.Contains(lang);
        }

        /// <summary>Highlights code, returning HTML with escaped text.</summary>
        public static string Highlight(string code, string lang)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            string l = (lang ?? "").ToLowerInvariant();

            try
            {
                switch (l)
                {
                    case "js": case "javascript": case "jsx":
                    case "ts": case "typescript": case "tsx":
                        return HighlightCLike(code, true);
                    case "json":
                        return HighlightJson(code);
                    case "css": case "scss": case "less":
                        return HighlightCss(code);
                    case "html": case "xml":
                        return HighlightMarkup(code);
                    case "python": case "py":
                        return HighlightPython(code);
                    case "c": case "h": case "cpp": case "c++": case "cc": case "hpp":
                    case "cs": case "csharp": case "java": case "go": case "rust": case "rs":
                    case "php": case "sql":
                        return HighlightCLike(code, false);
                    case "bash": case "sh": case "shell": case "powershell": case "ps1": case "bat": case "cmd":
                        return HighlightShell(code);
                    case "yaml": case "yml": case "ini": case "toml":
                        return HighlightConf(code);
                    default:
                        return MarkdownParser.Escape(code);
                }
            }
            catch
            {
                return MarkdownParser.Escape(code);
            }
        }

        // Token colours are resolved entirely through CSS classes.
        private static string Span(string cls, string text)
        {
            return "<span class=\"hl-" + cls + "\">" + MarkdownParser.Escape(text) + "</span>";
        }

        private static readonly Regex CLikeRx = new Regex(
            @"(?<comment>//[^\n]*|/\*[\s\S]*?\*/)" +
            @"|(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)" +
            @"|(?<number>\b0[xX][0-9a-fA-F]+\b|\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)" +
            @"|(?<keyword>\b(?:abstract|as|async|await|base|bool|break|byte|case|catch|char|class|const|continue|debugger|decimal|default|delegate|delete|do|double|else|enum|event|explicit|export|extends|extern|false|final|finally|float|for|foreach|from|func|function|get|global|go|if|implements|import|in|inline|instanceof|int|interface|internal|is|lambda|let|lock|long|namespace|native|new|nil|null|object|operator|out|override|package|params|private|protected|public|readonly|ref|register|return|sealed|set|short|signed|sizeof|static|strictfp|struct|super|switch|synchronized|this|throw|throws|transient|true|try|type|typedef|typeof|uint|ulong|undefined|union|unsigned|using|var|virtual|void|volatile|while|with|yield|fn|impl|mut|pub|use|mod|trait|match|loop|where|dyn|unsafe|println|print|self|Self)\b)" +
            @"|(?<func>\b[A-Za-z_]\w*(?=\s*\())" +
            @"|(?<type>\b[A-Z][A-Za-z0-9_]*\b)" +
            @"|(?<punct>[{}()\[\];,.<>+\-*/%=!&|^~?:@#]+)",
            RegexOptions.Compiled);

        private static string HighlightCLike(string code, bool jsMode)
        {
            var sb = new StringBuilder(code.Length * 2);
            int pos = 0;
            foreach (Match m in CLikeRx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["comment"].Success) sb.Append(Span("com", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else if (m.Groups["number"].Success) sb.Append(Span("num", m.Value));
                else if (m.Groups["keyword"].Success) sb.Append(Span("kw", m.Value));
                else if (m.Groups["func"].Success) sb.Append(Span("fn", m.Value));
                else if (m.Groups["type"].Success) sb.Append(Span("typ", m.Value));
                else sb.Append(Span("punct", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }

        private static string HighlightJson(string code)
        {
            var rx = new Regex(
                @"(?<key>""(?:\\.|[^""\\])*""(?=\s*:))" +
                @"|(?<string>""(?:\\.|[^""\\])*"")" +
                @"|(?<number>-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)" +
                @"|(?<keyword>\b(?:true|false|null)\b)" +
                @"|(?<punct>[{}\[\],:])");
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in rx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["key"].Success) sb.Append(Span("key", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else if (m.Groups["number"].Success) sb.Append(Span("num", m.Value));
                else if (m.Groups["keyword"].Success) sb.Append(Span("kw", m.Value));
                else sb.Append(Span("punct", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }

        private static string HighlightCss(string code)
        {
            var rx = new Regex(
                @"(?<comment>/\*[\s\S]*?\*/)" +
                @"|(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
                @"|(?<selector>[.#][\w-]+|\b[a-zA-Z][\w-]*(?=[^{};]*\{))" +
                @"|(?<prop>\b[a-zA-Z-]+(?=\s*:))" +
                @"|(?<number>\b\d+(?:\.\d+)?(?:px|em|rem|%|vh|vw|s|ms|pt|fr|deg)?\b)" +
                @"|(?<keyword>!important|@media|@import|@keyframes|@font-face)" +
                @"|(?<punct>[{}():;,>+~*])");
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in rx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["comment"].Success) sb.Append(Span("com", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else if (m.Groups["selector"].Success) sb.Append(Span("fn", m.Value));
                else if (m.Groups["prop"].Success) sb.Append(Span("kw", m.Value));
                else if (m.Groups["number"].Success) sb.Append(Span("num", m.Value));
                else if (m.Groups["keyword"].Success) sb.Append(Span("kw", m.Value));
                else sb.Append(Span("punct", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }

        private static string HighlightMarkup(string code)
        {
            var rx = new Regex(
                @"(?<comment><!--[\s\S]*?-->)" +
                @"|(?<tag></?[a-zA-Z][\w:-]*)" +
                @"|(?<attr>\s[a-zA-Z_:][\w:.-]*(?==))" +
                @"|(?<string>""[^""]*""|'[^']*')" +
                @"|(?<punct>>|/>)");
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in rx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["comment"].Success) sb.Append(Span("com", m.Value));
                else if (m.Groups["tag"].Success) sb.Append(Span("kw", m.Value));
                else if (m.Groups["attr"].Success) sb.Append(Span("fn", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else sb.Append(Span("punct", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }

        private static string HighlightPython(string code)
        {
            var rx = new Regex(
                @"(?<comment>#[^\n]*)" +
                @"|(?<string>""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
                @"|(?<decorator>@\w+)" +
                @"|(?<number>\b0[xXbBoO][0-9a-fA-F]+\b|\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)" +
                @"|(?<keyword>\b(?:and|as|assert|async|await|break|class|continue|def|del|elif|else|except|False|finally|for|from|global|if|import|in|is|lambda|None|nonlocal|not|or|pass|raise|return|True|try|while|with|yield|self)\b)" +
                @"|(?<func>\b[A-Za-z_]\w*(?=\s*\())" +
                @"|(?<punct>[{}()\[\];,.<>+\-*/%=!&|^~:]+)");
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in rx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["comment"].Success) sb.Append(Span("com", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else if (m.Groups["decorator"].Success) sb.Append(Span("fn", m.Value));
                else if (m.Groups["number"].Success) sb.Append(Span("num", m.Value));
                else if (m.Groups["keyword"].Success) sb.Append(Span("kw", m.Value));
                else if (m.Groups["func"].Success) sb.Append(Span("fn", m.Value));
                else sb.Append(Span("punct", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }

        private static string HighlightShell(string code)
        {
            var rx = new Regex(
                @"(?<comment>(?m:^)\s*#[^\n]*)" +
                @"|(?<string>""(?:\\.|[^""\\])*""|'[^']*')" +
                @"|(?<keyword>\b(?:if|then|else|elif|fi|for|while|do|done|case|esac|function|return|export|local|echo|cd|ls|cat|grep|sed|awk|source|exit|set|unset|alias|pip|npm|git|sudo)\b)" +
                @"|(?<var>\$\{?[\w@#?*]+\}?)" +
                @"|(?<number>\b\d+\b)" +
                @"|(?<punct>[{}()\[\];|&<>=\-]+)");
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in rx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["comment"].Success) sb.Append(Span("com", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else if (m.Groups["keyword"].Success) sb.Append(Span("kw", m.Value));
                else if (m.Groups["var"].Success) sb.Append(Span("var", m.Value));
                else if (m.Groups["number"].Success) sb.Append(Span("num", m.Value));
                else sb.Append(Span("punct", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }

        private static string HighlightConf(string code)
        {
            var rx = new Regex(
                @"(?<comment>#[^\n]*)" +
                @"|(?<key>(?m:^)\s*[\w.-]+(?=\s*[:=]))" +
                @"|(?<string>""(?:\\.|[^""\\])*""|'[^']*')" +
                @"|(?<number>\b\d+(?:\.\d+)?\b)" +
                @"|(?<keyword>\b(?:true|false|null|yes|no|on|off)\b)");
            var sb = new StringBuilder();
            int pos = 0;
            foreach (Match m in rx.Matches(code))
            {
                if (m.Index > pos) sb.Append(MarkdownParser.Escape(code.Substring(pos, m.Index - pos)));
                if (m.Groups["comment"].Success) sb.Append(Span("com", m.Value));
                else if (m.Groups["key"].Success) sb.Append(Span("kw", m.Value));
                else if (m.Groups["string"].Success) sb.Append(Span("str", m.Value));
                else if (m.Groups["number"].Success) sb.Append(Span("num", m.Value));
                else if (m.Groups["keyword"].Success) sb.Append(Span("kw", m.Value));
                pos = m.Index + m.Length;
            }
            if (pos < code.Length) sb.Append(MarkdownParser.Escape(code.Substring(pos)));
            return sb.ToString();
        }
    }
}
