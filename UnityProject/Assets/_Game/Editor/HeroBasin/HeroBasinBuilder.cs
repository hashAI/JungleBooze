using System.Collections.Generic;
using JungleBooze.App.HeroBasin;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using JungleBooze.Editor.Characters;
using JungleBooze.Editor.LookTest;
using JungleBooze.Editor.Scenery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Builds <see cref="ScenePath"/>: the waterfall basin of keyframe F4_e as one hero scene at full quality
    /// (ADR 0008). It reuses the look-test machinery: merge batches (one draw per material), Nature Lit, Water,
    /// layered Waterfall, Atmos Card mist and shafts, the project atmosphere and grade, and the environment kit
    /// (RS_HeroArch, RS_Outcrop, painted backdrop layers, plant atlases). Pista (real model, idle pose) stands on the
    /// ledge at the origin looking along +z. All numbers come from <see cref="HeroBasinConfigAsset"/>; the look
    /// values the shared shaders read are copied into a generated look config (<see cref="LookPath"/>).
    /// Generated meshes and the scene are rebuilt on every run (git-ignored).
    /// </summary>
    public static class HeroBasinBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/HeroBasin.unity";
        public const string ConfigFolder = "Assets/_Game/Config/HeroBasin";
        public const string ConfigPath = ConfigFolder + "/HeroBasinConfig.asset";
        public const string LookPath = ConfigFolder + "/HeroBasinLook.asset";
        public const string VolumePath = ConfigFolder + "/HeroBasinVolume.asset";
        public const string LightingPath = ConfigFolder + "/HeroBasinLighting.lighting";
        public const string ArtFolder = "Assets/_Game/Art/HeroBasin";
        public const string MeshFolder = ArtFolder + "/Meshes";
        public const string MaterialFolder = ArtFolder + "/Materials";
        public const string PlantsFolder = EnvironmentAssetRules.Root + "Plants/Textures";

        private const string LogPrefix = "[JungleBooze hero basin] ";

        /// <summary>
        /// Where one style's files live (ADR 0009). Realistic keeps the iteration 3 paths; Painterly gets its own
        /// config, scene, look/volume/lighting assets, materials and meshes, so building one never touches the other.
        /// </summary>
        public readonly struct StylePaths
        {
            public StylePaths(HeroBasinStyle style)
            {
                bool p = style == HeroBasinStyle.Painterly;
                string suffix = p ? "_Painterly" : string.Empty;
                Style = style;
                Config = ConfigFolder + "/HeroBasinConfig" + suffix + ".asset";
                Scene = "Assets/_Game/Scenes/HeroBasin" + suffix + ".unity";
                Look = ConfigFolder + "/HeroBasinLook" + suffix + ".asset";
                Volume = ConfigFolder + "/HeroBasinVolume" + suffix + ".asset";
                Lighting = ConfigFolder + "/HeroBasinLighting" + suffix + ".lighting";
                Materials = p ? ArtFolder + "/Painterly/Materials" : MaterialFolder;
                Meshes = p ? ArtFolder + "/Painterly/Meshes" : MeshFolder;
            }

            public HeroBasinStyle Style { get; }
            public string Config { get; }
            public string Scene { get; }
            public string Look { get; }
            public string Volume { get; }
            public string Lighting { get; }
            public string Materials { get; }
            public string Meshes { get; }
        }

        public static List<string> Build()
        {
            return Build(HeroBasinStyle.Realistic);
        }

        public static List<string> Build(HeroBasinStyle style)
        {
            var paths = new StylePaths(style);
            var report = new List<string>();
            HeroBasinConfigAsset h = EnsureConfig(style);
            LookTestConfigAsset look = BuildLook(h, paths.Look);

            var assets = new LookTestAssets();
            // Painterly: one material per shared atlas (painted crowns, ferns, vine crests). Realistic keeps its reviewed
            // output, which relies on the old stand-in fallback (ADR 0009 addendum).
            var materials = new LookTestMaterials(look, assets, paths.Materials) { ShareEnvironmentSets = style == HeroBasinStyle.Painterly };
            var layout = new LookTestStretchLayout(look);
            var batches = new LookTestBatchSet();
            var ctx = new LookTestBuildContext(look, layout, batches);
            Texture2D waterNormal = LookTestTextureGenerator.EnsureWaterNormal(look.Seed);
            Texture2D mistTexture = LookTestTextureGenerator.EnsureMist();
            Texture2D dapple = LookTestTextureGenerator.EnsureDapple(look.Seed);
            materials.WaterFx = LookTestTextureGenerator.EnsureWaterFx(look.Seed);
            Cubemap hdri = assets.FindHdri(look.SkyHdriId);

            ctx.Ground = materials.Ground();
            ctx.Rootstone = materials.Rootstone();
            ctx.Boulder = materials.Boulder();
            ctx.Bark = materials.Bark();
            ctx.Leaves = materials.Leaves();
            ctx.FarCanopy = materials.FarCanopy();
            ctx.River = materials.River(waterNormal);
            ctx.Waterfall = materials.Waterfall();
            ctx.Pool = materials.Pool(waterNormal);
            ctx.Pool.SetFloat("_ReflectionStrength", h.WaterReflection);
            ctx.Pool.SetFloat("_Opacity", h.WaterOpacity);
            ctx.Pool.SetFloat("_ShallowOpacity", h.WaterShallowOpacity);
            ctx.Pool.SetFloat("_SpecularStrength", 0.3f);
            ctx.Pool.SetFloat("_NormalStrength", 0.3f);
            ctx.Waterfall.SetFloat("_Translucency", h.FallTranslucency);
            ctx.Waterfall.SetColor("_WaterColor", h.FallWaterColor);
            ctx.Waterfall.SetFloat("_EdgeBreakup", h.FallEdgeBreakup);
            ctx.Waterfall.SetFloat("_WhiteBias", h.FallWhiteBias);
            ctx.Waterfall.SetFloat("_FoamGlow", h.FallFoamGlow);
            ctx.Waterfall.SetFloat("_Clumping", h.FallClumping);
            ctx.Waterfall.SetVector("_StreakTiling", new Vector4(h.FallStreakTiling.x, h.FallStreakTiling.y, 0f, 0f));
            ctx.Boulder.SetFloat("_MossAmount", 0.6f);
            ctx.Boulder.SetFloat("_WetSmoothness", 0.5f);
            ctx.Rootstone.SetFloat("_WetSmoothness", 0.5f);
            EditorUtility.SetDirty(ctx.Rootstone);
            ctx.Boulder.SetColor("_MossColor", new Color(0.26f, 0.38f, 0.13f, 1f));
            ctx.Boulder.SetColor("_BaseColor", new Color(0.78f, 0.84f, 0.72f, 1f));
            EditorUtility.SetDirty(ctx.Boulder);
            ctx.Waterfall.SetFloat("_AmbientBoost", 1.0f);
            EditorUtility.SetDirty(ctx.Waterfall);
            ctx.Leaves.SetColor("_BaseColor", h.LeafTint);
            ctx.Leaves.SetFloat("_Translucency", h.LeafTranslucency);
            EditorUtility.SetDirty(ctx.Leaves);
            EditorUtility.SetDirty(ctx.Pool);
            ctx.MistCard = materials.MistCard(mistTexture);
            ctx.MistCard.SetFloat("_SunScatter", h.MistSunScatter);
            ctx.MistCard.SetFloat("_ScatterNeutral", h.MistScatterNeutral);
            EditorUtility.SetDirty(ctx.MistCard);
            ctx.LightShaft = materials.LightShaft();
            ctx.LightShaft.SetVector("_NearFade", new Vector4(10f, 30f, 0f, 0f));
            ctx.LightShaft.SetVector("_FarFade", new Vector4(260f, 420f, 0f, 0f));
            EditorUtility.SetDirty(ctx.LightShaft);
            Material sky = materials.Sky(hdri);
            ctx.Kit = EnvironmentKit.Scan(style == HeroBasinStyle.Painterly);
            // Pieces sharing an atlas share one material: tint each material once.
            var tinted = new HashSet<Material>();
            for (int i = 0; i < ctx.Kit.Pieces.Count; i++)
            {
                ctx.Kit.Pieces[i].Material = materials.ForEnvironmentPiece(ctx.Kit.Pieces[i]);
                if (ctx.Kit.Pieces[i].Role == EnvironmentRole.PlantClump && ctx.Kit.Pieces[i].Material != null && tinted.Add(ctx.Kit.Pieces[i].Material))
                {
                    ctx.Kit.Pieces[i].Material.SetColor("_BaseColor", ctx.Kit.Pieces[i].Material.GetColor("_BaseColor") * h.PlantTint);
                    EditorUtility.SetDirty(ctx.Kit.Pieces[i].Material);
                }

                if (ctx.Kit.Pieces[i].Role == EnvironmentRole.Travertine && ctx.Kit.Pieces[i].Material != null)
                {
                    ctx.Kit.Pieces[i].Material.SetColor("_BaseColor", h.TravertineTint);
                    ctx.Kit.Pieces[i].Material.SetFloat("_MossAmount", h.TravertineMoss);
                    // Wet but not lacquered: the drop faces sit next to every cascade and caught the sun like plastic.
                    ctx.Kit.Pieces[i].Material.SetFloat("_WetSmoothness", 0.3f);
                    EditorUtility.SetDirty(ctx.Kit.Pieces[i].Material);
                }

                if (ctx.Kit.Pieces[i].Role == EnvironmentRole.HeroArch)
                {
                    TintArch(ctx.Kit.Pieces[i].Material, h);
                    for (int p = 0; p < ctx.Kit.Pieces[i].Parts.Count; p++)
                    {
                        TintArch(ctx.Kit.Pieces[i].Parts[p].Material, h);
                    }
                }
            }

            report.Add(ctx.Kit.Summary());
            var plants = new PlantMaterials(materials, report, h.PlantTint, style == HeroBasinStyle.Painterly);

            ClearMeshes(paths.Meshes);
            LookTestMeshAccumulator.ClearCache();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Light sun = LookTestSceneBuilder.CreateSun(look);
            sun.transform.rotation = Quaternion.LookRotation(h.KeyLightDirection, Vector3.up);
            LookTestSceneBuilder.ApplyEnvironment(look, sky, sun);
            Camera camera = LookTestSceneBuilder.CreateCamera(look);
            camera.farClipPlane = h.FarClipM;
            PoseCamera(camera, h, false, 2532f / 1170f);
            LookTestSceneBuilder.CreateVolume(look, paths.Volume);
            AddGradeLut(h, paths.Volume);
            var atmosphereObject = new GameObject("Atmosphere");
            LookTestAtmosphere atmosphere = atmosphereObject.AddComponent<LookTestAtmosphere>();
            // No light passed: the atmosphere's sun (fog in-scatter, shafts, sky glow) follows the visible sun of the
            // config, not the art-directed key light.
            atmosphere.Configure(look, dapple, null);

            var rng = new Pcg32Random((ulong)(uint)look.Seed, 4242UL);
            var world = new HeroBasinWorld(ctx, h, plants, rng, camera.transform.position);
            world.Build();
            Backdrops(ctx, h, materials, camera.transform.position, report);
            if (style == HeroBasinStyle.Painterly && h.Dressing != null && h.Dressing.Enabled)
            {
                // Set dressing (HERO_BASIN_DRESSING.md): placed by rays through the landscape frame, merged into the
                // same batches before they are emitted.
                new HeroBasinDressing(ctx, h, world, camera, report).Build();
                // Portrait (P1) sees nearer ground below and beside Pista: its own foreground pass, then back to F4.
                PoseCamera(camera, h, true, 1170f / 2532f);
                new HeroBasinDressing(ctx, h, world, camera, report, true).Build();
                PoseCamera(camera, h, false, 2532f / 1170f);
            }

            var root = new GameObject("HeroBasin").transform;
            var groups = new Dictionary<int, Transform>();
            string summary = batches.Emit(mesh => SaveMesh(mesh, paths.Meshes), (segment, group) =>
            {
                int key = segment * 16 + (int)group;
                if (!groups.TryGetValue(key, out Transform t))
                {
                    t = new GameObject((segment < 0 ? "Backdrop " : "World ") + group).transform;
                    t.SetParent(root, false);
                    groups.Add(key, t);
                }

                return t;
            });
            report.Add(summary);

            GameObject pista = PlacePista(h, report);
            if (pista != null)
            {
                pista.transform.SetParent(root, true);
                if (style == HeroBasinStyle.Realistic)
                {
                    RimLight(h, pista.transform, paths.Materials);
                }
            }

            if (style == HeroBasinStyle.Painterly)
            {
                report.Add(HeroBasinPainterly.Apply(h, root, pista, paths.Materials));
            }

            LookTestAssets.EnsureFolder(System.IO.Path.GetDirectoryName(paths.Scene).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, paths.Scene);
            sky.SetFloat("_HorizonFog", 0f);
            LookTestSceneBuilder.BakeEnvironment(look, hdri != null, report, paths.Lighting);
            sky.SetFloat("_HorizonFog", 1f);
            EditorUtility.SetDirty(sky);
            atmosphere.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, paths.Scene);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Built " + paths.Scene + System.Environment.NewLine + string.Join(System.Environment.NewLine, report));
            return report;
        }

        private static void TintArch(Material m, HeroBasinConfigAsset h)
        {
            if (m == null)
            {
                return;
            }

            m.SetColor("_BaseColor", h.ArchTint);
            m.SetFloat("_MossAmount", h.ArchMoss);
            EditorUtility.SetDirty(m);
        }

        public static HeroBasinConfigAsset EnsureConfig()
        {
            return EnsureConfig(HeroBasinStyle.Realistic);
        }

        /// <summary>
        /// The style's config. A missing painterly config starts as a copy of the realistic one (same layout and
        /// framing) with its style set to Painterly.
        /// </summary>
        public static HeroBasinConfigAsset EnsureConfig(HeroBasinStyle style)
        {
            string path = new StylePaths(style).Config;
            var config = AssetDatabase.LoadAssetAtPath<HeroBasinConfigAsset>(path);
            if (config != null)
            {
                return config;
            }

            LookTestAssets.EnsureFolder(ConfigFolder);
            if (style != HeroBasinStyle.Realistic && AssetDatabase.CopyAsset(EnsureConfig(HeroBasinStyle.Realistic) != null ? ConfigPath : string.Empty, path))
            {
                config = AssetDatabase.LoadAssetAtPath<HeroBasinConfigAsset>(path);
                var so = new SerializedObject(config);
                so.FindProperty("_style").enumValueIndex = (int)style;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                return config;
            }

            config = ScriptableObject.CreateInstance<HeroBasinConfigAsset>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>Places the camera for the landscape (F4) or portrait (P1) frame.</summary>
        public static void PoseCamera(Camera camera, HeroBasinConfigAsset h, bool portrait, float aspect)
        {
            camera.transform.SetPositionAndRotation(portrait ? h.PortraitPosition : h.LandscapePosition, Quaternion.Euler(portrait ? h.PortraitEuler : h.LandscapeEuler));
            camera.fieldOfView = portrait ? h.PortraitFovDeg : h.LandscapeFovDeg;
            camera.aspect = aspect;
            camera.farClipPlane = h.FarClipM;
        }

        /// <summary>
        /// The look config the shared shaders and materials read (sun, fog, grade, water), generated from the hero
        /// config so the hero scene has one source of numbers and never changes the look test's own config.
        /// </summary>
        private static LookTestConfigAsset BuildLook(HeroBasinConfigAsset h, string lookPath)
        {
            var look = ScriptableObject.CreateInstance<LookTestConfigAsset>();
            var so = new SerializedObject(look);
            void F(string name, float value) => so.FindProperty(name).floatValue = value;
            void C(string name, Color value) => so.FindProperty(name).colorValue = value;
            F("_sunElevationDeg", h.SunElevationDeg);
            F("_sunAzimuthDeg", h.SunAzimuthDeg);
            C("_sunColor", h.SunColor);
            F("_sunIntensity", h.SunIntensity);
            F("_skyExposure", h.SkyExposure);
            F("_ambientIntensity", h.AmbientIntensity);
            F("_cameraFarClipM", h.FarClipM);
            C("_fogColor", h.FogColor);
            C("_fogSunColor", h.FogSunColor);
            F("_fogDensity", h.FogDensity);
            F("_fogHeightFalloff", h.FogHeightFalloff);
            F("_fogBaseHeightM", h.FogBaseHeightM);
            F("_fogStartM", h.FogStartM);
            F("_fogMaxOpacity", h.FogMaxOpacity);
            F("_fogSunPower", h.FogSunPower);
            C("_mistColor", h.MistColor);
            F("_mistOpacity", h.MistOpacity);
            C("_shaftColor", h.ShaftColor);
            F("_shaftIntensity", h.ShaftIntensity);
            F("_postExposure", h.PostExposure);
            F("_contrast", h.Contrast);
            F("_saturation", h.Saturation);
            F("_whiteBalanceTemperature", h.WhiteBalanceTemperature);
            F("_whiteBalanceTint", h.WhiteBalanceTint);
            C("_splitShadows", h.SplitShadows);
            C("_splitHighlights", h.SplitHighlights);
            F("_bloomThreshold", h.BloomThreshold);
            F("_bloomIntensity", h.BloomIntensity);
            F("_vignetteIntensity", h.VignetteIntensity);
            so.FindProperty("_acesTonemapping").boolValue = h.AcesTonemapping;
            F("_splitBalance", h.SplitBalance);
            so.FindProperty("_gradeShadows").vector4Value = h.GradeShadows;
            so.FindProperty("_gradeMidtones").vector4Value = h.GradeMidtones;
            so.FindProperty("_gradeHighlights").vector4Value = h.GradeHighlights;
            F("_bloomScatter", h.BloomScatter);
            C("_bloomTint", h.BloomTint);
            F("_skyFogDistanceM", h.SkyFogDistanceM);
            C("_waterShallowColor", h.WaterShallowColor);
            C("_waterDeepColor", h.WaterDeepColor);
            so.ApplyModifiedPropertiesWithoutUndo();
            LookTestAssets.EnsureFolder(ConfigFolder);
            return LookTestAssets.SaveOrReplace(look, lookPath);
        }

        // ---------------------------------------------------------------- Backdrop layers

        private static void Backdrops(LookTestBuildContext ctx, HeroBasinConfigAsset h, LookTestMaterials materials, Vector3 eye, List<string> report)
        {
            HeroBackdropLayer[] layers = h.Backdrops;
            if (layers == null)
            {
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                HeroBackdropLayer layer = layers[i];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(EnvironmentAssetRules.BackdropsFolder + "/" + layer.Texture + ".png");
                if (texture == null)
                {
                    report.Add("Backdrop layer missing: " + layer.Texture);
                    continue;
                }

                EnsureClamp(texture);
                Material m = materials.BackdropLayer(i.ToString("00") + "_" + layer.Texture, texture, (int)RenderQueue.Transparent - 150 + i, layer.FogShare, 0.08f);
                m.SetFloat("_Exposure", layer.Exposure);
                // The painted sky carries the sun: an HDR core and halo toward the sun direction feed bloom.
                bool sky = i == 0;
                m.SetVector("_SunGlow", sky ? h.SunGlow : Vector4.zero);
                m.SetColor("_SunGlowColor", h.SunGlowColor);
                EditorUtility.SetDirty(m);
                SourceSize(texture, out int sourceWidth, out int sourceHeight);
                float heightDeg = layer.WidthDeg * sourceHeight / Mathf.Max(1f, sourceWidth);
                Mesh card = SphereStrip(layer.DistanceM, layer.AzimuthDeg, layer.WidthDeg, layer.BaseElevationDeg, layer.BaseElevationDeg + heightDeg, layer.Mirror);
                ctx.Batches.Get(LookTestBatchSet.Backdrop, "L5", "Layer " + i.ToString("00"), m, false, LookTestBatchSet.Group.Ground)
                    .Append(card, Matrix4x4.Translate(eye), null);
            }
        }

        /// <summary>A strip of a sphere around the origin (UV linear in azimuth and elevation), facing the origin.</summary>
        public static Mesh SphereStrip(float radius, float azimuthDeg, float widthDeg, float bottomDeg, float topDeg, bool mirror)
        {
            const int columns = 24;
            const int rows = 8;
            var positions = new Vector3[columns + 1, rows + 1];
            var frames = new LookTestMeshFactory.Frame[columns + 1, rows + 1];
            var uvs = new Vector2[columns + 1, rows + 1];
            var colors = new Color[columns + 1, rows + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = (float)i / columns;
                float az = (azimuthDeg + (u - 0.5f) * widthDeg) * Mathf.Deg2Rad;
                for (int j = 0; j <= rows; j++)
                {
                    float v = (float)j / rows;
                    float el = Mathf.Lerp(bottomDeg, topDeg, v) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
                    positions[i, j] = dir * radius;
                    frames[i, j] = new LookTestMeshFactory.Frame { Normal = -dir, Tangent = new Vector3(Mathf.Cos(az), 0f, -Mathf.Sin(az)), Bitangent = Vector3.up };
                    uvs[i, j] = new Vector2(mirror ? 1f - u : u, v);
                    colors[i, j] = Color.white;
                }
            }

            return LookTestMeshFactory.Grid("BackdropLayer", positions, frames, uvs, colors);
        }

        private static void EnsureClamp(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        /// <summary>The painting's own pixel size (the imported texture may have been rescaled).</summary>
        private static void SourceSize(Texture2D texture, out int width, out int height)
        {
            width = texture.width;
            height = texture.height;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            importer?.GetSourceTextureWidthAndHeight(out width, out height);
        }

        // ---------------------------------------------------------------- Grade LUT and rim light

        /// <summary>Adds the keyframe-matched grading LUT (URP Color Lookup) to the hero volume profile.</summary>
        private static void AddGradeLut(HeroBasinConfigAsset h, string volumePath)
        {
            if (string.IsNullOrEmpty(h.GradeLutPath) || h.GradeLutContribution <= 0f)
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(h.GradeLutPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning(LogPrefix + "Grade LUT not found: " + h.GradeLutPath);
                return;
            }

            // URP Color Lookup: values are gamma-space colours, sampled as stored (no sRGB decode), no mips, exact.
            if (importer.sRGBTexture || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(h.GradeLutPath);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if (profile == null)
            {
                return;
            }

            UnityEngine.Rendering.Universal.ColorLookup lut = profile.Add<UnityEngine.Rendering.Universal.ColorLookup>(true);
            lut.name = "ColorLookup";
            lut.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(lut, profile);
            lut.texture.Override(texture);
            lut.contribution.Override(h.GradeLutContribution);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The keyframe's backlit rim on Pista: an extra Rim Overlay material slot on each of her renderers (the slot
        /// past the last submesh draws it again, additively). The project's URP asset has additional lights off, so a
        /// rim point light would not render. Hero scene only; one extra draw per renderer.
        /// </summary>
        private static void RimLight(HeroBasinConfigAsset h, Transform pista, string materialFolder)
        {
            if (h.RimLightIntensity <= 0f)
            {
                return;
            }

            Shader shader = Shader.Find("JungleBooze/Rim Overlay");
            if (shader == null)
            {
                Debug.LogWarning(LogPrefix + "Rim Overlay shader missing: no rim on Pista.");
                return;
            }

            var rim = new Material(shader) { name = "PistaRim" };
            rim.SetColor("_RimColor", h.RimLightColor);
            rim.SetFloat("_RimIntensity", h.RimLightIntensity);
            rim.SetFloat("_RimPower", h.RimPower);
            rim.SetFloat("_SunFacing", h.RimSunFacing);
            LookTestAssets.EnsureFolder(materialFolder);
            rim = LookTestAssets.SaveOrReplace(rim, materialFolder + "/PistaRim.mat");
            foreach (Renderer renderer in pista.GetComponentsInChildren<Renderer>(true))
            {
                var list = new List<Material>(renderer.sharedMaterials) { rim };
                renderer.sharedMaterials = list.ToArray();
            }
        }

        // ---------------------------------------------------------------- Pista

        private static GameObject PlacePista(HeroBasinConfigAsset h, List<string> report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Prefab);
            if (prefab == null)
            {
                report.Add("Pista prefab missing (" + PistaPaths.Prefab + "): no character in the shot.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Pista";
            go.transform.SetPositionAndRotation(h.PistaPosition, Quaternion.Euler(0f, h.PistaYawDeg, 0f));
            PoseIdle(go, h.PistaIdleTimeS);
            return go;
        }

        /// <summary>Samples Pista's idle clip at <paramref name="time"/> (edit mode still pose).</summary>
        public static bool PoseIdle(GameObject pista, float time)
        {
            AnimationClip idle = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PistaPaths.Model))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview", System.StringComparison.Ordinal))
                {
                    string n = clip.name;
                    if (n == "Idle" || n.EndsWith("|Idle", System.StringComparison.Ordinal))
                    {
                        idle = clip;
                        break;
                    }
                }
            }

            if (idle == null)
            {
                return false;
            }

            float t = Mathf.Repeat(time, Mathf.Max(0.01f, idle.length));
            Animator animator = pista.GetComponentInChildren<Animator>();
            if (idle.humanMotion && animator != null && animator.avatar != null)
            {
                // Humanoid clips need the avatar's retargeting: evaluate once through a playable graph (edit mode); the
                // bones keep the pose after the graph is destroyed.
                var graph = PlayableGraph.Create("HeroBasinPose");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Pose", animator);
                var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, idle);
                playable.SetTime(t);
                output.SetSourcePlayable(playable);
                graph.Evaluate(0f);
                graph.Destroy();
                return true;
            }

            idle.SampleAnimation(pista, t);
            return true;
        }

        // ---------------------------------------------------------------- Helpers

        private static void ClearMeshes(string meshFolder)
        {
            if (!AssetDatabase.IsValidFolder(meshFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { meshFolder });
            var paths = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            AssetDatabase.DeleteAssets(paths.ToArray(), new List<string>());
        }

        private static Mesh SaveMesh(Mesh mesh, string meshFolder)
        {
            LookTestAssets.EnsureFolder(meshFolder);
            return LookTestAssets.SaveOrReplace(mesh, meshFolder + "/" + mesh.name + ".asset");
        }

        /// <summary>The plant atlas materials (null when an atlas has not landed).</summary>
        internal sealed class PlantMaterials
        {
            public PlantMaterials(LookTestMaterials materials, List<string> report, Color tint, bool painted = false)
            {
                _painted = painted;
                Broadleaf = Atlas(materials, "FP_Broadleaf", 0.55f, new Color(0.92f, 1f, 0.88f, 1f) * tint, report);
                Fronds = Atlas(materials, "FP_Fronds", 0.65f, new Color(0.88f, 1f, 0.84f, 1f) * tint, report);
                Bellcap = Atlas(materials, "FP_Bellcap", 0.5f, Color.white * tint, report);
            }

            private readonly bool _painted;

            public Material Broadleaf { get; }

            public Material Fronds { get; }

            public Material Bellcap { get; }

            private Material Atlas(LookTestMaterials materials, string name, float translucency, Color tint, List<string> report)
            {
                // Painterly style (ADR 0009): the hand-painted atlas (FP_Fronds_P) replaces the photo one when it exists.
                if (_painted && AssetDatabase.LoadAssetAtPath<Texture2D>(PlantsFolder + "/" + name + "_P_BaseColor.png") != null)
                {
                    name += "_P";
                }

                var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(PlantsFolder + "/" + name + "_BaseColor.png");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PlantsFolder + "/" + name + "_Normal.png");
                if (baseColor == null)
                {
                    report.Add("Plant atlas missing: " + name);
                    return null;
                }

                EnsureCoverage(baseColor);
                if (normal != null)
                {
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }
                }

                return materials.CardAtlas("Plant_" + name, baseColor, normal, translucency, tint);
            }

            private static void EnsureCoverage(Texture2D texture)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                if (importer != null && !importer.mipMapsPreserveCoverage)
                {
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = 0.4f;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
