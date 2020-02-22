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
    /// Stellt alle f+r d
    /// </summary>
    internal static class LogProvider
    {
        private const string cConfigSection = "log";

        private static DebugLevel _Debug;
        private static int _InstanceNumber;
        private static bool _IsRunning;
        private static object _LockHelper;
        private static Queue<LogEntry> _LogQueue;
        private static Thread _LogThrd;
        private static ManualResetEvent _LogWait;

        public delegate void Line(string line);

        public static event Line LineWritten;

        static LogProvider()
        {
            _InstanceNumber = -1;
            _LockHelper = new object(); // kleine Hilfsinstanz für lock(){}
            _LogWait = new ManualResetEvent(false);
            _LogQueue = new Queue<LogEntry>();
            _LogThrd = new Thread(Writer);
            _IsRunning = true;

            int? tmp = Globals.Config.GetInt32Value(cConfigSection, "debug");

            if (tmp == null)
                tmp = Globals.Defaults.GetInt32Value(cConfigSection, "debug");

            _Debug = (DebugLevel)tmp;

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

        public static void Dispose()
        {
            //TODO Disposal
            _IsRunning = false;
            _LogWait.Set();
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

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < len; i++)
            {
                if (i > 0 && i % 16 == 0)
                {
                    sb.AppendLine();
                }
                else if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(data[i + offset].ToString("x2"));
            }

            if (str == Message.Empty)
            {
                Add(level, section, Message.SingleParam, sb.ToString());
            }
            else
            {
                Add(level, section, Message.HexDump, str, Environment.NewLine, sb.ToString());
            }

            sb.Clear();
        }

        public static void Add(DebugLevel level, string section, Message m, params object[] values)
        {
            if (!_Debug.HasFlag(level))
                return;

            lock (_LogQueue)
            {
                _LogQueue.Enqueue(new LogEntry() { DLevel = level, Message = m, Values = values, Section = section, EventFired = false });
            }

            _LogWait.Set();
        }

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

        public static void Add(DebugLevel level, string section, EcmInfo info)
        {
            Add(level, section, Message.SingleParam, info);
        }

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
                    return;

                bool result;

                while (_LogQueue.Count > 0 && _IsRunning)
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
        }
    }
}
