using System.Runtime.InteropServices;

namespace ChromeTabs.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Win32MonitorInfo
    {
        public int Size;
        public Win32Rect Monitor;
        public Win32Rect WorkArea;
        public uint Flags;
    }
}
