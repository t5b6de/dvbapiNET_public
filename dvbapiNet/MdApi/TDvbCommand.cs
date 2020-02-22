using System.Runtime.InteropServices;

namespace dvbapiNet.MdApi
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TDvbCommand
    {
        public ushort CommandLength;
        public ushort Command1;
        public ushort Command2;
        public ushort Parity;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ushort[] Cw;
    }
}
