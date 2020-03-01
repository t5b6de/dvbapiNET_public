using dvbapiNet.Log.Locale;
using System;
using System.Text;

namespace dvbapiNet.Log
{
    internal class LogEntry
    {
        /// <summary>
        /// Die zu schreibende Nachricht
        /// </summary>
        public Message Message { get; set; }

        /// <summary>
        /// Debuglevel der Nachricht
        /// </summary>
        public DebugLevel DLevel { get; set; }

        /// <summary>
        /// Werte die in die Nachricht geschrieben werden.
        /// </summary>
        public object[] Values { get; set; }

        /// <summary>
        /// Section / Herkunft der Nachricht
        /// </summary>
        public string Section { get; set; }

        /// <summary>
        /// Dient für externes Logging über Event, ob diese Nachricht bereits darüber ausgegeben wurde, sollte die nachträgliche Verarbeitung
        /// fehlschlagen, wird dieses nicht nochmal ausgegeben.
        /// True zu setzen, wenn Ausgabe bereits erfolgt ist.
        /// </summary>
        public bool EventFired { get; set; }

        /// <summary>
        /// Nach Aufruf von Prepare beinhaltet diese Eigenschaft die Zeichenfolge die in die
        /// </summary>
        public string Log { get; private set; }

        /// <summary>
        /// Log-Eintrag als Byte-Array zum Schreiben in eine Datei.
        /// </summary>
        public byte[] LogData { get; private set; }

        /// <summary>
        /// Erstellt aus den vorliegenden Daten eine Lognachricht in Textform für die Logdatei
        /// </summary>
        /// <param name="instance"></param>
        public virtual void Prepare(int instance)
        {
            if (LogData != null)
                return;

            // Falls teile davon Messages sind, ersetzen:
            if (Values != null && Values.Length > 0)
            {
                for (int i = 0; i < Values.Length; i++)
                {
                    if (Values[i] != null && Values[i].GetType() == typeof(Message))
                    {
                        Values[i] = MessageProvider.FormatMessage((Message)Values[i], null);
                    }
                }
            }

            string logMessage = MessageProvider.FormatMessage(Message, Values);
            string[] lines = logMessage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            StringBuilder sb = new StringBuilder();

            foreach (string line in lines)
            {
                sb.Append("[");
                sb.Append(DateTime.Now.ToString("s"));

                if (instance < 0)
                {
                    sb.Append(" PL --");
                }
                else
                {
                    sb.Append(" PL ");
                    sb.Append(instance.ToString("00"));
                }

                sb.Append("] ");

                if (Section == null)
                    Section = "-";

                sb.Append(("(" + Section + ") ").PadLeft(17));

                sb.AppendLine(line);
            }

            Log = sb.ToString();
            LogData = Encoding.UTF8.GetBytes(Log);
        }
    }
}
