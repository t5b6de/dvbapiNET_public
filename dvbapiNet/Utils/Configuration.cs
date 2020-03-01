using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dvbapiNet.Utils
{
    /// <summary>
    /// Fasst 2 IniFiles zusammen für Konfiguration und Standardkonfiguraton
    /// </summary>
    public class Configuration
    {
        private IniFile _Conf;
        private IniFile _Defs;

        public enum ConfigRes
        {
            Ok,
            Default,
            Error
        }

        public Configuration(IniFile conf, IniFile defaults)
        {
            _Conf = conf;
            _Defs = defaults;
        }

        /// <summary>
        /// Ruft einen Zahlenwert aus der Konfiguration ab und schreibt diesen in die angegebene Variable
        /// </summary>
        /// <param name="section">Sektion in der die Option zu finden ist</param>
        /// <param name="key">Name / Schlüssel der gewünschten Option</param>
        /// <param name="validFrom">Minimaler Wert der Gültigkeitsprüfung</param>
        /// <param name="validTo">Maximaler Wert der Gültigkeitsprüfung</param>
        /// <param name="value">Variable in der das Ergebnis geschrieben werden soll</param>
        /// <returns>Ergebnis, OK bei Rückgabe aus Konfiguration, Default bei Rückgabe aus Standardwert, Error bei Fehler oder nicht vorhandenem Wert</returns>
        public ConfigRes Get(string section, string key, int validFrom, int validTo, ref int value)
        {
            int? tmp = _Conf.GetInt32Value(section, key);

            if (tmp != null && tmp >= validFrom && tmp <= validTo)
            {
                value = tmp.Value;
                return ConfigRes.Ok;
            }

            tmp = _Defs.GetInt32Value(section, key);
            if (tmp != null)
            {
                value = tmp.Value;
                return ConfigRes.Default;
            }

            return ConfigRes.Error;
        }

        /// <summary>
        /// Ruft einen boolschen Wert aus der Konfiguration ab und schreibt diesen in die angegebene Variable
        /// </summary>
        /// <param name="section">Sektion in der die Option zu finden ist</param>
        /// <param name="key">Name / Schlüssel der gewünschten Option</param>
        /// <param name="value">Variable in der das Ergebnis geschrieben werden soll</param>
        /// <returns>Ergebnis, OK bei Rückgabe aus Konfiguration, Default bei Rückgabe aus Standardwert, Error bei Fehler oder nicht vorhandenem Wert</returns>
        public ConfigRes Get(string section, string key, ref bool value)
        {
            bool? tmp = _Conf.GetBoolValue(section, key);

            if (tmp != null)
            {
                value = tmp.Value;
                return ConfigRes.Ok;
            }

            tmp = _Defs.GetBoolValue(section, key);
            if (tmp != null)
            {
                value = tmp.Value;
                return ConfigRes.Default;
            }

            return ConfigRes.Error;
        }

        /// <summary>
        /// Ruft einen Zeichenfolge aus der Konfiguration ab und schreibt diesen in die angegebene Variable
        /// </summary>
        /// <param name="section">Sektion in der die Option zu finden ist</param>
        /// <param name="key">Name / Schlüssel der gewünschten Option</param>
        /// <param name="value">Variable in der das Ergebnis geschrieben werden soll</param>
        /// <returns>Ergebnis, OK bei Rückgabe aus Konfiguration, Default bei Rückgabe aus Standardwert, Error bei Fehler oder nicht vorhandenem Wert</returns>
        public ConfigRes Get(string section, string key, ref string value)
        {
            string tmp = _Conf.GetValue(section, key);

            if (!string.IsNullOrWhiteSpace(tmp))
            {
                value = tmp;
                return ConfigRes.Ok;
            }

            tmp = _Defs.GetValue(section, key);
            if (tmp != null)
            {
                value = tmp;
                return ConfigRes.Default;
            }

            return ConfigRes.Error;
        }
    }
}
