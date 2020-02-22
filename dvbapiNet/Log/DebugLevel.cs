using System;

namespace dvbapiNet.Log
{
    [Flags]
    internal enum DebugLevel : uint
    {
        /// <summary>
        /// Allgemeine Fehler
        /// </summary>
        Error = 1,

        /// <summary>
        /// Warnungen
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Informationen
        /// </summary>
        Info = 4,

        /// <summary>
        /// Oscam ECM-Info
        /// </summary>
        EcmInfo = 8,

        /// <summary>
        /// Alle Meldungen bezüglich DVBApi
        /// </summary>
        DvbApi = 16,

        /// <summary>
        /// Alle Meldungen bezüglich DVBViewer Plugin Events
        /// </summary>
        DvbViewerPluginEvent = 32,

        /// <summary>
        /// Datenverkehr über Intercom-Sockets
        /// </summary>
        InterComEndpoint = 128,

        /// <summary>
        /// Befehle und Daten Bezogen auf Intercom-Client-Ebene (Plugin-Instanzen)
        /// </summary>
        InterComClientCommand = 256,

        /// <summary>
        /// Befehle und Daten bezogen auf Intercom-Server-Ebene (Hauptplugin-Instanz)
        /// </summary>
        InterComServerCommand = 512,

        /// <summary>
        /// CW-Log
        /// </summary>
        ControlWord = 1024,

        /// <summary>
        /// Schaltet Hex-Dump mit an.
        /// </summary>
        HexDump = 32768,
    }
}
