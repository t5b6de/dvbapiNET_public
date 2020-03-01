namespace dvbapiNet.DvbViewer
{
    /// <summary>
    /// Auflistung der DVBViewer Events
    /// </summary>
    public enum Event : uint
    {
        /// <summary>
        /// Entlädt Plugin
        /// </summary>
        Unload = 0,

        /// <summary>
        /// Initialisierung fertig
        /// </summary>
        InitComplete = 3,

        KeyPressed = 4,
        Action = 5,
        Remote = 6,
        Mouse = 9,

        /// <summary>
        /// Programm fertig gesetzt, Wiedergabe läuft
        /// </summary>
        FinishSetChannel = 10,

        MouseMove = 11,

        /// <summary>
        /// Im Datenfeld angegebener Sender wird nun getuned
        /// </summary>
        TuneChannel = 999,

        /// <summary>
        /// Im Datenfeld angegebener Sender wird nun untuned
        /// </summary>
        RemoveChannel = 998,

        EnableTuner = 1000,

        /// <summary>
        /// Tuner wird deaktiviert und damit laufendes Programm untuned
        /// </summary>
        DisableTuner = 1001
    }
}
