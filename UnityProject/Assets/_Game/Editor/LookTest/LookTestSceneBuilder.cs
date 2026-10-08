using System.Collections.Generic;
using System.IO;
using System.Text;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// JungleBooze > Look Test > Build Scene (ADR 0004): builds <see cref="ScenePath"/> from the CC0 assets and
    /// <see cref="LookTestConfigAsset"/>:
    /// URP-Realistic pipeline, HDRI sky (ambient and reflections baked from it, no lightmaps), sun with soft
    /// shadows, exponential fog, a global post-processing volume (ACES, grading, bloom, vignette), and a looping
    /// ~200 m stretch in segments: ground with blended path / forest floor / river pebbles, a river with scrolling
    /// normals, a cliff with a waterfall and mist, procedural trees with leaf-card crowns, CC0 ferns, plants and
    /// rocks, procedural boulders. Placement is seeded (same config = same scene). The LookTestRoot component
    /// runs Pista (stand-in) through it at Play. Running it again replaces the scene and its generated assets.
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
        private const int TrunkVariants = 6;
        private const int BoulderVariants = 6;
        private const float TrunkReferenceHeightM = 24f;

        // Random stream ids for scatter (editor only; fixed so a config always gives the same scene).
        private const ulong TreeStream = 11UL;
        private const ulong RockStream = 12UL;
        private const ulong FernStream = 13UL;
        private const ulong PlantStream = 14UL;
        private const ulong VariantStream = 15UL;

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            LookTestConfigAsset config = EnsureConfig();
            var report = new List<string>();
            var tris = new SortedDictionary<string, long>();

            try
            {
                EditorUtility.DisplayProgressBar("Look test", "Render pipeline", 0.05f);
                report.AddRange(LookTestPipelineSetup.ApplyRealistic(config));

                EditorUtility.DisplayProgressBar("Look test", "Materials", 0.15f);
                var assets = new LookTestAssets();
                var materials = new LookTestMaterials(config, assets);
                var shape = new LookTestTerrainShape(config);
                Texture2D waterNormal = LookTestTextureGenerator.EnsureWaterNormal(config.Seed);
                Texture2D mistTexture = LookTestTextureGenerator.EnsureMist();
                Cubemap hdri = assets.FindHdri(config.SkyHdriId);

                Material groundMaterial = materials.Ground();
                Material cliffMaterial = materials.Cliff();
                Material boulderMaterial = materials.Boulder();
                Material barkMaterial = materials.Bark();
                Material leafMaterial = materials.Leaves();
                Material riverMaterial = materials.River(waterNormal);
                Material waterfallMaterial = materials.Waterfall(waterNormal);
                Material mistMaterial = materials.Mist(mistTexture);
                Material skyMaterial = materials.Sky(hdri);

                EditorUtility.DisplayProgressBar("Look test", "Scene", 0.3f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Light sun = CreateSun(config);
                ApplyEnvironment(config, skyMaterial, sun);
                Camera camera = CreateCamera(config);
                CreateVolume(config);

                // Stretch segments.
                int count = config.SegmentCount;
                float segLength = config.LoopLengthM / count;
                var stretch = new GameObject("Stretch");
                var segments = new Transform[count];
                var groups = new SegmentGroups[count];
                for (int i = 0; i < count; i++)
                {
                    var segment = new GameObject("Segment_" + i).transform;
                    segment.SetParent(stretch.transform, false);
                    segment.localPosition = new Vector3(0f, 0f, i * segLength);
                    segments[i] = segment;
                    groups[i] = new SegmentGroups(segment);
                }

                EditorUtility.DisplayProgressBar("Look test", "Ground, river, cliff", 0.4f);
                for (int i = 0; i < count; i++)
                {
                    Mesh ground = SaveMesh(LookTestMeshFactory.Ground(shape, config, i));
                    AddRenderer(groups[i].Ground, "Ground", ground, groundMaterial, ShadowCastingMode.Off);
                    Add(tris, "Ground", LookTestMeshFactory.TriangleCount(ground));

                    Mesh river = SaveMesh(LookTestMeshFactory.River(shape, config, i));
                    AddRenderer(groups[i].Water, "River", river, riverMaterial, ShadowCastingMode.Off);
                    Add(tris, "Water", LookTestMeshFactory.TriangleCount(river));

                    Mesh cliff = LookTestMeshFactory.Cliff(shape, config, i);
                    if (cliff != null)
                    {
                        cliff = SaveMesh(cliff);
                        AddRenderer(groups[i].Ground, "Cliff", cliff, cliffMaterial, ShadowCastingMode.On);
                        Add(tris, "Cliff", LookTestMeshFactory.TriangleCount(cliff));
                    }
                }

                BuildWaterfall(config, shape, groups, segLength, waterfallMaterial, mistMaterial, tris);

                EditorUtility.DisplayProgressBar("Look test", "Trees", 0.55f);
                ScatterTrees(config, shape, groups, segLength, barkMaterial, leafMaterial, tris);

                EditorUtility.DisplayProgressBar("Look test", "Rocks", 0.65f);
                ScatterRocks(config, shape, groups, segLength, assets, materials, boulderMaterial, tris);

                EditorUtility.DisplayProgressBar("Look test", "Plants", 0.75f);
                ScatterPlants(config, shape, groups, segLength, assets, materials, tris);

                // Composition root.
                var rootObject = new GameObject("LookTestRoot");
                LookTestRoot root = rootObject.AddComponent<LookTestRoot>();
                var plantGroups = new GameObject[count];
                var waterGroups = new GameObject[count];
                for (int i = 0; i < count; i++)
                {
                    plantGroups[i] = groups[i].Plants.gameObject;
                    waterGroups[i] = groups[i].Water.gameObject;
                }

                root.Configure(config, camera, sun, stretch.transform, segments, plantGroups, waterGroups);
                EditorUtility.SetDirty(root);

                LookTestAssets.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    report.Add("ERROR: could not save " + ScenePath + ".");
                }

                EditorUtility.DisplayProgressBar("Look test", "Baking sky lighting (no lightmaps)", 0.85f);
                BakeEnvironment(config, hdri != null, report);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();

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

            Report(report, tris, config);
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

        private static Light CreateSun(LookTestConfigAsset c)
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(c.SunPitchDeg, c.SunYawDeg, 0f);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = c.SunColor;
            light.intensity = c.SunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            return light;
        }

        private static void ApplyEnvironment(LookTestConfigAsset c, Material sky, Light sun)
        {
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientIntensity = c.AmbientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = c.FogColor;
            RenderSettings.fogDensity = c.FogDensity;

            // Gradient ambient until BakeEnvironment switches to the baked sky.
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
            if (!haveHdri || !c.BakeEnvironmentLighting)
            {
                report.Add("Ambient light: flat gradient (no HDRI or bake turned off in the config).");
                return;
            }

            var settings = new LightingSettings { name = "LookTestLighting", bakedGI = false, realtimeGI = false };
            settings = LookTestAssets.SaveOrReplace(settings, LightingSettingsPath);
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

        private static Camera CreateCamera(LookTestConfigAsset c)
        {
            var go = new GameObject("LookTestCamera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, c.CameraOffsetUpM, -c.CameraOffsetBehindM);
            go.transform.LookAt(new Vector3(0f, c.CameraLookAtHeightM, c.CameraLookAheadM));
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = c.CameraFovDeg;
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

        private static void CreateVolume(LookTestConfigAsset c)
        {
            LookTestAssets.EnsureFolder(ConfigFolder);
            VolumeProfile profile = LookTestAssets.SaveOrReplace(ScriptableObject.CreateInstance<VolumeProfile>(), VolumeProfilePath);

            Tonemapping tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(c.AcesTonemapping ? TonemappingMode.ACES : TonemappingMode.Neutral);

            ColorAdjustments grading = AddComponent<ColorAdjustments>(profile);
            grading.postExposure.Override(c.PostExposure);
            grading.contrast.Override(c.Contrast);
            grading.saturation.Override(c.Saturation);

            WhiteBalance whiteBalance = AddComponent<WhiteBalance>(profile);
            whiteBalance.temperature.Override(c.WhiteBalanceTemperature);

            Bloom bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(c.BloomThreshold);
            bloom.intensity.Override(c.BloomIntensity);
            bloom.scatter.Override(c.BloomScatter);
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

        // ---------------------------------------------------------------- Waterfall

        private static void BuildWaterfall(
            LookTestConfigAsset c, LookTestTerrainShape shape, SegmentGroups[] groups, float segLength,
            Material waterfallMaterial, Material mistMaterial, SortedDictionary<string, long> tris)
        {
            float wz = Mathf.Repeat(c.WaterfallZM, c.LoopLengthM);
            if (shape.CliffPresence(wz) < 0.5f)
            {
                return;
            }

            int segment = Mathf.Clamp(Mathf.FloorToInt(wz / segLength), 0, groups.Length - 1);
            float localZ = wz - segment * segLength;
            float footX = shape.CliffFootX(wz) - 0.4f;
            float footY = c.WaterLevelM - 0.1f;
            Vector3 top = LookTestMeshFactory.CliffTop(shape, c, wz);

            Mesh sheet = SaveMesh(LookTestMeshFactory.Waterfall(c.WaterfallWidthM, top.y - footY, top.x - footX + 0.3f));
            Transform fall = AddRenderer(groups[segment].Water, "Waterfall", sheet, waterfallMaterial, ShadowCastingMode.Off);
            fall.localPosition = new Vector3(footX, footY, localZ);
            Add(tris, "Water", LookTestMeshFactory.TriangleCount(sheet));

            if (c.MistParticles <= 0)
            {
                return;
            }

            var mistObject = new GameObject("Mist");
            mistObject.transform.SetParent(groups[segment].Water, false);
            mistObject.transform.localPosition = new Vector3(footX - 0.8f, c.WaterLevelM + 0.4f, localZ);
            ParticleSystem particles = mistObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.maxParticles = c.MistParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = -0.03f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = c.MistParticles / 3f;

            ParticleSystem.ShapeModule emitter = particles.shape;
            emitter.shapeType = ParticleSystemShapeType.Box;
            emitter.scale = new Vector3(1.5f, 0.4f, c.WaterfallWidthM);

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer particleRenderer = mistObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = mistMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particles.Play();
        }

        // ---------------------------------------------------------------- Trees

        private static void ScatterTrees(
            LookTestConfigAsset c, LookTestTerrainShape shape, SegmentGroups[] groups, float segLength,
            Material bark, Material leaves, SortedDictionary<string, long> tris)
        {
            var variantRng = new Pcg32Random((ulong)(uint)c.Seed, VariantStream);
            var trunks = new Mesh[TrunkVariants];
            var crowns = new Mesh[TrunkVariants];
            for (int v = 0; v < TrunkVariants; v++)
            {
                float radius = Mathf.Lerp(c.TrunkRadiusMinM, c.TrunkRadiusMaxM, (v + 0.5f) / TrunkVariants);
                Mesh trunk = LookTestMeshFactory.Trunk(variantRng, TrunkReferenceHeightM, radius, c.BarkTileM);
                trunk.name = "Trunk_" + v;
                trunks[v] = SaveMesh(trunk);
                Mesh crown = LookTestMeshFactory.Crown(variantRng, TrunkReferenceHeightM, c.CanopyCardsPerTree, c.CanopyCardSizeM);
                crown.name = "Crown_" + v;
                crowns[v] = SaveMesh(crown);
            }

            var rng = new Pcg32Random((ulong)(uint)c.Seed, TreeStream);
            int placed = 0;
            int attempts = 0;
            while (placed < c.TreeCount && attempts < c.TreeCount * 20)
            {
                attempts++;
                float z = rng.NextFloat(0f, c.LoopLengthM);
                bool left = rng.Chance(0.6f);
                Vector3 position;
                if (left)
                {
                    float x = -c.PathHalfWidthM - 2.5f - 40f * rng.NextFloat() * rng.NextFloat();
                    position = new Vector3(x, shape.Height(x, z) - 0.3f, z);
                }
                else if (shape.CliffPresence(z) > 0.35f)
                {
                    if (shape.WaterfallBump(z, 10f) > 0.2f)
                    {
                        continue;
                    }

                    Vector3 back = LookTestMeshFactory.CliffBack(shape, c, z);
                    position = new Vector3(back.x + rng.NextFloat(0.5f, 14f), back.y - 0.4f, z);
                }
                else
                {
                    float minX = shape.RiverCenterX(z) + c.RiverHalfWidthM * 1.6f;
                    float x = minX + 30f * rng.NextFloat() * rng.NextFloat();
                    position = new Vector3(x, shape.Height(x, z) - 0.3f, z);
                }

                float height = rng.NextFloat(c.TrunkHeightMinM, c.TrunkHeightMaxM);
                int variant = rng.NextInt(0, TrunkVariants);
                Transform parent = Place(groups, segLength, position, out Vector3 local).Trees;
                var tree = new GameObject("Tree").transform;
                tree.SetParent(parent, false);
                tree.localPosition = local;
                tree.localRotation = Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f);
                tree.localScale = Vector3.one * (height / TrunkReferenceHeightM);

                AddRenderer(tree, "Trunk", trunks[variant], bark, ShadowCastingMode.On);
                AddRenderer(tree, "Crown", crowns[variant], leaves, ShadowCastingMode.On);
                Add(tris, "Trees", LookTestMeshFactory.TriangleCount(trunks[variant]) + LookTestMeshFactory.TriangleCount(crowns[variant]));
                placed++;
            }
        }

        // ---------------------------------------------------------------- Rocks

        private static void ScatterRocks(
            LookTestConfigAsset c, LookTestTerrainShape shape, SegmentGroups[] groups, float segLength,
            LookTestAssets assets, LookTestMaterials materials, Material boulderMaterial, SortedDictionary<string, long> tris)
        {
            var variantRng = new Pcg32Random((ulong)(uint)c.Seed, VariantStream + 100UL);
            var boulders = new Mesh[BoulderVariants];
            for (int v = 0; v < BoulderVariants; v++)
            {
                Mesh boulder = LookTestMeshFactory.Boulder(variantRng, 1f);
                boulder.name = "Boulder_" + v;
                boulders[v] = SaveMesh(boulder);
            }

            var rng = new Pcg32Random((ulong)(uint)c.Seed, RockStream);

            // Procedural boulders: path edges, river banks and bed, forest floor.
            for (int i = 0; i < c.BoulderCount; i++)
            {
                float z = rng.NextFloat(0f, c.LoopLengthM);
                float pick = rng.NextFloat();
                float x;
                float size;
                if (pick < 0.3f)
                {
                    x = (rng.Chance(0.5f) ? -1f : 1f) * (c.PathHalfWidthM + rng.NextFloat(1.2f, 3.5f));
                    size = rng.NextFloat(0.35f, 1.1f);
                }
                else if (pick < 0.7f)
                {
                    x = shape.RiverCenterX(z) + rng.NextFloat(-1.3f, 1.3f) * c.RiverHalfWidthM;
                    size = rng.NextFloat(0.5f, 1.8f);
                }
                else
                {
                    x = -c.PathHalfWidthM - rng.NextFloat(4f, 35f);
                    size = rng.NextFloat(0.6f, 2.4f);
                }

                var position = new Vector3(x, shape.Height(x, z) - size * 0.25f, z);
                Transform parent = Place(groups, segLength, position, out Vector3 local).Rocks;
                Transform rock = AddRenderer(parent, "Boulder", boulders[rng.NextInt(0, BoulderVariants)], boulderMaterial, ShadowCastingMode.On);
                rock.localPosition = local;
                rock.localRotation = Quaternion.Euler(rng.NextFloat(-8f, 8f), rng.NextFloat(0f, 360f), rng.NextFloat(-8f, 8f));
                rock.localScale = Vector3.one * size;
                AddCullLod(rock.gameObject, c.RockCullScreenFraction);
                Add(tris, "Rocks (procedural)", LookTestMeshFactory.TriangleCount(rock.GetComponent<MeshFilter>().sharedMesh));
            }

            // Scanned CC0 hero rocks near the path and the water.
            List<ModelVariant> heroes = CollectVariants(assets, materials, c.RockModelIds, false);
            if (heroes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < c.HeroRockCount; i++)
            {
                float z = (i + rng.NextFloat(0.2f, 0.8f)) * c.LoopLengthM / Mathf.Max(1, c.HeroRockCount);
                float x = i % 2 == 0
                    ? -c.PathHalfWidthM - rng.NextFloat(1.5f, 4f)
                    : shape.RiverCenterX(z) - c.RiverHalfWidthM * rng.NextFloat(0.6f, 1.1f);
                ModelVariant variant = heroes[rng.NextInt(0, heroes.Count)];
                float height = rng.NextFloat(1.2f, 2.6f);
                var position = new Vector3(x, shape.Height(x, z) - 0.15f * height, z);
                Transform instance = PlaceModel(groups, segLength, position, rng.NextFloat(0f, 360f), height, variant, ShadowCastingMode.On, "HeroRock", g => g.Rocks);
                AddCullLod(instance.gameObject, c.RockCullScreenFraction);
                Add(tris, "Rocks (scanned)", variant.Triangles);
            }
        }

        // ---------------------------------------------------------------- Ferns and plants

        private static void ScatterPlants(
            LookTestConfigAsset c, LookTestTerrainShape shape, SegmentGroups[] groups, float segLength,
            LookTestAssets assets, LookTestMaterials materials, SortedDictionary<string, long> tris)
        {
            List<ModelVariant> ferns = CollectVariants(assets, materials, new[] { c.FernModelId }, true);
            var rng = new Pcg32Random((ulong)(uint)c.Seed, FernStream);
            if (ferns.Count > 0)
            {
                for (int i = 0; i < c.FernCount; i++)
                {
                    float z = rng.NextFloat(0f, c.LoopLengthM);
                    float x = UndergrowthX(c, shape, rng, z, 0.9f);
                    ModelVariant variant = ferns[rng.NextInt(0, ferns.Count)];
                    float height = rng.NextFloat(c.FernHeightMinM, c.FernHeightMaxM);
                    var position = new Vector3(x, shape.Height(x, z) - 0.05f, z);
                    Transform instance = PlaceModel(groups, segLength, position, rng.NextFloat(0f, 360f), height, variant, ShadowCastingMode.Off, "Fern", g => g.Plants);
                    AddCullLod(instance.gameObject, c.PlantCullScreenFraction);
                    Add(tris, "Ferns", variant.Triangles);
                }
            }

            List<ModelVariant> plants = CollectVariants(assets, materials, c.PlantModelIds, true);
            rng = new Pcg32Random((ulong)(uint)c.Seed, PlantStream);
            if (plants.Count > 0)
            {
                for (int i = 0; i < c.PlantCount; i++)
                {
                    float z = rng.NextFloat(0f, c.LoopLengthM);
                    float x = UndergrowthX(c, shape, rng, z, 1.4f);
                    ModelVariant variant = plants[rng.NextInt(0, plants.Count)];
                    float height = rng.NextFloat(c.PlantHeightMinM, c.PlantHeightMaxM);
                    var position = new Vector3(x, shape.Height(x, z) - 0.05f, z);
                    Transform instance = PlaceModel(groups, segLength, position, rng.NextFloat(0f, 360f), height, variant, ShadowCastingMode.Off, "Plant", g => g.Plants);
                    AddCullLod(instance.gameObject, c.PlantCullScreenFraction);
                    Add(tris, "Plants", variant.Triangles);
                }
            }
        }

        /// <summary>Undergrowth x: mostly the path verges and the near bank, some deeper in the forest. Never on the path or in the river.</summary>
        private static float UndergrowthX(LookTestConfigAsset c, LookTestTerrainShape shape, IRandom rng, float z, float clearance)
        {
            float edge = c.PathHalfWidthM + clearance;
            float pick = rng.NextFloat();
            if (pick < 0.45f)
            {
                return -edge - 12f * rng.NextFloat() * rng.NextFloat();
            }

            if (pick < 0.75f)
            {
                float riverEdge = shape.RiverCenterX(z) - c.RiverHalfWidthM * 1.25f;
                return Mathf.Lerp(edge, Mathf.Max(edge, riverEdge), rng.NextFloat());
            }

            if (pick < 0.9f)
            {
                return -edge - rng.NextFloat(6f, 30f);
            }

            return shape.RiverCenterX(z) + c.RiverHalfWidthM * rng.NextFloat(1.4f, 3f);
        }

        // ---------------------------------------------------------------- Model variants (CC0 FBX)

        private struct ModelVariant
        {
            public string Name;
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 Matrix;
            public Bounds Bounds;
            public long Triangles;
        }

        /// <summary>Every mesh inside the given models becomes a variant, with its pose inside the model file.</summary>
        private static List<ModelVariant> CollectVariants(LookTestAssets assets, LookTestMaterials materials, string[] ids, bool foliage)
        {
            var result = new List<ModelVariant>();
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

                Material material = materials.ForModel(ids[m], foliage);
                MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
                Matrix4x4 toRoot = model.transform.worldToLocalMatrix;
                for (int f = 0; f < filters.Length; f++)
                {
                    Mesh mesh = filters[f].sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    Matrix4x4 matrix = toRoot * filters[f].transform.localToWorldMatrix;
                    Bounds bounds = TransformBounds(matrix, mesh.bounds);
                    if (bounds.size.y <= 0.0001f)
                    {
                        continue;
                    }

                    result.Add(new ModelVariant
                    {
                        Name = ids[m] + "/" + filters[f].name,
                        Mesh = mesh,
                        Material = material,
                        Matrix = matrix,
                        Bounds = bounds,
                        Triangles = LookTestMeshFactory.TriangleCount(mesh),
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Places a model variant standing on <paramref name="position"/> (stretch space), scaled to
        /// <paramref name="height"/> meters, centered on its footprint.
        /// </summary>
        private static Transform PlaceModel(
            SegmentGroups[] groups, float segLength, Vector3 position, float yawDeg, float height, ModelVariant variant,
            ShadowCastingMode shadows, string name, System.Func<SegmentGroups, Transform> group)
        {
            Transform parent = group(Place(groups, segLength, position, out Vector3 local));
            var instance = new GameObject(name).transform;
            instance.SetParent(parent, false);
            instance.localPosition = local;
            instance.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            // Scale to the wanted height, but never wider than 3 × that height (flat ground-cover meshes).
            float scale = height / variant.Bounds.size.y;
            float wide = Mathf.Max(variant.Bounds.size.x, variant.Bounds.size.z);
            if (wide * scale > height * 3f)
            {
                scale = height * 3f / wide;
            }

            instance.localScale = Vector3.one * scale;

            Matrix4x4 pose = Matrix4x4.Translate(new Vector3(-variant.Bounds.center.x, -variant.Bounds.min.y, -variant.Bounds.center.z)) * variant.Matrix;
            Transform child = AddRenderer(instance, variant.Mesh.name, variant.Mesh, variant.Material, shadows);
            child.localPosition = pose.GetColumn(3);
            child.localRotation = pose.rotation;
            child.localScale = pose.lossyScale;
            return instance;
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

        /// <summary>Segment that owns stretch-space <paramref name="position"/>, and the position local to it.</summary>
        private static SegmentGroups Place(SegmentGroups[] groups, float segLength, Vector3 position, out Vector3 local)
        {
            float loop = segLength * groups.Length;
            float z = Mathf.Repeat(position.z, loop);
            int index = Mathf.Clamp(Mathf.FloorToInt(z / segLength), 0, groups.Length - 1);
            local = new Vector3(position.x, position.y, z - index * segLength);
            return groups[index];
        }

        private static Transform AddRenderer(Transform parent, string name, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            var shared = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < shared.Length; i++)
            {
                shared[i] = material;
            }

            meshRenderer.sharedMaterials = shared;
            meshRenderer.shadowCastingMode = shadows;
            meshRenderer.receiveShadows = true;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return go.transform;
        }

        /// <summary>A one-level LODGroup: the object is culled once it is smaller than the given screen fraction.</summary>
        private static void AddCullLod(GameObject instance, float screenFraction)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            LODGroup lod = instance.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(screenFraction, renderers) });
            lod.RecalculateBounds();
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            return LookTestAssets.SaveOrReplace(mesh, MeshFolder + "/" + mesh.name + ".asset");
        }

        private static void Add(SortedDictionary<string, long> tris, string key, long value)
        {
            tris.TryGetValue(key, out long current);
            tris[key] = current + value;
        }

        private static void Report(List<string> report, SortedDictionary<string, long> tris, LookTestConfigAsset c)
        {
            var text = new StringBuilder();
            text.AppendLine("Look test scene built: " + ScenePath);
            long total = 0L;
            text.AppendLine("Triangles in the whole " + c.LoopLengthM + " m loop (the camera sees roughly half; plants and rocks are culled by distance):");
            foreach (KeyValuePair<string, long> pair in tris)
            {
                text.AppendLine("  " + pair.Key + ": " + pair.Value.ToString("N0"));
                total += pair.Value;
            }

            text.AppendLine("  Total: " + total.ToString("N0"));
            for (int i = 0; i < report.Count; i++)
            {
                text.AppendLine(report[i]);
            }

            text.AppendLine("Press Play to run through it. On-device numbers: see docs/PLAY_FIRST_BUILD.md, \"Look test\".");
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
            }

            public Transform Ground { get; }

            public Transform Water { get; }

            public Transform Trees { get; }

            public Transform Rocks { get; }

            public Transform Plants { get; }

            private static Transform Child(Transform parent, string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                return t;
            }
        }
    }
}
