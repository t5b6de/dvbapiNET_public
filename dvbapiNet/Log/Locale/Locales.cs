using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dvbapiNet.Log.Locale
{
    /// <summary>
    /// Basisklasse für die Lokalisierung
    /// </summary>
    internal static partial class Locales
    {
        /// <summary>
        /// Lädt die Standardtexte anhand der gegebenen Sprache.
        /// </summary>
        /// <param name="lang">2 Zeichen Sprachcode</param>
        /// <returns>Dictionary mit Sprach</returns>
        public static Dictionary<Message, string> GetDefault(string lang)
        {
            Dictionary<Message, string> result;

            switch (lang)
            {
                case "de":
                    result = GetLocales_DE();
                    break;

                case "en":
                    result = GetLocales_EN();
                    break;

                // falls weitere Sprachen eingefügt werden, hier die Defaults einbauen.

                default:
                    result = GetLocales_EN();
                    break;
            }

            // Sonderfälle:
            if (result.ContainsKey(Message.SingleParam))
                result.Remove(Message.SingleParam);

            result.Add(Message.SingleParam, "{0}");

            if (result.ContainsKey(Message.ExceptionFormat))
                result.Remove(Message.ExceptionFormat);

            result.Add(Message.ExceptionFormat, "{0}:{1}{2}");

            if (result.ContainsKey(Message.HexDump))
                result.Remove(Message.HexDump);

            result.Add(Message.HexDump, "{0}:{1}{2}");

            return result;
        }
    }
}
