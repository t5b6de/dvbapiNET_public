using System.Runtime.InteropServices;

namespace dvbapiNet.MdApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TPIDFilters
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        private byte[] FilterName;

        private byte FilterId;
        private ushort PID;
    };
}
