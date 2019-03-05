using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ChromeTabs.Interop;

namespace ChromeTabs.Utilities
{
    public static class WindowUtilities
    {
        private const int SwpNomove        = 0x0002;
        private const int SwpNosize        = 0x0001;            
        private const int SwpShowwindow    = 0x0040;
        private const int SwpNoactivate    = 0x0010;

        private const uint GwHwndnext = 2;

        public static void BringToFront(this Window window, bool activate = false)
        {
            window.Dispatcher.Invoke(() =>
            {
                window.Topmost = true;

                if (activate)
                    window.Activate();
            });

            window.Dispatcher.InvokeAsync(() => window.Topmost = false);

            //var handle = ((HwndSource) FromVisual(window))?.Handle;

            //if (handle == null)
            //    return;

            //SetWindowPos(handle.Value, 0, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow); // | SwpNoactivate
        }

        public static TWindow GetTopWindow<TWindow>(this Application app)
            where TWindow : Window
        {
            var unsorted = app.Windows.OfType<TWindow>();
            var byHandle = unsorted.ToDictionary(window => new WindowInteropHelper(window).Handle);

            for (var hWnd = GetTopWindow(IntPtr.Zero);
                hWnd != IntPtr.Zero;
                hWnd = GetWindow(hWnd, GwHwndnext))
            {
                if (byHandle.ContainsKey(hWnd))
                    return byHandle[hWnd];
            }

            return null;
        }

        /// <summary>
        /// Used P/Invoke to find and return the top window under the cursor position
        /// </summary>
        /// <param name="source"></param>
        /// <param name="screenPoint"></param>
        /// <returns></returns>
        public static TWindow FindWindowUnderAt<TWindow>(this TWindow source, Point screenPoint)  // WPF units (96dpi), not device units
            where TWindow : Window
        {
            var unsorted = Application.Current.Windows.OfType<TWindow>();
            var byHandle = unsorted.ToDictionary(window => new WindowInteropHelper(window).Handle);

            for (var hWnd = GetTopWindow(IntPtr.Zero);
                hWnd != IntPtr.Zero;
                hWnd = GetWindow(hWnd, GwHwndnext))
            {
                if (!byHandle.ContainsKey(hWnd))
                    continue;

                var win = byHandle[hWnd];

                if ((win.WindowState == WindowState.Maximized || new Rect(win.Left, win.Top, win.Width, win.Height).Contains(screenPoint))
                    && !Equals(win, source))
                {
                    return win;
                }
            }

            return null;
        }

        /// <summary>
        /// We need to do some P/Invoke magic to get the windows on screen
        /// </summary>
        /// <param name="unsorted"></param>
        /// <returns></returns>
        private static IEnumerable<Window> SortWindowsTopToBottom(IEnumerable<Window> unsorted)
        {
            var byHandle = unsorted.ToDictionary(window => new WindowInteropHelper(window).Handle);

            for (var hWnd = GetTopWindow(IntPtr.Zero);
                hWnd != IntPtr.Zero;
                hWnd = GetWindow(hWnd, GwHwndnext))
            {
                if (byHandle.ContainsKey(hWnd))
                    yield return byHandle[hWnd];
            }
        }

        #region image preview api

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref Win32MonitorInfo lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Win32Point pt, uint dwFlags);

        #endregion

        #region window api

        [DllImport("User32")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("User32")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint wCmd);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        #endregion
    }
}
