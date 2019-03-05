using System.Runtime.InteropServices;

namespace ChromeTabs.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Win32Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
