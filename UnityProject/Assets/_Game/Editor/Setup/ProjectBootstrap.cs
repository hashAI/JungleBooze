using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace JungleBooze.Editor.Setup
{
    /// <summary>
    /// One-time project setup that runs automatically the first time the project is opened in the editor
    /// (and again whenever <see cref="SetupVersion"/> is raised). See docs/adr/0003-first-playable-bootstrap.md.
    ///
    /// What it does (each step is idempotent and only touches values that still look like new-project defaults):
    ///  1. Editor: Force Text serialization, Visible Meta Files.
    ///  2. Player: Active Input Handling = Both (needs an editor restart; the user is asked).
    ///  3. Graphics: creates Assets/_Game/Config/Rendering/URP-Mobile.asset (+ renderer) and assigns it,
    ///     unless a render pipeline is already assigned.
    ///  4. Player (iOS): product name, bundle id, portrait, iPhone only, minimum iOS, Linear color space.
    ///  5. Creates Assets/_Game/Scenes/Run.unity if missing and puts it first in the build scene list.
    ///  6. Writes ProjectSettings/JungleBoozeSetup.txt so the automatic run happens only once.
    ///
    /// Menu: JungleBooze/Setup/Run Project Setup (run again at any time).
    /// Batch mode: -executeMethod JungleBooze.Editor.Setup.ProjectBootstrap.RunFromCommandLine
    /// The automatic run is skipped in batch mode so CI test runs are never changed behind their back.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        /// <summary>Raise this when a new setup step is added so existing checkouts run setup again.</summary>
        public const int SetupVersion = 1;

        public const string RunScenePath = "Assets/_Game/Scenes/Run.unity";
        public const string RenderingFolder = "Assets/_Game/Config/Rendering";
        public const string PipelineAssetPath = RenderingFolder + "/URP-Mobile.asset";
        public const string RendererAssetPath = RenderingFolder + "/URP-Mobile-Renderer.asset";
        public const string MarkerRelativePath = "ProjectSettings/JungleBoozeSetup.txt";

        private const string LogPrefix = "[JungleBooze setup] ";
        private const string MenuRun = "JungleBooze/Setup/Run Project Setup";
        private const string MenuBuiltIn = "JungleBooze/Setup/Fallback: Use Built-in Renderer";
        private const string DialogTitle = "JungleBooze project setup";
        private const string UrpPackageFolder = "Packages/com.unity.render-pipelines.universal";
        private const string AutoRunSessionKey = "JungleBooze.Setup.AutoRunThisSession";

        // Values of PlayerSettings.activeInputHandler: 0 = Input Manager (Old), 1 = Input System (New), 2 = Both.
        private const string ActiveInputHandlerProperty = "activeInputHandler";
        private const int ActiveInputHandlerBoth = 2;

        static ProjectBootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (ProjectSetupRules.ParseMarkerVersion(ReadMarker()) >= SetupVersion)
            {
                return;
            }

            // At most one automatic attempt per editor session, so a failing step does not nag after every compile.
            if (SessionState.GetBool(AutoRunSessionKey, false))
            {
                return;
            }

            // Wait until every [InitializeOnLoad] class (including the Input System's own first-run check) has run
            // and the asset database is idle.
            EditorApplication.delayCall += RunWhenEditorIsReady;
        }

        /// <summary>Menu entry: runs every step again, then reports what changed.</summary>
        [MenuItem(MenuRun, false, 1)]
        public static void RunFromMenu()
        {
            Run(true);
        }

        /// <summary>Batch-mode entry point (no dialogs, no restart). Exits with 0 on success, 1 on failure.</summary>
        public static void RunFromCommandLine()
        {
            SetupResult result = Run(false);
            EditorApplication.Exit(result.Errors.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// Troubleshooting only: removes the URP asset from Graphics and Quality settings so the project renders
        /// with the built-in pipeline. "Run Project Setup" assigns URP again.
        /// </summary>
        [MenuItem(MenuBuiltIn, false, 50)]
        public static void UseBuiltInRenderer()
        {
            if (!EditorUtility.DisplayDialog(DialogTitle,
                    "Switch rendering to Unity's built-in pipeline?\n\nUse this only if shapes look pink or black " +
                    "after setup and you want to keep playing. Run 'JungleBooze > Setup > Run Project Setup' to " +
                    "switch back to URP.", "Switch", "Cancel"))
            {
                return;
            }

            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Rendering switched to the built-in pipeline (fallback).");
        }

        private static void RunWhenEditorIsReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunWhenEditorIsReady;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.SetBool(AutoRunSessionKey, true);
            Run(true);
        }

        private static SetupResult Run(bool interactive)
        {
            var result = new SetupResult();
            Debug.Log(LogPrefix + "Running project setup (version " + SetupVersion + ").");

            RunStep(result, "editor settings", () => ApplyEditorSettings(result));
            RunStep(result, "input handling", () => ApplyInputHandling(result));
            RunStep(result, "render pipeline", () => ApplyRenderPipeline(result));
            RunStep(result, "iOS player settings", () => ApplyPlayerSettings(result));
            RunStep(result, "Run scene", () => EnsureRunScene(result, interactive));
            RunStep(result, "build scene list", () => EnsureSceneInBuildList(result));

            AssetDatabase.SaveAssets();

            if (result.Errors.Count == 0)
            {
                WriteMarker();
            }

            Report(result, interactive);
            return result;
        }

        private static void RunStep(SetupResult result, string name, System.Action step)
        {
            try
            {
                step();
            }
            catch (System.Exception exception)
            {
                result.Errors.Add("Step '" + name + "' failed: " + exception.Message);
                Debug.LogException(exception);
            }
        }

        // ---------------------------------------------------------------- 1. Editor settings

        private static void ApplyEditorSettings(SetupResult result)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                result.Changes.Add("Asset serialization set to Force Text.");
            }

            const string visibleMeta = "Visible Meta Files";
            if (VersionControlSettings.mode != visibleMeta)
            {
                VersionControlSettings.mode = visibleMeta;
                result.Changes.Add("Version control mode set to Visible Meta Files.");
            }
        }

        // ---------------------------------------------------------------- 2. Input handling

        private static void ApplyInputHandling(SetupResult result)
        {
            // There is no public PlayerSettings API for this value, so it is changed through the serialized
            // PlayerSettings object (the same approach the Input System package uses for its own prompt).
            UnityEngine.Object playerSettings = FindPlayerSettingsObject();
            if (playerSettings == null)
            {
                result.Warnings.Add("Could not find the PlayerSettings object. Set Edit > Project Settings > " +
                                    "Player > Other Settings > Active Input Handling to 'Both' by hand.");
                return;
            }

            var serialized = new SerializedObject(playerSettings);
            SerializedProperty property = serialized.FindProperty(ActiveInputHandlerProperty);
            if (property == null)
            {
                result.Warnings.Add("PlayerSettings has no '" + ActiveInputHandlerProperty + "' field in this " +
                                    "Unity version. Set Active Input Handling to 'Both' by hand.");
                return;
            }

            if (property.intValue == ActiveInputHandlerBoth)
            {
                return;
            }

            property.intValue = ActiveInputHandlerBoth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            result.Changes.Add("Active Input Handling set to 'Both' (takes effect after an editor restart).");
            result.NeedsRestart = true;
        }

        private static UnityEngine.Object FindPlayerSettingsObject()
        {
            PlayerSettings[] loaded = UnityEngine.Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (loaded != null && loaded.Length > 0)
            {
                return loaded[0];
            }

            UnityEngine.Object[] fromDisk = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            return fromDisk != null && fromDisk.Length > 0 ? fromDisk[0] : null;
        }

        // ---------------------------------------------------------------- 3. Render pipeline (URP, mobile)

        private static void ApplyRenderPipeline(SetupResult result)
        {
            RenderPipelineAsset current = GraphicsSettings.defaultRenderPipeline;
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

            if (current != null && current != asset)
            {
                result.Notes.Add("A render pipeline asset is already assigned (" + current.name + "); left as is.");
                return;
            }

            if (asset == null)
            {
                asset = CreatePipelineAsset(result);
                if (asset == null)
                {
                    return;
                }
            }

            if (current != asset)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                result.Changes.Add("Graphics: render pipeline set to " + PipelineAssetPath + ".");
            }

            // A quality level can override the default pipeline. Only the active level is checked; a fresh
            // project has no overrides.
            if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != asset)
            {
                QualitySettings.renderPipeline = asset;
                result.Changes.Add("Quality level '" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                                   "': render pipeline override set to URP-Mobile.");
            }
        }

        private static UniversalRenderPipelineAsset CreatePipelineAsset(SetupResult result)
        {
            EnsureFolder(RenderingFolder);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
            }

            ConfigureRenderer(rendererData, result);

            UniversalRenderPipelineAsset asset = UniversalRenderPipelineAsset.Create(rendererData);
            if (asset == null)
            {
                result.Errors.Add("URP did not create a pipeline asset. Rendering stays on the built-in pipeline.");
                return null;
            }

            AssetDatabase.CreateAsset(asset, PipelineAssetPath);
            ConfigurePipeline(asset, result);
            AssetDatabase.SaveAssets();
            result.Changes.Add("Created " + PipelineAssetPath + " and " + RendererAssetPath + " (mobile settings).");
            return asset;
        }

        /// <summary>Mobile settings from ADR 0001, applied through serialized fields (missing ones are reported).</summary>
        private static void ConfigurePipeline(UniversalRenderPipelineAsset asset, SetupResult result)
        {
            var serialized = new SerializedObject(asset);
            SetBool(serialized, "m_SupportsHDR", false, result);
            SetInt(serialized, "m_MSAA", 2, result); // 2x MSAA: cheap on tile-based Apple GPUs.
            SetFloat(serialized, "m_RenderScale", 1f, result);
            SetBool(serialized, "m_RequireDepthTexture", false, result);
            SetBool(serialized, "m_RequireOpaqueTexture", false, result);
            SetBool(serialized, "m_MainLightShadowsSupported", false, result); // Blob shadows instead (ADR 0001).
            SetInt(serialized, "m_AdditionalLightsRenderingMode", 0, result); // Disabled.
            SetBool(serialized, "m_SoftShadowsSupported", false, result);
            SetBool(serialized, "m_UseSRPBatcher", true, result);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void ConfigureRenderer(UniversalRendererData rendererData, SetupResult result)
        {
            var serialized = new SerializedObject(rendererData);
            SetInt(serialized, "m_RenderingMode", 0, result); // Forward.

            // Post-processing data ships with URP. Without it post-processing is simply unavailable, which is
            // acceptable for First Playable.
            SerializedProperty postProcess = serialized.FindProperty("postProcessData") ??
                                             serialized.FindProperty("m_PostProcessData");
            if (postProcess != null && postProcess.objectReferenceValue == null)
            {
                ScriptableObject data = FindUrpPostProcessData();
                if (data != null)
                {
                    postProcess.objectReferenceValue = data;
                }
                else
                {
                    result.Notes.Add("URP post-processing data not found; post-processing is off (fine for FP1).");
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        private static ScriptableObject FindUrpPostProcessData()
        {
            // Same asset URP's own "Create > Rendering > URP Asset" menu assigns (URP 17.3).
            var known = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                UrpPackageFolder + "/Runtime/Data/PostProcessData.asset");
            if (known != null)
            {
                return known;
            }

            string[] guids = AssetDatabase.FindAssets("t:PostProcessData", new[] { UrpPackageFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (data != null)
                {
                    return data;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- 4. Player settings (iOS)

        private static void ApplyPlayerSettings(SetupResult result)
        {
            string projectFolderName = new DirectoryInfo(ProjectRoot()).Name;
            if (ProjectSetupRules.ShouldReplaceProductName(PlayerSettings.productName, projectFolderName))
            {
                PlayerSettings.productName = ProjectSetupRules.PlaceholderProductName;
                result.Changes.Add("Product name set to '" + ProjectSetupRules.PlaceholderProductName +
                                   "' (placeholder until the owner picks the app name).");
            }

            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (ProjectSetupRules.ShouldReplaceBundleId(bundleId))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ProjectSetupRules.PlaceholderBundleId);
                result.Changes.Add("iOS bundle identifier set to " + ProjectSetupRules.PlaceholderBundleId +
                                   " (placeholder).");
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                result.Changes.Add("Default orientation set to Portrait.");
            }

            if (PlayerSettings.iOS.targetDevice != iOSTargetDevice.iPhoneOnly)
            {
                PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
                result.Changes.Add("iOS target device set to iPhone only.");
            }

            if (ProjectSetupRules.IsBelowMinimumVersion(PlayerSettings.iOS.targetOSVersionString,
                    ProjectSetupRules.MinimumIosVersion))
            {
                PlayerSettings.iOS.targetOSVersionString = ProjectSetupRules.MinimumIosVersion;
                result.Changes.Add("Minimum iOS version set to " + ProjectSetupRules.MinimumIosVersion + ".");
            }

            if (PlayerSettings.colorSpace != UnityEngine.ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = UnityEngine.ColorSpace.Linear;
                result.Changes.Add("Color space set to Linear.");
            }

            if (ProjectSetupRules.ContainsForbiddenWord(PlayerSettings.companyName))
            {
                result.Warnings.Add("Company name contains the word '" + ProjectSetupRules.ForbiddenVisibleWord +
                                    "'. Change it in Project Settings > Player.");
            }
        }

        // ---------------------------------------------------------------- 5. Run scene

        private static void EnsureRunScene(SetupResult result, bool interactive)
        {
            if (File.Exists(Path.Combine(ProjectRoot(), RunScenePath)))
            {
                OpenRunSceneIfEditorIsEmpty(result, interactive);
                return;
            }

            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                result.Warnings.Add("Run scene was not created because saving the open scene was cancelled. " +
                                    "Run 'JungleBooze > Setup > Run Project Setup' again.");
                return;
            }

            EnsureFolder(Path.GetDirectoryName(RunScenePath).Replace('\\', '/'));

            // The scene is intentionally empty: the game's runtime bootstrap builds the camera, light, track,
            // player and HUD when Play is pressed.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, RunScenePath))
            {
                result.Errors.Add("Could not save " + RunScenePath + ".");
                return;
            }

            result.Changes.Add("Created " + RunScenePath + " (empty; the game builds itself at Play).");
        }

        private static void OpenRunSceneIfEditorIsEmpty(SetupResult result, bool interactive)
        {
            if (!interactive || SceneManager.sceneCount != 1)
            {
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                return;
            }

            EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            result.Notes.Add("Opened " + RunScenePath + ".");
        }

        // ---------------------------------------------------------------- 6. Build scene list

        private static void EnsureSceneInBuildList(SetupResult result)
        {
            if (!File.Exists(Path.Combine(ProjectRoot(), RunScenePath)))
            {
                return;
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length > 0 && scenes[0].path == RunScenePath && scenes[0].enabled)
            {
                return;
            }

            var updated = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(RunScenePath, true) };
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != RunScenePath)
                {
                    updated.Add(scenes[i]);
                }
            }

            EditorBuildSettings.scenes = updated.ToArray();
            result.Changes.Add("Build scene list: " + RunScenePath + " added as the first, enabled scene.");
        }

        // ---------------------------------------------------------------- Reporting and helpers

        private static void Report(SetupResult result, bool interactive)
        {
            var text = new System.Text.StringBuilder();
            AppendSection(text, "Changed", result.Changes);
            AppendSection(text, "Notes", result.Notes);
            AppendSection(text, "Needs your attention", result.Warnings);
            AppendSection(text, "Errors", result.Errors);
            if (text.Length == 0)
            {
                text.AppendLine("Everything was already set up. Nothing changed.");
            }

            string summary = text.ToString();
            if (result.Errors.Count > 0)
            {
                Debug.LogError(LogPrefix + "Setup finished with errors:\n" + summary);
            }
            else if (result.Warnings.Count > 0)
            {
                Debug.LogWarning(LogPrefix + "Setup finished with warnings:\n" + summary);
            }
            else
            {
                Debug.Log(LogPrefix + "Setup finished:\n" + summary);
            }

            if (!interactive || Application.isBatchMode)
            {
                return;
            }

            if (result.NeedsRestart)
            {
                bool restart = EditorUtility.DisplayDialog(DialogTitle,
                    summary + "\nUnity must restart once so keyboard, mouse and touch input work. Restart now?",
                    "Restart now", "Later");
                if (restart)
                {
                    AssetDatabase.SaveAssets();
                    EditorApplication.OpenProject(ProjectRoot());
                }

                return;
            }

            if (result.Changes.Count > 0 || result.Warnings.Count > 0 || result.Errors.Count > 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, summary, "OK");
            }
        }

        private static void AppendSection(System.Text.StringBuilder text, string title, List<string> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            text.AppendLine(title + ":");
            for (int i = 0; i < lines.Count; i++)
            {
                text.AppendLine("- " + lines[i]);
            }

            text.AppendLine();
        }

        private static void SetBool(SerializedObject serialized, string name, bool value, SetupResult result)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                result.Notes.Add("Setting '" + name + "' not found on " + serialized.targetObject.name +
                                 " (URP version difference); default kept.");
                return;
            }

            property.boolValue = value;
        }

        private static void SetInt(SerializedObject serialized, string name, int value, SetupResult result)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null || (property.propertyType != SerializedPropertyType.Integer &&
                                     property.propertyType != SerializedPropertyType.Enum))
            {
                result.Notes.Add("Setting '" + name + "' not found on " + serialized.targetObject.name +
                                 " (URP version difference); default kept.");
                return;
            }

            property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string name, float value, SetupResult result)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                result.Notes.Add("Setting '" + name + "' not found on " + serialized.targetObject.name +
                                 " (URP version difference); default kept.");
                return;
            }

            property.floatValue = value;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string ReadMarker()
        {
            string path = Path.Combine(ProjectRoot(), MarkerRelativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static void WriteMarker()
        {
            string path = Path.Combine(ProjectRoot(), MarkerRelativePath);
            File.WriteAllText(path,
                "# Written by JungleBooze.Editor.Setup.ProjectBootstrap. Commit this file with ProjectSettings.\n" +
                "# Delete it (or raise SetupVersion) to make the automatic setup run again.\n" +
                "setupVersion=" + SetupVersion + "\n");
        }

        private sealed class SetupResult
        {
            public List<string> Changes { get; } = new List<string>();

            public List<string> Notes { get; } = new List<string>();

            public List<string> Warnings { get; } = new List<string>();

            public List<string> Errors { get; } = new List<string>();

            public bool NeedsRestart { get; set; }
        }
    }
}
