namespace dvbapiNet.Oscam.InterCom
{
    internal enum InterComCommand : int
    {
        // ApiClient -> Adapter
        SetInstanceNumber,

        /// <summary>
        /// Setzt Descrambler-Typ,
        /// CaIndex             32 bit Int
        /// Algo                32 bit Enum (DescramblerAlgo)
        /// Mode                32 bit Enum (DescramblerMode)
        /// </summary>
        SetDescramblerMode, //(Data) Setzt Descrambler-Typ // DVBCSA/AES/DES

        /// <summary>
        /// Setzt Zusatzdaten für descrambler, z.B. (extended) CW, Initialisierungsvektor...
        /// CaIndex             32 bit Int
        /// DescramblerParity   32 bit Enum (DescramblerParity)
        /// DescramblerDataType 32 bit Enum (DescramblerDataType)
        /// DataLength          32 bit Int
        /// Daten               byte[]
        /// </summary>
        SetDescramblerData,

        SetFilter,
        DelFilter,
        ClearFilter, // löscht alle filter

        /// <summary>
        /// sendet pid, die descrambled werden soll.
        /// </summary>
        SetPid,

        // beide Richtungen.
        Stop,

        // Adapter -> Api Client
        /// <summary>
        /// Sendet authentifizierungs-Token. Ist für den Server, der antwortet mit Instance.
        /// Nicht dass andere Prozesse Random auf dem Port rumhacken.
        /// </summary>
        Initiate,

        /// <summary>
        /// Übermittelt PMT-Section inkl Service ID, PMT Pid:
        /// An Oscam DVBAPI-Client
        /// Sid             32 bit Int
        /// PMT Pid         32 Bit int
        /// PMT Section     byte[]
        /// </summary>
        Pmt,

        /// <summary>
        /// Übermittelt gefilterte Daten an den DVBApi Client:
        /// Filter ID       32 Bit int
        /// Filtered Data   byte[]
        /// </summary>
        FilterData,
    }
}
