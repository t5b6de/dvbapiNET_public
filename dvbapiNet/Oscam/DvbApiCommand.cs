namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Auflistung aller DVBAPI-Befehle
    /// </summary>
    internal enum DvbApiCommand : uint
    {
        /*
        #define DVBAPI_PROTOCOL_VERSION   3
76	#define DVBAPI_MAX_PACKET_SIZE    262        // maximum possible packet size
77
78	#define DVBAPI_CA_GET_DESCR_INFO  0x80086F83
79	#define DVBAPI_CA_SET_DESCR       0x40106F86
80	#define DVBAPI_CA_SET_PID         0x40086F87
81	#define DVBAPI_CA_SET_DESCR_MODE  0x400C6F88
82	#define DVBAPI_CA_SET_DESCR_DATA  0x40186F89
83	//#define DVBAPI_DMX_START          0x00006F29 // in case we ever need this
84	#define DVBAPI_DMX_STOP           0x00006F2A
85	#define DVBAPI_DMX_SET_FILTER     0x403C6F2B
86
87	#define DVBAPI_AOT_CA             0x9F803000
88	#define DVBAPI_AOT_CA_PMT         0x9F803200 // least significant byte is length (ignored)
89	#define DVBAPI_AOT_CA_STOP        0x9F803F04
90	#define DVBAPI_FILTER_DATA        0xFFFF0000
91	#define DVBAPI_CLIENT_INFO        0xFFFF0001
92	#define DVBAPI_SERVER_INFO        0xFFFF0002
93	#define DVBAPI_ECM_INFO           0xFFFF0003
94
95	#define DVBAPI_INDEX_DISABLE      0xFFFFFFFF // only used for ca_pid_t

         */

        CaGetDescrInfo = 0x80086F83,
        CaSetDescr = 0x40106F86,
        CaSetPid = 0x40086F87,
        CaSetDescrMode = 0x400C6F88,
        CaSetDescrData = 0x40186F89,

        //DmxStart = 0x00006F29, // in case we ever need this
        DmxStop = 0x00006F2A,

        DmxSetFilter = 0x403C6F2B,
        AotCa = 0x9F803000,
        AotCaPmt = 0x9F803200, // least significant byte is length (ignored)
        AotCaStop = 0x9F803F04, // in Dokumentation ist das Stop Demux, in CaPmt notation ist das Stop Ca Device.
        FilterData = 0xFFFF0000,
        ClientInfo = 0xFFFF0001,
        ServerInfo = 0xFFFF0002,
        EcmInfo = 0xFFFF0003,
        IndexDisable = 0xFFFFFFFF
    }
}
