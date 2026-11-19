using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MDviewer.UI
{
    /// <summary>Owner-drawn outline list used by the sidebar.</summary>
    public sealed class OutlineList : ListBox
    {
        private readonly List<MDviewer.Core.Heading> _items = new List<MDviewer.Core.Heading>();
        private int _activeIndex = -1;
        private bool _suppressSelection;
        private int _hoverIndex = -1;

        public event EventHandler<HeadingEventArgs> HeadingActivated;

        public OutlineList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            SelectionMode = SelectionMode.One;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            ItemHeight = 30;
            BackColor = Theme.Current.SidebarBg;
            ForeColor = Theme.Current.Text;
        }

        public IList<MDviewer.Core.Heading> Headings
        {
            get { return _items; }
        }

        public int ActiveIndex
        {
            get { return _activeIndex; }
            set
            {
                if (_activeIndex == value) return;
                int old = _activeIndex;
                _activeIndex = value;
                if (old >= 0 && old < _items.Count) InvalidateItem(old);
                if (value >= 0 && value < _items.Count) InvalidateItem(value);
            }
        }

        public void SetHeadings(List<MDviewer.Core.Heading> headings)
        {
            _items.Clear();
            if (headings != null) _items.AddRange(headings);
            _activeIndex = -1;
            _suppressSelection = true;
            BeginUpdate();
            Items.Clear();
            foreach (MDviewer.Core.Heading h in _items) Items.Add(h.Text);
            EndUpdate();
            _suppressSelection = false;
        }

        private void InvalidateItem(int index)
        {
            try
            {
                if (index < 0 || index >= Items.Count) return;
                Rectangle r = GetItemRectangle(index);
                if (r.Height > 0) Invalidate(r);
            }
            catch { }
        }

        public void ApplyTheme()
        {
            Theme t = Theme.Current;
            BackColor = t.SidebarBg;
            ForeColor = t.Text;
            TryThemeScrollBar();
            Invalidate();
        }

        private void TryThemeScrollBar()
        {
            try
            {
                if (IsHandleCreated)
                    Native.SetWindowTheme(Handle, Theme.Current.IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryThemeScrollBar();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _items.Count) return;

            Theme t = Theme.Current;
            MDviewer.Core.Heading h = _items[e.Index];
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle row = new Rectangle(0, e.Bounds.Y, Width, e.Bounds.Height);

            // Background.
            using (var bg = new SolidBrush(t.SidebarBg))
                g.FillRectangle(bg, row);

            bool active = e.Index == _activeIndex;
            bool hover = e.Index == _hoverIndex;

            Rectangle pill = new Rectangle(6, row.Y + 2, Width - 12, row.Height - 4);
            if (active)
            {
                Ui.Fill(this, g, t.SidebarSelected, pill, Ui.S(this, 6));
                using (var accent = new SolidBrush(t.Accent))
                {
                    int barW = Math.Max(2, Ui.S(this, 3));
                    Rectangle bar = new Rectangle(6, pill.Y + Ui.S(this, 6), barW, pill.Height - Ui.S(this, 12));
                    Ui.Fill(this, g, t.Accent, bar, barW);
                }
            }
            else if (hover)
            {
                Ui.Fill(this, g, t.Hover, pill, Ui.S(this, 6));
            }

            // Indentation and typography per heading level.
            int indent = 10 + (Math.Max(1, h.Level) - 1) * Ui.S(this, 13);
            int rightPad = Ui.S(this, 10);

            float fontSize;
            FontStyle style;
            Color fg;
            switch (h.Level)
            {
                case 1:
                    fontSize = 9.75f; style = FontStyle.Bold; fg = active ? t.Accent : t.Text; break;
                case 2:
                    fontSize = 9.25f; style = FontStyle.Bold; fg = active ? t.Accent : t.Text; break;
                case 3:
                    fontSize = 9f; style = FontStyle.Regular; fg = active ? t.Accent : t.TextMuted; break;
                default:
                    fontSize = 8.75f; style = FontStyle.Regular; fg = active ? t.Accent : t.TextMuted; break;
            }

            Rectangle textRect = new Rectangle(
                indent, row.Y, Math.Max(10, Width - indent - rightPad), row.Height);

            using (var font = Ui.UiFont(this, fontSize, style))
            {
                TextRenderer.DrawText(
                    g, h.Text, font, textRect, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = IndexFromPoint(e.Location);
            if (idx != _hoverIndex)
            {
                int old = _hoverIndex;
                _hoverIndex = idx;
                if (old >= 0) InvalidateItem(old);
                if (idx >= 0) InvalidateItem(idx);
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            int old = _hoverIndex;
            _hoverIndex = -1;
            if (old >= 0) InvalidateItem(old);
            Cursor = Cursors.Default;
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            if (_suppressSelection) return;
            if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
            {
                ActiveIndex = SelectedIndex;
                if (HeadingActivated != null)
                    HeadingActivated(this, new HeadingEventArgs(_items[SelectedIndex]));
            }
        }

        /// <summary>Selects the heading reported by the page without raising the navigate event.</summary>
        public void SyncActive(string id)
        {
            int idx = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Id, id, StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }
            ActiveIndex = idx;
            if (idx >= 0)
            {
                _suppressSelection = true;
                try { SelectedIndex = idx; } catch { }
                _suppressSelection = false;
                try
                {
                    int visible = Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
                    if (idx < TopIndex) TopIndex = idx;
                    else if (idx >= TopIndex + visible) TopIndex = Math.Max(0, idx - visible + 1);
                }
                catch { }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ItemHeight = Math.Max(Ui.S(this, 26), Ui.S(this, 30));
        }
    }

    public sealed class HeadingEventArgs : EventArgs
    {
        public MDviewer.Core.Heading Heading { get; private set; }

        public HeadingEventArgs(MDviewer.Core.Heading heading)
        {
            Heading = heading;
        }
    }
}
