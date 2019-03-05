using System.Runtime.InteropServices;

namespace ChromeTabs.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Win32Point
    {
        public int X;
        public int Y;
    }
}
