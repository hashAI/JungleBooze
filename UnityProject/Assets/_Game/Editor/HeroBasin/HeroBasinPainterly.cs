using System.Collections.Generic;
using JungleBooze.App.HeroBasin;
using JungleBooze.Editor.LookTest;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Painterly style trial (ADR 0009): turns the painterly scene's own materials (under its style folder, never the
    /// realistic ones) into the _PAINTERLY shader variants with the values of <see cref="HeroPainterlyLook"/>, and gives
    /// Pista's scene instance a Character Painterly material built from her existing textures (her model, prefab and
    /// material files are not touched). Run by <see cref="HeroBasinBuilder.Build(HeroBasinStyle)"/> after the world
    /// is emitted.
    /// </summary>
    public static class HeroBasinPainterly
    {
        public const string Keyword = "_PAINTERLY";
        private const string NatureShader = "JungleBooze/Nature Lit";
        private const string WaterShader = "JungleBooze/Water";
        private const string WaterfallShader = "JungleBooze/Waterfall";
        private const string BackdropShader = "JungleBooze/Backdrop Card";
        private const string CharacterShaderPath = LookTestMaterials.ShaderFolder + "/CharacterPainterly.shader";

        public static string Apply(HeroBasinConfigAsset h, Transform root, GameObject pista, string materialFolder)
        {
            HeroPainterlyLook p = h.Painterly ?? new HeroPainterlyLook();
            var seen = new HashSet<Material>();
            var backdrops = new List<Material>();
            int stone = 0, foliage = 0, ground = 0, water = 0, falls = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (pista != null && renderer.transform.IsChildOf(pista.transform))
                {
                    continue;
                }

                foreach (Material m in renderer.sharedMaterials)
                {
                    if (m == null || !seen.Add(m) || !AssetDatabase.GetAssetPath(m).StartsWith(materialFolder, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    switch (m.shader.name)
                    {
                        case NatureShader:
                            if (m.IsKeywordEnabled("_ALPHATEST_ON") || m.name.Contains("Canopy") || m.name.Contains("Leaves"))
                            {
                                Foliage(m, p);
                                foliage++;
                            }
                            else if (m.IsKeywordEnabled("_LAYERS_ON"))
                            {
                                Stone(m, p, p.GroundTint);
                                ground++;
                            }
                            else
                            {
                                Stone(m, p, p.StoneTint);
                                stone++;
                            }

                            break;
                        case WaterShader:
                            Water(m, p, h.Cards);
                            water++;
                            break;
                        case WaterfallShader:
                            Falls(m, p);
                            falls++;
                            break;
                        case BackdropShader:
                            backdrops.Add(m);
                            break;
                    }
                }
            }

            backdrops.Sort((a, b) => a.renderQueue.CompareTo(b.renderQueue));
            for (int i = 0; i < backdrops.Count; i++)
            {
                float farness = backdrops.Count > 1 ? 1f - (float)i / (backdrops.Count - 1) : 1f;
                Backdrop(backdrops[i], p, i == 0 ? 0.5f : farness);
            }

            string character = Character(p, pista, materialFolder);
            return "Painterly: " + stone + " stone, " + ground + " ground, " + foliage + " foliage, " + water + " water, " + falls
                + " falls, " + backdrops.Count + " backdrop materials; " + character;
        }

        private static void Shared(Material m, HeroPainterlyLook p)
        {
            m.SetFloat("_Painterly", 1f);
            m.EnableKeyword(Keyword);
            m.SetColor("_PaintShadowTint", p.ShadowTint);
            m.SetColor("_PaintTerminator", p.Terminator);
            m.SetColor("_PaintAOTint", p.AOTint);
            m.SetFloat("_PaintWrap", p.Wrap);
            m.SetFloat("_PaintRampSoftness", p.RampSoftness);
            m.SetFloat("_PaintRamp", p.Ramp);
            m.SetColor("_PaintRim", p.Rim);
            // The realistic Fresnel sheen and detail normals are replaced by the painted rim and broad normals.
            m.SetFloat("_RimStrength", 0f);
        }

        /// <summary>
        /// True when the material's albedo is a hand-painted set (asset-pipeline "_P" variant): its painted gradients,
        /// seams and moss are kept as authored (no softening, flattening or warm compensation tint).
        /// </summary>
        private static bool IsPainted(Material m)
        {
            Texture albedo = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            return albedo != null && Scenery.EnvironmentAssetRules.IsPaintedVariant(albedo.name);
        }

        private static void Stone(Material m, HeroPainterlyLook p, Color tint)
        {
            Shared(m, p);
            if (IsPainted(m))
            {
                StonePainted(m, p);
                return;
            }

            m.SetFloat("_PaintBlur", p.StoneBlur);
            m.SetFloat("_PaintFlatten", p.StoneFlatten);
            m.SetFloat("_PaintSaturation", p.StoneSaturation);
            m.SetFloat("_PaintNormal", p.StoneNormal);
            m.SetFloat("_PaintSpecular", p.StoneSpecular);
            m.SetFloat("_PaintSpecPower", 10f);
            m.SetColor("_PaintTint", tint);
            m.SetColor("_PaintCore", new Color(1f, 1f, 1f, 0f));
            m.SetColor("_PaintTip", new Color(1f, 1f, 1f, 0f));
            m.SetColor("_PaintBackLight", new Color(0f, 0f, 0f, 0f));
            if (m.GetFloat("_MossAmount") > 0f)
            {
                m.SetColor("_MossColor", p.MossColor);
            }

            m.DisableKeyword("_DETAIL_ON");
            m.SetFloat("_Detail", 0f);
            EditorUtility.SetDirty(m);
        }

        private static void StonePainted(Material m, HeroPainterlyLook p)
        {
            m.SetFloat("_PaintBlur", 0f);
            m.SetFloat("_PaintFlatten", 0f);
            m.SetFloat("_PaintSaturation", 1.05f);
            m.SetFloat("_PaintNormal", p.PaintedNormal);
            m.SetFloat("_PaintSpecular", p.StoneSpecular);
            m.SetFloat("_PaintSpecPower", 10f);
            m.SetColor("_PaintTint", Color.white);
            m.SetColor("_BaseColor", Color.white);
            // Moss caps on the tops (keyframe: green on every upward face of the arch), on top of the painted moss.
            if (m.GetFloat("_MossAmount") > 0f)
            {
                m.SetColor("_MossColor", p.MossColor);
            }

            m.SetColor("_PaintCore", new Color(1f, 1f, 1f, 0f));
            m.SetColor("_PaintTip", new Color(1f, 1f, 1f, 0f));
            m.SetColor("_PaintBackLight", new Color(0f, 0f, 0f, 0f));
            m.DisableKeyword("_DETAIL_ON");
            m.SetFloat("_Detail", 0f);
            EditorUtility.SetDirty(m);
        }

        private static void Foliage(Material m, HeroPainterlyLook p)
        {
            Shared(m, p);
            bool painted = IsPainted(m);
            m.SetFloat("_PaintBlur", painted ? 0f : p.LeafBlur);
            m.SetFloat("_PaintFlatten", painted ? 0f : p.LeafFlatten);
            m.SetFloat("_PaintSaturation", p.LeafSaturation);
            m.SetFloat("_PaintNormal", p.LeafNormal);
            m.SetFloat("_PaintWrap", p.LeafWrap);
            m.SetFloat("_PaintSpecular", 0.1f);
            m.SetFloat("_PaintSpecPower", 8f);
            m.SetColor("_PaintTint", p.LeafTint);
            m.SetColor("_PaintCore", p.LeafCore);
            m.SetColor("_PaintTip", p.LeafTip);
            m.SetColor("_PaintBackLight", p.LeafBackLight);
            EditorUtility.SetDirty(m);
        }

        private static void Water(Material m, HeroPainterlyLook p, HeroCards cards)
        {
            // v4 (ADR 0011): painted foam atlas instead of the thresholded cream shapes.
            Texture2D foam = cards != null && cards.PaintedFoam
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(HeroBasinCards.EffectsFolder + "/" + cards.FoamTexture + ".png")
                : null;
            m.SetFloat("_PaintedFoam", foam != null ? 1f : 0f);
            if (foam != null)
            {
                m.EnableKeyword("_PAINTED_FOAM");
                m.SetTexture("_PaintFoamTex", foam);
                m.SetVector("_PaintFoamCell", new Vector4(cards.FoamCellM.x, cards.FoamCellM.y, 0f, 0f));
                m.SetFloat("_PaintFoamMax", cards.FoamMaxAlpha);
            }
            else
            {
                m.DisableKeyword("_PAINTED_FOAM");
            }

            m.SetFloat("_Painterly", 1f);
            m.EnableKeyword(Keyword);
            m.SetColor("_ShallowColor", p.WaterShallow);
            m.SetColor("_PaintMidColor", p.WaterMid);
            m.SetColor("_DeepColor", p.WaterDeep);
            m.SetColor("_PaintSkyColor", p.WaterSky);
            m.SetColor("_FoamColor", p.Foam);
            m.SetFloat("_PaintDepthGain", p.WaterDepthGain);
            m.SetFloat("_ReflectionStrength", p.WaterReflection);
            m.SetFloat("_PaintLight", p.WaterLight);
            m.SetFloat("_PaintFoamScale", p.FoamScale);
            m.SetFloat("_PaintSparkle", p.Sparkle);
            EditorUtility.SetDirty(m);
        }

        private static void Falls(Material m, HeroPainterlyLook p)
        {
            m.SetFloat("_Painterly", 1f);
            m.EnableKeyword(Keyword);
            m.SetColor("_PaintLitWhite", p.FallLitWhite);
            m.SetColor("_PaintShadeWhite", p.FallShadeWhite);
            m.SetFloat("_PaintWhiteCut", p.FallWhiteCut);
            m.SetFloat("_PaintEdge", p.FallEdge);
            m.SetFloat("_PaintStreakShade", p.FallStreakShade);
            m.SetColor("_FoamColor", p.Foam);
            EditorUtility.SetDirty(m);
        }

        private static void Backdrop(Material m, HeroPainterlyLook p, float farness)
        {
            m.SetFloat("_Painterly", 1f);
            m.EnableKeyword(Keyword);
            m.SetFloat("_PaintBlur", p.BackdropBlur);
            m.SetFloat("_PaintFlatten", p.BackdropFlatten);
            m.SetFloat("_PaintSaturation", p.BackdropSaturation);
            Color haze = p.BackdropHaze;
            haze.a *= farness;
            m.SetColor("_PaintHaze", haze);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Gives Pista's scene instance the Character Painterly material (her textures, painterly shading).</summary>
        private static string Character(HeroPainterlyLook p, GameObject pista, string materialFolder)
        {
            if (pista == null)
            {
                return "no Pista";
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CharacterShaderPath);
            if (shader == null)
            {
                return "Character Painterly shader missing";
            }

            var made = new Dictionary<Material, Material>();
            int slots = 0;
            foreach (Renderer renderer in pista.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !source.HasProperty("_BaseMap"))
                    {
                        continue;
                    }

                    if (!made.TryGetValue(source, out Material painted))
                    {
                        painted = new Material(shader) { name = source.name + "_Painterly" };
                        painted.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                        painted.SetColor("_BaseColor", source.GetColor("_BaseColor") * p.PistaAlbedoTint);
                        Copy(source, painted, "_BumpMap");
                        Copy(source, painted, "_MetallicGlossMap");
                        Copy(source, painted, "_OcclusionMap");
                        painted.SetFloat("_NormalBlur", p.PistaNormalBlur);
                        painted.SetFloat("_NormalStrength", p.PistaNormalStrength);
                        painted.SetFloat("_Wrap", p.PistaWrap);
                        painted.SetColor("_SkinTint", p.PistaSkin);
                        painted.SetFloat("_RimIntensity", p.PistaRim);
                        painted.SetColor("_RimColor", p.PistaRimColor);
                        painted.SetColor("_SunFill", p.PistaSunFill);
                        painted.SetVector("_FillDirection", p.PistaFillDirection);
                        painted.SetColor("_Bounce", p.PistaBounce);
                        painted.SetFloat("_Saturation", p.PistaSaturation);
                        painted.SetColor("_ShadowTint", new Color(p.ShadowTint.r, p.ShadowTint.g, p.ShadowTint.b, p.ShadowTint.a * 0.85f));
                        painted.SetColor("_AOTint", p.AOTint);
                        LookTestAssets.EnsureFolder(materialFolder);
                        painted = LookTestAssets.SaveOrReplace(painted, materialFolder + "/" + painted.name + ".mat");
                        made.Add(source, painted);
                    }

                    materials[i] = painted;
                    slots++;
                }

                renderer.sharedMaterials = materials;
            }

            return "Pista: " + slots + " slot(s) on Character Painterly";
        }

        private static void Copy(Material from, Material to, string name)
        {
            if (from.HasProperty(name) && from.GetTexture(name) != null)
            {
                to.SetTexture(name, from.GetTexture(name));
            }
        }
    }
}
