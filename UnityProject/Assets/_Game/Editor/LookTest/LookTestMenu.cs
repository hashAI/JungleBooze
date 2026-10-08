using System.Collections.Generic;
using System.IO;
using JungleBooze.App.LookTest;
using JungleBooze.Editor.Setup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Menu JungleBooze > Look Test (ADR 0004, docs/PLAY_FIRST_BUILD.md "Look test"):
    /// Build Scene, Open Scene, Select Config, Use Look Test iOS Build Settings (look test first in the build,
    /// landscape, URP-Realistic) and Restore Run Build Settings (Run first, portrait, URP-Mobile). The portrait Run
    /// scene keeps working; the owner decides the final orientation later.
    /// </summary>
    public static class LookTestMenu
    {
        private const string Root = "JungleBooze/Look Test/";
        private const string LogPrefix = "[JungleBooze look test] ";

        [MenuItem(Root + "Build Scene", false, 1)]
        public static void BuildScene()
        {
            LookTestSceneBuilder.Build();
        }

        [MenuItem(Root + "Open Scene", false, 2)]
        public static void OpenScene()
        {
            if (!File.Exists(LookTestSceneBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog("Look test", "The look test scene does not exist yet. Use JungleBooze > Look Test > Build Scene.", "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(LookTestSceneBuilder.ScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem(Root + "Select Config", false, 3)]
        public static void SelectConfig()
        {
            LookTestConfigAsset config = LookTestSceneBuilder.EnsureConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem(Root + "Use Look Test iOS Build Settings", false, 20)]
        public static void UseLookTestBuildSettings()
        {
            if (!File.Exists(LookTestSceneBuilder.ScenePath))
            {
                EditorUtility.DisplayDialog("Look test", "Build the look test scene first (JungleBooze > Look Test > Build Scene).", "OK");
                return;
            }

            SetFirstScene(LookTestSceneBuilder.ScenePath);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.enableFrameTimingStats = true;
            LookTestPipelineSetup.ApplyRealistic(LookTestSceneBuilder.EnsureConfig());
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Build settings: LookTest is the first scene, landscape (left/right), URP-Realistic, Frame Timing Stats on.");
        }

        [MenuItem(Root + "Restore Run Build Settings", false, 21)]
        public static void RestoreRunBuildSettings()
        {
            SetFirstScene(ProjectBootstrap.RunScenePath);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            LookTestPipelineSetup.RestoreMobile();
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Build settings: Run is the first scene, portrait, URP-Mobile.");
        }

        private static void SetFirstScene(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            var updated = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != path)
                {
                    updated.Add(scenes[i]);
                }
            }

            EditorBuildSettings.scenes = updated.ToArray();
        }
    }
}
