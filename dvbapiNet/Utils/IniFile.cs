using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using System;
using System.Collections.Generic;
using System.IO;

namespace dvbapiNet.Utils
{
    /// <summary>
    /// Unvollständige Ini-Implementierung, Save-Funktion fehlt.
    /// </summary>
    public class IniFile
    {
        private const string cLogSection = "inifile";
        private readonly FileInfo _Fi;

        private Dictionary<string, Dictionary<string, string>> _IniContent;

        public Dictionary<string, string> this[string section]
        {
            get
            {
                if (_IniContent.ContainsKey(section))
                    return _IniContent[section];

                return null;
            }
        }

        public IniFile(FileInfo file)
        {
            _IniContent = new Dictionary<string, Dictionary<string, string>>();

            if (file != null) // dann leere Datei, lediglich als "dictionary"
            {
                try
                {
                    _Fi = file;
                    if (_Fi.Exists)
                    {
                        Load();
                    }
                }
                catch (Exception ex)
                {
                    LogProvider.Exception(cLogSection, Message.IniFileError, ex);
                }
            }
        }

        public int? GetInt32Value(string section, string key)
        {
            string val = GetValue(section, key);

            int tmp = 0;

            if (val != null && int.TryParse(val, out tmp))
                return tmp;

            return null;
        }

        public bool? GetBoolValue(string section, string key)
        {
            string val = GetValue(section, key);

            int tmp = 0;

            if (val != null && int.TryParse(val, out tmp))
                return tmp != 0;

            return null;
        }

        public string GetValue(string section, string key)
        {
            section = section.ToLowerInvariant().Trim();
            key = key.ToLowerInvariant().Trim();

            if (!_IniContent.ContainsKey(section))
                return null;

            Dictionary<string, string> dict = _IniContent[section];

            if (!dict.ContainsKey(key))
                return null;

            return dict[key];
        }

        public void SetValue(string section, string key, string value)
        {
            section = section.ToLowerInvariant().Trim();
            key = key.ToLowerInvariant().Trim();

            if (!_IniContent.ContainsKey(section))
            {
                _IniContent.Add(section, new Dictionary<string, string>());
            }

            Dictionary<string, string> dict = _IniContent[section];

            if (!dict.ContainsKey(key))
            {
                dict.Add(key, value);
            }
            else
            {
                dict[key] = value;
            }
        }

        private void Load()
        {
            string[] lines = File.ReadAllLines(_Fi.FullName);

            int i = 0;

            string curSection = "";

            foreach (string l in lines)
            {
                if (string.IsNullOrWhiteSpace(l) || l[0] == '#' || l[0] == ';') // Kommentar oder Leerzeile.
                    continue;

                if (l.Length < 3)
                    throw new Exception(MessageProvider.FormatMessage(Message.IniFileParseEx, i + 1));

                if (l[0] == '[' && l[l.Length - 1] == ']' && l.Length > 2) // Section Zeile
                {
                    curSection = l.Substring(1, l.Length - 2);
                }
                else
                {
                    string[] parts = l.Split("=".ToCharArray(), 2);

                    if (parts.Length < 2)
                        throw new Exception(MessageProvider.FormatMessage(Message.IniFileParseEx, i + 1));

                    SetValue(curSection, parts[0], parts[1]);
                }

                i++;
            }
        }
    }
}
