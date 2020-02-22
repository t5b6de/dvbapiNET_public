using System.Runtime.InteropServices;

namespace dvbapiNet.MdApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TCA_System82
    {
        private ushort CA_Typ;
        private ushort ECM;
        private ushort EMM;
        private uint Provider_Id;
    };
}
