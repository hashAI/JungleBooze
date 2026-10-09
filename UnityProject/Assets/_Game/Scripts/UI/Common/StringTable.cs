using System;
using System.Collections.Generic;
using System.Globalization;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// A localization table ("key = value" lines, '#' comments, "\n" escapes). Lookups happen when screens are built
    /// or refreshed, never per frame. A missing key returns "#key" so it shows up in screenshots and tests.
    /// </summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        public StringTable(string locale)
        {
            Locale = locale ?? "en";
        }

        public string Locale { get; }

        public int Count => _values.Count;

        public static StringTable Parse(string locale, string text)
        {
            var table = new StringTable(locale);
            if (string.IsNullOrEmpty(text))
            {
                return table;
            }

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                table._values[key] = value;
            }

            return table;
        }

        public bool Has(string key)
        {
            return key != null && _values.ContainsKey(key);
        }

        public string Get(string key)
        {
            return key != null && _values.TryGetValue(key, out string value) ? value : "#" + key;
        }

        public string Format(string key, object a)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), a);
        }

        public string Format(string key, object a, object b)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), a, b);
        }

        public string Format(string key, object a, object b, object c)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), a, b, c);
        }

        /// <summary>Keys present in this table and missing in <paramref name="other"/> (translation checks).</summary>
        public List<string> MissingIn(StringTable other)
        {
            var missing = new List<string>();
            foreach (string key in _values.Keys)
            {
                if (!other.Has(key))
                {
                    missing.Add(key);
                }
            }

            return missing;
        }
    }
}
