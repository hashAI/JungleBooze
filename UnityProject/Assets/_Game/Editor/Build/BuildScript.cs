using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JungleBooze.Editor.Build
{
    /// <summary>
    /// Batch-mode build entry points. Called by fastlane (lane "beta") through tools/build/build_ios.sh:
    ///
    ///   Unity -batchmode -quit -projectPath UnityProject -buildTarget iOS
    ///         -executeMethod JungleBooze.Editor.Build.BuildScript.BuildIos
    ///         -logFile &lt;file&gt; -jbBundleId &lt;id&gt; -jbVersion 0.1.0 -jbBuildNumber 12
    ///         [-jbBuildType release|development] [-jbOutput Builds/iOS] [-jbTeamId ABCDE12345]
    ///
    /// Arguments and their environment-variable fallbacks are documented on <see cref="IosBuildConfig"/>.
    /// The script exports an Xcode project only; signing, archiving and upload happen in fastlane.
    /// Player settings changed for the build (bundle id, version, build number, signing) are restored afterwards
    /// so a build never leaves ProjectSettings modified in git.
    /// Exit codes: 0 success, 1 build failed, 2 invalid settings or missing prerequisites.
    /// </summary>
    public static class BuildScript
    {
        private const string LogPrefix = "[JungleBooze build] ";
        private const string XcodeProjectName = "Unity-iPhone.xcodeproj";

        /// <summary>Entry point for -executeMethod. Always exits the editor with an exit code in batch mode.</summary>
        public static void BuildIos()
        {
            int exitCode;
            try
            {
                exitCode = RunIosBuild(Environment.GetCommandLineArgs());
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Unexpected error: " + exception);
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static int RunIosBuild(string[] args)
        {
            if (!IosBuildConfig.TryParse(args, Environment.GetEnvironmentVariable, out IosBuildConfig config,
                    out string error))
            {
                Debug.LogError(LogPrefix + "Invalid build settings:" + Environment.NewLine + error);
                return 2;
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogError(LogPrefix + "iOS Build Support is not installed for this Unity editor. " +
                               "Add it in Unity Hub: Installs > (this version) > Add modules > iOS Build Support.");
                return 2;
            }

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError(LogPrefix + "No scenes are enabled in the build scene list " +
                               "(File > Build Profiles > Scene List). Nothing to build.");
                return 2;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputPath = Path.GetFullPath(Path.IsPathRooted(config.OutputPath)
                ? config.OutputPath
                : Path.Combine(projectRoot, config.OutputPath));

            if (!PrepareOutputFolder(outputPath, out string folderError))
            {
                Debug.LogError(LogPrefix + folderError);
                return 2;
            }

            Debug.Log(LogPrefix + "Building iOS Xcode project: bundle id " + config.BundleId +
                      ", version " + config.Version + " (" + config.BuildNumber + "), " + config.BuildType +
                      ", output " + outputPath + ", scenes: " + string.Join(", ", scenes));

            // Captured so the build never leaves ProjectSettings modified in git.
            string savedApplicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            string savedBundleVersion = PlayerSettings.bundleVersion;
            string savedBuildNumber = PlayerSettings.iOS.buildNumber;
            bool savedAutomaticSigning = PlayerSettings.iOS.appleEnableAutomaticSigning;
            string savedTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
            try
            {
                ApplySettings(config);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    options = BuildOptions.StrictMode,
                };

                if (config.BuildType == IosBuildType.Development)
                {
                    options.options |= BuildOptions.Development;
                }

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError(LogPrefix + "Build " + summary.result + " with " + summary.totalErrors +
                                   " error(s). Search this log for 'error' to see the cause.");
                    return 1;
                }

                if (!Directory.Exists(Path.Combine(outputPath, XcodeProjectName)))
                {
                    Debug.LogError(LogPrefix + "Build reported success but " + XcodeProjectName +
                                   " was not found in " + outputPath + ".");
                    return 1;
                }

                Debug.Log(LogPrefix + "Build succeeded in " + summary.totalTime + ". Xcode project: " + outputPath);
                return 0;
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, savedApplicationIdentifier);
                PlayerSettings.bundleVersion = savedBundleVersion;
                PlayerSettings.iOS.buildNumber = savedBuildNumber;
                PlayerSettings.iOS.appleEnableAutomaticSigning = savedAutomaticSigning;
                PlayerSettings.iOS.appleDeveloperTeamID = savedTeamId;
                AssetDatabase.SaveAssets();
            }
        }

        private static string[] GetEnabledScenes()
        {
            var result = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    result.Add(scene.path);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Makes sure the output folder can be (re)written. A previous Unity export is deleted so no stale files
        /// survive; any other non-empty folder is refused, so a wrong path can never delete unrelated files.
        /// </summary>
        private static bool PrepareOutputFolder(string outputPath, out string error)
        {
            error = string.Empty;
            if (!Directory.Exists(outputPath))
            {
                return true;
            }

            if (Directory.Exists(Path.Combine(outputPath, XcodeProjectName)))
            {
                Directory.Delete(outputPath, true);
                return true;
            }

            if (Directory.GetFileSystemEntries(outputPath).Length == 0)
            {
                return true;
            }

            error = "Output folder " + outputPath + " is not empty and is not a previous Unity iOS export. " +
                    "Choose another folder or empty it.";
            return false;
        }

        private static void ApplySettings(IosBuildConfig config)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, config.BundleId);
            PlayerSettings.bundleVersion = config.Version;
            PlayerSettings.iOS.buildNumber = config.BuildNumber;

            // fastlane match installs the distribution profile and sets it on the exported Xcode project.
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            if (config.TeamId.Length > 0)
            {
                PlayerSettings.iOS.appleDeveloperTeamID = config.TeamId;
            }
        }
    }
}
