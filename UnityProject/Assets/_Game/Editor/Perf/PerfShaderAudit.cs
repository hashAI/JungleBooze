using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.Perf
{
    /// <summary>
    /// Compiles the forward pass of every material variant used in a view for Metal / iOS (the exact keywords the
    /// material enables plus the pipeline's main-light shadow keywords) and counts the work in the generated Metal
    /// source: statements, scalar ALU ops, transcendentals, texture and shadow samples. Unity emits flat,
    /// already-optimised code, so these counts are a fair relative cost; the absolute GPU time comes from the
    /// calibrated model in docs/perf. The Metal source of each variant is written next to the report.
    /// </summary>
    public static class PerfShaderAudit
    {
        private static readonly string[] PipelineKeywords = { "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT_MEDIUM", "_SHADOWS_SOFT" };
        private static readonly Regex Assign = new Regex(@"^\s*(?:half|float|int|uint|bool)?[234]?\s*([A-Za-z_][A-Za-z0-9_\.\[\]]*?)(\.[xyzw]{1,4})?\s*(=|\+=|-=|\*=|/=)\s*(.+);\s*$", RegexOptions.Compiled);
        private static readonly Regex Transcendental = new Regex(@"\b(exp2|log2|pow|sqrt|rsqrt|sin|cos|exp|log|fast::\w+|precise::\w+|divide|normalize|length|distance)\s*\(|\s/\s", RegexOptions.Compiled);
        private static readonly Regex Sample = new Regex(@"\.(sample|read|gather)\s*\(", RegexOptions.Compiled);
        private static readonly Regex ShadowSample = new Regex(@"\.sample_compare\s*\(", RegexOptions.Compiled);

        /// <summary>Key: shader name + sorted keywords (what the SRP Batcher treats as one variant).</summary>
        public static string VariantKey(Material m)
        {
            string[] k = m.shaderKeywords.OrderBy(s => s, StringComparer.Ordinal).ToArray();
            return m.shader.name + (k.Length > 0 ? " [" + string.Join(" ", k) + "]" : string.Empty);
        }

        public static Dictionary<string, PerfShaderCost[]> Audit(IEnumerable<Material> materials, string outDir, StringBuilder report)
        {
            var result = new Dictionary<string, PerfShaderCost[]>();
            string codeDir = Path.Combine(outDir, "metal");
            Directory.CreateDirectory(codeDir);
            report.AppendLine("Shader variants used in the hero views, compiled for Metal / iOS (forward pass):");
            report.AppendLine("  variant | frag statements, scalar ops, transcendentals, samples (+shadow compares) | vert statements, scalar ops | keyword space");
            foreach (Material m in materials)
            {
                if (m == null || m.shader == null)
                {
                    continue;
                }

                string key = VariantKey(m);
                if (result.ContainsKey(key))
                {
                    continue;
                }

                PerfShaderCost[] costs = Compile(m, codeDir, out string space);
                result[key] = costs;
                PerfShaderCost f = costs[1];
                PerfShaderCost v = costs[0];
                report.Append("  ").Append(key).Append(" | ")
                    .Append(f.Success ? f.Lines + " st, " + f.ScalarOps + " ops, " + f.Transcendentals + " tr, " + f.Samples + " tex" + (f.ShadowSamples > 0 ? " +" + f.ShadowSamples + " shadow" : string.Empty) : "FAILED " + f.Message)
                    .Append(" | ").Append(v.Success ? v.Lines + " st, " + v.ScalarOps + " ops" : "FAILED " + v.Message)
                    .Append(" | ").AppendLine(space);
            }

            return result;
        }

        /// <summary>[0] vertex, [1] fragment cost of the material's forward pass.</summary>
        public static PerfShaderCost[] Compile(Material m, string codeDir, out string keywordSpace)
        {
            keywordSpace = string.Empty;
            ShaderData data = ShaderUtil.GetShaderData(m.shader);
            ShaderData.Subshader sub = data.ActiveSubshader ?? data.GetSerializedSubshader(0);
            int passIndex = 0;
            for (int i = 0; i < sub.PassCount; i++)
            {
                string mode = sub.GetPass(i).FindTagValue(new ShaderTagId("LightMode")).name;
                if (mode == "UniversalForward" || mode == "SRPDefaultUnlit" || string.IsNullOrEmpty(mode))
                {
                    passIndex = i;
                    break;
                }
            }

            ShaderData.Pass pass = sub.GetPass(passIndex);
            var pid = new PassIdentifier((uint)data.ActiveSubshaderIndex, (uint)passIndex);
            var declared = new HashSet<string>(StringComparer.Ordinal);
            int vertexSpace = 0;
            int fragmentSpace = 0;
            foreach (LocalKeyword k in ShaderUtil.GetPassKeywords(m.shader, in pid, ShaderType.Vertex))
            {
                declared.Add(k.name);
                vertexSpace++;
            }

            foreach (LocalKeyword k in ShaderUtil.GetPassKeywords(m.shader, in pid, ShaderType.Fragment))
            {
                declared.Add(k.name);
                fragmentSpace++;
            }

            keywordSpace = "pass '" + pass.Name + "', " + declared.Count + " keywords (vert " + vertexSpace + ", frag " + fragmentSpace + ")";
            var keywords = new List<string>();
            foreach (string k in m.shaderKeywords)
            {
                if (declared.Contains(k))
                {
                    keywords.Add(k);
                }
            }

            bool soft = false;
            foreach (string k in PipelineKeywords)
            {
                if (!declared.Contains(k))
                {
                    continue;
                }

                if (k == "_SHADOWS_SOFT" && soft)
                {
                    continue;
                }

                soft |= k.StartsWith("_SHADOWS_SOFT", StringComparison.Ordinal);
                keywords.Add(k);
            }

            string[] kw = keywords.ToArray();
            string safe = Regex.Replace(VariantKey(m), "[^A-Za-z0-9_]+", "_");
            return new[]
            {
                CompileStage(pass, ShaderType.Vertex, kw, Path.Combine(codeDir, safe + ".vert.metal")),
                CompileStage(pass, ShaderType.Fragment, kw, Path.Combine(codeDir, safe + ".frag.metal")),
            };
        }

        private static PerfShaderCost CompileStage(ShaderData.Pass pass, ShaderType stage, string[] keywords, string path)
        {
            if (!pass.HasShaderStage(stage))
            {
                return new PerfShaderCost(path, 0, 0, 0, 0, 0, false, "no stage");
            }

            ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, keywords, ShaderCompilerPlatform.Metal, BuildTarget.iOS, true);
            if (!info.Success || info.ShaderData == null || info.ShaderData.Length == 0)
            {
                string msg = info.Messages != null && info.Messages.Length > 0 ? info.Messages[0].message : "compile failed";
                return new PerfShaderCost(path, 0, 0, 0, 0, 0, false, msg);
            }

            string code = Encoding.UTF8.GetString(info.ShaderData);
            int start = code.IndexOf("#include <metal_stdlib>", StringComparison.Ordinal);
            if (start > 0)
            {
                code = code.Substring(start);
            }

            File.WriteAllText(path, code);
            return Count(path, code);
        }

        /// <summary>Counts the statements of the stage's entry function (the body after "vertex"/"fragment").</summary>
        public static PerfShaderCost Count(string key, string code)
        {
            string[] lines = code.Split('\n');
            bool inEntry = false;
            int depth = 0;
            int statements = 0;
            int ops = 0;
            int trans = 0;
            int samples = 0;
            int shadow = 0;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (!inEntry)
                {
                    if (line.StartsWith("vertex ", StringComparison.Ordinal) || line.StartsWith("fragment ", StringComparison.Ordinal))
                    {
                        inEntry = true;
                        depth = 0;
                    }
                    else
                    {
                        continue;
                    }
                }

                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                    }
                }

                if (!line.EndsWith(";", StringComparison.Ordinal) || line.StartsWith("return", StringComparison.Ordinal))
                {
                    continue;
                }

                shadow += ShadowSample.Matches(line).Count;
                samples += Sample.Matches(line).Count;
                trans += Transcendental.Matches(line).Count;
                Match a = Assign.Match(line);
                if (a.Success)
                {
                    statements++;
                    string swizzle = a.Groups[2].Value;
                    ops += swizzle.Length > 1 ? swizzle.Length - 1 : 1;
                }
            }

            return new PerfShaderCost(key, statements, ops, trans, samples, shadow, true, string.Empty);
        }
    }
}
