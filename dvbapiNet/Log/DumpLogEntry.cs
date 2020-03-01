using dvbapiNet.Log.Locale;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dvbapiNet.Log
{
    /// <summary>
    /// Logentry speziell für Hex-Dumps
    /// </summary>
    internal class DumpLogEntry : LogEntry
    {
        private byte[] _Data;
        private Message _Msg;
        private bool _Pretty;

        /// <summary>
        /// Erstellt eine neue Instanz vom DumpLogEntry und kopiert dabei das Quell-Array direkt in ein internes Array damit Daten auch bei späterer Verwendung konsistent sind.
        /// </summary>
        /// <param name="section">Section für das Logging, aus welchem Bereich kommt dieser Eintrag</param>
        /// <param name="str">Nachricht die dem Dump vorangestellt wid</param>
        /// <param name="data">Daten für das Array</param>
        /// <param name="offset">Offset für den Dump im Quellarray</param>
        /// <param name="len">Länge der auszugebenden Daten</param>
        /// <param name="prettyPrint">true wenn die Ausgabe formatiert erfolgen soll, anderenfalls false.</param>
        public DumpLogEntry(string section, Message str, byte[] data, int offset, int len, bool prettyPrint)
        {
            _Data = new byte[len];
            Array.Copy(data, offset, _Data, 0, len);
            Section = section;
            _Msg = str;
            _Pretty = prettyPrint;
        }

        /// <summary>
        /// Gibt einen ausführlichen Hex-Dump mit Ascii-Text zurück
        /// </summary>
        /// <returns></returns>
        private string PrettyPrint()
        {
            char[] hex = "0123456789abcdef".ToArray();
            char[] line;
            StringBuilder sb = new StringBuilder();

            line = "offset   | 00 01 02 03 04 05 06 07 08 09 0a 0b 0c 0d 0e 0f | text".ToArray();
            sb.AppendLine(new string(line));
            line = "---------+-------------------------------------------------+-----------------".ToArray();
            sb.AppendLine(new string(line));

            for (int ptr = 0; ptr < _Data.Length;)
            {
                line = "00000000 | ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? | ????????????????".ToArray();

                for (int k = 0; k < 8; k++)
                {
                    line[7 - k] = hex[(ptr >> (k * 4)) & 0xf];
                }

                for (int j = 0; j < 16; j++, ptr++)
                {
                    int hexIdx = 11 + (3 * j);
                    int asciiIdx = 61 + j;

                    if (ptr < _Data.Length)
                    {
                        int val = _Data[ptr];

                        line[hexIdx] = hex[val >> 4];
                        line[hexIdx + 1] = hex[val & 0xf];

                        line[asciiIdx] = char.IsControl((char)val) ? '.' : (char)val;
                    }
                    else
                    {
                        line[hexIdx] = ' ';
                        line[hexIdx + 1] = ' ';
                        line[asciiIdx] = ' ';
                    }
                }

                sb.AppendLine(new string(line));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gibt einen einfachen Hexdump zurück
        /// </summary>
        /// <returns></returns>
        private string SimplePrint()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < _Data.Length; i++)
            {
                sb.Append(_Data[i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generiert aus den vorliegenden Daten eine Lognachricht in Textform für die Logdatei.
        /// </summary>
        /// <param name="instance">Instanznummer für den Fall, dass mehrere Plugins gleichzeitig laufen.</param>
        public override void Prepare(int instance)
        {
            if (LogData != null)
                return;

            string res;

            if (_Pretty)
            {
                res = PrettyPrint();
            }
            else
            {
                res = SimplePrint();
            }

            if (_Msg == Message.Empty)
            {
                Message = Message.SingleParam;
                Values = new object[] { res };
            }
            else
            {
                Message = Message.HexDump;
                Values = new object[] { _Msg, Environment.NewLine, res };
            }

            base.Prepare(instance);
        }
    }
}
