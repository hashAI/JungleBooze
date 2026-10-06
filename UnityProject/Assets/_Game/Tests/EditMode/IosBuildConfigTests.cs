using System.Collections.Generic;
using JungleBooze.Editor.Build;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    public sealed class IosBuildConfigTests
    {
        private static readonly string[] ValidArgs =
        {
            "/Applications/Unity/Unity.app/Contents/MacOS/Unity", "-batchmode",
            "-jbBundleId", "com.example.game",
            "-jbVersion", "0.1.0",
            "-jbBuildNumber", "12",
        };

        private static string NoEnvironment(string name)
        {
            return null;
        }

        [Test]
        public void TryParse_ValidArguments_UsesValuesAndDefaults()
        {
            bool ok = IosBuildConfig.TryParse(ValidArgs, NoEnvironment, out IosBuildConfig config, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual("com.example.game", config.BundleId);
            Assert.AreEqual("0.1.0", config.Version);
            Assert.AreEqual("12", config.BuildNumber);
            Assert.AreEqual(IosBuildType.Release, config.BuildType);
            Assert.AreEqual(IosBuildConfig.DefaultOutputPath, config.OutputPath);
            Assert.AreEqual(string.Empty, config.TeamId);
        }

        [Test]
        public void TryParse_EnvironmentFallback_IsUsedWhenArgumentMissing()
        {
            var environment = new Dictionary<string, string>
            {
                { "JB_BUNDLE_ID", "com.example.game" },
                { "JB_VERSION", "1.2.3" },
                { "JB_BUILD_NUMBER", "7" },
                { "JB_BUILD_TYPE", "Development" },
                { "APPLE_TEAM_ID", "ABCDE12345" },
            };

            bool ok = IosBuildConfig.TryParse(
                new[] { "Unity" },
                name => environment.TryGetValue(name, out string value) ? value : null,
                out IosBuildConfig config,
                out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual("1.2.3", config.Version);
            Assert.AreEqual("7", config.BuildNumber);
            Assert.AreEqual(IosBuildType.Development, config.BuildType);
            Assert.AreEqual("ABCDE12345", config.TeamId);
        }

        [Test]
        public void TryParse_ArgumentWinsOverEnvironment()
        {
            bool ok = IosBuildConfig.TryParse(
                ValidArgs,
                name => name == "JB_VERSION" ? "9.9.9" : null,
                out IosBuildConfig config,
                out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual("0.1.0", config.Version);
        }

        [Test]
        public void TryParse_NothingProvided_ReportsEveryMissingValue()
        {
            bool ok = IosBuildConfig.TryParse(new[] { "Unity" }, NoEnvironment, out IosBuildConfig config,
                out string error);

            Assert.IsFalse(ok);
            Assert.IsNull(config);
            StringAssert.Contains("JB_BUNDLE_ID", error);
            StringAssert.Contains("JB_VERSION", error);
            StringAssert.Contains("JB_BUILD_NUMBER", error);
        }

        [TestCase("com.example")]
        [TestCase("com.example.jungle-run")]
        [TestCase("io.Example.Game2")]
        public void TryParse_ValidBundleId_IsAccepted(string bundleId)
        {
            Assert.IsTrue(ParseWith("-jbBundleId", bundleId, out string error), error);
        }

        [TestCase("game")]
        [TestCase("com.example.")]
        [TestCase("com.example game")]
        [TestCase("com.example_game")]
        public void TryParse_InvalidBundleId_IsRejected(string bundleId)
        {
            Assert.IsFalse(ParseWith("-jbBundleId", bundleId, out _));
        }

        [TestCase("0.1")]
        [TestCase("1.2.3.4")]
        [TestCase("01.2.3")]
        [TestCase("1.2.x")]
        public void TryParse_InvalidVersion_IsRejected(string version)
        {
            Assert.IsFalse(ParseWith("-jbVersion", version, out _));
        }

        [TestCase("0")]
        [TestCase("1.0")]
        [TestCase("abc")]
        public void TryParse_InvalidBuildNumber_IsRejected(string buildNumber)
        {
            Assert.IsFalse(ParseWith("-jbBuildNumber", buildNumber, out _));
        }

        [Test]
        public void TryParse_UnknownBuildType_IsRejected()
        {
            Assert.IsFalse(ParseWith("-jbBuildType", "debug", out string error));
            StringAssert.Contains("release", error);
        }

        [Test]
        public void TryParse_InvalidTeamId_IsRejected()
        {
            Assert.IsFalse(ParseWith("-jbTeamId", "abc", out _));
        }

        [Test]
        public void TryParse_ArgumentWithoutValue_IsRejected()
        {
            var args = new List<string>(ValidArgs) { "-jbBuildType" };

            bool ok = IosBuildConfig.TryParse(args, NoEnvironment, out _, out string error);

            Assert.IsFalse(ok);
            StringAssert.Contains("-jbBuildType", error);
        }

        /// <summary>Parses the valid arguments with one argument replaced or added.</summary>
        private static bool ParseWith(string argumentName, string value, out string error)
        {
            var args = new List<string>(ValidArgs);
            int index = args.IndexOf(argumentName);
            if (index >= 0)
            {
                args[index + 1] = value;
            }
            else
            {
                args.Add(argumentName);
                args.Add(value);
            }

            return IosBuildConfig.TryParse(args, NoEnvironment, out _, out error);
        }
    }
}
