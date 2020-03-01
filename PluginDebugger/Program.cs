using dvbapiNet;
using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam;
using dvbapiNet.Utils;
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

            Globals.Dispose();
            Console.WriteLine("Beendet.");
            Thread.Sleep(1500);
        }

        private static DvbApiClient _ApiClient;

        private static void InitDvbapi()
        {
            string cConfigSection = "dvbapi";

            string srv = "";
            int port = 0;
            bool old = false;

            int adapterOffset = 0;

            Globals.Config.Get(cConfigSection, "server", ref srv);
            Globals.Config.Get(cConfigSection, "oldproto", ref old);

            if (Globals.Config.Get(cConfigSection, "offset", 0, 128, ref adapterOffset) != Configuration.ConfigRes.Ok)
                Console.WriteLine("Adaper Offset ungültig oder fehlt, nutze standard.");

            if (Globals.Config.Get(cConfigSection, "port", 1, 65535, ref port) != Configuration.ConfigRes.Ok)
                Console.WriteLine("port ungültig oder fehlt, nutze standard");

            _ApiClient = new DvbApiClient(srv, port, Globals.PipeName, Globals.Info, old, adapterOffset);
            _ApiClient.Start();
        }
    }
}
