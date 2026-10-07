using System;

namespace JungleBooze.Editor.Setup
{
    /// <summary>
    /// Plain C# rules used by <see cref="ProjectBootstrap"/> to decide whether a setting still has an
    /// "untouched new project" value that the bootstrap may replace. Kept free of UnityEditor so it can be
    /// unit-tested in EditMode.
    /// </summary>
    public static class ProjectSetupRules
    {
        /// <summary>Placeholder bundle identifier until the owner picks the final one. [ASSUMED]</summary>
        public const string PlaceholderBundleId = "com.pistaduko.junglerunner";

        /// <summary>Placeholder app name shown under the icon. Must never contain the word "booze". [ASSUMED]</summary>
        public const string PlaceholderProductName = "Jungle Runner";

        /// <summary>Lowest iOS version we build for (see docs/adr/0003-first-playable-bootstrap.md).</summary>
        public const string MinimumIosVersion = "15.0";

        /// <summary>Word that must never appear in user-visible strings (App Store guideline 1.4.3 risk).</summary>
        public const string ForbiddenVisibleWord = "booze";

        /// <summary>
        /// True when the product name is empty, still Unity's default (the project folder name), or contains the
        /// forbidden word. Anything else is treated as a deliberate choice and left alone.
        /// </summary>
        public static bool ShouldReplaceProductName(string current, string projectFolderName)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return true;
            }

            if (ContainsForbiddenWord(current))
            {
                return true;
            }

            return !string.IsNullOrEmpty(projectFolderName) &&
                   string.Equals(current.Trim(), projectFolderName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when the bundle identifier is empty, one of Unity's generated defaults
        /// (com.DefaultCompany.*, com.Company.*, com.unity.*), or contains the forbidden word.
        /// </summary>
        public static bool ShouldReplaceBundleId(string current)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return true;
            }

            string value = current.Trim();
            return value.StartsWith("com.DefaultCompany.", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("com.Company.", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase) ||
                   ContainsForbiddenWord(value);
        }

        /// <summary>Case-insensitive check for the forbidden word.</summary>
        public static bool ContainsForbiddenWord(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(ForbiddenVisibleWord, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// True when <paramref name="current"/> is missing, unparsable, or lower than
        /// <paramref name="minimum"/>. Versions are dotted numbers such as "13.0" or "15".
        /// </summary>
        public static bool IsBelowMinimumVersion(string current, string minimum)
        {
            if (!TryParseVersion(minimum, out Version minimumVersion))
            {
                throw new ArgumentException("Minimum version '" + minimum + "' is not a dotted number.",
                    nameof(minimum));
            }

            if (!TryParseVersion(current, out Version currentVersion))
            {
                return true;
            }

            return currentVersion < minimumVersion;
        }

        /// <summary>Reads the setup version from the marker file text ("setupVersion=N"); 0 when absent.</summary>
        public static int ParseMarkerVersion(string markerText)
        {
            if (string.IsNullOrEmpty(markerText))
            {
                return 0;
            }

            string[] lines = markerText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                const string key = "setupVersion=";
                if (line.StartsWith(key, StringComparison.Ordinal) &&
                    int.TryParse(line.Substring(key.Length), out int version))
                {
                    return version;
                }
            }

            return 0;
        }

        private static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string value = text.Trim();
            if (value.IndexOf('.') < 0)
            {
                value += ".0";
            }

            return Version.TryParse(value, out version);
        }
    }
}
