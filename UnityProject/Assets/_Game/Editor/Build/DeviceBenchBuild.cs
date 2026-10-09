using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JungleBooze.App.HeroBasin;
using JungleBooze.App.Perf;
using JungleBooze.Editor.LookTest;
using JungleBooze.Editor.Perf;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JungleBooze.Editor.Build
{
    /// <summary>
    /// Device benchmark player (tools/build/device_bench.sh; docs/perf/2026-10-iphone12-readiness.md). Batch entries:
    ///
    ///   JungleBooze.Editor.Build.DeviceBenchBuild.BuildIos   [-jbOutput Builds/iOS-Bench] [-jbBundleId id] [-jbTeamId TEAM] [-jbRelease]
    ///   JungleBooze.Editor.Build.DeviceBenchBuild.BuildMac   [-jbOutput Builds/Mac-Bench/AureliaBench.app] (M4 cross-check)
    ///
    /// Sets up the quality tiers, writes the bootstrap scene Scenes/Benchmark/DeviceBench.unity (a
    /// <see cref="DeviceBenchRunner"/> with the bench config and the painterly hero config), and builds
    /// [DeviceBench, HeroBasin_Painterly, Expedition] as a development player (profiler-friendly; -jbRelease for a
    /// release player). iOS: Xcode project with automatic signing (team from -jbTeamId, or picked in Xcode), both
    /// orientations, and file sharing on so Documents/bench shows in Finder. Player settings are restored afterwards;
    /// a build-size report goes next to the output. Exit codes: 0 ok, 1 build failed, 2 missing prerequisites.
    /// </summary>
    public static class DeviceBenchBuild
    {
        public const string BenchScenePath = "Assets/_Game/Scenes/Benchmark/DeviceBench.unity";
        public const string ExpeditionScenePath = "Assets/_Game/Scenes/Expedition.unity";
        public const string DefaultBundleId = "com.aurelia.devbench";
        private const string LogPrefix = "[JungleBooze bench] ";

        /// <summary>True while a bench build runs (the iOS post-process only touches bench builds).</summary>
        public static bool Building { get; private set; }

        public static void BuildIos()
        {
            Exit(Run(BuildTarget.iOS));
        }

        public static void BuildMac()
        {
            Exit(Run(BuildTarget.StandaloneOSX));
        }

        [MenuItem("JungleBooze/Perf/Write Device Bench Scene")]
        public static void WriteBenchSceneFromMenu()
        {
            QualityTierSetup.Apply();
            WriteBenchScene();
        }

        private static int Run(BuildTarget target)
        {
            try
            {
                return Build(target, Environment.GetCommandLineArgs());
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Unexpected error: " + exception);
                return 1;
            }
        }

        private static int Build(BuildTarget target, string[] args)
        {
            foreach (string note in QualityTierSetup.Apply())
            {
                Debug.Log(LogPrefix + note);
            }

            if (!File.Exists(PerfAudit.HeroScenePath))
            {
                Debug.LogError(LogPrefix + PerfAudit.HeroScenePath + " is missing. Build it first: tools/ci/unity.sh method " +
                               "JungleBooze.Editor.HeroBasin.HeroBasinBatch.BuildScene -jbHeroStyle painterly (device_bench.sh does this).");
                return 2;
            }

            if (!WriteBenchScene())
            {
                return 2;
            }

            bool ios = target == BuildTarget.iOS;
            bool release = args.Contains("-jbRelease");
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string output = Arg(args, "-jbOutput") ?? (ios ? "Builds/iOS-Bench" : "Builds/Mac-Bench/AureliaBench.app");
            output = Path.GetFullPath(Path.IsPathRooted(output) ? output : Path.Combine(projectRoot, output));
            string bundleId = Arg(args, "-jbBundleId") ?? DefaultBundleId;
            string teamId = Arg(args, "-jbTeamId") ?? string.Empty;

            NamedBuildTarget named = ios ? NamedBuildTarget.iOS : NamedBuildTarget.Standalone;
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
            string savedId = PlayerSettings.GetApplicationIdentifier(named);
            string savedProduct = PlayerSettings.productName;
            UIOrientation savedOrientation = PlayerSettings.defaultInterfaceOrientation;
            bool savedPortrait = PlayerSettings.allowedAutorotateToPortrait;
            bool savedPortraitDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            bool savedLeft = PlayerSettings.allowedAutorotateToLandscapeLeft;
            bool savedRight = PlayerSettings.allowedAutorotateToLandscapeRight;
            bool savedSigning = PlayerSettings.iOS.appleEnableAutomaticSigning;
            string savedTeam = PlayerSettings.iOS.appleDeveloperTeamID;
            bool savedTiming = PlayerSettings.enableFrameTimingStats;
            try
            {
                if (ios)
                {
                    // Mac cross-check players keep the project's identifier (setting it would add a Standalone entry).
                    PlayerSettings.SetApplicationIdentifier(named, bundleId);
                }

                PlayerSettings.productName = "Aurelia Bench";
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = true;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.enableFrameTimingStats = true;
                if (ios)
                {
                    PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                    if (teamId.Length > 0)
                    {
                        PlayerSettings.iOS.appleDeveloperTeamID = teamId;
                    }
                }

                if (Directory.Exists(output) && (!ios || Directory.Exists(Path.Combine(output, "Unity-iPhone.xcodeproj"))))
                {
                    Directory.Delete(output, true);
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { BenchScenePath, PerfAudit.HeroScenePath, ExpeditionScenePath },
                    locationPathName = output,
                    target = target,
                    targetGroup = ios ? BuildTargetGroup.iOS : BuildTargetGroup.Standalone,
                    options = release ? BuildOptions.None : BuildOptions.Development,
                };

                Debug.Log(LogPrefix + "Building " + (release ? "release" : "development") + " " + target + " bench player, bundle id " + bundleId +
                          (teamId.Length > 0 ? ", team " + teamId : ", team chosen in Xcode") + " -> " + output);
                Building = true;
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Building = false;
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError(LogPrefix + "Build " + report.summary.result + " with " + report.summary.totalErrors + " error(s).");
                    return 1;
                }

                WriteSizeReport(report, output, ios);
                Debug.Log(LogPrefix + "Build succeeded in " + report.summary.totalTime + ": " + output);
                return 0;
            }
            finally
            {
                Building = false;
                if (ios)
                {
                    PlayerSettings.SetApplicationIdentifier(named, savedId);
                }

                PlayerSettings.productName = savedProduct;
                PlayerSettings.defaultInterfaceOrientation = savedOrientation;
                PlayerSettings.allowedAutorotateToPortrait = savedPortrait;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = savedPortraitDown;
                PlayerSettings.allowedAutorotateToLandscapeLeft = savedLeft;
                PlayerSettings.allowedAutorotateToLandscapeRight = savedRight;
                PlayerSettings.iOS.appleEnableAutomaticSigning = savedSigning;
                PlayerSettings.iOS.appleDeveloperTeamID = savedTeam;
                PlayerSettings.enableFrameTimingStats = savedTiming;
                AssetDatabase.SaveAssets();

                // Leave the project on the platform it was on (other tools render editor captures on macOS).
                if (EditorUserBuildSettings.activeBuildTarget != activeTarget && !args.Contains("-jbKeepTarget"))
                {
                    Debug.Log(LogPrefix + "Switching the editor back to " + activeTarget + ".");
                    EditorUserBuildSettings.SwitchActiveBuildTarget(activeGroup, activeTarget);
                }
            }
        }

        /// <summary>Writes the bootstrap scene: one DeviceBenchRunner wired to the bench and hero configs.</summary>
        public static bool WriteBenchScene()
        {
            LookTestAssets.EnsureFolder(Path.GetDirectoryName(BenchScenePath).Replace('\\', '/'));

            // Load the configs after NewScene: opening a new scene unloads unreferenced assets, and a reference taken
            // before it would be saved as null.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bench = AssetDatabase.LoadAssetAtPath<DeviceBenchConfigAsset>(QualityTierSetup.BenchConfigPath);
            var hero = AssetDatabase.LoadAssetAtPath<HeroBasinConfigAsset>(PerfAudit.HeroConfigPath);
            if (bench == null || hero == null)
            {
                Debug.LogError(LogPrefix + "Missing " + (bench == null ? QualityTierSetup.BenchConfigPath : PerfAudit.HeroConfigPath) + ".");
                return false;
            }

            var go = new GameObject("DeviceBench");
            DeviceBenchRunner runner = go.AddComponent<DeviceBenchRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("_config").objectReferenceValue = bench;
            so.FindProperty("_hero").objectReferenceValue = hero;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (so.FindProperty("_config").objectReferenceValue == null || so.FindProperty("_hero").objectReferenceValue == null)
            {
                Debug.LogError(LogPrefix + "Bench runner references did not stick.");
                return false;
            }

            var cameraObject = new GameObject("BootCamera");
            Camera boot = cameraObject.AddComponent<Camera>();
            boot.clearFlags = CameraClearFlags.SolidColor;
            boot.backgroundColor = Color.black;
            boot.cullingMask = 0;
            bool saved = EditorSceneManager.SaveScene(scene, BenchScenePath);
            Debug.Log(LogPrefix + "Bench scene " + (saved ? "written: " : "NOT written: ") + BenchScenePath + " (" + bench.Phases.Length + " phases, " +
                      (bench.TotalSeconds / 60f).ToString("0.0", CultureInfo.InvariantCulture) + " min).");
            return saved;
        }

        /// <summary>Largest packed assets and totals by type, for the build-size budget (ARCHITECTURE 10.1).</summary>
        private static void WriteSizeReport(BuildReport report, string output, bool ios)
        {
            var text = new StringBuilder();
            text.AppendLine("Device bench build size report (" + report.summary.platform + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
            text.AppendLine("Total output size reported by Unity: " + (report.summary.totalSize / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB");
            var entries = report.packedAssets.SelectMany(p => p.contents).ToList();
            text.AppendLine("By type (packed, uncompressed in the player data):");
            foreach (var group in entries.GroupBy(e => e.type != null ? e.type.Name : "?").OrderByDescending(g => g.Sum(e => (long)e.packedSize)))
            {
                text.AppendLine("  " + group.Key.PadRight(28) + (group.Sum(e => (long)e.packedSize) / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(8) + " MB  (" + group.Count() + ")");
            }

            text.AppendLine("Largest 40 assets:");
            foreach (var e in entries.OrderByDescending(e => e.packedSize).Take(40))
            {
                text.AppendLine("  " + (e.packedSize / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture).PadLeft(7) + " MB  " + e.sourceAssetPath);
            }

            string path = ios ? Path.Combine(output, "bench_size_report.txt") : output + "_size_report.txt";
            File.WriteAllText(path, text.ToString());
            Debug.Log(LogPrefix + "Size report: " + path);
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
