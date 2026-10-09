using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JungleBooze.App.HeroBasin;
using JungleBooze.App.LookTest;
using JungleBooze.Editor.HeroBasin;
using JungleBooze.Editor.LookTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.Perf
{
    /// <summary>
    /// Offline iPhone 12 readiness audit of the painterly hero basin (performance-engineer; results in
    /// docs/perf/2026-10-iphone12-readiness.md). Batch entry (needs a GPU, so no -nographics):
    ///
    ///   Unity -batchmode -projectPath UnityProject -executeMethod JungleBooze.Editor.Perf.PerfAudit.Run
    ///         [-jbPerfOut dir] [-jbPerfScene Assets/...unity] [-jbPerfWidth 1918 -jbPerfHeight 886] -quit
    ///
    /// For the landscape and portrait keyframe cameras at the iPhone 12 internal resolution (1.7 MP High tier) it
    /// writes the overdraw heat map and a per-draw table (category, budget layer, variant, triangles, shaded fragments,
    /// Metal ALU/sample cost of the variant), then a shader variant report, a texture audit and a mesh audit, and a
    /// summary (report.txt). The scene is opened read-only and never saved.
    /// </summary>
    public static class PerfAudit
    {
        public const string HeroScenePath = "Assets/_Game/Scenes/HeroBasin_Painterly.unity";
        public const string HeroConfigPath = "Assets/_Game/Config/HeroBasin/HeroBasinConfig_Painterly.asset";
        private const string LogPrefix = "[JungleBooze perf] ";

        public static void Run()
        {
            int code;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string outDir = Arg(args, "-jbPerfOut") ?? "/tmp/junglebooze-perf";
                string scene = Arg(args, "-jbPerfScene") ?? HeroScenePath;
                int w = int.Parse(Arg(args, "-jbPerfWidth") ?? "1918", CultureInfo.InvariantCulture);
                int h = int.Parse(Arg(args, "-jbPerfHeight") ?? "886", CultureInfo.InvariantCulture);
                code = Audit(outDir, scene, w, h);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Audit failed: " + exception);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        public static int Audit(string outDir, string scenePath, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(LogPrefix + "No GPU device: run Unity without -nographics.");
                return 2;
            }

            if (!File.Exists(scenePath))
            {
                Debug.LogError(LogPrefix + "Scene not found: " + scenePath + " (build it: HeroBasinBatch.BuildScene -jbHeroStyle painterly).");
                return 2;
            }

            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var hero = AssetDatabase.LoadAssetAtPath<HeroBasinConfigAsset>(HeroConfigPath);
            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (hero == null || camera == null)
            {
                Debug.LogError(LogPrefix + "Hero config or camera missing.");
                return 2;
            }

            var report = new StringBuilder();
            report.AppendLine("AURELIA iPhone 12 readiness audit (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ", editor " +
                              SystemInfo.graphicsDeviceName + ", Unity " + Application.unityVersion + ")");
            report.AppendLine("Scene " + scenePath + ", internal resolution " + width + "x" + height + " (" +
                              (width * (double)height / 1e6).ToString("0.00", CultureInfo.InvariantCulture) + " MP).");

            var allDraws = new List<PerfDrawSample>();
            var renderers = new HashSet<Renderer>();
            using (var overdraw = new PerfOverdraw())
            {
                if (!overdraw.Ready)
                {
                    Debug.LogError(LogPrefix + "Shader Hidden/JungleBooze/Perf Overdraw not found.");
                    return 2;
                }

                foreach (bool portrait in new[] { false, true })
                {
                    int w = portrait ? height : width;
                    int h = portrait ? width : height;
                    HeroBasinBuilder.PoseCamera(camera, hero, portrait, (float)w / h);
                    string view = portrait ? "portrait" : "landscape";
                    List<PerfDrawSample> draws = overdraw.Measure(camera, w, h);
                    allDraws.AddRange(draws);
                    foreach (PerfDrawSample d in draws)
                    {
                        if (d.Renderer != null)
                        {
                            renderers.Add(d.Renderer);
                        }
                    }

                    WriteHeat(overdraw, Path.Combine(outDir, "overdraw_" + view + ".png"));
                    Summarize(view, draws, overdraw, report);
                    report.AppendLine("  Budget tally (LookTestFrameBudget):");
                    var look = AssetDatabase.LoadAssetAtPath<LookTestConfigAsset>("Assets/_Game/Config/HeroBasin/HeroBasinLook_Painterly.asset");
                    if (look != null)
                    {
                        report.Append(LookTestFrameBudget.Measure(camera, look));
                    }

                    PerDrawCsv(Path.Combine(outDir, "draws_" + view + ".csv"), draws, w * (double)h);
                }

                camera.ResetAspect();
            }

            report.AppendLine();
            IEnumerable<Material> materials = allDraws.Where(d => d.Material != null).Select(d => d.Material).Distinct();
            Dictionary<string, PerfShaderCost[]> costs = PerfShaderAudit.Audit(materials, outDir, report);
            AppendCosts(Path.Combine(outDir, "draws_landscape.csv"), costs, allDraws);
            AppendCosts(Path.Combine(outDir, "draws_portrait.csv"), costs, allDraws);

            // Pista (skinned) and every scene renderer, not only the ones in view.
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                renderers.Add(r);
            }

            PerfAssetAudit.Audit(renderers, outDir, report);
            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
            Debug.Log(LogPrefix + "Audit written to " + outDir + Environment.NewLine + report);
            return 0;
        }

        private static void Summarize(string view, List<PerfDrawSample> draws, PerfOverdraw overdraw, StringBuilder report)
        {
            double pixels = overdraw.Width * (double)overdraw.Height;
            report.AppendLine().AppendLine("View " + view + " (" + overdraw.Width + "x" + overdraw.Height + ")");
            string[] cats = { PerfDrawSample.Opaque, PerfDrawSample.Cutout, PerfDrawSample.Sky, PerfDrawSample.Blended };
            double total = 0.0;
            foreach (string cat in cats)
            {
                List<PerfDrawSample> list = draws.Where(d => d.Category == cat).ToList();
                double frags = list.Sum(d => d.Fragments);
                total += frags;
                report.Append("  ").Append(cat.PadRight(8)).Append(" draws ").Append(list.Count(d => d.Renderer != null).ToString().PadLeft(3))
                    .Append("  shaded fragments/pixel ").Append((frags / pixels).ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            }

            report.Append("  total shaded fragments/pixel ").Append((total / pixels).ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');

            // Per-pixel distribution of all shaded fragments.
            var perPixel = new float[overdraw.Width * overdraw.Height];
            var blended = new float[perPixel.Length];
            for (int i = 0; i < perPixel.Length; i++)
            {
                perPixel[i] = overdraw.Heat[0][i] + overdraw.Heat[1][i] + overdraw.Heat[2][i] + overdraw.Heat[3][i];
                blended[i] = overdraw.Heat[3][i];
            }

            Array.Sort(perPixel);
            Array.Sort(blended);
            report.Append("  per pixel (all): p50 ").Append(Pct(perPixel, 0.5)).Append("  p90 ").Append(Pct(perPixel, 0.9)).Append("  p99 ").Append(Pct(perPixel, 0.99))
                .Append("  max ").Append(perPixel[perPixel.Length - 1].ToString("0", CultureInfo.InvariantCulture)).Append('\n');
            report.Append("  per pixel (blended only): p50 ").Append(Pct(blended, 0.5)).Append("  p90 ").Append(Pct(blended, 0.9)).Append("  p99 ").Append(Pct(blended, 0.99))
                .Append("  max ").Append(blended[blended.Length - 1].ToString("0", CultureInfo.InvariantCulture)).Append('\n');

            report.AppendLine("  by budget layer (shaded fragments/pixel, draws, triangles):");
            foreach (IGrouping<string, PerfDrawSample> g in draws.GroupBy(d => d.Layer).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                report.Append("    ").Append(g.Key.PadRight(4)).Append(' ').Append((g.Sum(d => d.Fragments) / pixels).ToString("0.00", CultureInfo.InvariantCulture).PadLeft(6))
                    .Append("  draws ").Append(g.Count(d => d.Renderer != null).ToString().PadLeft(3)).Append("  tris ").Append(g.Sum(d => d.Triangles).ToString("N0", CultureInfo.InvariantCulture)).Append('\n');
            }

            report.AppendLine("  top 12 draws by shaded fragments:");
            foreach (PerfDrawSample d in draws.OrderByDescending(d => d.Fragments).Take(12))
            {
                report.Append("    ").Append((d.Fragments / pixels).ToString("0.00", CultureInfo.InvariantCulture).PadLeft(5)).Append(" px/px  ")
                    .Append(d.Category.PadRight(7)).Append(' ').Append(d.Name).Append(" / ").Append(d.MaterialName).Append(" (").Append(d.ShaderName).Append(")\n");
            }

            // SRP Batcher: a new SetPass when the shader variant changes along the submission order.
            int setPass = 0;
            string last = null;
            foreach (PerfDrawSample d in draws.Where(d => d.Material != null))
            {
                string key = PerfShaderAudit.VariantKey(d.Material);
                if (key != last)
                {
                    setPass++;
                    last = key;
                }
            }

            int variants = draws.Where(d => d.Material != null).Select(d => PerfShaderAudit.VariantKey(d.Material)).Distinct().Count();
            report.Append("  draws ").Append(draws.Count(d => d.Renderer != null)).Append(" (+ sky, shadow, post), shader variants ").Append(variants)
                .Append(", SetPass estimate ").Append(setPass).Append(" (+ ~8 shadow/sky/post)\n");
        }

        private static string Pct(float[] sorted, double p)
        {
            int i = Mathf.Clamp((int)(p * (sorted.Length - 1)), 0, sorted.Length - 1);
            return sorted[i].ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static void PerDrawCsv(string path, List<PerfDrawSample> draws, double pixels)
        {
            var csv = new StringBuilder("order,category,layer,renderer,material,variant,queue,triangles,fragments,frag_per_pixel,covered_pct\n");
            int order = 0;
            foreach (PerfDrawSample d in draws)
            {
                csv.Append(order++).Append(',').Append(d.Category).Append(',').Append(d.Layer).Append(',').Append(Clean(d.Name)).Append(',')
                    .Append(Clean(d.MaterialName)).Append(',').Append(d.Material != null ? Clean(PerfShaderAudit.VariantKey(d.Material)) : "sky").Append(',')
                    .Append(d.Material != null ? d.Material.renderQueue : 1000).Append(',').Append(d.Triangles).Append(',')
                    .Append(d.Fragments.ToString("0", CultureInfo.InvariantCulture)).Append(',').Append((d.Fragments / pixels).ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append((100.0 * d.Covered / pixels).ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            }

            File.WriteAllText(path, csv.ToString());
        }

        /// <summary>Adds the variant's fragment and vertex cost columns to a per-draw table.</summary>
        private static void AppendCosts(string path, Dictionary<string, PerfShaderCost[]> costs, List<PerfDrawSample> draws)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string[] lines = File.ReadAllLines(path);
            var outText = new StringBuilder(lines[0] + ",frag_alu_slots,frag_samples,frag_shadow_samples,vert_alu_slots\n");
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cols = lines[i].Split(',');
                string variant = cols.Length > 5 ? cols[5] : string.Empty;
                PerfShaderCost[] c = costs.FirstOrDefault(p => Clean(p.Key) == variant).Value;
                outText.Append(lines[i]).Append(',');
                if (c != null)
                {
                    outText.Append(c[1].AluSlots).Append(',').Append(c[1].Samples).Append(',').Append(c[1].ShadowSamples).Append(',').Append(c[0].AluSlots);
                }
                else
                {
                    outText.Append(",,,");
                }

                outText.Append('\n');
            }

            File.WriteAllText(path, outText.ToString());
        }

        private static string Clean(string s)
        {
            return s.Replace(',', ';');
        }

        private static void WriteHeat(PerfOverdraw overdraw, string path)
        {
            int w = overdraw.Width;
            int h = overdraw.Height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = overdraw.Heat[0][i] + overdraw.Heat[1][i] + overdraw.Heat[2][i] + overdraw.Heat[3][i];
                pixels[i] = Ramp(v);
            }

            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>0 black, 1 dark blue, 2 teal, 3 green, 4 yellow, 6 orange, 8 red, 12+ white.</summary>
        private static Color32 Ramp(float v)
        {
            Color[] stops = { Color.black, new Color(0.05f, 0.1f, 0.45f), new Color(0f, 0.55f, 0.6f), new Color(0.1f, 0.75f, 0.2f), new Color(0.95f, 0.9f, 0.1f), new Color(1f, 0.5f, 0f), new Color(0.9f, 0.05f, 0.05f), Color.white };
            float[] at = { 0f, 1f, 2f, 3f, 4f, 6f, 8f, 12f };
            for (int i = 1; i < at.Length; i++)
            {
                if (v <= at[i])
                {
                    return Color.Lerp(stops[i - 1], stops[i], (v - at[i - 1]) / (at[i] - at[i - 1]));
                }
            }

            return Color.white;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
