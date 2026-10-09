using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Overdraw and fragment-cost measurement of one camera view (ADR 0011), modelled on how an Apple TBDR GPU (A14,
    /// iPhone 12) shades: opaque surfaces get hidden-surface removal, so only their visible pixels are shaded;
    /// alpha-tested surfaces (clip / alpha-to-coverage) defeat it, so every fragment in front of the opaque depth is
    /// shaded; blended surfaces are shaded wherever they pass the depth test of everything opaque. Each renderer is
    /// drawn with a counting shader into a float target and read back; the sky counts once per uncovered pixel.
    /// GPU time is a model: fragments x an estimated per-fragment cost of the renderer's shader (ALU ops at FP16
    /// rate, texture samples at the bilinear rate) on A14 throughput, plus fixed passes (shadow map, post, resolve).
    /// It ranks layers and catches regressions; the real number comes from an Xcode GPU capture on the device.
    /// Editor only; allocates.
    /// </summary>
    internal static class HeroBasinOverdraw
    {
        private const string ShaderPath = "Assets/_Game/Editor/HeroBasin/OverdrawCount.shader";

        // A14 GPU model: 4 cores x 128 ALUs at 1.278 GHz, FP16 at twice the FP32 rate; 4 x 8 bilinear samples per clock.
        private const double AluOpsPerSecond = 4.0 * 128.0 * 1.278e9 * 2.0;
        private const double TexelsPerSecond = 4.0 * 8.0 * 1.278e9;

        /// <summary>Fixed GPU passes at native 2532x1170 (estimates): shadow map, post stack, MSAA resolve, UI.</summary>
        public const double FixedPassesMs = 2.6;

        private static RenderTexture DepthTarget;

        private static readonly Dictionary<Renderer, Mesh> MeshOf = new Dictionary<Renderer, Mesh>();

        /// <summary>Draws a renderer's submesh with an explicit world matrix (DrawRenderer leaves per-draw constants unset here).</summary>
        private static void Draw(CommandBuffer cmd, Renderer r, Material m, int submesh, int pass)
        {
            Matrix4x4 matrix = r is SkinnedMeshRenderer ? Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one) : r.localToWorldMatrix;
            cmd.DrawMesh(MeshOf[r], matrix, m, submesh, pass);
        }

        private enum Kind
        {
            Opaque,
            AlphaTest,
            Blended,
        }

        private sealed class Entry
        {
            public string Name;
            public Kind Kind;
            public string Shader;
            public double Fragments;
            public double Ms;
        }

        /// <summary>
        /// Measures the view of <paramref name="camera"/> at <paramref name="width"/> x <paramref name="height"/>
        /// (counts are scaled to <paramref name="nativeWidth"/> x <paramref name="nativeHeight"/>), writes a heat map
        /// to <paramref name="heatMapPath"/> and returns the report.
        /// </summary>
        public static string Measure(Camera camera, int width, int height, int nativeWidth, int nativeHeight, string heatMapPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                return "Overdraw: count shader missing (" + ShaderPath + ")." + System.Environment.NewLine;
            }

            var draws = new List<(Renderer renderer, int submesh, Material source, Kind kind)>();
            var baked = new List<Mesh>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !(r is MeshRenderer || r is SkinnedMeshRenderer))
                {
                    continue;
                }

                if (r is SkinnedMeshRenderer skinned)
                {
                    var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    skinned.BakeMesh(mesh, true);
                    MeshOf[r] = mesh;
                    baked.Add(mesh);
                }
                else
                {
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    MeshOf[r] = filter.sharedMesh;
                }

                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length && i < MeshOf[r].subMeshCount; i++)
                {
                    if (materials[i] != null)
                    {
                        draws.Add((r, i, materials[i], KindOf(materials[i])));
                    }
                }
            }

            var materialsFor = new Dictionary<Material, Material>();
            Material Counter(Material source, bool clip)
            {
                if (!materialsFor.TryGetValue(source, out Material m))
                {
                    m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    Texture tex = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    if (tex != null)
                    {
                        m.SetTexture("_MainTex", tex);
                    }

                    m.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
                    bool card = source.shader != null && source.shader.name == "JungleBooze/Atmos Card";
                    m.SetFloat("_Card", card ? 1f : 0f);
                    m.SetFloat("_MistPull", card && source.IsKeywordEnabled("_MIST") ? 1f : 0f);
                    materialsFor.Add(source, m);
                }

                m.SetFloat("_Clip", clip ? 1f : 0f);
                return m;
            }

            var target = RenderTexture.GetTemporary(new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 0) { msaaSamples = 1, sRGB = false });
            DepthTarget = RenderTexture.GetTemporary(new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 0) { msaaSamples = 1, sRGB = false });
            DepthTarget.filterMode = FilterMode.Point;
            var readback = new Texture2D(width, height, TextureFormat.RFloat, false, true);
            Matrix4x4 view = camera.worldToCameraMatrix;
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var entries = new List<Entry>();
            float scale = (float)nativeWidth * nativeHeight / (width * height);
            var totalMap = new float[width * height];
            try
            {
                // Every count re-renders its depth first in the same command buffer (depth is not kept between
                // buffers on this path). Alpha-tested layers count against opaque depth only (no HSR help); opaque
                // (visible after HSR) and blended layers count against opaque + clipped alpha-tested depth.
                void Prepass(CommandBuffer c, bool withAlphaTest)
                {
                    ToDepth(c);
                    foreach (var d in draws)
                    {
                        if (d.kind == Kind.Opaque || (withAlphaTest && d.kind == Kind.AlphaTest))
                        {
                            Draw(c, d.renderer, Counter(d.source, d.kind == Kind.AlphaTest), d.submesh, 0);
                        }
                    }

                    ToCount(c, target);
                }

                foreach (var d in draws)
                {
                    CommandBuffer cmd = Begin(camera, target, view, proj, true);
                    Prepass(cmd, d.kind != Kind.AlphaTest);
                    entries.Add(CountOne(cmd, target, d.renderer, d.submesh, d.source, Counter(d.source, d.kind == Kind.AlphaTest), d.kind, readback, scale, totalMap));
                }

                // 3. Sky: once per pixel that no opaque or alpha-tested surface covers.
                CommandBuffer skyCmd = Begin(camera, target, view, proj, true);
                Prepass(skyCmd, true);
                double skyFragments = SkyPixels(camera, skyCmd, target, readback) * scale;
                entries.Add(new Entry { Name = "Sky (skybox, uncovered pixels)", Kind = Kind.Opaque, Shader = "JungleBooze/Sky", Fragments = skyFragments, Ms = Cost("JungleBooze/Sky", skyFragments) });
            }
            finally
            {
                foreach (Material m in materialsFor.Values)
                {
                    Object.DestroyImmediate(m);
                }
            }

            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(DepthTarget);
            DepthTarget = null;
            Object.DestroyImmediate(readback);
            foreach (Mesh m in baked)
            {
                Object.DestroyImmediate(m);
            }

            MeshOf.Clear();
            WriteHeatMap(totalMap, width, height, heatMapPath);
            return Format(entries, nativeWidth * (double)nativeHeight, totalMap, width, height);
        }

        private static Kind KindOf(Material m)
        {
            int queue = m.renderQueue;
            if (queue > 2500)
            {
                return Kind.Blended;
            }

            return queue >= 2450 || m.IsKeywordEnabled("_ALPHATEST_ON") ? Kind.AlphaTest : Kind.Opaque;
        }

        private static CommandBuffer Begin(Camera camera, RenderTexture target, Matrix4x4 view, Matrix4x4 proj, bool clearDepth)
        {
            var cmd = new CommandBuffer { name = "Overdraw" };
            cmd.SetViewProjectionMatrices(view, proj);
            cmd.SetGlobalVector("_CountCameraPos", camera.transform.position);
            cmd.SetGlobalVector("_CountCameraFwd", camera.transform.forward);
            cmd.SetGlobalVector("_CountScreen", new Vector4(target.width, target.height, 0f, 0f));
            if (clearDepth)
            {
                cmd.SetRenderTarget(DepthTarget);
                cmd.ClearRenderTarget(false, true, new Color(1e9f, 0f, 0f, 0f));
            }

            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.clear);
            return cmd;
        }

        /// <summary>Switches the buffer to the depth target (prepass draws go there), then back with <see cref="ToCount"/>.</summary>
        private static void ToDepth(CommandBuffer cmd)
        {
            cmd.SetRenderTarget(DepthTarget);
        }

        private static void ToCount(CommandBuffer cmd, RenderTexture target)
        {
            cmd.SetGlobalTexture("_CountDepth", DepthTarget);
            cmd.SetRenderTarget(target);
        }

        private static void Run(CommandBuffer cmd)
        {
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        private static Entry CountOne(CommandBuffer cmd, RenderTexture target, Renderer renderer, int submesh, Material source, Material counter, Kind kind,
            Texture2D readback, float scale, float[] totalMap)
        {
            Draw(cmd, renderer, counter, submesh, 1);
            Run(cmd);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            readback.Apply(false);
            RenderTexture.active = previous;
            float[] data = readback.GetRawTextureData<float>().ToArray();
            double sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i];
                totalMap[i] += data[i];
            }

            string shader = source.shader != null ? source.shader.name : "?";
            double fragments = sum * scale;
            return new Entry { Name = renderer.name + (submesh > 0 ? " #" + submesh : string.Empty), Kind = kind, Shader = shader, Fragments = fragments, Ms = Cost(shader, fragments) };
        }

        private static double SkyPixels(Camera camera, CommandBuffer cmd, RenderTexture target, Texture2D readback)
        {
            // After the depth prepass, a far-plane quad counted with the depth test survives exactly where the
            // skybox would be drawn.
            float far = camera.farClipPlane * 0.995f;
            Vector3 bl = HeroBasinCards.AtDepth(camera, -0.01f, 1.01f, far);
            Vector3 br = HeroBasinCards.AtDepth(camera, 1.01f, 1.01f, far);
            Vector3 tl = HeroBasinCards.AtDepth(camera, -0.01f, -0.01f, far);
            Vector3 tr = HeroBasinCards.AtDepth(camera, 1.01f, -0.01f, far);
            var quad = new Mesh { vertices = new[] { bl, br, tl, tr }, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
            quad.RecalculateBounds();
            var sky = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)) { hideFlags = HideFlags.HideAndDontSave };
            cmd.DrawMesh(quad, Matrix4x4.identity, sky, 0, 1);
            Run(cmd);
            Object.DestroyImmediate(quad);
            Object.DestroyImmediate(sky);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            readback.Apply(false);
            RenderTexture.active = previous;
            double sum = 0;
            foreach (float v in readback.GetRawTextureData<float>())
            {
                sum += v;
            }

            return sum;
        }

        /// <summary>Estimated per-fragment cost of a shader on A14 (ms for <paramref name="fragments"/> fragments).</summary>
        private static double Cost(string shader, double fragments)
        {
            Vector2 c = PerFragment(shader);
            return fragments * (c.x / AluOpsPerSecond + c.y / TexelsPerSecond) * 1000.0;
        }

        /// <summary>(ALU ops, texture samples) per fragment, estimated from each project shader's fragment code.</summary>
        private static Vector2 PerFragment(string shader)
        {
            switch (shader)
            {
                case "JungleBooze/Nature Lit": return new Vector2(130f, 4f); // painterly ramp, moss, AO, fog, shadow
                case "JungleBooze/Water": return new Vector2(160f, 7f); // 2 normals, 3 foam, shadow, fog
                case "JungleBooze/Waterfall": return new Vector2(120f, 5f);
                case "JungleBooze/Painted Sheet": return new Vector2(45f, 2f);
                case "JungleBooze/Painted Card": return new Vector2(30f, 1f);
                case "JungleBooze/Atmos Card": return new Vector2(70f, 2f);
                case "JungleBooze/Backdrop Card": return new Vector2(45f, 2f);
                case "JungleBooze/Character Painterly": return new Vector2(170f, 5f);
                case "JungleBooze/Sky": return new Vector2(35f, 1f);
                default: return new Vector2(100f, 3f);
            }
        }

        private static string Format(List<Entry> entries, double nativePixels, float[] totalMap, int width, int height)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            var b = new StringBuilder();
            entries.Sort((x, y) => y.Ms.CompareTo(x.Ms));
            double[] fr = new double[3];
            double[] ms = new double[3];
            double all = 0;
            double allMs = 0;
            foreach (Entry e in entries)
            {
                fr[(int)e.Kind] += e.Fragments;
                ms[(int)e.Kind] += e.Ms;
                all += e.Fragments;
                allMs += e.Ms;
            }

            int heavy = 0;
            double blendedLayers = 0;
            for (int i = 0; i < totalMap.Length; i++)
            {
                heavy += totalMap[i] >= 4f ? 1 : 0;
            }

            blendedLayers = fr[(int)Kind.Blended] / nativePixels;
            b.AppendLine("Overdraw (A14 TBDR model; fragments shaded per screen pixel at native resolution):");
            b.AppendLine(string.Format(ci, "  total {0:0.00}x  = opaque (HSR, visible only) {1:0.00}x + alpha-tested {2:0.00}x + blended {3:0.00}x;  pixels with >= 4 counted layers {4:0.0}%",
                all / nativePixels, fr[0] / nativePixels, fr[1] / nativePixels, blendedLayers, 100.0 * heavy / totalMap.Length));
            b.AppendLine(string.Format(ci, "  est. fragment GPU {0:0.00} ms (opaque {1:0.00}, alpha-tested {2:0.00}, blended {3:0.00}) + fixed passes {4:0.0} ms = {5:0.00} ms (budget 12 ms)",
                allMs, ms[0], ms[1], ms[2], FixedPassesMs, allMs + FixedPassesMs));
            b.AppendLine("  per layer (est. ms, layers/pixel, shader):");
            foreach (Entry e in entries)
            {
                if (e.Fragments / nativePixels < 0.005)
                {
                    continue;
                }

                b.AppendLine(string.Format(ci, "    {0,-9} {1,6:0.00} ms  {2,5:0.00}x  {3,-60} {4}", e.Kind, e.Ms, e.Fragments / nativePixels, Trim(e.Name, 60), e.Shader));
            }

            return b.ToString();
        }

        private static string Trim(string s, int n)
        {
            return s.Length <= n ? s : s.Substring(0, n - 1) + "~";
        }

        private static void WriteHeatMap(float[] map, int width, int height, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[map.Length];
            Color32[] ramp =
            {
                new Color32(10, 10, 30, 255), new Color32(20, 60, 160, 255), new Color32(20, 160, 90, 255), new Color32(230, 220, 40, 255),
                new Color32(240, 120, 20, 255), new Color32(220, 20, 20, 255), new Color32(255, 255, 255, 255),
            };
            for (int i = 0; i < map.Length; i++)
            {
                int k = Mathf.Clamp(Mathf.RoundToInt(map[i]), 0, ramp.Length - 1);
                pixels[i] = ramp[k];
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
