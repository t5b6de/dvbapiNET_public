namespace dvbapiNet.MdApi
{
    internal enum MdApiMessages : uint
    {
        GetTransponder = 0x01020000,
        SetTransponder = 0x01020001,
        GetProgram = 0x01020010,
        SetProgram = 0x01020011,
        RescanProgram = 0x01020012,
        SaveProgram = 0x01020013,
        GetProgramNumber = 0x01020014,
        SetProgramNumber = 0x01020015,
        StartFilter = 0x01020020,
        StopFilter = 0x01020021,
        ScanCurrentTp = 0x01020030,
        ScanCurrentCat = 0x01020031,
        DvbCommand = 0x01020060,
        DvbSetDescrCmd = 0x0410
    }
}
