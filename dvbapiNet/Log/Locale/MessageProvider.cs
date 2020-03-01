using dvbapiNet.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace dvbapiNet.Log.Locale
{
    /// <summary>
    /// KLasse für die Bereitstellung und Übersetzung der Lognachrichten in lokalisierte Zeichenfolgen.
    /// </summary>
    internal static class MessageProvider
    {
        private static Dictionary<Message, string> _Messages;
        private static string _LocaleCode;

        static MessageProvider()
        {
            CultureInfo ci = CultureInfo.InstalledUICulture;

            if (ci != null)
            {
                _Messages = Locales.GetDefault(ci.TwoLetterISOLanguageName);
                _LocaleCode = ci.Name;
            }
            else
            {
                _Messages = Locales.GetDefault("");
                _LocaleCode = "";
            }
        }

        public static string FormatMessage(Message m, params object[] values)
        {
            try
            {
                string fmt = _Messages[m];

                if (values == null)
                    return fmt;

                return string.Format(fmt, values);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append($"Error while formatting string: \"{m}\" in locale \"{_LocaleCode}\"");
                sb.AppendLine(m.ToString());
                sb.AppendLine(ex.ToString());

                if (values != null && values.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Following should be logged:");

                    for (int i = 0; i < values.Length; i++)
                    {
                        sb.Append($"Param: {i}: ");
                        if (values[i] != null)
                        {
                            try
                            {
                                sb.AppendLine(values[i].ToString());
                            }
                            catch
                            {
                                sb.AppendLine("(value ToString() failed)");
                            }
                        }
                        else
                        {
                            sb.AppendLine("null");
                        }
                    }
                }

                return sb.ToString();
            }
        }
    }
}
