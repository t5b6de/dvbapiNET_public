using dvbapiNet.Dvb.Crypto;
using dvbapiNet.Oscam.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using dvbapiNet.Log.Locale;

#if DEBUG

using System.Diagnostics;

#endif

namespace dvbapiNet.Log
{
    /// <summary>
    /// Stellt diverse Funktionen für die Erstellung von Log-Nachrichte
    /// </summary>
    internal static class LogProvider
    {
        private const string cConfigSection = "log";

        private static DebugLevel _Debug;
        private static int _InstanceNumber;
        private static bool _IsRunning;
        private static Queue<LogEntry> _LogQueue;
        private static Thread _LogThrd;
        private static ManualResetEvent _LogWait;
        private static ManualResetEvent _EndWait;

        private static bool _PrettyHex;

        public delegate void Line(string line);

        public static event Line LineWritten;

        /// <summary>
        /// Initialisiert das Log-System startet Writer-Prozess
        /// </summary>
        static LogProvider()
        {
            _InstanceNumber = -1;
            _LogWait = new ManualResetEvent(false);
            _EndWait = new ManualResetEvent(false);
            _LogQueue = new Queue<LogEntry>();
            _LogThrd = new Thread(Writer);
            _IsRunning = true;
            _PrettyHex = false;

            int tmp = 0;

            Globals.Config.Get(cConfigSection, "pretty", ref _PrettyHex);
            Globals.Config.Get(cConfigSection, "debug", 0, int.MaxValue, ref tmp);

            _Debug = (DebugLevel)tmp;
            _LogThrd.IsBackground = true;
            _LogThrd.Start();

            LineWritten += Globals.ExternalLogHandler;

#if DEBUG
            LineWritten += (s) =>
            {
                Debug.Write(s);
            };
#endif

            if (tmp > 0)
            {
                Add(_Debug, "main", Message.DvbapiNETVersion, Globals.Info);
                Add(_Debug, "main", Message.DvbapiNETLogstart, _Debug);
            }
        }

        /// <summary>
        /// Stoppt den Writer-Thread
        /// </summary>
        public static void Dispose()
        {
            _IsRunning = false;
            _LogWait.Set();
            _EndWait.WaitOne(5000); // Timeout falls etwas hängt.
        }

        /// <summary>
        /// Setzt Instanzen-Nummer / Adapternummer für die Zuordnung der Plugins innerhalb der Logfiles.
        /// </summary>
        /// <param name="num"></param>
        public static void SetInstanceNumber(int num)
        {
            _InstanceNumber = num;
        }

        /// <summary>
        /// Schreibt einen Hex-Dump in die Logdatei.
        /// TODO: muss umgebaut werden, im Add nur noch Array kopieren.
        /// Ableitung von Logentry erstellen
        /// </summary>
        /// <param name="level"></param>
        /// <param name="str"></param>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="len"></param>
        public static void Add(DebugLevel level, string section, Message str, byte[] data, int offset, int len)
        {
            if (!_Debug.HasFlag(level))
                return;

            DumpLogEntry dle = new DumpLogEntry(section, str, data, offset, len, _PrettyHex);

            lock (_LogQueue)
                _LogQueue.Enqueue(dle);
        }

        /// <summary>
        /// Loggt einfache Nachricht und fügt parameter in Message ein.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="section"></param>
        /// <param name="m"></param>
        /// <param name="values"></param>
        public static void Add(DebugLevel level, string section, Message m, params object[] values)
        {
            if (!_Debug.HasFlag(level))
                return;

            lock (_LogQueue)

                _LogQueue.Enqueue(new LogEntry() { DLevel = level, Message = m, Values = values, Section = section, EventFired = false });

            _LogWait.Set();
        }

        /// <summary>
        /// Loggt Ausnahme
        /// </summary>
        /// <param name="section"></param>
        /// <param name="msg"></param>
        /// <param name="ex"></param>
        public static void Exception(string section, Message msg, Exception ex)
        {
            if (msg == Message.Empty)
            {
                Add(DebugLevel.Error, section, Message.SingleParam, ex.ToString());
            }
            else
            {
                Add(DebugLevel.Error, section, Message.ExceptionFormat, msg, Environment.NewLine, ex);
            }
        }

        /// <summary>
        /// Loggt ECM-Info
        /// </summary>
        /// <param name="level"></param>
        /// <param name="section"></param>
        /// <param name="info"></param>
        public static void Add(DebugLevel level, string section, EcmInfo info)
        {
            Add(level, section, Message.SingleParam, info);
        }

        /// <summary>
        /// Loggt ECM
        /// </summary>
        /// <param name="level"></param>
        /// <param name="section"></param>
        /// <param name="parity"></param>
        /// <param name="cw"></param>
        public static void WriteCWLog(DebugLevel level, string section, DescramblerParity parity, byte[] cw)
        {
            if (!_Debug.HasFlag(level))
                return;

            StringBuilder sb = new StringBuilder();

            byte[] fullcw = new byte[cw.Length << 1];

            switch (parity)
            {
                case DescramblerParity.Even:
                    Array.Copy(cw, 0, fullcw, 0, cw.Length);
                    break;

                case DescramblerParity.Odd:
                    Array.Copy(cw, 0, fullcw, cw.Length, cw.Length);
                    break;

                default:
                    sb.AppendLine(MessageProvider.FormatMessage(Message.CwLogParityError, parity));
                    fullcw = cw;
                    break;
            }
            if (cw.Length > 8)
            {
                sb.Append(MessageProvider.FormatMessage(Message.CwLogExt));
            }
            else
            {
                sb.Append(MessageProvider.FormatMessage(Message.CwLog));
            }

            for (int i = 0; i < fullcw.Length; i++)
            {
                sb.Append(fullcw[i].ToString("x2"));
            }

            Add(level, section, Message.SingleParam, sb.ToString());
        }

        /// <summary>
        /// Schreibt angegebene LogEntry in die Logdatei.
        /// </summary>
        /// <param name="entry">Enthält alle Infos </param>
        /// <param name="instance"></param>
        /// <returns></returns>
        private static bool WriteLog(LogEntry entry, int instance)
        {
            entry.Prepare(instance);

            if (!entry.EventFired)
            {
                try
                {
                    LineWritten?.Invoke(entry.Log);
                }
                catch { }
                entry.EventFired = true;
            }

            FileInfo fi = Globals.GetLogfile();
            if (fi == null)
                return false;

            using (FileStream fs = new FileStream(fi.FullName, FileMode.OpenOrCreate))
            {
                fs.Seek(0, SeekOrigin.End);
                fs.Write(entry.LogData, 0, entry.LogData.Length);
            }

            return true;
        }

        /// <summary>
        /// Methode für den Thread der die Logdaten schreibt.
        /// </summary>
        /// <param name="state">Immer null, wird nicht benötigt</param>
        private static void Writer(object state)
        {
            while (true)
            {
                _LogWait.WaitOne();
                _LogWait.Reset();

                if (!_IsRunning)
                    break;

                bool result;

                while (_LogQueue.Count > 0)
                {
                    result = false;
                    LogEntry le;

                    lock (_LogQueue)
                        le = _LogQueue.Peek();

                    // Muss mit try-catch laufen, da mehrere instanzen schreiben können.
                    try
                    {
                        result = WriteLog(_LogQueue.Peek(), _InstanceNumber);
                    }
                    catch { }

                    if (result)
                    {
                        lock (_LogQueue)
                            _LogQueue.Dequeue();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            _EndWait.Set();
        }
    }
}
