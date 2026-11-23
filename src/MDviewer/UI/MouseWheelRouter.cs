using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MDviewer.UI
{
    /// <summary>
    /// Sends wheel input to the outline list when the pointer hovers the sidebar.
    ///
    /// Windows delivers WM_MOUSEWHEEL to the focused window, so once the WebView2
    /// document owns focus (it does after opening a file or clicking a heading) the
    /// wheel always scrolls the page - even while the pointer is over the sidebar.
    /// The WebView2 input window lives in the browser process, so a WinForms message
    /// filter on the UI thread never sees those messages; a low-level mouse hook can
    /// intercept them, scroll the list and suppress the page scroll.
    /// </summary>
    internal sealed class MouseWheelRouter : IDisposable
    {
        private readonly Form _owner;
        private readonly Control _region;
        private readonly ListBox _target;
        private readonly Native.LowLevelMouseProc _proc;
        private IntPtr _hook;

        public MouseWheelRouter(Form owner, Control region, ListBox target)
        {
            _owner = owner;
            _region = region;
            _target = target;
            _proc = OnMouseHook; // keep the delegate alive for the hook

            try
            {
                IntPtr module = Native.GetModuleHandle(null);
                _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, module, 0);
            }
            catch
            {
                _hook = IntPtr.Zero;
            }
        }

        private IntPtr OnMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)Native.WM_MOUSEWHEEL)
                {
                    Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                        lParam, typeof(Native.MSLLHOOKSTRUCT));

                    if (IsHoveringRegion(new Point(info.pt.X, info.pt.Y)) && IsOwnerActive())
                    {
                        ScrollOutline(info.mouseData);
                        return (IntPtr)1; // swallow: never let the document scroll
                    }
                }
            }
            catch
            {
                // Never let an exception escape a hook callback.
            }

            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void ScrollOutline(uint mouseData)
        {
            int delta = (short)((mouseData >> 16) & 0xFFFF);
            if (delta == 0) return;

            int notches = Math.Abs(delta) / 120;
            if (notches == 0) notches = 1;

            int visible = Math.Max(1, _target.ClientSize.Height / Math.Max(1, _target.ItemHeight));
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines <= 0 ? visible : lines; // 0 means "one screen at a time"

            int next = _target.TopIndex + (delta > 0 ? -1 : 1) * notches * step;
            int maxTop = Math.Max(0, _target.Items.Count - visible);
            if (next < 0) next = 0;
            if (next > maxTop) next = maxTop;

            if (next != _target.TopIndex) _target.TopIndex = next;
        }

        private bool IsHoveringRegion(Point screenPoint)
        {
            if (_region == null || _target == null) return false;
            if (_region.IsDisposed || !_region.Visible || !_region.IsHandleCreated) return false;
            if (_target.IsDisposed || !_target.Visible || !_target.IsHandleCreated) return false;

            Rectangle bounds = _region.RectangleToScreen(_region.ClientRectangle);
            return bounds.Contains(screenPoint);
        }

        private bool IsOwnerActive()
        {
            if (_owner == null || _owner.IsDisposed || !_owner.IsHandleCreated) return false;
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            return Native.GetAncestor(foreground, Native.GA_ROOT) == _owner.Handle;
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                try { Native.UnhookWindowsHookEx(_hook); }
                catch { }
                _hook = IntPtr.Zero;
            }
        }
    }
}
