using dvbapiNet.Log;
using dvbapiNet.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace dvbapiNet
{
    /// <summary>
    /// Stellt Global benötigte Eigenschaften bereit
    /// </summary>
    public static class Globals
    {
        private const string cName = "dvbapiNET";
        private static Configuration _Config;
        private static DirectoryInfo _HomeDir;

        private static string _Info;
        private static string _PipeName;

        public delegate void ExternalLog(string line);

        public static event ExternalLog LoggedLine;

        /// <summary>
        /// Gibt die Plugin-Konfiguration zurück
        /// </summary>
        public static Configuration Config
        {
            get
            {
                LoadConfig();

                return _Config;
            }
        }

        /// <summary>
        /// Gibt die PLugin-Information zurück, z.b. dvbapiNET x.y (#01234abcd)
        /// </summary>
        public static string Info
        {
            get
            {
                return _Info;
            }
        }

        /// <summary>
        /// Gibt den Pipe-Namen an, der für die Mitteilung der Internen Kommunikationsparameter dient.
        /// </summary>
        public static string PipeName
        {
            get
            {
                return _PipeName;
            }
        }

        /// <summary>
        /// Gibt die lokale Assembly zurück
        /// </summary>
        public static Assembly PluginAssembly
        {
            get
            {
                return typeof(Globals).Assembly;
            }
        }

        /// <summary>
        /// Gibt die FileVersionInfo dieses Plugins zurück.
        /// </summary>
        public static FileVersionInfo PluginInfo
        {
            get
            {
                return FileVersionInfo.GetVersionInfo(PluginAssembly.Location);
            }
        }

        /// <summary>
        /// Gibt das Konfigurationsverzeichnis für dieses Plugin an
        /// </summary>
        public static DirectoryInfo HomeDirectory
        {
            get
            {
                return _HomeDir;
            }
        }

        /// <summary>
        /// Initialisiert die globalen Variablen
        /// </summary>
        static Globals()
        {
            _HomeDir = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), cName));

            try
            {
                if (!_HomeDir.Exists)
                    _HomeDir.Create();
            }
            catch { }

            Assembly asm = PluginAssembly;
            Version ver = asm.GetName().Version;

            string hexBuild = ver.Build.ToString("x4") + ver.Revision.ToString("x4");

            _PipeName = $"{cName}.{hexBuild}";

            _Info = $"{cName} v{ver.Major}.{ver.Minor} (#{hexBuild})";
        }

        /// <summary>
        /// Gibt den Pfad für die Logdatei zurück.
        /// </summary>
        /// <returns></returns>
        public static FileInfo GetLogfile()
        {
            FileInfo f = new FileInfo(Path.Combine(_HomeDir.FullName, cName + ".log"));

            return f;
        }

        /// <summary>
        /// Lädt die Konfiguration, sofern vorhanden.
        /// </summary>
        private static void LoadConfig()
        {
            if (_Config != null)
                return;

            FileInfo f = new FileInfo(Path.Combine(_HomeDir.FullName, cName + ".ini"));

            IniFile cfg = new IniFile(f);

            IniFile def = new IniFile(null);

            /*
             * [dvbapi]
             * server=127.0.0.1
             * port=633
             * offset=0
             *
             * [log]
             * debug=0
             * pretty=1
             *
             * [debug]
             * streamdump=0
             */

            def.SetValue("dvbapi", "server", "127.0.0.1");
            def.SetValue("dvbapi", "port", "633");
            def.SetValue("dvbapi", "offset", "0");

            def.SetValue("log", "debug", "0");
            def.SetValue("log", "pretty", "1");
            def.SetValue("debug", "streamdump", "0");

            _Config = new Configuration(cfg, def);
        }

        /// <summary>
        /// Ruft das LoggedLine Ereignis auf, wird vom LogProvider verwendet
        /// </summary>
        /// <param name="s"></param>
        internal static void ExternalLogHandler(string s)
        {
            LoggedLine?.Invoke(s);
        }

        public static void Dispose()
        {
            LogProvider.Dispose();
        }
    }
}
