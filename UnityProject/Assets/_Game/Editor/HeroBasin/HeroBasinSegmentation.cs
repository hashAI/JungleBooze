using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Measures the frame budgets of HERO_BASIN_DRESSING.md s2 on a capture. An ID pass renders every renderer flat,
    /// unlit, in its category colour (alpha-cut where the source is cut-out); the beauty frame supplies foam (bright
    /// cream on water) and orange (bellcaps, Pista's shoulder). Open water is water with no break (anything not water,
    /// or foam) within <see cref="OpenWaterRadiusU"/> of the frame width. Editor only.
    /// </summary>
    public static class HeroBasinSegmentation
    {
        public const string ShaderPath = "Assets/_Game/Editor/HeroBasin/SegmentId.shader";

        /// <summary>Radius (fraction of the frame width) of the "no break" neighbourhood for open water (0.08 u across).</summary>
        public const float OpenWaterRadiusU = 0.04f;

        public enum Category
        {
            Sky,
            BackdropFar,
            BackdropJungle,
            Foliage,
            ArchTop,
            ArchSide,
            Rock,
            Ground,
            Water,
            Falls,
            Wood,
            Pista,
        }

        public static readonly Color[] Palette =
        {
            new Color(0.55f, 0.75f, 1f), new Color(0.6f, 0.45f, 0.3f), new Color(0.2f, 0.45f, 0.2f), new Color(0.1f, 0.85f, 0.1f),
            new Color(1f, 0.85f, 0f), new Color(0.85f, 0.55f, 0.1f), new Color(0.5f, 0.5f, 0.5f), new Color(0.75f, 1f, 0.45f),
            new Color(0f, 0.6f, 0.75f), new Color(1f, 1f, 1f), new Color(0.35f, 0.18f, 0.05f), new Color(1f, 0f, 0.8f),
        };

        /// <summary>The measured shares of one frame.</summary>
        public sealed class Result
        {
            public readonly float[] Share = new float[Palette.Length];
            public float Foam;
            public float OpenWater;
            public float SkyNearSun;
            public float Orange;
            public float ArchTopsBare;
            public Texture2D Mask;
        }

        public static Result Measure(Camera camera, Texture2D beauty, Vector3 sunDirection)
        {
            int w = beauty.width;
            int h = beauty.height;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning("[JungleBooze] Segment Id shader missing: " + ShaderPath);
                return null;
            }

            var saved = new List<KeyValuePair<Renderer, Material[]>>();
            var enabled = new List<KeyValuePair<Renderer, bool>>();
            var made = new Dictionary<Material, Material>();
            var archRenderers = new List<Renderer>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                saved.Add(new KeyValuePair<Renderer, Material[]>(renderer, renderer.sharedMaterials));
                enabled.Add(new KeyValuePair<Renderer, bool>(renderer, renderer.enabled));
                Material[] materials = renderer.sharedMaterials;
                bool keep = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Category? category = CategoryOf(renderer, materials[i]);
                    if (category == null)
                    {
                        materials[i] = null;
                        continue;
                    }

                    keep = true;
                    if (category == Category.ArchSide && !archRenderers.Contains(renderer))
                    {
                        archRenderers.Add(renderer);
                    }

                    materials[i] = IdMaterial(shader, materials[i], category.Value, made);
                }

                renderer.sharedMaterials = materials;
                renderer.enabled = renderer.enabled && keep;
            }

            CameraClearFlags clear = camera.clearFlags;
            Color background = camera.backgroundColor;
            bool hdr = camera.allowHDR;
            bool msaa = camera.allowMSAA;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            bool post = data != null && data.renderPostProcessing;
            var result = new Result();
            Category[] ids;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette[(int)Category.Sky];
                camera.allowHDR = false;
                camera.allowMSAA = false;
                if (data != null)
                {
                    data.renderPostProcessing = false;
                }

                ids = RenderIds(camera, w, h, out Texture2D mask);
                result.Mask = mask;

                // Arch alone: every up-facing strand pixel the camera could see without occluders.
                foreach (KeyValuePair<Renderer, bool> pair in enabled)
                {
                    pair.Key.enabled = pair.Key.enabled && archRenderers.Contains(pair.Key);
                }

                Category[] archOnly = RenderIds(camera, w, h, out Texture2D archMask);
                Object.DestroyImmediate(archMask);
                int topsAll = 0, topsVisible = 0;
                for (int i = 0; i < ids.Length; i++)
                {
                    topsAll += archOnly[i] == Category.ArchTop ? 1 : 0;
                    topsVisible += ids[i] == Category.ArchTop ? 1 : 0;
                }

                result.ArchTopsBare = topsAll > 0 ? topsVisible / (float)topsAll : 0f;
            }
            finally
            {
                foreach (KeyValuePair<Renderer, Material[]> pair in saved)
                {
                    pair.Key.sharedMaterials = pair.Value;
                }

                foreach (KeyValuePair<Renderer, bool> pair in enabled)
                {
                    pair.Key.enabled = pair.Value;
                }

                camera.clearFlags = clear;
                camera.backgroundColor = background;
                camera.allowHDR = hdr;
                camera.allowMSAA = msaa;
                if (data != null)
                {
                    data.renderPostProcessing = post;
                }

                foreach (Material m in made.Values)
                {
                    Object.DestroyImmediate(m);
                }
            }

            Color32[] pixels = beauty.GetPixels32();
            var counts = new int[Palette.Length];
            var water = new bool[ids.Length];
            var breaks = new bool[ids.Length];
            int foam = 0, orange = 0, skyNearSun = 0;
            Vector3 sunViewport = camera.WorldToViewportPoint(camera.transform.position - sunDirection.normalized * 1000f);
            var sun = new Vector2(sunViewport.x * w, sunViewport.y * h);
            float sunRadius = 0.2f * w;
            for (int i = 0; i < ids.Length; i++)
            {
                counts[(int)ids[i]]++;
                Color c = pixels[i];
                Color.RGBToHSV(c, out float hue, out float sat, out float val);
                bool isFoam = ids[i] == Category.Water && val > 0.8f && sat < 0.35f;
                foam += isFoam ? 1 : 0;
                water[i] = ids[i] == Category.Water && !isFoam;
                breaks[i] = !water[i];
                orange += IsOrange(hue, sat, val) ? 1 : 0;
                if (ids[i] == Category.Sky && Vector2.Distance(new Vector2(i % w, i / w), sun) < sunRadius)
                {
                    skyNearSun++;
                }
            }

            float total = ids.Length;
            for (int k = 0; k < counts.Length; k++)
            {
                result.Share[k] = counts[k] / total;
            }

            result.Foam = foam / total;
            result.Orange = orange / total;
            result.SkyNearSun = counts[(int)Category.Sky] > 0 ? skyNearSun / (float)counts[(int)Category.Sky] : 0f;
            result.OpenWater = OpenShare(water, w, h, OpenWaterRadiusU * w, 4);
            return result;
        }

        /// <summary>Bellcap / shoulder orange: hue 15-45 degrees, saturated, not dark (pure; unit-tested).</summary>
        public static bool IsOrange(float hue, float saturation, float value)
        {
            float deg = hue * 360f;
            return deg >= 15f && deg <= 42f && saturation > 0.6f && value > 0.45f;
        }

        /// <summary>
        /// Share of the frame that is <paramref name="open"/> with no non-open pixel within <paramref name="radiusPx"/>
        /// (chamfer distance on a grid downsampled by <paramref name="step"/>; pure, unit-tested).
        /// </summary>
        public static float OpenShare(bool[] open, int w, int h, float radiusPx, int step)
        {
            int gw = Mathf.Max(1, w / step);
            int gh = Mathf.Max(1, h / step);
            var dist = new float[gw * gh];
            const float big = 1e9f;
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    bool allOpen = true;
                    for (int yy = 0; yy < step && allOpen; yy++)
                    {
                        for (int xx = 0; xx < step && allOpen; xx++)
                        {
                            int px = Mathf.Min(w - 1, x * step + xx);
                            int py = Mathf.Min(h - 1, y * step + yy);
                            allOpen = open[py * w + px];
                        }
                    }

                    dist[y * gw + x] = allOpen ? big : 0f;
                }
            }

            const float d1 = 1f;
            const float d2 = 1.41421f;
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    float d = dist[y * gw + x];
                    if (x > 0) d = Mathf.Min(d, dist[y * gw + x - 1] + d1);
                    if (y > 0) d = Mathf.Min(d, dist[(y - 1) * gw + x] + d1);
                    if (x > 0 && y > 0) d = Mathf.Min(d, dist[(y - 1) * gw + x - 1] + d2);
                    if (x < gw - 1 && y > 0) d = Mathf.Min(d, dist[(y - 1) * gw + x + 1] + d2);
                    dist[y * gw + x] = d;
                }
            }

            for (int y = gh - 1; y >= 0; y--)
            {
                for (int x = gw - 1; x >= 0; x--)
                {
                    float d = dist[y * gw + x];
                    if (x < gw - 1) d = Mathf.Min(d, dist[y * gw + x + 1] + d1);
                    if (y < gh - 1) d = Mathf.Min(d, dist[(y + 1) * gw + x] + d1);
                    if (x < gw - 1 && y < gh - 1) d = Mathf.Min(d, dist[(y + 1) * gw + x + 1] + d2);
                    if (x > 0 && y < gh - 1) d = Mathf.Min(d, dist[(y + 1) * gw + x - 1] + d2);
                    dist[y * gw + x] = d;
                }
            }

            // Frame edges are not breaks: water running out of frame stays open (the budget is about what is shown).
            float radius = radiusPx / step;
            int openCells = 0;
            for (int i = 0; i < dist.Length; i++)
            {
                openCells += dist[i] > radius ? 1 : 0;
            }

            return openCells / (float)dist.Length;
        }

        /// <summary>
        /// Nearest palette entry of an ID-pass colour read from the linear target (the palette is authored in sRGB and
        /// written linear by the project's linear colour space; pure, unit-tested).
        /// </summary>
        public static Category Classify(Color linear)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int k = 0; k < Palette.Length; k++)
            {
                Color p = Palette[k].linear;
                float dr = linear.r - p.r;
                float dg = linear.g - p.g;
                float db = linear.b - p.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestD)
                {
                    bestD = d;
                    best = k;
                }
            }

            return (Category)best;
        }

        public static string Format(string title, Result r)
        {
            if (r == null)
            {
                return title + ": segmentation unavailable." + System.Environment.NewLine;
            }

            string P(float v) => (v * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
            float S(Category c) => r.Share[(int)c];
            float foliage = S(Category.Foliage);
            float arch = S(Category.ArchTop) + S(Category.ArchSide);
            float water = S(Category.Water);
            var t = new StringBuilder();
            t.AppendLine(title + " frame budgets (HERO_BASIN_DRESSING.md s2; ID pass + beauty pass):");
            t.AppendLine("  Open water (no break within 0.04 u radius): " + P(r.OpenWater) + "   budget <= 10%   " + (r.OpenWater <= 0.10f ? "ok" : "OVER")
                + "   [all water " + P(water + r.Foam) + ", of which foam " + P(r.Foam) + "]");
            float sky = S(Category.Sky);
            t.AppendLine("  Sky: " + P(sky) + "   budget 10-16%   " + (sky >= 0.10f && sky <= 0.16f ? "ok" : "MISS")
                + "   [within 0.2 u of the sun: " + P(r.SkyNearSun) + " of the sky, needs >= 33%]");
            t.AppendLine("  Bare arch stone: " + P(arch) + "   budget <= 20%   " + (arch <= 0.20f ? "ok" : "OVER")
                + "   [strand tops left bare: " + P(r.ArchTopsBare) + ", budget <= 35%]");
            t.AppendLine("  Lawn / terrain ground: " + P(S(Category.Ground)) + "   budget 0%   " + (S(Category.Ground) < 0.005f ? "ok" : "OVER"));
            float withWall = foliage + S(Category.BackdropJungle);
            t.AppendLine("  Foliage (3D): " + P(foliage) + ", with painted jungle wall " + P(withWall) + "   budget 40-50%   "
                + (withWall >= 0.40f && withWall <= 0.50f ? "ok" : "MISS"));
            t.AppendLine("  Orange: " + P(r.Orange) + "   budget 1-2%   " + (r.Orange >= 0.01f && r.Orange <= 0.02f ? "ok" : "MISS"));
            t.AppendLine("  Other: rock " + P(S(Category.Rock)) + ", falls " + P(S(Category.Falls)) + ", far backdrop " + P(S(Category.BackdropFar))
                + ", wood " + P(S(Category.Wood)) + ", Pista " + P(S(Category.Pista)));
            return t.ToString();
        }

        private static Category[] RenderIds(Camera camera, int w, int h, out Texture2D mask)
        {
            var target = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { msaaSamples = 1, sRGB = false });
            target.Create();
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            mask = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            mask.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            mask.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = previousTarget;
            target.Release();
            Object.DestroyImmediate(target);
            Color[] colors = mask.GetPixels();
            var ids = new Category[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                ids[i] = Classify(colors[i]);
            }

            return ids;
        }

        private static Material IdMaterial(Shader shader, Material source, Category category, Dictionary<Material, Material> made)
        {
            if (made.TryGetValue(source, out Material m))
            {
                return m;
            }

            m = new Material(shader) { name = "Id_" + source.name, hideFlags = HideFlags.HideAndDontSave };
            m.SetColor("_IdColor", Palette[(int)category]);
            bool arch = category == Category.ArchSide;
            m.SetColor("_UpColor", Palette[(int)(arch ? Category.ArchTop : category)]);
            m.SetFloat("_UpThreshold", arch ? 0.5f : 2f);
            Texture alpha = null;
            float cutoff = 0.5f;
            if (source.shader.name == "JungleBooze/Backdrop Card" && source.HasProperty("_MainTex"))
            {
                alpha = source.GetTexture("_MainTex");
            }
            else if (source.HasProperty("_BaseMap") && (source.IsKeywordEnabled("_ALPHATEST_ON") || (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f)))
            {
                // As Nature Lit: atlases cut on the albedo's A, other foliage on the R of a separate mask.
                bool fromBase = !source.HasProperty("_AlphaMap") || source.GetTexture("_AlphaMap") == null
                    || (source.HasProperty("_AlphaFromBaseA") && source.GetFloat("_AlphaFromBaseA") > 0.5f);
                alpha = fromBase ? source.GetTexture("_BaseMap") : source.GetTexture("_AlphaMap");
                m.SetFloat("_AlphaFromR", fromBase ? 0f : 1f);
                cutoff = source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f;
            }

            if (alpha != null)
            {
                m.SetTexture("_BaseMap", alpha);
                m.SetFloat("_UseAlpha", 1f);
                m.SetFloat("_Cutoff", cutoff);
            }

            made.Add(source, m);
            return m;
        }

        /// <summary>The budget category of a renderer's material, or null to leave it out (mist, shafts, mist bands).</summary>
        private static Category? CategoryOf(Renderer renderer, Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (renderer.transform.root.name == "Pista" || HasAncestor(renderer.transform, "Pista"))
            {
                return Category.Pista;
            }

            string shader = material.shader.name;
            string name = renderer.name;
            switch (shader)
            {
                case "JungleBooze/Backdrop Card":
                    if (material.name.Contains("MistBand"))
                    {
                        return null;
                    }

                    if (material.name.Contains("_Sky"))
                    {
                        return Category.Sky;
                    }

                    return material.name.Contains("JungleWall") ? Category.BackdropJungle : Category.BackdropFar;
                case "JungleBooze/Water":
                    return Category.Water;
                case "JungleBooze/Waterfall":
                    return Category.Falls;
                case "JungleBooze/Nature Lit":
                    break;
                default:
                    return null;
            }

            string label = LabelOf(name);
            // Smooth mossy mounds (the outcrops around the ledge) read as lawn: counted with the terrain.
            if (label == "Terrain" || (label == "Ledge" && material.name.Contains("Outcrop")))
            {
                return Category.Ground;
            }

            if (label == "Arch" && name.StartsWith("L2", System.StringComparison.Ordinal))
            {
                return Category.ArchSide;
            }

            if (label == "Wood")
            {
                return Category.Wood;
            }

            bool cut = material.IsKeywordEnabled("_ALPHATEST_ON") || (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f);
            if (cut || name.StartsWith("L1", System.StringComparison.Ordinal) || name.StartsWith("L3", System.StringComparison.Ordinal)
                || name.StartsWith("L4", System.StringComparison.Ordinal))
            {
                return Category.Foliage;
            }

            return Category.Rock;
        }

        private static bool HasAncestor(Transform t, string name)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The label of a merged renderer name "L2 Arch (Material)" → "Arch".</summary>
        public static string LabelOf(string rendererName)
        {
            int space = rendererName.IndexOf(' ');
            int paren = rendererName.LastIndexOf(" (", System.StringComparison.Ordinal);
            if (space < 0 || paren <= space)
            {
                return rendererName;
            }

            return rendererName.Substring(space + 1, paren - space - 1);
        }
    }
}
