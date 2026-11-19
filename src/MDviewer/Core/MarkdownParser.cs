using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MDviewer.Core
{
    /// <summary>
    /// A self-contained CommonMark-flavoured Markdown parser.
    /// No external dependencies so the app stays fully offline and trivially portable.
    /// Emits GitHub-compatible HTML (tables, fenced code, task lists, strikethrough...).
    /// </summary>
    public sealed class MarkdownParser
    {
        private readonly bool _allowRawHtml;

        public MarkdownParser(bool allowRawHtml)
        {
            _allowRawHtml = allowRawHtml;
        }

        /// <summary>Parses markdown, collecting headings for the outline/sidebar.</summary>
        public string ToHtml(string markdown, out List<Heading> headings)
        {
            headings = new List<Heading>();
            if (string.IsNullOrEmpty(markdown)) return string.Empty;

            markdown = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
            // Strip UTF-8 BOM if present.
            if (markdown.Length > 0 && markdown[0] == '\uFEFF') markdown = markdown.Substring(1);

            string[] lines = markdown.Split('\n');

            // 1) Extract fenced code blocks first so their content is never interpreted.
            var blocks = new Dictionary<string, string>();
            lines = ExtractFencedCode(lines, blocks);

            // 2) Block level parsing.
            var html = new StringBuilder();
            ParseBlocks(lines, html, headings);

            // 3) Re-insert code blocks (already escaped).
            string result = html.ToString();
            foreach (var kv in blocks)
                result = result.Replace(kv.Key, kv.Value);

            return result;
        }

        // ---------------------------------------------------------------- fences

        private static string[] ExtractFencedCode(string[] lines, Dictionary<string, string> blocks)
        {
            var output = new List<string>(lines.Length);
            int i = 0;
            int counter = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                Match m = Regex.Match(line, @"^ {0,3}(`{3,}|~{3,})[ \t]*([^`]*)$");
                if (m.Success)
                {
                    string fence = m.Groups[1].Value;
                    string info = m.Groups[2].Value.Trim();
                    char fenceChar = fence[0];
                    int fenceLen = fence.Length;

                    var body = new StringBuilder();
                    i++;
                    bool closed = false;
                    while (i < lines.Length)
                    {
                        string cur = lines[i];
                        Match close = Regex.Match(cur, @"^ {0,3}(`{3,}|~{3,})[ \t]*$");
                        if (close.Success && close.Groups[1].Value[0] == fenceChar &&
                            close.Groups[1].Value.Length >= fenceLen)
                        {
                            closed = true;
                            i++;
                            break;
                        }
                        if (body.Length > 0) body.Append('\n');
                        body.Append(cur);
                        i++;
                    }
                    if (!closed)
                    {
                        // Unclosed fence: treat as normal text (spec behaviour is to close at EOF).
                    }

                    string placeholder = "\u0000CODEBLOCK" + (counter++) + "\u0000";
                    string lang = info;
                    int space = lang.IndexOfAny(new[] { ' ', '\t' });
                    if (space > 0) lang = lang.Substring(0, space);

                    var sb = new StringBuilder();
                    sb.Append("<div class=\"highlight highlight-source-" + Attr(lang) + " notranslate position-relative overflow-auto\">");
                    sb.Append("<pre class=\"mdviewer-code\">");
                    string langAttr = string.IsNullOrEmpty(lang) ? "" : " class=\"language-" + Attr(lang) + "\"";
                    sb.Append("<code" + langAttr + ">");
                    string rawCode = body.ToString();
                    if (Highlighter.CanHighlight(lang)) sb.Append(Highlighter.Highlight(rawCode, lang));
                    else sb.Append(Escape(rawCode));
                    sb.Append("</code></pre></div>");

                    blocks[placeholder] = sb.ToString();
                    output.Add(placeholder);
                }
                else
                {
                    output.Add(line);
                    i++;
                }
            }
            return output.ToArray();
        }

        // ------------------------------------------------------------ block level

        private void ParseBlocks(string[] lines, StringBuilder html, List<Heading> headings)
        {
            int i = 0;
            var paragraph = new StringBuilder();

            Action flushParagraph = () =>
            {
                if (paragraph.Length > 0)
                {
                    html.Append("<p>").Append(Inline(paragraph.ToString().TrimEnd('\n'))).Append("</p>\n");
                    paragraph.Clear();
                }
            };

            while (i < lines.Length)
            {
                string line = lines[i];

                // Blank line closes a paragraph.
                if (line.Trim().Length == 0)
                {
                    flushParagraph();
                    i++;
                    continue;
                }

                // Placeholder for code block.
                if (line.StartsWith("\u0000CODEBLOCK", StringComparison.Ordinal))
                {
                    flushParagraph();
                    html.Append(line).Append('\n');
                    i++;
                    continue;
                }

                // ATX heading.
                Match h = Regex.Match(line, @"^ {0,3}(#{1,6})(?:\s+(.*?))?\s*#*\s*$");
                if (h.Success)
                {
                    flushParagraph();
                    int level = h.Groups[1].Value.Length;
                    string text = h.Groups[2].Success ? h.Groups[2].Value.Trim() : "";
                    text = Regex.Replace(text, @"\s+#+\s*$", "");
                    string plain = StripInline(text);
                    string id = Slugify(plain);
                    headings.Add(new Heading { Level = level, Text = plain, Id = id });
                    html.Append("<h").Append(level).Append(" id=\"").Append(Attr(id)).Append("\">")
                        .Append(Inline(text)).Append("</h").Append(level).Append(">\n");
                    i++;
                    continue;
                }

                // Setext heading (=== / ---).
                if (i + 1 < lines.Length && line.Trim().Length > 0)
                {
                    string next = lines[i + 1];
                    if (Regex.IsMatch(next, @"^ {0,3}=+\s*$") && !IsBlockStart(line))
                    {
                        flushParagraph();
                        string text = line.Trim();
                        string plain = StripInline(text);
                        string id = Slugify(plain);
                        headings.Add(new Heading { Level = 1, Text = plain, Id = id });
                        html.Append("<h1 id=\"").Append(Attr(id)).Append("\">").Append(Inline(text)).Append("</h1>\n");
                        i += 2;
                        continue;
                    }
                    if (Regex.IsMatch(next, @"^ {0,3}-+\s*$") && !IsBlockStart(line) && line.Trim().Length > 0)
                    {
                        flushParagraph();
                        string text = line.Trim();
                        string plain = StripInline(text);
                        string id = Slugify(plain);
                        headings.Add(new Heading { Level = 2, Text = plain, Id = id });
                        html.Append("<h2 id=\"").Append(Attr(id)).Append("\">").Append(Inline(text)).Append("</h2>\n");
                        i += 2;
                        continue;
                    }
                }

                // Thematic break.
                if (Regex.IsMatch(line, @"^ {0,3}((\*\s*){3,}|(-\s*){3,}|(_\s*){3,})$"))
                {
                    flushParagraph();
                    html.Append("<hr>\n");
                    i++;
                    continue;
                }

                // Blockquote.
                if (Regex.IsMatch(line, @"^ {0,3}>"))
                {
                    flushParagraph();
                    var inner = new List<string>();
                    while (i < lines.Length && (Regex.IsMatch(lines[i], @"^ {0,3}>") || lines[i].Trim().Length == 0))
                    {
                        if (lines[i].Trim().Length == 0) break;
                        string l = Regex.Replace(lines[i], @"^ {0,3}> ?", "");
                        inner.Add(l);
                        i++;
                    }
                    var innerHtml = new StringBuilder();
                    var dummy = new List<Heading>();
                    ParseBlocks(inner.ToArray(), innerHtml, dummy);
                    foreach (var dd in dummy) headings.Add(dd);
                    html.Append("<blockquote>\n").Append(innerHtml).Append("</blockquote>\n");
                    continue;
                }

                // HTML block (raw pass-through when allowed).
                if (_allowRawHtml && Regex.IsMatch(line, @"^ {0,3}<[a-zA-Z!/]") && LooksLikeHtmlBlock(line))
                {
                    flushParagraph();
                    var block = new StringBuilder();
                    while (i < lines.Length && lines[i].Trim().Length > 0)
                    {
                        block.Append(lines[i]).Append('\n');
                        i++;
                    }
                    html.Append(block);
                    continue;
                }

                // List.
                if (Regex.IsMatch(line, @"^ {0,3}([-*+]|\d{1,9}[.)])\s+"))
                {
                    flushParagraph();
                    i = ParseList(lines, i, html, headings);
                    continue;
                }

                // Table.
                if (line.Contains("|") && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
                {
                    flushParagraph();
                    i = ParseTable(lines, i, html);
                    continue;
                }

                // Indented code block (4 spaces) — only when we are not in a paragraph.
                if (paragraph.Length == 0 && Regex.IsMatch(line, @"^ {4,}"))
                {
                    var code = new StringBuilder();
                    while (i < lines.Length && (Regex.IsMatch(lines[i], @"^ {4,}") || lines[i].Trim().Length == 0))
                    {
                        if (lines[i].Trim().Length == 0 && (i + 1 >= lines.Length || !Regex.IsMatch(lines[i + 1], @"^ {4,}"))) break;
                        code.Append(lines[i].Length >= 4 ? lines[i].Substring(4) : "").Append('\n');
                        i++;
                    }
                    html.Append("<pre class=\"mdviewer-code\"><code>").Append(Escape(code.ToString().TrimEnd('\n'))).Append("</code></pre>\n");
                    continue;
                }

                // Default: paragraph accumulation.
                if (paragraph.Length > 0) paragraph.Append('\n');
                paragraph.Append(line.TrimStart());
                i++;
            }

            flushParagraph();
        }

        private static bool LooksLikeHtmlBlock(string line)
        {
            // Only treat as raw HTML if line looks like a complete tag / comment start.
            string t = line.TrimStart();
            if (t.StartsWith("<!--", StringComparison.Ordinal)) return true;
            if (t.StartsWith("<!", StringComparison.Ordinal)) return true;
            if (Regex.IsMatch(t, @"^</?[a-zA-Z][a-zA-Z0-9-]*(\s|>|/>|$)")) return true;
            return false;
        }

        private static bool IsBlockStart(string line)
        {
            return Regex.IsMatch(line, @"^ {0,3}(#{1,6}\s|>|[-*+]\s|\d{1,9}[.)]\s)");
        }

        // ---------------------------------------------------------------- lists

        private int ParseList(string[] lines, int start, StringBuilder html, List<Heading> headings)
        {
            Match first = Regex.Match(lines[start], @"^( {0,3})([-*+]|\d{1,9}[.)])(\s+)(.*)$");
            bool ordered = char.IsDigit(first.Groups[2].Value[0]);
            string marker = first.Groups[2].Value;
            string startNum = ordered ? marker.TrimEnd('.', ')') : null;

            var items = new List<List<string>>();
            var current = new List<string>();
            int i = start;
            bool loose = false;
            int contentIndent = first.Groups[1].Length + marker.Length + Math.Min(first.Groups[3].Length, 4);

            while (i < lines.Length)
            {
                string line = lines[i];
                Match m = Regex.Match(line, @"^( {0,3})([-*+]|\d{1,9}[.)])(\s+)(.*)$");
                bool sameType = m.Success && (char.IsDigit(m.Groups[2].Value[0]) == ordered) &&
                                (m.Groups[1].Length <= 3);

                if (sameType)
                {
                    if (current.Count > 0) items.Add(current);
                    current = new List<string> { m.Groups[4].Value };
                    i++;
                    continue;
                }

                if (line.Trim().Length == 0)
                {
                    // Possible end of list or loose item separator.
                    bool nextIsItem = i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^( {0,3})([-*+]|\d{1,9}[.)])\s+");
                    bool nextIsContinuation = i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^\s{2,}\S");
                    if (nextIsItem || nextIsContinuation)
                    {
                        loose = true;
                        if (current.Count > 0) current.Add("");
                        i++;
                        continue;
                    }
                    break;
                }

                // Continuation line (indented) or lazy continuation.
                if (Regex.IsMatch(line, @"^\s+") || (current.Count > 0 && line.Trim().Length > 0))
                {
                    if (Regex.IsMatch(line, @"^\s+"))
                    {
                        int strip = Math.Min(line.Length - line.TrimStart().Length, contentIndent);
                        current.Add(line.Substring(strip));
                    }
                    else
                    {
                        current.Add(line);
                    }
                    i++;
                    continue;
                }

                break;
            }
            if (current.Count > 0) items.Add(current);

            string tag = ordered ? "ol" : "ul";
            html.Append('<').Append(tag);
            if (ordered && startNum != null && startNum != "1")
                html.Append(" start=\"").Append(Attr(startNum)).Append('"');
            // Detect task list.
            bool hasTask = false;
            foreach (var it in items)
                if (it.Count > 0 && Regex.IsMatch(it[0], @"^\[[ xX]\]\s")) { hasTask = true; break; }
            if (hasTask) html.Append(" class=\"contains-task-list\"");
            html.Append(">\n");

            foreach (var item in items)
            {
                string firstLine = item.Count > 0 ? item[0] : "";
                bool task = Regex.IsMatch(firstLine, @"^\[[ xX]\]\s");
                bool isChecked = task && Regex.IsMatch(firstLine, @"^\[[xX]\]");

                html.Append("<li");
                if (task) html.Append(" class=\"task-list-item\"");
                html.Append('>');

                if (task)
                {
                    firstLine = Regex.Replace(firstLine, @"^\[[ xX]\]\s*", "");
                    item[0] = firstLine;
                    html.Append("<input type=\"checkbox\" class=\"task-list-item-checkbox\" disabled");
                    if (isChecked) html.Append(" checked");
                    html.Append("> ");
                }

                var itemHtml = new StringBuilder();
                var itemHeadings = new List<Heading>();
                var itemLines = item.ToArray();
                // A tight (non-loose) single-paragraph item gets no <p> wrapper.
                bool singlePara = !loose && item.Count == 1;
                if (singlePara)
                {
                    itemHtml.Append(Inline(itemLines[0].Trim()));
                }
                else
                {
                    ParseBlocks(itemLines, itemHtml, itemHeadings);
                }
                foreach (var hh in itemHeadings) headings.Add(hh);

                string itemStr = itemHtml.ToString();
                if (singlePara && itemStr.StartsWith("<p>", StringComparison.Ordinal) && itemStr.EndsWith("</p>", StringComparison.Ordinal))
                    itemStr = itemStr.Substring(3, itemStr.Length - 7);

                html.Append(itemStr);
                html.Append("</li>\n");
            }

            html.Append("</").Append(tag).Append(">\n");
            return i;
        }

        // --------------------------------------------------------------- tables

        private static bool IsTableSeparator(string line)
        {
            string t = line.Trim();
            if (!t.Contains("-")) return false;
            return Regex.IsMatch(t, @"^\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?$");
        }

        private int ParseTable(string[] lines, int start, StringBuilder html)
        {
            var headerCells = SplitRow(lines[start]);
            var aligns = new List<string>();
            foreach (var sep in SplitRow(lines[start + 1]))
            {
                string s = sep.Trim();
                bool left = s.StartsWith(":", StringComparison.Ordinal);
                bool right = s.EndsWith(":", StringComparison.Ordinal);
                if (left && right) aligns.Add("center");
                else if (right) aligns.Add("right");
                else if (left) aligns.Add("left");
                else aligns.Add("");
            }

            html.Append("<div class=\"mdviewer-table-wrap\"><table>\n<thead>\n<tr>");
            for (int c = 0; c < headerCells.Count; c++)
            {
                string align = c < aligns.Count && aligns[c].Length > 0 ? " style=\"text-align:" + aligns[c] + "\"" : "";
                html.Append("<th").Append(align).Append('>').Append(Inline(headerCells[c].Trim())).Append("</th>");
            }
            html.Append("</tr>\n</thead>\n<tbody>\n");

            int i = start + 2;
            while (i < lines.Length && lines[i].Trim().Length > 0 && lines[i].Contains("|"))
            {
                var cells = SplitRow(lines[i]);
                html.Append("<tr>");
                for (int c = 0; c < headerCells.Count; c++)
                {
                    string align = c < aligns.Count && aligns[c].Length > 0 ? " style=\"text-align:" + aligns[c] + "\"" : "";
                    string val = c < cells.Count ? cells[c].Trim() : "";
                    html.Append("<td").Append(align).Append('>').Append(Inline(val)).Append("</td>");
                }
                html.Append("</tr>\n");
                i++;
            }
            html.Append("</tbody>\n</table></div>\n");
            return i;
        }

        private static List<string> SplitRow(string line)
        {
            string t = line.Trim();
            if (t.StartsWith("|", StringComparison.Ordinal)) t = t.Substring(1);
            if (t.EndsWith("|", StringComparison.Ordinal) && !t.EndsWith("\\|", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);

            var cells = new List<string>();
            var cur = new StringBuilder();
            for (int i = 0; i < t.Length; i++)
            {
                char ch = t[i];
                if (ch == '\\' && i + 1 < t.Length && t[i + 1] == '|')
                {
                    cur.Append('|');
                    i++;
                    continue;
                }
                if (ch == '\\' && i + 1 < t.Length)
                {
                    cur.Append(ch).Append(t[i + 1]);
                    i++;
                    continue;
                }
                if (ch == '|')
                {
                    cells.Add(cur.ToString());
                    cur.Clear();
                    continue;
                }
                cur.Append(ch);
            }
            cells.Add(cur.ToString());
            return cells;
        }

        // ---------------------------------------------------------- inline level

        private static readonly Regex InlineCodeRx = new Regex(@"(`+)(.+?)\1", RegexOptions.Singleline);
        private static readonly Regex ImageRx = new Regex(@"!\[([^\]]*)\]\(\s*<?([^)\s>]+)>?(?:\s+""([^""]*)"")?\s*\)");
        private static readonly Regex LinkRx = new Regex(@"\[((?:[^\[\]]|\[[^\]]*\])*)\]\(\s*<?([^)\s>]+)>?(?:\s+""([^""]*)""\s*)?\)");
        private static readonly Regex AutolinkRx = new Regex(@"<((?:https?://|ftp://|mailto:)[^\s<>]+)>");
        private static readonly Regex RawUrlRx = new Regex(@"(?<![""'>=\]])\b(https?://[^\s<>""']+[^\s<>""'.,;:)\]])");

        public string Inline(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Protect inline code spans and raw HTML tags.
            var stash = new List<string>();
            Func<string, string> store = s =>
            {
                stash.Add(s);
                return "\u0001" + (stash.Count - 1) + "\u0001";
            };

            text = InlineCodeRx.Replace(text, m =>
            {
                string content = m.Groups[2].Value.Trim();
                return store("<code>" + Escape(content) + "</code>");
            });

            // Images.
            text = ImageRx.Replace(text, m =>
            {
                string alt = m.Groups[1].Value;
                string src = m.Groups[2].Value;
                string title = m.Groups[3].Success ? m.Groups[3].Value : null;
                string titleAttr = title != null ? " title=\"" + Attr(title) + "\"" : "";
                return store("<img src=\"" + Attr(src) + "\" alt=\"" + Attr(alt) + "\"" + titleAttr + ">");
            });

            // Links.
            text = LinkRx.Replace(text, m =>
            {
                string label = m.Groups[1].Value;
                string href = m.Groups[2].Value;
                string title = m.Groups[3].Success ? m.Groups[3].Value : null;
                string titleAttr = title != null ? " title=\"" + Attr(title) + "\"" : "";
                string inner = InlineInline(label);
                return store("<a href=\"" + Attr(href) + "\"" + titleAttr + ">" + inner + "</a>");
            });

            // Autolinks <http://...>
            text = AutolinkRx.Replace(text, m =>
            {
                string url = m.Groups[1].Value;
                return store("<a href=\"" + Attr(url) + "\">" + Escape(url) + "</a>");
            });

            // Raw inline HTML tags pass through.
            if (_allowRawHtml)
            {
                text = Regex.Replace(text, @"</?[a-zA-Z][a-zA-Z0-9-]*(\s+[^<>]*?)?/?>", m => store(m.Value));
            }

            // Escape everything that is left.
            text = Escape(text);

            // Emphasis: bold, italic, strikethrough.
            text = Regex.Replace(text, @"\*\*\*(?=\S)(.+?)(?<=\S)\*\*\*", "<strong><em>$1</em></strong>");
            text = Regex.Replace(text, @"___(?=\S)(.+?)(?<=\S)___", "<strong><em>$1</em></strong>");
            text = Regex.Replace(text, @"\*\*(?=\S)(.+?)(?<=\S)\*\*", "<strong>$1</strong>");
            text = Regex.Replace(text, @"(?<!\w)__(?=\S)(.+?)(?<=\S)__(?!\w)", "<strong>$1</strong>");
            text = Regex.Replace(text, @"(?<![\*\w])\*(?=\S)(.+?)(?<=\S)\*(?![\*\w])", "<em>$1</em>");
            text = Regex.Replace(text, @"(?<![\w_])_(?=\S)(.+?)(?<=\S)_(?![\w_])", "<em>$1</em>");
            text = Regex.Replace(text, @"~~(?=\S)(.+?)(?<=\S)~~", "<del>$1</del>");

            // Bare URLs become links.
            text = RawUrlRx.Replace(text, m =>
            {
                string url = m.Groups[1].Value;
                return "<a href=\"" + url + "\">" + url + "</a>";
            });

            // Line break: two spaces at end of line, or backslash.
            text = Regex.Replace(text, @"(?: {2,}|\\)\n", "<br>\n");

            // Restore stashed fragments.
            text = Regex.Replace(text, "\u0001(\\d+)\u0001", m =>
            {
                int idx = int.Parse(m.Groups[1].Value);
                return idx < stash.Count ? stash[idx] : m.Value;
            });

            return text;
        }

        /// <summary>Inline rendering used inside link labels (no nested anchors).</summary>
        private string InlineInline(string text)
        {
            string html = Inline(text);
            return html;
        }

        // ------------------------------------------------------------- utilities

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string Attr(string s)
        {
            return Escape(s ?? string.Empty);
        }

        public static string StripInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string t = InlineCodeRx.Replace(text, "$2");
            t = ImageRx.Replace(t, "$1");
            t = LinkRx.Replace(t, "$1");
            t = Regex.Replace(t, @"(\*\*\*|\*\*|\*|___|__|_|~~|`)", "");
            t = Regex.Replace(t, "<[^>]+>", "");
            t = Regex.Replace(t, @"\\([\\`*_{}\[\]()#+\-.!>])", "$1");
            return t.Trim();
        }

        public static string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "section";
            string s = text.ToLowerInvariant();
            s = Regex.Replace(s, @"[^\w\u00C0-\uFFFF\s-]", "");
            s = Regex.Replace(s, @"\s+", "-");
            s = s.Trim('-');
            if (s.Length == 0) s = "section";
            return s;
        }
    }

    public sealed class Heading
    {
        public int Level { get; set; }
        public string Text { get; set; }
        public string Id { get; set; }
    }
}
