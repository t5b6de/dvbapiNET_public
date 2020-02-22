using dvbapiNet.Log.Locale;
using System;
using System.Text;

namespace dvbapiNet.Log
{
    internal class LogEntry
    {
        public Message Message { get; set; }
        public DebugLevel DLevel { get; set; }
        public object[] Values { get; set; }
        public string Section { get; set; }
        public bool EventFired { get; set; }

        public string Log { get; private set; }
        public byte[] LogData { get; private set; }

        public void Prepare(int instance)
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
