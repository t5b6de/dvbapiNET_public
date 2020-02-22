using System.Runtime.InteropServices;

namespace dvbapiNet.MdApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TProgramm82
    {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] Name;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] Anbieter;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] Land;

        public uint freq;
        public byte Typ;
        public byte volt;
        public byte afc;

        public byte diseqc;
        public uint srate;

        public byte qam;
        public byte fec;
        public byte norm;

        public ushort TransportStreamId;
        public ushort VideoPid;
        public ushort AudioPid;
        public ushort TeletextPid;          /* Teletext PID */

        public ushort PmtPid;
        public ushort PcrPid;
        public ushort EcmPid;
        public ushort ServiceId;
        public ushort AC3_pid;

        public byte TVType; //  == 00 PAL ; 11 == NTSC
        public byte ServiceTyp;
        public byte CA_ID;
        public ushort Temp_Audio;
        public ushort Filteranzahl;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public TPIDFilters[] Filters;

        public ushort CaCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public TCA_System82[] CaSystem;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] CA_Land;

        public byte Merker;
        public short Link_TP;
        public short Link_SID;
        public byte Dynamisch;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Extern_Buffer;
    };
}
