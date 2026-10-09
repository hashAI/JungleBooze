using System.Collections.Generic;
using System.IO;
using System.Text;
using JungleBooze.App.Expedition;
using JungleBooze.Editor.Characters;
using JungleBooze.Editor.FeelTest;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Views;
using JungleBooze.Gameplay.Views.World;
using JungleBooze.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JungleBooze.Editor.Expedition
{
    /// <summary>
    /// Creates the vertical-slice content and scene (spec 102–103 Part A): the 15 chunk assets, the catalog, the
    /// Expedition 1 script, director/pickup/results tuning (created only if missing, so tuning is never overwritten),
    /// journal entries, abilities, the content bundle, the offline validation (writes each chunk's validated mask
    /// and a report to Logs/ChunkValidation.txt), gray-box materials, the Expedition scene (camera, sun,
    /// <see cref="ExpeditionRoot"/>, real Pista) and the build settings (Expedition first, FeelTest kept).
    /// </summary>
    public static class ExpeditionSetup
    {
        private const string LogPrefix = "[JungleBooze expedition] ";

        [MenuItem("JungleBooze/Expedition/Build Scene", false, 1)]
        public static void BuildFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildAll(false);
        }

        [MenuItem("JungleBooze/Expedition/Reset Chunks To Spec Layouts", false, 20)]
        public static void ResetFromMenu()
        {
            BuildAll(true);
        }

        [MenuItem("JungleBooze/Expedition/Validate Chunks", false, 21)]
        public static void ValidateFromMenu()
        {
            ExpeditionContentAsset content = AssetDatabase.LoadAssetAtPath<ExpeditionContentAsset>(ExpeditionPaths.Content);
            if (content == null)
            {
                BuildAll(false);
                return;
            }

            Validate(content);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("JungleBooze/Expedition/Open Scene", false, 2)]
        public static void OpenScene()
        {
            if (!File.Exists(ExpeditionPaths.Scene))
            {
                BuildAll(false);
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ExpeditionPaths.Scene, OpenSceneMode.Single);
            }
        }

        /// <summary>Everything; <paramref name="resetLayouts"/> rewrites chunk layouts, script, journal and abilities from code.</summary>
        public static CatalogValidation.Result BuildAll(bool resetLayouts)
        {
            if (AssetDatabase.LoadAssetAtPath<HealthConfigAsset>(FeelTestPaths.Health) == null || AssetDatabase.LoadAssetAtPath<GestureConfigAsset>(FeelTestPaths.Gesture) == null)
            {
                FeelTestSetup.BuildAll();
            }

            EnsureFolders();
            ExpeditionContentAsset content = EnsureContent(resetLayouts);
            WorldPalette palette = EnsurePalette();
            AssetDatabase.SaveAssets();

            CatalogValidation.Result validation = Validate(content);
            AssetDatabase.SaveAssets();

            BuildScene(content, palette);
            UseExpeditionBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Expedition ready: " + ExpeditionPaths.Scene + " (validation failures: " + validation.Failures + ")");
            return validation;
        }

        public static CatalogValidation.Result Validate(ExpeditionContentAsset content)
        {
            ExpeditionContent built = content.Build();
            var movement = FeelTestSetup.EnsureMovementAssets().Build();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            CatalogValidation.Result result = CatalogValidation.Run(built.Library, built.Script, movement, built.Director);
            timer.Stop();

            // The built library shares the asset's definition objects: write the masks there and save.
            IReadOnlyList<ChunkDefinitionAsset> chunks = content.Catalog.Chunks;
            for (int i = 0; i < chunks.Count; i++)
            {
                int d = built.Library.FindDefinition(chunks[i].Definition.Id);
                chunks[i].Definition.ValidatedMask = d >= 0 ? result.Masks[d] : 0;
                EditorUtility.SetDirty(chunks[i]);
            }

            var report = new StringBuilder();
            report.AppendLine("Chunk validation (spec 102 §4) — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ", " + result.BotRuns + " bot runs, " + timer.Elapsed.TotalSeconds.ToString("0.0") + " s");
            report.AppendLine("Checked: V1 (Perfect bot, every open route, speed extremes / script speeds ±0.5 m/s and 0.8×), V2, V4, V5, V6, V8, V9, V10, V12. Not checked: V3 margin, V7 visibility, V11 (director), V13/V14, W1–W5 (Part B).");
            for (int i = 0; i < result.Reports.Count; i++)
            {
                report.AppendLine(result.Reports[i].ToString());
            }

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "ChunkValidation.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString());
            if (result.Failures > 0)
            {
                Debug.LogWarning(LogPrefix + "Chunk validation found " + result.Failures + " failing checks; see " + path + "\n" + report);
            }
            else
            {
                Debug.Log(LogPrefix + "Chunk validation passed (" + result.Reports.Count + " checks, " + result.BotRuns + " bot runs); report " + path);
            }

            return result;
        }

        [MenuItem("JungleBooze/Expedition/Use Expedition Build Settings", false, 22)]
        public static void UseExpeditionBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            var updated = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ExpeditionPaths.Scene, true) };
            bool hasFeel = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ExpeditionPaths.Scene)
                {
                    continue;
                }

                hasFeel |= scenes[i].path == FeelTestPaths.Scene;
                updated.Add(scenes[i]);
            }

            if (!hasFeel && File.Exists(FeelTestPaths.Scene))
            {
                updated.Insert(1, new EditorBuildSettingsScene(FeelTestPaths.Scene, true));
            }

            EditorBuildSettings.scenes = updated.ToArray();
        }

        private static void EnsureFolders()
        {
            EnsureFolder(ExpeditionPaths.ConfigRoot, "Chunks");
            EnsureFolder(ExpeditionPaths.ConfigRoot, "World");
            EnsureFolder(ExpeditionPaths.ConfigRoot, "Rewards");
            EnsureFolder(ExpeditionPaths.ConfigRoot, "Discovery");
            EnsureFolder(ExpeditionPaths.ConfigRoot, "Progression");
            EnsureFolder(ExpeditionPaths.ConfigRoot, "UI");
            EnsureFolder("Assets/_Game/Art", "Expedition");
            EnsureFolder("Assets/_Game", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static T Ensure<T>(string path, System.Action<T> initialize, bool reset) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                if (reset && initialize != null)
                {
                    initialize(asset);
                    EditorUtility.SetDirty(asset);
                }

                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log(LogPrefix + "Created " + path);
            return asset;
        }

        private static ExpeditionContentAsset EnsureContent(bool reset)
        {
            var chunkAssets = new List<ChunkDefinitionAsset>();
            foreach (ChunkDefinition def in SliceChunkLayouts.CreateChunks())
            {
                ChunkDefinition layout = def;
                chunkAssets.Add(Ensure<ChunkDefinitionAsset>(ExpeditionPaths.Chunk(def.Id), a => a.SetDefinition(layout), reset));
            }

            ChunkCatalogAsset catalog = Ensure<ChunkCatalogAsset>(ExpeditionPaths.Catalog, null, false);
            catalog.SetChunks(chunkAssets);
            EditorUtility.SetDirty(catalog);

            ExpeditionScriptAsset script = Ensure<ExpeditionScriptAsset>(ExpeditionPaths.Script, a => a.SetValues(SliceChunkLayouts.CreateScript()), reset);
            WorldDirectorConfigAsset director = Ensure<WorldDirectorConfigAsset>(ExpeditionPaths.Director, a => a.SetValues(new WorldDirectorConfig()), false);
            PickupConfigAsset pickups = Ensure<PickupConfigAsset>(ExpeditionPaths.Pickups, a => a.SetValues(new PickupConfig()), false);
            ResultsConfigAsset results = Ensure<ResultsConfigAsset>(ExpeditionPaths.Results, a => a.SetValues(new ResultsConfig()), false);

            var discoveries = new List<DiscoveryEntryAsset>();
            foreach (DiscoveryEntry entry in SliceChunkLayouts.CreateDiscoveries())
            {
                DiscoveryEntry e = entry;
                discoveries.Add(Ensure<DiscoveryEntryAsset>(ExpeditionPaths.Discovery(entry.Id), a => a.SetValues(e), reset));
            }

            var abilities = new List<AbilityDefinitionAsset>();
            foreach (AbilityDefinition ability in SliceChunkLayouts.CreateAbilities())
            {
                AbilityDefinition a0 = ability;
                abilities.Add(Ensure<AbilityDefinitionAsset>(ExpeditionPaths.Ability(ability.Name), a => a.SetValues(a0), reset));
            }

            ExpeditionContentAsset content = Ensure<ExpeditionContentAsset>(ExpeditionPaths.Content, null, false);
            content.Catalog = catalog;
            content.Script = script;
            content.Director = director;
            content.Pickups = pickups;
            content.Results = results;
            content.Discoveries = discoveries;
            content.Abilities = abilities;
            EditorUtility.SetDirty(content);
            return content;
        }

        private static WorldPalette EnsurePalette()
        {
            WorldPalette p = Ensure<WorldPalette>(ExpeditionPaths.Palette, null, false);
            p.Path = Lit("EX_Path", Hex(0xC9C6BE), 0.15f);
            p.PathSafe = Lit("EX_PathSafe", Hex(0xA9C9A0), 0.15f);
            p.PathRisky = Lit("EX_PathRisky", Hex(0xE3A766), 0.15f);
            p.PathSecret = Lit("EX_PathSecret", Hex(0xA48BD6), 0.2f);
            p.PathSide = Lit("EX_PathSide", Hex(0x6E6A63), 0.05f);
            p.Hedge = Lit("EX_Hedge", Hex(0x4F7F3C), 0.1f);
            p.Low = Lit("EX_Low", Hex(0xC8A26B), 0.2f);
            p.High = Lit("EX_High", Hex(0x7A5230), 0.2f);
            p.Blocker = Lit("EX_Blocker", Hex(0x4A4A4E), 0.25f);
            p.Thorns = Lit("EX_Thorns", Hex(0x9E2238), 0.3f);
            p.Divider = Lit("EX_Divider", Hex(0x55555B), 0.2f);
            p.WaterPlaceholder = Lit("EX_WaterPlaceholder", Hex(0x3C8FC4), 0.6f);
            p.CanopyPlaceholder = Lit("EX_CanopyPlaceholder", Hex(0x8A6A44), 0.15f);
            p.ShallowWater = Lit("EX_ShallowWater", Hex(0x8FC3C6), 0.55f);
            p.Curtain = Transparent("EX_Curtain", new Color(0.75f, 0.9f, 1f, 0.45f));
            p.Marker = Lit("EX_Marker", Hex(0xEDE6D2), 0.1f);
            p.Ground = Lit("EX_Ground", Hex(0x3E5A34), 0.05f);
            p.Coin = Emissive("EX_Coin", Hex(0xF2C230), 0.35f);
            p.Crystal = Emissive("EX_Crystal", Hex(0x48E0F0), 0.8f);
            p.Shield = Transparent("EX_Shield", new Color(0.35f, 0.75f, 1f, 0.6f));
            p.Discovery = Emissive("EX_Discovery", Hex(0xB98CFF), 0.3f);
            p.Runner = Lit("EX_Runner", Hex(0xD9783A), 0.35f);
            p.RunnerAccent = Lit("EX_RunnerAccent", Hex(0x2F4F6F), 0.3f);
            p.Leaf = Particle("EX_Leaf", Color.white);
            EditorUtility.SetDirty(p);
            return p;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = ExpeditionPaths.MaterialFolder + "/" + name + ".mat";
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

        private static void BuildScene(ExpeditionContentAsset content, WorldPalette palette)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var landscape = AssetDatabase.LoadAssetAtPath<CameraProfileAsset>(FeelTestPaths.CameraLandscape);
            var portrait = AssetDatabase.LoadAssetAtPath<CameraProfileAsset>(FeelTestPaths.CameraPortrait);
            var gestures = AssetDatabase.LoadAssetAtPath<GestureConfigAsset>(FeelTestPaths.Gesture);

            var cameraObject = new GameObject("ExpeditionCamera") { tag = "MainCamera" };
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

            var rootObject = new GameObject("ExpeditionRoot");
            ExpeditionRoot root = rootObject.AddComponent<ExpeditionRoot>();
            root.Configure(FeelTestSetup.EnsureMovementAssets(), gestures, landscape, portrait, content, palette, camera);
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
            EditorSceneManager.SaveScene(scene, ExpeditionPaths.Scene);
        }
    }
}
