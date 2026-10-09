using System.Collections.Generic;
using System.IO;
using JungleBooze.App.FeelTest;
using JungleBooze.Editor.Characters;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JungleBooze.Editor.FeelTest
{
    /// <summary>
    /// Creates the Phase 1 feel test (spec 101): movement config assets with spec values (only if missing, so
    /// tuning is never overwritten), the feel course asset, gray-box materials and palette, the FeelTest scene
    /// (camera, sun, <see cref="FeelTestRoot"/>), FeelTest first in the build, and the iOS run-screen settings
    /// (defer system edge gestures, auto-hide the home indicator; spec 101 §3.3 rule 5).
    /// </summary>
    public static class FeelTestSetup
    {
        private const string LogPrefix = "[JungleBooze feel test] ";

        [MenuItem("JungleBooze/Feel Test/Build Scene", false, 1)]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolders();
            MovementConfigAssets movement = EnsureMovementAssets();
            GestureConfigAsset gestures = EnsureAsset<GestureConfigAsset>(FeelTestPaths.Gesture, null);
            CameraProfileAsset landscape = EnsureAsset<CameraProfileAsset>(FeelTestPaths.CameraLandscape, a => a.SetValues(new CameraProfile()));
            CameraProfileAsset portrait = EnsureAsset<CameraProfileAsset>(FeelTestPaths.CameraPortrait, a => a.SetValues(CameraProfile.DefaultPortrait()));
            FeelCourseAsset course = EnsureAsset<FeelCourseAsset>(FeelTestPaths.FeelCourse, a => a.SetCourse(FeelCourseLayout.Create()));
            FeelTestPalette palette = EnsurePalette();
            AssetDatabase.SaveAssets();

            BuildScene(movement, gestures, landscape, portrait, course, palette);
            UseFeelTestBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Feel test ready: " + FeelTestPaths.Scene);
        }

        [MenuItem("JungleBooze/Feel Test/Open Scene", false, 2)]
        public static void OpenScene()
        {
            if (!File.Exists(FeelTestPaths.Scene))
            {
                BuildAll();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(FeelTestPaths.Scene, OpenSceneMode.Single);
            }
        }

        [MenuItem("JungleBooze/Feel Test/Reset Feel Course To Spec Layout", false, 20)]
        public static void ResetCourse()
        {
            var course = AssetDatabase.LoadAssetAtPath<FeelCourseAsset>(FeelTestPaths.FeelCourse);
            if (course == null)
            {
                BuildAll();
                return;
            }

            course.SetCourse(FeelCourseLayout.Create());
            EditorUtility.SetDirty(course);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Feel course reset to the spec 101 §6 layout.");
        }

        [MenuItem("JungleBooze/Feel Test/Use Feel Test Build Settings", false, 21)]
        public static void UseFeelTestBuildSettings()
        {
            // The Expedition scene (vertical slice) stays the first build scene once it exists; FeelTest follows it.
            const string expedition = "Assets/_Game/Scenes/Expedition.unity";
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            var updated = new List<EditorBuildSettingsScene>();
            if (File.Exists(expedition))
            {
                updated.Add(new EditorBuildSettingsScene(expedition, true));
            }

            updated.Add(new EditorBuildSettingsScene(FeelTestPaths.Scene, true));
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != FeelTestPaths.Scene && scenes[i].path != expedition)
                {
                    updated.Add(scenes[i]);
                }
            }

            EditorBuildSettings.scenes = updated.ToArray();

            // Swipe up from the bottom edge must jump, not leave the app; the home indicator hides during a run.
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.hideHomeButton = true;

            // The owner judges both orientations on the phone (ART_DIRECTION §9): autorotate between portrait and
            // both landscapes; the camera rig switches profiles. Upside-down portrait stays off (iPhone convention).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;

            // Batch mode doesn't write ProjectSettings.asset for these setters unless the singleton is dirty.
            Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (playerSettings != null)
            {
                EditorUtility.SetDirty(playerSettings);
            }
        }

        public static MovementConfigAssets EnsureMovementAssets()
        {
            return new MovementConfigAssets
            {
                Speed = EnsureAsset<RunSpeedConfigAsset>(FeelTestPaths.RunSpeed, null),
                Lateral = EnsureAsset<LateralMovementConfigAsset>(FeelTestPaths.Lateral, null),
                JumpSlide = EnsureAsset<JumpSlideConfigAsset>(FeelTestPaths.JumpSlide, null),
                Hitbox = EnsureAsset<HitboxConfigAsset>(FeelTestPaths.Hitbox, null),
                Health = EnsureAsset<HealthConfigAsset>(FeelTestPaths.Health, null),
                Flow = EnsureAsset<RunFlowConfigAsset>(FeelTestPaths.RunFlow, null),
            };
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/Config", "Movement");
            EnsureFolder("Assets/_Game/Art", "FeelTest");
            EnsureFolder("Assets/_Game", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static T EnsureAsset<T>(string path, System.Action<T> initialize) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log(LogPrefix + "Created " + path);
            return asset;
        }

        private static FeelTestPalette EnsurePalette()
        {
            string path = FeelTestPaths.MaterialFolder + "/FeelTestPalette.asset";
            FeelTestPalette palette = EnsureAsset<FeelTestPalette>(path, null);
            palette.Path = Lit("FT_Path", Hex(0xC9C6BE), 0.15f);
            palette.PathSafe = Lit("FT_PathSafe", Hex(0xA9C9A0), 0.15f);
            palette.PathRisky = Lit("FT_PathRisky", Hex(0xE3A766), 0.15f);
            palette.PathSide = Lit("FT_PathSide", Hex(0x6E6A63), 0.05f);
            palette.Ground = Lit("FT_Ground", Hex(0x3E5A34), 0.05f);
            palette.Hedge = Lit("FT_Hedge", Hex(0x4F7F3C), 0.1f);
            palette.Low = Lit("FT_Low", Hex(0xC8A26B), 0.2f);
            palette.High = Lit("FT_High", Hex(0x7A5230), 0.2f);
            palette.Blocker = Lit("FT_Blocker", Hex(0x4A4A4E), 0.25f);
            palette.Thorns = Lit("FT_Thorns", Hex(0x9E2238), 0.3f);
            palette.Coin = Emissive("FT_Coin", Hex(0xF2C230), 0.35f);
            palette.Divider = Lit("FT_Divider", Hex(0x55555B), 0.2f);
            palette.Finish = Emissive("FT_Finish", Hex(0xF4F1E8), 0.15f);
            palette.Runner = Lit("FT_Runner", Hex(0xD9783A), 0.35f);
            palette.RunnerAccent = Lit("FT_RunnerAccent", Hex(0x2F4F6F), 0.3f);
            palette.Marker = Lit("FT_Marker", Hex(0xEDE6D2), 0.1f);
            palette.Leaf = Particle("FT_Leaf", Color.white);
            palette.DebugHitbox = Transparent("FT_DebugHitbox", new Color(1f, 0.15f, 0.15f, 0.28f));
            palette.DebugRunner = Transparent("FT_DebugRunner", new Color(0.2f, 1f, 0.35f, 0.35f));
            palette.DebugTarget = Transparent("FT_DebugTarget", new Color(0.2f, 0.6f, 1f, 0.8f));
            EditorUtility.SetDirty(palette);
            return palette;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = FeelTestPaths.MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static Material Lit(string name, Color color, float smoothness)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Emissive(string name, Color color, float smoothness)
        {
            Material m = Lit(name, color, smoothness);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Particle(string name, Color color)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Particles/Simple Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Smoothness", 0.1f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Transparent(string name, Color color)
        {
            Material m = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void BuildScene(MovementConfigAssets movement, GestureConfigAsset gestures, CameraProfileAsset landscape, CameraProfileAsset portrait, FeelCourseAsset course, FeelTestPalette palette)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("FeelCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.70f, 1f);
            camera.fieldOfView = landscape.Values.FovBaseDeg;
            camera.nearClipPlane = landscape.Values.NearClip;
            camera.farClipPlane = landscape.Values.FarClip;
            cameraObject.AddComponent<AudioListener>();

            var sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.50f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 210f;
            RenderSettings.skybox = null;
            RenderSettings.sun = sun;

            var rootObject = new GameObject("FeelTestRoot");
            FeelTestRoot root = rootObject.AddComponent<FeelTestRoot>();
            root.Configure(movement, gestures, landscape, portrait, course, palette, camera);

            // Rigged Pista when the model is in the project; otherwise the gray-box capsule stays.
            if (File.Exists(PistaPaths.Model))
            {
                GameObject pista = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Prefab);
                if (pista == null)
                {
                    pista = PistaPrefabSetup.BuildAll();
                }

                root.SetAvatarPrefab(pista != null ? pista.GetComponent<RunnerAvatar>() : null);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FeelTestPaths.Scene);
        }
    }
}
