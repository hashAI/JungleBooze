using JungleBooze.App.LookTest;
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

        private const int AlphaTestQueue = 2450;

        private readonly LookTestConfigAsset _config;
        private readonly LookTestAssets _assets;
        private readonly Shader _nature;
        private readonly Shader _water;
        private readonly Shader _sky;
        private readonly Shader _mist;

        public LookTestMaterials(LookTestConfigAsset config, LookTestAssets assets)
        {
            _config = config;
            _assets = assets;
            _nature = LoadShader(NatureShaderPath);
            _water = LoadShader(WaterShaderPath);
            _sky = LoadShader(SkyShaderPath);
            _mist = LoadShader(MistShaderPath);
        }

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

        public Material Cliff()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.CliffTextureId, string.Empty);
            Material m = Nature("Cliff", set, 1f / _config.CliffTileM, new Color(0.45f, 0.43f, 0.38f, 1f));
            m.SetFloat("_VertexAO", 1f);
            return Save(m);
        }

        public Material Boulder()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.BoulderTextureId, string.Empty);
            Material m = Nature("Boulder", set, 0.5f, new Color(0.40f, 0.42f, 0.33f, 1f));
            m.SetFloat("_VertexAO", 1f);
            return Save(m);
        }

        public Material Bark()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.TexturesFolder, _config.BarkTextureId, string.Empty);
            Material m = Nature("Bark", set, 1f / _config.BarkTileM, new Color(0.33f, 0.27f, 0.21f, 1f));
            m.SetFloat("_VertexAO", 1f);
            return Save(m);
        }

        /// <summary>Canopy leaf cards: alpha clip, two-sided, translucent, swaying with the wind.</summary>
        public Material Leaves()
        {
            LookTestAssets.TextureSet set = _assets.FindTextures(LookTestAssets.ModelsFolder, _config.LeafCardModelId, "leaves");
            Material m = Nature("Leaves", set, 1f, new Color(0.22f, 0.36f, 0.14f, 1f));
            Foliage(m, set, 0.55f, 0.04f);
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
                m.SetColor("_BaseColor", _config.ScannedRockTint);
            }

            return Save(m);
        }

        public Material River(Texture2D normal)
        {
            Material m = Water("River", normal);
            m.SetVector("_FlowA", new Vector4(0.02f, _config.RiverFlowSpeed, 0f, 0f));
            m.SetVector("_FlowB", new Vector4(-0.03f, _config.RiverFlowSpeed * 0.63f, 0f, 0f));
            return Save(m);
        }

        public Material Waterfall(Texture2D normal)
        {
            Material m = Water("Waterfall", normal);
            m.SetVector("_FlowA", new Vector4(0f, _config.WaterfallFlowSpeed, 0f, 0f));
            m.SetVector("_FlowB", new Vector4(0.02f, _config.WaterfallFlowSpeed * 1.37f, 0f, 0f));
            m.SetFloat("_NormalStrength", 1f);
            m.SetFloat("_Opacity", 0.8f);
            m.SetFloat("_FoamStrength", 1.2f);
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

        public Material Sky(Cubemap hdri)
        {
            Material m = Fresh("Sky", _sky);
            if (hdri != null)
            {
                m.SetTexture("_Tex", hdri);
            }

            m.SetFloat("_Exposure", _config.SkyExposure);
            m.SetFloat("_Rotation", Mathf.Repeat(_config.SkyRotationDeg, 360f));
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
            return m;
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

        private static Material Save(Material material)
        {
            return LookTestAssets.SaveOrReplace(material, Folder + "/" + material.name + ".mat");
        }

        private static Shader LoadShader(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }
    }
}
