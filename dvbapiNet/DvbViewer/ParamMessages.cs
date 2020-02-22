namespace dvbapiNet.DvbViewer
{
    internal enum ParamMessages : uint
    {
        Version = 0x1018,
        AddCall = 0x2210,
        DelCall = 0x2211,
        AddTsCall = 0x2214,
        DelTsCall = 0x2215,
        StartPidCallback = 0x45104,
        StopPidCallback = 0x45105,

        Remote = 0x00815,
        IniRefresh = 0x1020,
        EpgChannel = 0x1031,
        SetSid = 0x2100,
        GetSid = 0x2101,
        StartFilter = 0x2120,
        StopFilter = 0x2130,

        VcrGetCount = 0x2300,
        VcrGetItem = 0x2301,
        VcrSetItem = 0x2302,
        VcrDeleteItem = 0x2303,
        GetColorKey = 0x2005,
        IniLoad = 0x1020,
        IniSave = 0x1021,
        Shutdown = 0x2207,

        SetEpg = 0x2310,
        GetEpg = 0x2311,
        EpgSave = 0x2312,
        EpgSaveAll = 0x2313,
        EpgLoad = 0x2314,
        GetVtPage = 0x2200,
        AvState = 0x2213,
        DvbStandby = 0x2320
    }
}
