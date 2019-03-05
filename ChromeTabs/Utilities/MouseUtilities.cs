using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using ChromeTabs.Interop;

namespace ChromeTabs.Utilities
{
    public static class MouseUtilities
    {
        public static Point CorrectGetPosition(Visual relativeTo)
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);

            return relativeTo.PointFromScreen(new Point(w32Mouse.X, w32Mouse.Y));
        }

        public static Point GetScreenPosition()
        {
            var w32Mouse = new Win32Point();

            if (!GetCursorPos(ref w32Mouse))
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);
    }
}
