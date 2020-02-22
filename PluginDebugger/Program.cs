using dvbapiNet;
using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace PluginDebugger
{
    /// <summary>
    /// Test-Tool um die Internen Plugin-Module (DVBAPI Client, DVBAPI-Adapter usw.) zu testen
    /// </summary>
    internal class Program
    {
        //// Service zum Teste:
        //private static int[] _ServiceId = new int[] { 1, 2 };

        //private const string _SourceFile = @"Z:\ts-dumps\cissa-sample.ts";
        //private const bool _WriteBack = true; // zurückschreiben um entschl. zu testen.

        //private const string cConfigSection = "dvbapi";
        //private static FileInfo _TsFi = new FileInfo(_SourceFile);
        //private static DvbApiAdapter[] _Adapters;

        //private static TsReader _TsRdr;

        //private static IntPtr _PacketBuffer;

        //private static List<int>[] _ActivePids;

        private static void Main(string[] args)
        {
            Globals.LoggedLine += (s) => { Console.Write(s); };

            Console.WriteLine("Zum Beenden, Eingabetaste drücken");

            try
            {
                InitDvbapi();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler bei Initialisierung DVBAPI-Client:");
                Console.WriteLine(ex.ToString());
            }

            Console.ReadLine();
            Console.WriteLine("Entlade DVBAPIClient...");

            _ApiClient.Dispose();

            Console.WriteLine("Beendet.");
        }

        private static DvbApiClient _ApiClient;

        private static void InitDvbapi()
        {
            string cConfigSection = "dvbapi";

            string srv = Globals.Config.GetValue(cConfigSection, "server");
            int? port = Globals.Config.GetInt32Value(cConfigSection, "port");
            bool? old = Globals.Config.GetBoolValue(cConfigSection, "oldproto");

            int? adapterOffset = Globals.Config.GetInt32Value(cConfigSection, "offset");

            if (string.IsNullOrWhiteSpace(srv)) // fallback default:
                srv = Globals.Defaults.GetValue(cConfigSection, "server");

            if (port == null)
                port = Globals.Defaults.GetInt32Value(cConfigSection, "port");

            if (adapterOffset == null)
                adapterOffset = Globals.Defaults.GetInt32Value(cConfigSection, "offset");

            if (old == null)
                old = Globals.Defaults.GetBoolValue(cConfigSection, "oldproto");

            if (adapterOffset < 0 || adapterOffset > 222)
            {
                Console.WriteLine($"Ungültiger adapter-offset: {adapterOffset}");
                adapterOffset = Globals.Defaults.GetInt32Value(cConfigSection, "offset");
            }

            if (port <= 1 || port >= 65536)
            {
                Console.WriteLine($"Ungültiger port: {port}");
                port = Globals.Defaults.GetInt32Value(cConfigSection, "port");
            }

            _ApiClient = new DvbApiClient(srv, port.Value, Globals.PipeName, Globals.Info, old.Value, adapterOffset.Value);
            _ApiClient.Start();
        }
    }
}
