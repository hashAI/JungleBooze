using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Materials of the grounded rigs: shared opaque colors from the gray-box kit (cloned from the Resources template,
    /// no shader lookup by name), the two theme-tinted body materials, and a few alpha-blended decal materials that
    /// share one generated soft-blob texture (no texture assets). Setup-time only (allocates); <see cref="Dispose"/>
    /// destroys what this object made.
    /// </summary>
    public sealed class RigMaterials : IDisposable
    {
        private const int BlobSize = 32;

        private readonly GrayBoxKit _kit;
        private readonly Material _opaqueTemplate;
        private readonly Texture2D _blob;
        private readonly Material[] _owned;

        /// <summary>Wood and stone body of low, high and full obstacles; the world theme tints it.</summary>
        public Material Body { get; }

        /// <summary>Body of the mover; the world theme tints it.</summary>
        public Material Mover { get; }

        /// <summary>Soft dark pad under every grounded object.</summary>
        public Material ContactShadow { get; }

        /// <summary>Dark disturbed-soil bowl of the boulder's wallow.</summary>
        public Material Wallow { get; }

        /// <summary>Pale loose sand of the end catch.</summary>
        public Material Sand { get; }

        /// <summary>Scuffed groove between wallow and sand bar.</summary>
        public Material Furrow { get; }

        /// <summary>Pale old roll marks behind the scree bank.</summary>
        public Material OldMarks { get; }

        /// <summary>Dust puffs (soft blob, ground-colored).</summary>
        public Material Dust { get; }

        public RigMaterials(GrayBoxKit kit)
        {
            _kit = kit;
            _opaqueTemplate = RuntimeMaterialTemplates.GetOpaqueTemplate();
            Material transparent = RuntimeMaterialTemplates.GetTransparentTemplate();
            _blob = CreateBlob();

            Body = new Material(_opaqueTemplate) { name = "World_ObstacleBody", color = StylePalette.HazardWood, enableInstancing = true };
            Mover = new Material(_opaqueTemplate) { name = "World_MoverBody", color = StylePalette.HazardStone, enableInstancing = true };
            ContactShadow = CreateDecal(transparent, "Grounding_ContactShadow", _blob, new Color(StylePalette.Ink.r, StylePalette.Ink.g, StylePalette.Ink.b, 0.55f));
            Wallow = CreateDecal(transparent, "Grounding_Wallow", _blob, new Color(GroundingPalette.Soil.r, GroundingPalette.Soil.g, GroundingPalette.Soil.b, 0.85f));
            Sand = CreateDecal(transparent, "Grounding_Sand", _blob, new Color(GroundingPalette.Sand.r, GroundingPalette.Sand.g, GroundingPalette.Sand.b, 0.9f));
            Furrow = CreateDecal(transparent, "Grounding_Furrow", null, new Color(GroundingPalette.Soil.r, GroundingPalette.Soil.g, GroundingPalette.Soil.b, 0.75f));
            OldMarks = CreateDecal(transparent, "Grounding_OldMarks", null, new Color(GroundingPalette.Sand.r, GroundingPalette.Sand.g, GroundingPalette.Sand.b, 0.45f));
            Dust = CreateDecal(transparent, "Grounding_Dust", _blob, new Color(GroundingPalette.Sand.r, GroundingPalette.Sand.g, GroundingPalette.Sand.b, 0.55f));
            _owned = new[] { Body, Mover, ContactShadow, Wallow, Sand, Furrow, OldMarks, Dust };

            // Every palette color is made now, so choosing a material for a rig never creates one during a run.
            for (int i = 0; i < GroundingPalette.All.Length; i++)
            {
                Opaque(GroundingPalette.All[i]);
            }
        }

        /// <summary>The shared opaque material of <paramref name="color"/> (no allocation once the color has been used).</summary>
        public Material Opaque(Color color)
        {
            return _kit.GetMaterial(color, _opaqueTemplate);
        }

        /// <summary>World themes (GDD 9) tint the obstacle and mover bodies. Allocation free.</summary>
        public void ApplyTheme(Color obstacleBody, Color moverBody)
        {
            Body.color = obstacleBody;
            Mover.color = moverBody;
        }

        public void Dispose()
        {
            for (int i = 0; _owned != null && i < _owned.Length; i++)
            {
                if (_owned[i] != null)
                {
                    Object.Destroy(_owned[i]);
                }
            }

            if (_blob != null)
            {
                Object.Destroy(_blob);
            }
        }

        private static Texture2D CreateBlob()
        {
            var pixels = new Color32[BlobSize * BlobSize];
            float half = (BlobSize - 1) * 0.5f;
            for (int y = 0; y < BlobSize; y++)
            {
                for (int x = 0; x < BlobSize; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - (2f * a));
                    pixels[(y * BlobSize) + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            }

            var texture = new Texture2D(BlobSize, BlobSize, TextureFormat.RGBA32, false)
            {
                name = "Grounding_Blob",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Material CreateDecal(Material template, string name, Texture2D texture, Color color)
        {
            var material = new Material(template) { name = name };
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = true;
            material.color = color;
            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                }
            }

            return material;
        }
    }
}
