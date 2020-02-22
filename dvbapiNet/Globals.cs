using dvbapiNet.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace dvbapiNet
{
    public static class Globals
    {
        private const string cName = "dvbapiNET";
        private static IniFile _ConfigFile;
        private static IniFile _DefaultConfig;
        private static DirectoryInfo _HomeDir;

        private static string _Info;
        private static string _PipeName;

        public delegate void ExternalLog(string line);

        public static event ExternalLog LoggedLine;

        public static IniFile Config
        {
            get
            {
                LoadConfig();

                return _ConfigFile;
            }
        }

        public static IniFile Defaults
        {
            get
            {
                LoadDefaults();

                return _DefaultConfig;
            }
        }

        public static string Info
        {
            get
            {
                return _Info;
            }
        }

        public static string PipeName
        {
            get
            {
                return _PipeName;
            }
        }

        public static Assembly PluginAssembly
        {
            get
            {
                return typeof(Globals).Assembly;
            }
        }

        public static FileVersionInfo PluginInfo
        {
            get
            {
                return FileVersionInfo.GetVersionInfo(PluginAssembly.Location);
            }
        }

        public static DirectoryInfo HomeDirectory
        {
            get
            {
                return _HomeDir;
            }
        }

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

        public static FileInfo GetLogfile()
        {
            FileInfo f = new FileInfo(Path.Combine(_HomeDir.FullName, cName + ".log"));

            return f;
        }

        private static void LoadConfig()
        {
            if (_ConfigFile != null)
                return;

            FileInfo f = new FileInfo(Path.Combine(_HomeDir.FullName, cName + ".ini"));

            _ConfigFile = new IniFile(f);
        }

        internal static void ExternalLogHandler(string s)
        {
            LoggedLine?.Invoke(s);
        }

        private static void LoadDefaults()
        {
            if (_DefaultConfig != null)
                return;

            _DefaultConfig = new IniFile(null);

            /*
             * [dvbapi]
             * server=127.0.0.1
             * port=633
             * oldproto=1
             * offset=0
             *
             * [log]
             * debug=0
             *
             * [debug]
             * streamdump=0
             */

            _DefaultConfig.SetValue("dvbapi", "server", "127.0.0.1");
            _DefaultConfig.SetValue("dvbapi", "port", "633");
            _DefaultConfig.SetValue("dvbapi", "oldproto", "1");
            _DefaultConfig.SetValue("dvbapi", "offset", "0");

            _DefaultConfig.SetValue("log", "debug", "0");
            _DefaultConfig.SetValue("debug", "streamdump", "0");
        }
    }
}
