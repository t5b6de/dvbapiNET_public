using System.Runtime.InteropServices;

namespace dvbapiNet.DvbViewer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TChannel
    {
        //TTunerType TunerType;
        public byte TunerType;

        public byte Group;

        //TChannelGroup Group;
        public byte Unused1;

        public byte Flags;
        public uint Frequency;
        public uint SymbolRate;
        public ushort LOF;
        public ushort PmtPid;
        public byte Volume;
        public byte Unused;
        public byte SatModulation;
        public byte AVFormat;
        public byte FEC;
        public byte AudioChannel;
        public ushort Unused3;
        public byte Polarity;
        public byte Unused4;
        public ushort Unused5;
        public byte Tone;
        public byte EPGFlag;
        public ushort DiSEqCValue;
        public byte DiSEqC;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Lang;

        public ushort AudioPid;
        public byte NetworkNr;
        public byte Favourite;
        public ushort VideoPid;
        public ushort TransportStreamId;
        public ushort TelePid;
        public ushort NetworkId;
        public ushort Sid;
        public ushort PcrPid;
    }
}
