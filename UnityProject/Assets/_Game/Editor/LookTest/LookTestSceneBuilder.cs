using System.Collections.Generic;
using System.IO;
using System.Text;
using JungleBooze.App.LookTest;
using JungleBooze.Editor.Scenery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// JungleBooze > Look Test > Build Scene (ADR 0004; look test v2 per design/aurelia/ENVIRONMENT_STRATEGY.md,
    /// build-order steps 2 and 3): builds <see cref="ScenePath"/> from <see cref="LookTestConfigAsset"/>:
    /// URP-Realistic pipeline, low backlighting sun, HDRI sky (ambient and reflections baked from it, no lightmaps),
    /// the atmosphere (height fog with sun in-scatter, canopy light; <see cref="LookTestAtmosphere"/>), the graded
    /// post-processing volume, and the curved, climbing 225 m loop in 25 m segments. Every segment is merged into
    /// one mesh per budget layer and material (<see cref="LookTestBatchSet"/>), so draw calls are known before any
    /// art goes in. Placement is seeded (same config = same scene). Running it again replaces the scene and its
    /// generated meshes (Assets/_Game/Art/LookTest/Meshes is cleared first).
    /// </summary>
    public static class LookTestSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/LookTest.unity";
        public const string ConfigFolder = "Assets/_Game/Config/LookTest";
        public const string ConfigPath = ConfigFolder + "/LookTestConfig.asset";
        public const string VolumeProfilePath = ConfigFolder + "/LookTestVolume.asset";
        public const string LightingSettingsPath = ConfigFolder + "/LookTestLighting.lighting";
        public const string MeshFolder = "Assets/_Game/Art/LookTest/Meshes";

        private const string LogPrefix = "[JungleBooze look test] ";

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            LookTestConfigAsset config = EnsureConfig();
            var report = new List<string>();
            string batchSummary = string.Empty;

            try
            {
                EditorUtility.DisplayProgressBar("Look test", "Render pipeline", 0.05f);
                report.AddRange(LookTestPipelineSetup.ApplyRealistic(config));

                EditorUtility.DisplayProgressBar("Look test", "Materials", 0.15f);
                var assets = new LookTestAssets();
                var materials = new LookTestMaterials(config, assets);
                var layout = new LookTestStretchLayout(config);
                var batches = new LookTestBatchSet();
                var ctx = new LookTestBuildContext(config, layout, batches);
                Texture2D waterNormal = LookTestTextureGenerator.EnsureWaterNormal(config.Seed);
                Texture2D mistTexture = LookTestTextureGenerator.EnsureMist();
                Texture2D dapple = LookTestTextureGenerator.EnsureDapple(config.Seed);
                materials.WaterFx = LookTestTextureGenerator.EnsureWaterFx(config.Seed);
                Cubemap hdri = assets.FindHdri(config.SkyHdriId);

                ctx.Ground = materials.Ground();
                ctx.Rootstone = materials.Rootstone();
                ctx.Boulder = materials.Boulder();
                ctx.Bark = materials.Bark();
                ctx.Leaves = materials.Leaves();
                ctx.FarCanopy = materials.FarCanopy();
                ctx.River = materials.River(waterNormal);
                ctx.Waterfall = materials.Waterfall();
                ctx.Pool = materials.Pool(waterNormal);
                ctx.MistCard = materials.MistCard(mistTexture);
                ctx.LightShaft = materials.LightShaft();
                Material skyMaterial = materials.Sky(hdri);
                ctx.Ferns.AddRange(CollectVariants(assets, materials, new[] { config.FernModelId }));
                ctx.Plants.AddRange(CollectVariants(assets, materials, config.PlantModelIds));

                // Environment assets that have landed (ADR 0008): mapped to Nature Lit, placed at the hooks.
                ctx.Kit = EnvironmentKit.Scan();
                for (int i = 0; i < ctx.Kit.Pieces.Count; i++)
                {
                    ctx.Kit.Pieces[i].Material = materials.ForEnvironmentPiece(ctx.Kit.Pieces[i]);
                }

                for (int i = 0; i < ctx.Kit.Backdrops.Count; i++)
                {
                    ctx.BackdropMaterials.Add(materials.Backdrop(ctx.Kit.Backdrops[i]));
                }

                report.Add(ctx.Kit.Summary());

                EditorUtility.DisplayProgressBar("Look test", "Scene", 0.25f);
                ClearGeneratedMeshes();
                LookTestMeshAccumulator.ClearCache();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Light sun = CreateSun(config);
                ApplyEnvironment(config, skyMaterial, sun);
                Camera camera = CreateCamera(config);
                CreateVolume(config);
                var atmosphereObject = new GameObject("Atmosphere");
                LookTestAtmosphere atmosphere = atmosphereObject.AddComponent<LookTestAtmosphere>();
                atmosphere.Configure(config, dapple, sun);

                int count = config.SegmentCount;
                var stretch = new GameObject("Stretch");
                var segments = new Transform[count];
                var groups = new SegmentGroups[count];
                for (int i = 0; i < count; i++)
                {
                    var segment = new GameObject("Segment_" + i).transform;
                    segment.SetParent(stretch.transform, false);
                    segments[i] = segment;
                    groups[i] = new SegmentGroups(segment);
                }

                var backdropRoot = new GameObject("Backdrop").transform;
                backdropRoot.SetParent(stretch.transform, false);
                var backdropGroups = new SegmentGroups(backdropRoot);

                for (int i = 0; i < count; i++)
                {
                    EditorUtility.DisplayProgressBar("Look test", "Segment " + i, 0.3f + 0.4f * i / count);
                    LookTestScatter.BuildSegment(ctx, i);
                }

                EditorUtility.DisplayProgressBar("Look test", "Landmarks and backdrop", 0.72f);
                LookTestLandmarks.Build(ctx);
                LookTestLandmarks.Backdrop(ctx);

                EditorUtility.DisplayProgressBar("Look test", "Merging meshes", 0.78f);
                batchSummary = batches.Emit(SaveMesh, (segment, group) => (segment < 0 ? backdropGroups : groups[segment]).For(group));

                var rootObject = new GameObject("LookTestRoot");
                LookTestRoot root = rootObject.AddComponent<LookTestRoot>();
                var plantGroups = new GameObject[count];
                var detailGroups = new GameObject[count];
                var waterGroups = new GameObject[count + 1];
                for (int i = 0; i < count; i++)
                {
                    plantGroups[i] = groups[i].Plants.gameObject;
                    detailGroups[i] = groups[i].Detail.gameObject;
                    waterGroups[i] = groups[i].Water.gameObject;
                }

                waterGroups[count] = backdropGroups.Water.gameObject;
                root.Configure(config, camera, sun, stretch.transform, segments, plantGroups, waterGroups, backdropRoot, detailGroups);
                EditorUtility.SetDirty(root);

                // Place the world for the first frame (the editor view and screenshots start here).
                LookTestPath path = layout.Path;
                LookTestWorldView view = stretch.AddComponent<LookTestWorldView>();
                view.Init(segments, config.LoopLengthM, path.LoopOffset, config.RecycleBehindM, backdropRoot);
                view.InitDetail(detailGroups, config.DetailRangeM);
                view.Render(0.0);
                LookTestCameraRig.Pose(path, config.LandscapeCamera, 0.0, 0f, out Vector3 camPos, out Quaternion camRot);
                camera.transform.SetPositionAndRotation(camPos, camRot);

                LookTestAssets.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    report.Add("ERROR: could not save " + ScenePath + ".");
                }

                EditorUtility.DisplayProgressBar("Look test", "Baking sky lighting (no lightmaps)", 0.85f);
                // Bake the ambient from the plain sky: the horizon haze is a view effect, not a light source.
                skyMaterial.SetFloat("_HorizonFog", 0f);
                BakeEnvironment(config, hdri != null, report);
                skyMaterial.SetFloat("_HorizonFog", 1f);
                EditorUtility.SetDirty(skyMaterial);
                if (float.IsNaN(RenderSettings.ambientProbe[0, 0]))
                {
                    report.Add("ERROR: the baked ambient light is not a number (NaN): every lit surface renders black.");
                }

                atmosphere.Apply();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();

                report.Add("Loop offset per lap: " + path.LoopOffset.ToString("F1") + " m. River fall at s = " + layout.RiverFallS().ToString("F1") + " m.");
                if (assets.Missing.Count > 0)
                {
                    report.Add("Missing CC0 assets (plain stand-in materials used). Run: python3 tools/assets/fetch_cc0.py");
                    for (int i = 0; i < assets.Missing.Count; i++)
                    {
                        report.Add("  - " + assets.Missing[i]);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Report(report, batchSummary);
        }

        public static LookTestConfigAsset EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<LookTestConfigAsset>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            LookTestAssets.EnsureFolder(ConfigFolder);
            config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        // ---------------------------------------------------------------- Light, sky, camera, post

        internal static Light CreateSun(LookTestConfigAsset c)
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.LookRotation(c.SunLightDirection, Vector3.up);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = c.SunColor;
            light.intensity = c.SunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.92f;
            return light;
        }

        internal static void ApplyEnvironment(LookTestConfigAsset c, Material sky, Light sun)
        {
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientIntensity = c.AmbientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.reflectionIntensity = 1f;
            // The project's own height fog (JBAtmosphere.hlsl) replaces Unity fog.
            RenderSettings.fog = false;
            UseTrilightAmbient(c);
        }

        private static void UseTrilightAmbient(LookTestConfigAsset c)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = c.FallbackSkyAmbient;
            RenderSettings.ambientEquatorColor = c.FallbackEquatorAmbient;
            RenderSettings.ambientGroundColor = c.FallbackGroundAmbient;
        }

        private static void BakeEnvironment(LookTestConfigAsset c, bool haveHdri, List<string> report)
        {
            BakeEnvironment(c, haveHdri, report, LightingSettingsPath);
        }

        /// <summary>Bakes ambient light and reflections from the sky (no lightmaps), lighting settings saved at <paramref name="settingsPath"/>.</summary>
        internal static void BakeEnvironment(LookTestConfigAsset c, bool haveHdri, List<string> report, string settingsPath)
        {
            if (!haveHdri || !c.BakeEnvironmentLighting)
            {
                report.Add("Ambient light: flat gradient (no HDRI or bake turned off in the config).");
                return;
            }

            var settings = new LightingSettings { name = "LookTestLighting", bakedGI = false, realtimeGI = false };
            settings = LookTestAssets.SaveOrReplace(settings, settingsPath);
            Lightmapping.lightingSettings = settings;
            RenderSettings.ambientMode = AmbientMode.Skybox;

            if (Lightmapping.Bake())
            {
                report.Add("Ambient light and reflections baked from the HDRI sky (no lightmaps).");
            }
            else
            {
                UseTrilightAmbient(c);
                report.Add("WARNING: sky lighting bake failed; using the flat gradient ambient. Try Window > Rendering > Lighting > Generate Lighting.");
            }
        }

        internal static Camera CreateCamera(LookTestConfigAsset c)
        {
            var go = new GameObject("LookTestCamera");
            go.tag = "MainCamera";
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = c.LandscapeCamera.VerticalFovDeg;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = c.CameraFarClipM;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            go.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            return camera;
        }

        /// <summary>
        /// Post-processing: ACES, exposure, contrast, saturation, white balance, split toning and shadows/midtones/
        /// highlights. URP bakes all grading into one 32³ LUT per frame (one lookup in the final pass), so this is the
        /// grading LUT of strategy 4.4; a keyframe-matched external LUT (ColorLookup) replaces the hand values once
        /// the owner approves keyframes.
        /// </summary>
        private static void CreateVolume(LookTestConfigAsset c)
        {
            CreateVolume(c, VolumeProfilePath);
        }

        /// <summary>The graded post-processing volume, profile saved at <paramref name="profilePath"/>.</summary>
        internal static void CreateVolume(LookTestConfigAsset c, string profilePath)
        {
            LookTestAssets.EnsureFolder(Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            VolumeProfile profile = LookTestAssets.SaveOrReplace(ScriptableObject.CreateInstance<VolumeProfile>(), profilePath);

            Tonemapping tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(c.AcesTonemapping ? TonemappingMode.ACES : TonemappingMode.Neutral);

            ColorAdjustments grading = AddComponent<ColorAdjustments>(profile);
            grading.postExposure.Override(c.PostExposure);
            grading.contrast.Override(c.Contrast);
            grading.saturation.Override(c.Saturation);

            WhiteBalance whiteBalance = AddComponent<WhiteBalance>(profile);
            whiteBalance.temperature.Override(c.WhiteBalanceTemperature);
            whiteBalance.tint.Override(c.WhiteBalanceTint);

            SplitToning split = AddComponent<SplitToning>(profile);
            split.shadows.Override(c.SplitShadows);
            split.highlights.Override(c.SplitHighlights);
            split.balance.Override(c.SplitBalance);

            ShadowsMidtonesHighlights smh = AddComponent<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(c.GradeShadows);
            smh.midtones.Override(c.GradeMidtones);
            smh.highlights.Override(c.GradeHighlights);

            Bloom bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(c.BloomThreshold);
            bloom.intensity.Override(c.BloomIntensity);
            bloom.scatter.Override(c.BloomScatter);
            bloom.tint.Override(c.BloomTint);
            bloom.highQualityFiltering.Override(false);

            Vignette vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(c.VignetteIntensity);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProcessing");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        private static T AddComponent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ---------------------------------------------------------------- Model variants (CC0 FBX)

        /// <summary>Every mesh inside the given models becomes a variant, with its pose inside the model file.</summary>
        private static List<LookTestBuildContext.ModelVariant> CollectVariants(LookTestAssets assets, LookTestMaterials materials, string[] ids)
        {
            var result = new List<LookTestBuildContext.ModelVariant>();
            if (ids == null)
            {
                return result;
            }

            for (int m = 0; m < ids.Length; m++)
            {
                GameObject model = assets.FindModel(ids[m]);
                if (model == null)
                {
                    continue;
                }

                Material material = materials.ForModel(ids[m], true);
                MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
                Matrix4x4 toRoot = model.transform.worldToLocalMatrix;
                for (int f = 0; f < filters.Length; f++)
                {
                    Mesh mesh = filters[f].sharedMesh;
                    if (mesh == null || IsLowerLod(filters[f].name))
                    {
                        continue;
                    }

                    Matrix4x4 matrix = toRoot * filters[f].transform.localToWorldMatrix;
                    Bounds bounds = TransformBounds(matrix, mesh.bounds);
                    if (bounds.size.y <= 0.0001f)
                    {
                        continue;
                    }

                    result.Add(new LookTestBuildContext.ModelVariant
                    {
                        Name = ids[m] + "/" + filters[f].name,
                        Mesh = mesh,
                        Material = material,
                        Matrix = matrix,
                        Bounds = bounds,
                    });
                }
            }

            return result;
        }

        /// <summary>Scans that ship their own LOD chain (name_LOD1..3): only LOD0 is a variant.</summary>
        internal static bool IsLowerLod(string meshName)
        {
            int index = meshName.LastIndexOf("_LOD", System.StringComparison.OrdinalIgnoreCase);
            return index >= 0 && index + 4 < meshName.Length && meshName.Substring(index + 4) != "0";
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var result = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }

            return result;
        }

        // ---------------------------------------------------------------- Helpers

        private static void ClearGeneratedMeshes()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder });
            var paths = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            var failed = new List<string>();
            AssetDatabase.DeleteAssets(paths.ToArray(), failed);
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            return LookTestAssets.SaveOrReplace(mesh, MeshFolder + "/" + mesh.name + ".asset");
        }

        private static void Report(List<string> report, string batchSummary)
        {
            var text = new StringBuilder();
            text.AppendLine("Look test scene built: " + ScenePath);
            text.Append(batchSummary);
            for (int i = 0; i < report.Count; i++)
            {
                text.AppendLine(report[i]);
            }

            text.AppendLine("Press Play to run through it. Shots and per-view budgets: LookTestBatch.CaptureShots.");
            string summary = text.ToString();
            Debug.Log(LogPrefix + summary);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Look test", summary, "OK");
            }
        }

        /// <summary>Child groups of one segment (toggled separately by the quality panel).</summary>
        private sealed class SegmentGroups
        {
            public SegmentGroups(Transform segment)
            {
                Ground = Child(segment, "Ground");
                Water = Child(segment, "Water");
                Trees = Child(segment, "Trees");
                Rocks = Child(segment, "Rocks");
                Plants = Child(segment, "Plants");
                Detail = Child(segment, "Detail");
            }

            public Transform Ground { get; }

            public Transform Water { get; }

            public Transform Trees { get; }

            public Transform Rocks { get; }

            public Transform Plants { get; }

            public Transform Detail { get; }

            public Transform For(LookTestBatchSet.Group group)
            {
                switch (group)
                {
                    case LookTestBatchSet.Group.Water:
                        return Water;
                    case LookTestBatchSet.Group.Trees:
                        return Trees;
                    case LookTestBatchSet.Group.Rocks:
                        return Rocks;
                    case LookTestBatchSet.Group.Plants:
                        return Plants;
                    case LookTestBatchSet.Group.Detail:
                        return Detail;
                    default:
                        return Ground;
                }
            }

            private static Transform Child(Transform parent, string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                return t;
            }
        }
    }
}
