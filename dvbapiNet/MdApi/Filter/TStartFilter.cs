using System;
using System.Runtime.InteropServices;

namespace dvbapiNet.MdApi.Filter
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TStartFilter
    {
        public ushort DllId;
        public ushort FilterId;
        public ushort Pid;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Name;

        public IntPtr FilterCallback;
        public int RunningId;

        public uint ExtSize;
        public byte SectionMask;
        public byte SectionData;
        public byte m185Mode; // bool
        public FilterType Type;
    }
}
