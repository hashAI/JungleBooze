using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.Perf
{
    /// <summary>
    /// Texture and mesh audit of the open scene for iOS: every texture its materials use (iOS format from the
    /// importer's iPhone settings, size, mips, read/write, estimated GPU memory) and every mesh its renderers use
    /// (vertices, triangles, vertex stride, index format, read/write copy, estimated memory). Writes textures.csv and
    /// meshes.csv and appends a summary with the flagged items to <c>report</c>.
    /// </summary>
    public static class PerfAssetAudit
    {
        private const string IosPlatform = "iPhone";

        public static void Audit(IEnumerable<Renderer> renderers, string outDir, StringBuilder report)
        {
            var materials = new HashSet<Material>();
            var meshes = new HashSet<Mesh>();
            foreach (Renderer r in renderers)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null)
                    {
                        materials.Add(m);
                    }
                }

                Mesh mesh = PerfOverdraw.MeshOf(r);
                if (mesh != null)
                {
                    meshes.Add(mesh);
                }
            }

            AuditTextures(materials, outDir, report);
            AuditMeshes(meshes, outDir, report);
        }

        private static void AuditTextures(IEnumerable<Material> materials, string outDir, StringBuilder report)
        {
            var textures = new HashSet<Texture>();
            foreach (Material m in materials)
            {
                foreach (int id in m.GetTexturePropertyNameIDs())
                {
                    Texture t = m.GetTexture(id);
                    if (t != null)
                    {
                        textures.Add(t);
                    }
                }
            }

            // Globals the scene's shaders read (sky cubemap, LUT, dapple) are found through the scene's dependencies.
            foreach (string dep in AssetDatabase.GetDependencies(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, true))
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(dep) is Type type && typeof(Texture).IsAssignableFrom(type))
                {
                    Texture t = AssetDatabase.LoadAssetAtPath<Texture>(dep);
                    if (t != null)
                    {
                        textures.Add(t);
                    }
                }
            }

            var csv = new StringBuilder("path,width,height,ios_format,bpp,mips,readable,srgb,mb,flags\n");
            double totalMb = 0.0;
            var flagged = new List<string>();
            var byFormat = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (Texture t in textures.OrderBy(x => AssetDatabase.GetAssetPath(x), StringComparer.Ordinal))
            {
                string path = AssetDatabase.GetAssetPath(t);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                string format = "runtime " + t.graphicsFormat;
                double bpp = 32.0;
                bool mips = t.mipmapCount > 1;
                bool readable = t.isReadable;
                bool srgb = !GraphicsFormatUtilityIsLinear(t);
                int w = t.width;
                int h = t.height;
                if (importer != null)
                {
                    TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings(IosPlatform);
                    TextureImporterFormat f = ios.overridden ? ios.format : importer.GetAutomaticFormat(IosPlatform);
                    format = f.ToString();
                    bpp = BitsPerPixel(f);
                    int max = ios.overridden ? ios.maxTextureSize : importer.maxTextureSize;
                    float shrink = Mathf.Min(1f, (float)max / Mathf.Max(w, h));
                    w = Mathf.Max(1, Mathf.RoundToInt(w * shrink));
                    h = Mathf.Max(1, Mathf.RoundToInt(h * shrink));
                    mips = importer.mipmapEnabled;
                    readable = importer.isReadable;
                    srgb = importer.sRGBTexture;
                }

                int faces = t.dimension == TextureDimension.Cube ? 6 : 1;
                int depth3d = t is Texture3D t3 ? t3.depth : 1;
                double mb = (double)w * h * faces * depth3d * bpp / 8.0 * (mips ? 4.0 / 3.0 : 1.0) / (1024.0 * 1024.0);
                if (readable)
                {
                    mb *= 2.0;
                }

                var flags = new List<string>();
                if (!format.StartsWith("ASTC", StringComparison.Ordinal) && !path.Contains("/Grade/") && importer != null)
                {
                    flags.Add("not-ASTC");
                }

                if (readable)
                {
                    flags.Add("read/write (CPU copy)");
                }

                if (!mips && importer != null && importer.textureType != TextureImporterType.Sprite && !path.Contains("/Grade/") && faces == 1)
                {
                    flags.Add("no mips");
                }

                if (Mathf.Max(w, h) > 2048)
                {
                    flags.Add(">2048");
                }

                totalMb += mb;
                byFormat.TryGetValue(format, out double sum);
                byFormat[format] = sum + mb;
                string flagText = string.Join(" ", flags);
                if (flags.Count > 0)
                {
                    flagged.Add(path + " (" + w + "x" + h + " " + format + ", " + mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB): " + flagText);
                }

                csv.Append(path).Append(',').Append(w).Append(',').Append(h).Append(',').Append(format).Append(',')
                    .Append(bpp.ToString("0.##", CultureInfo.InvariantCulture)).Append(',').Append(mips ? 1 : 0).Append(',').Append(readable ? 1 : 0).Append(',')
                    .Append(srgb ? 1 : 0).Append(',').Append(mb.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(flagText).Append('\n');
            }

            File.WriteAllText(Path.Combine(outDir, "textures.csv"), csv.ToString());
            report.AppendLine().Append("Textures used by the scene: ").Append(textures.Count).Append(", estimated iOS GPU memory ")
                .Append(totalMb.ToString("0.0", CultureInfo.InvariantCulture)).AppendLine(" MB (with mips, read/write copies doubled).");
            foreach (KeyValuePair<string, double> pair in byFormat.OrderByDescending(p => p.Value))
            {
                report.Append("  ").Append(pair.Key).Append(": ").Append(pair.Value.ToString("0.0", CultureInfo.InvariantCulture)).AppendLine(" MB");
            }

            report.Append("  Flagged (").Append(flagged.Count).AppendLine("):");
            foreach (string f in flagged)
            {
                report.Append("    ").AppendLine(f);
            }
        }

        private static bool GraphicsFormatUtilityIsLinear(Texture t)
        {
            return !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat);
        }

        private static void AuditMeshes(IEnumerable<Mesh> meshes, string outDir, StringBuilder report)
        {
            var csv = new StringBuilder("mesh,path,vertices,triangles,submeshes,stride_bytes,index_bits,readable,mb,attributes\n");
            double totalMb = 0.0;
            double readableMb = 0.0;
            long totalTris = 0;
            long totalVerts = 0;
            int readableCount = 0;
            var rows = new List<(double mb, string text)>();
            foreach (Mesh mesh in meshes)
            {
                int stride = 0;
                for (int s = 0; s < mesh.vertexBufferCount; s++)
                {
                    stride += mesh.GetVertexBufferStride(s);
                }

                long tris = 0;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    tris += mesh.GetTopology(s) == MeshTopology.Triangles ? (long)mesh.GetIndexCount(s) / 3 : 0;
                }

                int indexBytes = mesh.indexFormat == IndexFormat.UInt32 ? 4 : 2;
                double gpuMb = ((double)mesh.vertexCount * stride + tris * 3.0 * indexBytes) / (1024.0 * 1024.0);
                double mb = mesh.isReadable ? gpuMb * 2.0 : gpuMb;
                totalMb += mb;
                totalTris += tris;
                totalVerts += mesh.vertexCount;
                if (mesh.isReadable)
                {
                    readableMb += gpuMb;
                    readableCount++;
                }

                string attributes = string.Join(" ", mesh.GetVertexAttributes().Select(a => a.attribute + ":" + a.format + "x" + a.dimension));
                string path = AssetDatabase.GetAssetPath(mesh);
                rows.Add((mb, mesh.name + "," + path + "," + mesh.vertexCount + "," + tris + "," + mesh.subMeshCount + "," + stride + "," + indexBytes * 8 + "," +
                              (mesh.isReadable ? 1 : 0) + "," + mb.ToString("0.00", CultureInfo.InvariantCulture) + "," + attributes));
            }

            foreach ((double _, string text) in rows.OrderByDescending(r => r.mb))
            {
                csv.Append(text).Append('\n');
            }

            File.WriteAllText(Path.Combine(outDir, "meshes.csv"), csv.ToString());
            report.AppendLine().Append("Meshes used by the scene: ").Append(rows.Count).Append(", ").Append(totalVerts.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" vertices, ").Append(totalTris.ToString("N0", CultureInfo.InvariantCulture)).Append(" triangles, estimated memory ")
                .Append(totalMb.ToString("0.0", CultureInfo.InvariantCulture)).Append(" MB; ").Append(readableCount).Append(" meshes keep a read/write CPU copy (")
                .Append(readableMb.ToString("0.0", CultureInfo.InvariantCulture)).AppendLine(" MB that could be freed).");
        }

        /// <summary>Bits per pixel of an importer format on iOS (RGB24 is padded to RGBA32 on Metal).</summary>
        public static double BitsPerPixel(TextureImporterFormat f)
        {
            switch (f)
            {
                case TextureImporterFormat.ASTC_4x4:
                case TextureImporterFormat.ASTC_HDR_4x4:
                    return 8.0;
                case TextureImporterFormat.ASTC_5x5:
                case TextureImporterFormat.ASTC_HDR_5x5:
                    return 128.0 / 25.0;
                case TextureImporterFormat.ASTC_6x6:
                case TextureImporterFormat.ASTC_HDR_6x6:
                    return 128.0 / 36.0;
                case TextureImporterFormat.ASTC_8x8:
                case TextureImporterFormat.ASTC_HDR_8x8:
                    return 2.0;
                case TextureImporterFormat.ASTC_10x10:
                case TextureImporterFormat.ASTC_HDR_10x10:
                    return 1.28;
                case TextureImporterFormat.ASTC_12x12:
                case TextureImporterFormat.ASTC_HDR_12x12:
                    return 128.0 / 144.0;
                case TextureImporterFormat.Alpha8:
                case TextureImporterFormat.R8:
                    return 8.0;
                case TextureImporterFormat.RGB16:
                case TextureImporterFormat.RGBA16:
                case TextureImporterFormat.ARGB16:
                case TextureImporterFormat.R16:
                case TextureImporterFormat.RHalf:
                case TextureImporterFormat.RG16:
                    return 16.0;
                case TextureImporterFormat.RGBAHalf:
                    return 64.0;
                case TextureImporterFormat.RGBAFloat:
                    return 128.0;
                case TextureImporterFormat.RGB48:
                case TextureImporterFormat.RGBA64:
                    return 64.0;
                default:
                    return 32.0;
            }
        }
    }
}
