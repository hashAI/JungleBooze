using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JungleBooze.Editor.Build
{
    /// <summary>
    /// Settings for one iOS build, read from command-line arguments with environment-variable fallbacks.
    /// Plain C# with no UnityEditor dependency so it can be unit-tested in EditMode.
    ///
    /// Command line (wins)        Environment variable    Required  Example
    /// -jbBundleId &lt;id&gt;           JB_BUNDLE_ID            yes       com.example.game
    /// -jbVersion &lt;x.y.z&gt;         JB_VERSION              yes       0.1.0
    /// -jbBuildNumber &lt;n&gt;         JB_BUILD_NUMBER         yes       12
    /// -jbBuildType &lt;type&gt;        JB_BUILD_TYPE           no        release (default) or development
    /// -jbOutput &lt;dir&gt;            JB_XCODE_OUTPUT         no        Builds/iOS (default, relative to UnityProject/)
    /// -jbTeamId &lt;id&gt;             APPLE_TEAM_ID           no        ABCDE12345 (10 characters)
    /// </summary>
    public sealed class IosBuildConfig
    {
        public const string DefaultOutputPath = "Builds/iOS";

        private static readonly Regex BundleIdPattern =
            new Regex(@"^[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)+$", RegexOptions.CultureInvariant);

        // CFBundleShortVersionString: three period-separated non-negative integers.
        private static readonly Regex VersionPattern =
            new Regex(@"^(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})$", RegexOptions.CultureInvariant);

        // CFBundleVersion: we use a single positive integer that only ever goes up.
        private static readonly Regex BuildNumberPattern =
            new Regex(@"^[1-9][0-9]{0,8}$", RegexOptions.CultureInvariant);

        private static readonly Regex TeamIdPattern =
            new Regex(@"^[A-Z0-9]{10}$", RegexOptions.CultureInvariant);

        private IosBuildConfig(
            string bundleId,
            string version,
            string buildNumber,
            IosBuildType buildType,
            string outputPath,
            string teamId)
        {
            BundleId = bundleId;
            Version = version;
            BuildNumber = buildNumber;
            BuildType = buildType;
            OutputPath = outputPath;
            TeamId = teamId;
        }

        public string BundleId { get; }

        public string Version { get; }

        public string BuildNumber { get; }

        public IosBuildType BuildType { get; }

        /// <summary>Xcode project output folder; relative paths are relative to the Unity project folder.</summary>
        public string OutputPath { get; }

        /// <summary>Apple Developer Team ID, or an empty string when not provided.</summary>
        public string TeamId { get; }

        /// <summary>
        /// Reads and validates the settings. Returns false and a human-readable message listing every problem
        /// when anything is missing or malformed.
        /// </summary>
        /// <param name="args">Command-line arguments (for example Environment.GetCommandLineArgs()).</param>
        /// <param name="getEnvironmentVariable">Environment lookup; returns null when a variable is not set.</param>
        public static bool TryParse(
            IReadOnlyList<string> args,
            Func<string, string> getEnvironmentVariable,
            out IosBuildConfig config,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (getEnvironmentVariable == null)
            {
                throw new ArgumentNullException(nameof(getEnvironmentVariable));
            }

            var problems = new List<string>();

            string bundleId = Read(args, getEnvironmentVariable, "-jbBundleId", "JB_BUNDLE_ID", problems);
            string version = Read(args, getEnvironmentVariable, "-jbVersion", "JB_VERSION", problems);
            string buildNumber = Read(args, getEnvironmentVariable, "-jbBuildNumber", "JB_BUILD_NUMBER", problems);
            string buildTypeText = Read(args, getEnvironmentVariable, "-jbBuildType", "JB_BUILD_TYPE", problems);
            string outputPath = Read(args, getEnvironmentVariable, "-jbOutput", "JB_XCODE_OUTPUT", problems);
            string teamId = Read(args, getEnvironmentVariable, "-jbTeamId", "APPLE_TEAM_ID", problems);

            if (string.IsNullOrEmpty(bundleId))
            {
                problems.Add("Bundle identifier is missing (-jbBundleId or JB_BUNDLE_ID).");
            }
            else if (!BundleIdPattern.IsMatch(bundleId))
            {
                problems.Add(
                    "Bundle identifier '" + bundleId + "' is not valid. Use reverse-domain form with letters, " +
                    "digits, hyphens and dots, for example com.example.game.");
            }

            if (string.IsNullOrEmpty(version))
            {
                problems.Add("Version is missing (-jbVersion or JB_VERSION), for example 0.1.0.");
            }
            else if (!VersionPattern.IsMatch(version))
            {
                problems.Add("Version '" + version + "' must be three numbers separated by dots, for example 0.1.0.");
            }

            if (string.IsNullOrEmpty(buildNumber))
            {
                problems.Add("Build number is missing (-jbBuildNumber or JB_BUILD_NUMBER).");
            }
            else if (!BuildNumberPattern.IsMatch(buildNumber))
            {
                problems.Add("Build number '" + buildNumber + "' must be a positive whole number, for example 12.");
            }

            IosBuildType buildType = IosBuildType.Release;
            if (!string.IsNullOrEmpty(buildTypeText))
            {
                if (string.Equals(buildTypeText, "release", StringComparison.OrdinalIgnoreCase))
                {
                    buildType = IosBuildType.Release;
                }
                else if (string.Equals(buildTypeText, "development", StringComparison.OrdinalIgnoreCase))
                {
                    buildType = IosBuildType.Development;
                }
                else
                {
                    problems.Add("Build type '" + buildTypeText + "' must be 'release' or 'development'.");
                }
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = DefaultOutputPath;
            }

            if (teamId == null)
            {
                teamId = string.Empty;
            }
            else if (teamId.Length > 0 && !TeamIdPattern.IsMatch(teamId))
            {
                problems.Add("Apple Team ID '" + teamId + "' must be 10 capital letters or digits.");
            }

            if (problems.Count > 0)
            {
                config = null;
                error = string.Join(Environment.NewLine, problems);
                return false;
            }

            config = new IosBuildConfig(bundleId, version, buildNumber, buildType, outputPath, teamId);
            error = string.Empty;
            return true;
        }

        private static string Read(
            IReadOnlyList<string> args,
            Func<string, string> getEnvironmentVariable,
            string argumentName,
            string environmentName,
            List<string> problems)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], argumentName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    problems.Add("Argument " + argumentName + " needs a value after it.");
                    return null;
                }

                return args[i + 1].Trim();
            }

            string fromEnvironment = getEnvironmentVariable(environmentName);
            return fromEnvironment == null ? null : fromEnvironment.Trim();
        }
    }
}
