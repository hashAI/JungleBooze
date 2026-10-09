using JungleBooze.App.LookTest;
using JungleBooze.Editor.Scenery;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Creates the look-test materials (saved under <see cref="Folder"/>) from the CC0 texture sets and the
    /// project's own shaders, loaded by asset path (never by name lookup, so builds always contain them).
    /// A missing texture set still gives a usable material (plain tinted color), and is reported by
    /// <see cref="LookTestAssets.Missing"/>. Editor only.
    /// </summary>
    public sealed class LookTestMaterials
    {
        public const string Folder = "Assets/_Game/Art/LookTest/Materials";
        public const string ShaderFolder = "Assets/_Game/Art/Shaders";
        public const string NatureShaderPath = ShaderFolder + "/NatureLit.shader";
        public const string WaterShaderPath = ShaderFolder + "/Water.shader";
        public const string SkyShaderPath = ShaderFolder + "/SkyHdri.shader";
        public const string MistShaderPath = ShaderFolder + "/Mist.shader";
        public const string AtmosCardShaderPath = ShaderFolder + "/AtmosCard.shader";
        public const string WaterfallShaderPath = ShaderFolder + "/Waterfall.shader";
        public const string BackdropShaderPath = ShaderFolder + "/BackdropCard.shader";

        private const int AlphaTestQueue = 2450;

        private readonly LookTestConfigAsset _config;
        private readonly LookTestAssets _assets;
        private readonly Shader _nature;
        private readonly Shader _water;
        private readonly Shader _sky;
        private readonly Shader _mist;
        private readonly Shader _atmosCard;
        private readonly Shader _waterfall;
        private readonly Shader _backdrop;

        private readonly string _folder;

        // Environment set materials made in this build, by name (used when ShareEnvironmentSets is on). Pieces sharing
        // an atlas (FP_CanopyCrown_A..D, FP_Canopy_Clump, the vines' crest slot) need one material: saving a second
        // one under the same path deletes the first, leaving the earlier pieces with a destroyed material, so they
        // silently fall back to stand-ins (procedural photo-leaf crowns, no fern clumps, no vine crests).
        private readonly System.Collections.Generic.Dictionary<string, Material> _environmentSets = new System.Collections.Generic.Dictionary<string, Material>();

        /// <summary>
        /// Share one material per environment texture set (fixes the stand-in fallback above). Off by default so the
        /// look test and the realistic hero basin keep their reviewed output; the painterly hero basin turns it on.
        /// </summary>
        public bool ShareEnvironmentSets { get; set; }

        public LookTestMaterials(LookTestConfigAsset config, LookTestAssets assets)
            : this(config, assets, Folder)
        {
        }

        /// <summary>Materials saved under <paramref name="folder"/> (another scene keeps its own copies).</summary>
        public LookTestMaterials(LookTestConfigAsset config, LookTestAssets assets, string folder)
        {
            _folder = folder;
            _config = config;
            _assets = assets;
            _nature = LoadShader(NatureShaderPath);
            _water = LoadShader(WaterShaderPath);
            _sky = LoadShader(SkyShaderPath);
            _mist = LoadShader(MistShaderPath);
            _atmosCard = LoadShader(AtmosCardShaderPath);
            _waterfall = LoadShader(WaterfallShaderPath);
            _backdrop = LoadShader(BackdropShaderPath);
        }

        /// <summary>Water effects texture (falling streaks, lacy foam), set before River, Pool and Waterfall.</summary>
        public Texture2D WaterFx { get; set; }

        public Material Ground()
        {
            Vector3 tile = _config.GroundTileM;
            LookTestAssets.TextureSet floor = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.ForestFloorTextureId, string.Empty);
            LookTestAssets.TextureSet path = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.PathTextureId, string.Empty);
            LookTestAssets.TextureSet bed = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.RiverBedTextureId, string.Empty);

            Material m = Nature("Ground", floor, 1f / tile.x, new Color(0.42f, 0.36f, 0.26f, 1f));
            m.SetFloat("_Layers", 1f);
            m.EnableKeyword("_LAYERS_ON");
            SetLayer(m, "_Layer2", path, 1f / tile.y);
            SetLayer(m, "_Layer3", bed, 1f / tile.z);
            m.SetColor("_BaseColor", _config.ForestFloorTint);
            m.SetColor("_Layer2Color", _config.PathTint);
            m.SetColor("_Layer3Color", _config.RiverBedTint);
            m.SetFloat("_VertexAO", 1f);
            m.SetFloat("_HeightBlend", 0.6f);
            m.SetFloat("_BlendDepth", 0.2f);
            return Save(m);
        }

        /// <summary>Rootstone landforms (graybox): pillars, arches, stilt-root rock, ledges.</summary>
        public Material Rootstone()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.RockTextureId, string.Empty);
            Material m = Nature("Rootstone", set, 1f / _config.RockTileM, new Color(0.45f, 0.43f, 0.38f, 1f));
            m.SetColor("_BaseColor", set.Albedo != null ? _config.RootstoneTint : new Color(0.45f, 0.43f, 0.38f, 1f));
            m.SetFloat("_VertexAO", 1f);
            SurfaceResponse(m, _config.RootstoneMoss, _config.RootstoneRim, true);
            return Save(m);
        }

        /// <summary>Distant forest canopy seen from above (valley floor, pillar tops): lumpy green blobs, no alpha.</summary>
        public Material FarCanopy()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.ForestFloorTextureId, string.Empty);
            Material m = Nature("FarCanopy", set, 1f / 6f, new Color(0.16f, 0.30f, 0.14f, 1f));
            m.SetColor("_BaseColor", set.Albedo != null ? new Color(0.36f, 0.55f, 0.30f, 1f) : new Color(0.16f, 0.30f, 0.14f, 1f));
            m.SetFloat("_VertexAO", 1f);
            return Save(m);
        }

        /// <summary>Additive gold light shafts (Atmos Card shader).</summary>
        public Material LightShaft()
        {
            Material m = Fresh("LightShaft", _atmosCard);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_Intensity", 1f);
            m.SetVector("_NearFade", new Vector4(7f, 18f, 0f, 0f));
            m.SetVector("_FarFade", new Vector4(110f, 170f, 0f, 0f));
            m.renderQueue = (int)RenderQueue.Transparent + 6;
            return Save(m);
        }

        /// <summary>Alpha-blended mist billows (Atmos Card shader, _MIST).</summary>
        public Material MistCard(Texture2D soft)
        {
            Material m = Fresh("MistCard", _atmosCard);
            m.SetTexture("_MainTex", soft);
            m.SetFloat("_Mist", 1f);
            m.EnableKeyword("_MIST");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Intensity", 1f);
            m.SetVector("_NearFade", new Vector4(3f, 12f, 0f, 0f));
            m.SetVector("_FarFade", new Vector4(2500f, 3000f, 0f, 0f));
            m.renderQueue = (int)RenderQueue.Transparent + 3;
            return Save(m);
        }

        public Material Boulder()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.BoulderTextureId, string.Empty);
            Material m = Nature("Boulder", set, 0.5f, new Color(0.40f, 0.42f, 0.33f, 1f));
            m.SetFloat("_VertexAO", 1f);
            SurfaceResponse(m, 0f, _config.RootstoneRim, true);
            return Save(m);
        }

        public Material Bark()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.BarkTextureId, string.Empty);
            Material m = Nature("Bark", set, 1f / _config.BarkTileM, new Color(0.33f, 0.27f, 0.21f, 1f));
            if (set.Albedo != null)
            {
                m.SetColor("_BaseColor", _config.BarkTint);
            }

            m.SetFloat("_VertexAO", 1f);
            SurfaceResponse(m, _config.BarkMoss, _config.BarkRim, false);
            return Save(m);
        }

        /// <summary>Canopy leaf cards: alpha clip, two-sided, translucent, swaying with the wind.</summary>
        public Material Leaves()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.ModelsFolder, _config.LeafCardModelId, "leaves");
            Material m = Nature("Leaves", set, 1f, new Color(0.22f, 0.36f, 0.14f, 1f));
            Foliage(m, set, 0.7f, 0.04f);
            if (set.Albedo != null)
            {
                m.SetColor("_BaseColor", _config.LeafTint);
            }

            m.SetFloat("_VertexAO", 1f);
            return Save(m);
        }

        /// <summary>Material for an imported CC0 model (fern, plant or rock), named after the asset id.</summary>
        public Material ForModel(string id, bool foliage)
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.ModelsFolder, id, string.Empty);
            Material m = Nature("Model_" + id, set, 1f, foliage ? new Color(0.25f, 0.40f, 0.18f, 1f) : new Color(0.42f, 0.40f, 0.35f, 1f));
            if (foliage)
            {
                Foliage(m, set, 0.35f, 0.6f);
            }
            else if (set.Albedo != null)
            {
                m.SetColor("_BaseColor", _config.RootstoneTint);
            }

            return Save(m);
        }

        public Material River(Texture2D normal)
        {
            Material m = Water("River", normal);
            m.SetVector("_FlowA", new Vector4(0.02f, _config.RiverFlowSpeed, 0f, 0f));
            m.SetVector("_FlowB", new Vector4(-0.03f, _config.RiverFlowSpeed * 0.63f, 0f, 0f));
            m.SetFloat("_FoamStrength", 1f);
            m.SetFloat("_ReflectionStrength", 0.7f);
            m.SetFloat("_FresnelPower", 5f);
            m.SetFloat("_Opacity", 0.86f);
            m.SetFloat("_SpecularStrength", 1.0f);
            return Save(m);
        }

        /// <summary>Layered waterfall sheets (Waterfall shader): streaks, foam cells, ragged edges.</summary>
        public Material Waterfall()
        {
            Material m = Fresh("Waterfall", _waterfall);
            m.SetTexture("_FxTex", WaterFx);
            m.SetFloat("_FlowSpeed", _config.WaterfallFlowSpeed);
            m.SetColor("_WaterColor", _config.WaterfallColor);
            m.SetFloat("_Opacity", _config.WaterfallOpacity);
            m.renderQueue = (int)RenderQueue.Transparent + 1;
            return Save(m);
        }

        public Material Mist(Texture2D soft)
        {
            Material m = Fresh("Mist", _mist);
            m.SetTexture("_BaseMap", soft);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f));
            m.renderQueue = (int)RenderQueue.Transparent + 2;
            return Save(m);
        }

        /// <summary>Plunge pool and terrace pools: the river water, calmer.</summary>
        public Material Pool(Texture2D normal)
        {
            Material m = Water("Pool", normal);
            m.SetVector("_FlowA", new Vector4(0.03f, 0.06f, 0f, 0f));
            m.SetVector("_FlowB", new Vector4(-0.04f, 0.03f, 0f, 0f));
            m.SetFloat("_FoamStrength", 1.2f);
            m.SetFloat("_Opacity", 0.9f);
            return Save(m);
        }

        public Material Sky(Cubemap hdri)
        {
            Material m = Fresh("Sky", _sky);
            if (hdri != null)
            {
                m.SetTexture("_Tex", hdri);
            }

            m.SetFloat("_Exposure", _config.SkyExposure);
            m.SetFloat("_Rotation", Mathf.Repeat(_config.SkyRotationDeg, 360f));
            m.SetColor("_Tint", _config.SkyTint);
            m.SetFloat("_HorizonFog", 1f);
            return Save(m);
        }

        private Material Water(string name, Texture2D normal)
        {
            Material m = Fresh(name, _water);
            m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalTiling", _config.WaterNormalTilingPerM);
            m.SetColor("_ShallowColor", _config.WaterShallowColor);
            m.SetColor("_DeepColor", _config.WaterDeepColor);
            m.SetFloat("_Opacity", _config.WaterOpacity);
            m.SetFloat("_ShallowOpacity", _config.WaterShallowOpacity);
            m.SetColor("_ReflectionTint", _config.WaterReflectionTint);
            m.SetTexture("_FxTex", WaterFx);
            m.SetFloat("_FoamTiling", _config.WaterFoamTilingPerM);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private Material Nature(string name, LookTestAssets.TextureSet set, float tiling, Color fallbackColor)
        {
            Material m = Fresh(name, _nature);
            m.enableInstancing = true;
            if (set.Albedo != null)
            {
                m.SetTexture("_BaseMap", set.Albedo);
                m.SetColor("_BaseColor", Color.white);
            }
            else
            {
                m.SetColor("_BaseColor", fallbackColor);
            }

            if (set.Normal != null)
            {
                m.SetTexture("_BumpMap", set.Normal);
            }

            if (set.Arm != null)
            {
                m.SetTexture("_ArmMap", set.Arm);
            }

            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            m.SetFloat("_VertexAO", 0f);
            // Look test v2: every Nature Lit mesh is merged per segment with canopy cover painted in vertex color B.
            m.SetFloat("_CanopyCover", 1f);
            m.SetFloat("_WindVertexColor", 1f);
            return m;
        }

        /// <summary>
        /// Nature Lit material for an environment piece with its own textures (ADR 0008), set up by role like the
        /// stand-in it replaces; null if the piece has no textures (it then shares the stand-in material and batch).
        /// </summary>
        public Material ForEnvironmentPiece(EnvironmentKit.Piece piece)
        {
            if (piece == null)
            {
                return null;
            }

            // Parts with their own texture set (for example RS_HeroArch_A / _B) get their own material, and parts
            // with several material slots one material per slot (named after the slot's set, so pieces share them).
            for (int i = 0; i < piece.Parts.Count; i++)
            {
                EnvironmentKit.Part part = piece.Parts[i];
                if (part.Textures.HasAny)
                {
                    part.Material = ForEnvironmentSet("Env_" + part.Name, part.Textures, piece.Role);
                }

                if (part.SlotTextures != null)
                {
                    part.SlotMaterials = new Material[part.SlotTextures.Length];
                    for (int slot = 0; slot < part.SlotTextures.Length; slot++)
                    {
                        EnvironmentKit.TextureSet set = part.SlotTextures[slot];
                        if (set.HasAny)
                        {
                            part.SlotMaterials[slot] = ForEnvironmentSet("Env_" + SetName(set, part.Name + "_" + slot), set, piece.Role);
                        }
                    }
                }

                piece.Parts[i] = part;
            }

            // Named after the texture set, so pieces sharing an atlas (FP_CanopyCrown_A..D) share one material and batch.
            return piece.Textures.HasAny ? ForEnvironmentSet("Env_" + SetName(piece.Textures, piece.Name), piece.Textures, piece.Role) : null;
        }

        /// <summary>The texture set's name from its albedo file ("FP_Canopy_BaseColor" → "FP_Canopy"), else the fallback.</summary>
        private static string SetName(EnvironmentKit.TextureSet set, string fallback)
        {
            if (set.Albedo == null)
            {
                return fallback;
            }

            int length = EnvironmentAssetRules.TextureSetName(set.Albedo.name).Length;
            return length > 0 && length <= set.Albedo.name.Length ? set.Albedo.name.Substring(0, length) : fallback;
        }

        private Material ForEnvironmentSet(string name, EnvironmentKit.TextureSet textures, EnvironmentRole role)
        {
            if (ShareEnvironmentSets && _environmentSets.TryGetValue(name, out Material made) && made != null)
            {
                return made;
            }

            var set = new LookTestAssets.TextureSet
            {
                Albedo = textures.Albedo,
                Normal = textures.Normal,
                Arm = textures.Arm,
                Alpha = textures.Alpha,
            };
            Material m = Nature(name, set, 1f, new Color(0.45f, 0.43f, 0.38f, 1f));
            m.SetFloat("_VertexAO", 1f);
            switch (role)
            {
                case EnvironmentRole.Stiltwood:
                    SurfaceResponse(m, _config.BarkMoss, _config.BarkRim, false);
                    break;
                case EnvironmentRole.LeafCards:
                case EnvironmentRole.PlantClump:
                case EnvironmentRole.CanopyCrown:
                case EnvironmentRole.ArchVines:
                    if (set.Alpha != null)
                    {
                        Foliage(m, set, 0.6f, 0.04f);
                    }
                    else
                    {
                        // Atlas with the cutout in the albedo's alpha (Plants/ README): clip about 0.5, two-sided.
                        Foliage(m, set, 0.6f, 0.04f);
                        m.SetFloat("_AlphaFromBaseA", 1f);
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_AlphaToMask", 1f);
                        m.SetFloat("_Cutoff", 0.5f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                        m.renderQueue = AlphaTestQueue;
                    }

                    break;
                default:
                    SurfaceResponse(m, _config.RootstoneMoss, _config.RootstoneRim, true);
                    DetailNormal(m);
                    break;
            }

            m = Save(m);
            _environmentSets[name] = m;
            return m;
        }

        /// <summary>Matte-painted backdrop layer (Backdrop Card shader); farther layers draw first.</summary>
        public Material Backdrop(EnvironmentKit.BackdropLayer layer)
        {
            Material m = Fresh("Backdrop_" + layer.Name, _backdrop);
            m.SetTexture("_MainTex", layer.Texture);
            m.renderQueue = (int)RenderQueue.Transparent - 50 + Mathf.Clamp(layer.Order, 0, 40);
            return Save(m);
        }

        /// <summary>
        /// Leaf-card atlas material (alpha in the albedo's A channel; ADR 0008 plant atlases): alpha clip with
        /// alpha-to-coverage, two-sided, translucent, wind from vertex color R.
        /// </summary>
        public Material CardAtlas(string name, Texture2D baseColor, Texture2D normal, float translucency, Color tint)
        {
            var set = new LookTestAssets.TextureSet { Albedo = baseColor, Normal = normal };
            Material m = Nature(name, set, 1f, new Color(0.25f, 0.40f, 0.18f, 1f));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_AlphaFromBaseA", 1f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_AlphaToMask", 1f);
            m.SetFloat("_Cutoff", 0.4f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = AlphaTestQueue;
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_Translucency", translucency);
            m.SetFloat("_Wind", 1f);
            m.SetFloat("_WindStrength", _config.WindStrengthM);
            m.EnableKeyword("_WIND_ON");
            m.SetFloat("_VertexAO", 1f);
            return Save(m);
        }

        /// <summary>
        /// Tiling rock detail normal on UV1 (kit pieces: UV1 = world box projection, 1 unit = 4 m; README asks for
        /// the CC0 rock_face_03 normal at about 0.3), so big uniquely-baked pieces stay crisp up close.
        /// </summary>
        private void DetailNormal(Material m)
        {
            LookTestAssets.TextureSet rock = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.RockTextureId, string.Empty);
            if (rock.Normal == null)
            {
                return;
            }

            m.SetTexture("_DetailNormalMap", rock.Normal);
            m.SetFloat("_DetailNormalScale", 0.3f);
            m.SetFloat("_DetailTiling", 1f);
            m.SetFloat("_Detail", 1f);
            m.EnableKeyword("_DETAIL_ON");
        }

        /// <summary>Backdrop card for a painted layer, drawn in the given queue (farther layers first).</summary>
        public Material BackdropLayer(string name, Texture2D texture, int renderQueue, float fogShare, float edgeFade)
        {
            Material m = Fresh("Backdrop_" + name, _backdrop);
            m.SetTexture("_MainTex", texture);
            m.SetFloat("_FogAmount", fogShare);
            m.SetFloat("_EdgeFade", edgeFade);
            m.renderQueue = renderQueue;
            return Save(m);
        }

        /// <summary>Moss on upward faces, ground bounce, rim sheen and (optionally) wetness from vertex G (Nature Lit).</summary>
        private void SurfaceResponse(Material m, float moss, float rim, bool wet)
        {
            m.SetColor("_MossColor", _config.MossColor);
            m.SetFloat("_MossAmount", moss);
            m.SetColor("_BounceColor", _config.GroundBounce);
            m.SetFloat("_RimStrength", rim);
            m.SetFloat("_WetFromVertexG", wet ? 1f : 0f);
            m.SetFloat("_WetDarken", _config.WetDarken);
            m.SetFloat("_WetSmoothness", _config.WetSmoothness);
        }

        private void Foliage(Material m, LookTestAssets.TextureSet set, float translucency, float windHeightScale)
        {
            if (set.Alpha != null)
            {
                m.SetTexture("_AlphaMap", set.Alpha);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_AlphaToMask", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = AlphaTestQueue;
            }

            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_Translucency", translucency);
            m.SetFloat("_Wind", 1f);
            m.SetFloat("_WindStrength", _config.WindStrengthM);
            m.SetFloat("_WindHeightScale", windHeightScale);
            m.EnableKeyword("_WIND_ON");
        }

        private static void SetLayer(Material m, string prefix, LookTestAssets.TextureSet set, float tiling)
        {
            if (set.Albedo != null)
            {
                m.SetTexture(prefix + "Map", set.Albedo);
            }

            if (set.Normal != null)
            {
                m.SetTexture(prefix + "BumpMap", set.Normal);
            }

            if (set.Arm != null)
            {
                m.SetTexture(prefix + "ArmMap", set.Arm);
            }

            m.SetTextureScale(prefix + "Map", new Vector2(tiling, tiling));
        }

        private static Material Fresh(string name, Shader shader)
        {
            if (shader == null)
            {
                throw new System.InvalidOperationException("Shader missing under " + ShaderFolder + " for material " + name + ".");
            }

            return new Material(shader) { name = name };
        }

        private Material Save(Material material)
        {
            LookTestAssets.EnsureFolder(_folder);
            return LookTestAssets.SaveOrReplace(material, _folder + "/" + material.name + ".mat");
        }

        private static Shader LoadShader(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }
    }
}
