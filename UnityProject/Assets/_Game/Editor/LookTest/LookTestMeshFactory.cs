using System.Collections.Generic;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Procedural meshes for the look test (editor only, allocates): ground and river strips in path space (shape
    /// from <see cref="LookTestStretchLayout"/>, so neighbouring segments meet exactly and the loop is seamless),
    /// tubes (stilt roots, rootstone pillars and arches, vines), leaf-card clumps and crowns, trunks, boulders,
    /// pools and waterfall sheets. UVs are in meters (materials set the tiling). Vertex color: R/G material layers or
    /// foam, B canopy cover (painted when merged), A ambient occlusion or edge fade.
    /// Triangle convention of <see cref="Grid"/>: for grid axes u (first index) and v (second index) the front
    /// face points along cross(dP/dv, dP/du).
    /// </summary>
    public static class LookTestMeshFactory
    {
        /// <summary>Per-vertex surface frame: normal, tangent (direction of +uv.x), bitangent (direction of +uv.y).</summary>
        public struct Frame
        {
            public Vector3 Normal;
            public Vector3 Tangent;
            public Vector3 Bitangent;
        }

        /// <summary>Lateral sample positions of the ground strip: fine near the trail, coarse far out; the runnable edges are exact.</summary>
        public static List<float> StripColumns(LookTestConfigAsset c)
        {
            var columns = new List<float>();
            float w = c.StripHalfWidthM;
            float run = c.RunnableHalfWidthM;
            for (float d = -w; d < w - 0.01f;)
            {
                columns.Add(d);
                float ad = Mathf.Abs(d);
                d += ad < run + 4f ? 0.5f : ad < 16f ? 1f : ad < 30f ? 2f : 3.5f;
            }

            columns.Add(w);
            InsertSorted(columns, -run);
            InsertSorted(columns, run);
            return columns;
        }

        /// <summary>Ground of the path range [s0, s1] (world space, loop 0). UV = (d, s) in meters.</summary>
        public static Mesh GroundStrip(LookTestStretchLayout layout, float s0, float s1, string name)
        {
            LookTestConfigAsset c = layout.Config;
            List<float> columns = StripColumns(c);
            int rows = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / c.GroundStepM));
            var positions = new Vector3[columns.Count, rows + 1];
            var frames = new Frame[columns.Count, rows + 1];
            var uvs = new Vector2[columns.Count, rows + 1];
            var colors = new Color[columns.Count, rows + 1];
            const float e = 0.3f;
            for (int i = 0; i < columns.Count; i++)
            {
                float d = columns[i];
                for (int j = 0; j <= rows; j++)
                {
                    float s = Mathf.Lerp(s0, s1, (float)j / rows);
                    positions[i, j] = GroundPoint(layout, s, d);
                    Vector3 alongS = GroundPoint(layout, s + e, d) - GroundPoint(layout, s - e, d);
                    Vector3 alongD = GroundPoint(layout, s, d + e) - GroundPoint(layout, s, d - e);
                    frames[i, j] = new Frame { Normal = Vector3.Cross(alongS, alongD).normalized, Tangent = alongD.normalized, Bitangent = alongS.normalized };
                    uvs[i, j] = new Vector2(d, s);
                    colors[i, j] = layout.GroundWeights(s, d);
                }
            }

            return Grid(name, positions, frames, uvs, colors);
        }

        public static Vector3 GroundPoint(LookTestStretchLayout layout, float s, float d)
        {
            return layout.Path.World(s, d, layout.GroundY(s, d));
        }

        /// <summary>
        /// River water over [s0, s1] (world space, loop 0), up to where it falls off the plateau edge; null if the
        /// river is not in the range. Columns run along d (the river is defined along d). R = foam (shoreline where the
        /// water is shallow, rapids before the fall), G = depth 0..1 (Water.shader body color), A = soft edge.
        /// </summary>
        public static Mesh RiverStrip(LookTestStretchLayout layout, float s0, float s1, string name)
        {
            LookTestConfigAsset c = layout.Config;
            float fall = layout.RiverFallS();
            float end = Mathf.Min(c.RiverEndSM, fall > 0f ? fall + 0.6f : c.RiverEndSM);
            float a = Mathf.Max(s0, c.RiverStartSM);
            float b = Mathf.Min(s1, end);
            if (b - a < 0.25f)
            {
                return null;
            }

            const int across = 11;
            int rows = Mathf.Max(1, Mathf.CeilToInt((b - a) / 0.5f));
            float reach = c.RiverHalfWidthM * 1.35f;
            var positions = new Vector3[across, rows + 1];
            var frames = new Frame[across, rows + 1];
            var uvs = new Vector2[across, rows + 1];
            var colors = new Color[across, rows + 1];
            float along = a;
            Vector3 previous = Vector3.zero;
            for (int j = 0; j <= rows; j++)
            {
                float s = Mathf.Lerp(a, b, (float)j / rows);
                float rd = layout.RiverD(s);
                float y = layout.WaterY(rd);
                Vector3 center = layout.Path.World(s, rd, y);
                if (j > 0)
                {
                    along += Vector3.Distance(center, previous);
                }

                previous = center;
                for (int i = 0; i < across; i++)
                {
                    float t = (float)i / (across - 1) * 2f - 1f;
                    float d = rd + t * reach;
                    positions[i, j] = layout.Path.World(s, d, y);
                    frames[i, j] = new Frame { Normal = Vector3.up, Tangent = layout.Path.Right(s), Bitangent = layout.Path.Forward(s) };
                    uvs[i, j] = new Vector2(t * reach, along);
                    colors[i, j] = RiverColor(layout, s, d, y, t, fall);
                }
            }

            return Grid(name, positions, frames, uvs, colors);
        }

        /// <summary>
        /// Vertex color of the river surface at (s, d), water height <paramref name="y"/>, across-fraction
        /// <paramref name="t"/> (−1..1): R = foam, G = depth 0..1, A = soft edge. Pure; unit-tested.
        /// </summary>
        public static Color RiverColor(LookTestStretchLayout layout, float s, float d, float y, float t, float fallS)
        {
            LookTestConfigAsset c = layout.Config;
            float depth = y - layout.GroundY(s, d);
            float shore = 1f - LookTestMath.Smooth(0.02f, 0.2f, depth);
            float rapids = fallS > 0f ? 0.55f * LookTestMath.Smooth(fallS - 9f, fallS - 0.5f, s) : 0f;
            float ford = 1f - LookTestMath.Smooth(c.RunnableHalfWidthM, c.RunnableHalfWidthM + 3f, Mathf.Abs(d));
            float foam = Mathf.Clamp01(Mathf.Max(shore * 0.9f, rapids) + 0.08f * ford);
            float deep = Mathf.Clamp01(depth / Mathf.Max(0.1f, c.RiverDepthM));
            float edge = 1f - LookTestMath.Smooth(0.82f, 1f, Mathf.Abs(t));
            return new Color(foam, deep, 0f, edge);
        }

        /// <summary>
        /// Flat round pool surface of <paramref name="radius"/> at the origin: foam (R) near the rim, depth (G) from
        /// shallow at the rim to deep inside, soft edge (A).
        /// </summary>
        public static Mesh Pool(float radius, float foamWidth, string name)
        {
            return Disc(radius, name, fromRim => new Color(
                1f - LookTestMath.Smooth(0f, foamWidth, fromRim),
                LookTestMath.Smooth(0f, foamWidth * 1.6f, fromRim),
                0f,
                LookTestMath.Smooth(0f, 0.8f, fromRim)));
        }

        /// <summary>
        /// Impact foam where a fall hits a pool: a disc of white water (R = 1 at the centre) fading out to the rim,
        /// drawn with the pool material on top of the pool surface (same merged mesh, no extra draw).
        /// </summary>
        public static Mesh ImpactFoam(float radius)
        {
            return Disc(radius, "ImpactFoam", fromRim =>
            {
                float inner = fromRim / Mathf.Max(0.01f, radius);
                return new Color(LookTestMath.Smooth(0.05f, 0.75f, inner), 1f, 0f, LookTestMath.Smooth(0f, 0.45f, inner));
            });
        }

        private static Mesh Disc(float radius, string name, System.Func<float, Color> colorFromRim)
        {
            const int rings = 6;
            const int sides = 32;
            var positions = new Vector3[sides + 1, rings + 1];
            var frames = new Frame[sides + 1, rings + 1];
            var uvs = new Vector2[sides + 1, rings + 1];
            var colors = new Color[sides + 1, rings + 1];
            for (int k = 0; k <= sides; k++)
            {
                float a = 2f * Mathf.PI * k / sides;
                for (int j = 0; j <= rings; j++)
                {
                    float r = radius * (1f - (float)j / rings);
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    positions[k, j] = p;
                    frames[k, j] = new Frame { Normal = Vector3.up, Tangent = Vector3.right, Bitangent = Vector3.forward };
                    uvs[k, j] = new Vector2(p.x, p.z);
                    colors[k, j] = colorFromRim(radius - r);
                }
            }

            Mesh mesh = Grid(name, positions, frames, uvs, colors);
            FaceUp(mesh);
            return mesh;
        }

        /// <summary>
        /// Tube along <paramref name="spine"/> with per-point radii: stilt roots, rootstone pillars and arches, vines.
        /// <paramref name="lobes"/> braided strands (radius ripples around the tube) that twist along it; parallel
        /// transport frames, so bends do not twist the surface. UV x = around (m, whole repeats of
        /// <paramref name="uvTileM"/>), y = length (m). Vertex color A = ambient occlusion (dark underside and base).
        /// </summary>
        public static Mesh Tube(string name, IList<Vector3> spine, IList<float> radii, int sides, float uvTileM, int lobes, float lobeAmount, float twistPerM, float seed)
        {
            int n = spine.Count;
            var tangents = new Vector3[n];
            for (int j = 0; j < n; j++)
            {
                Vector3 t = spine[Mathf.Min(j + 1, n - 1)] - spine[Mathf.Max(j - 1, 0)];
                tangents[j] = t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.up;
            }

            Vector3 normal0 = Vector3.Cross(tangents[0], Mathf.Abs(tangents[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var normals = new Vector3[n];
            normals[0] = normal0;
            for (int j = 1; j < n; j++)
            {
                // Parallel transport: rotate the previous normal by the rotation between tangents.
                Quaternion q = Quaternion.FromToRotation(tangents[j - 1], tangents[j]);
                normals[j] = (q * normals[j - 1]).normalized;
            }

            float meanRadius = 0f;
            for (int j = 0; j < n; j++)
            {
                meanRadius += radii[j];
            }

            meanRadius /= n;
            float around = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * meanRadius / uvTileM)) * uvTileM;
            float minY = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                minY = Mathf.Min(minY, spine[j].y);
            }

            var positions = new Vector3[sides + 1, n];
            var frames = new Frame[sides + 1, n];
            var uvs = new Vector2[sides + 1, n];
            var colors = new Color[sides + 1, n];
            float length = 0f;
            for (int j = 0; j < n; j++)
            {
                if (j > 0)
                {
                    length += Vector3.Distance(spine[j], spine[j - 1]);
                }

                Vector3 bin = Vector3.Cross(tangents[j], normals[j]);
                for (int k = 0; k <= sides; k++)
                {
                    float theta = 2f * Mathf.PI * k / sides;
                    float lobe = lobes > 0 ? Mathf.Pow(0.5f + 0.5f * Mathf.Cos(lobes * theta + twistPerM * length + seed), 2f) : 0.5f;
                    float wobble = 0.06f * Mathf.Sin(3f * theta + 0.7f * length + seed * 2.3f) + 0.04f * Mathf.Sin(7f * theta - 1.3f * length + seed);
                    float r = radii[j] * (1f + lobeAmount * (lobe - 0.5f) + wobble * lobeAmount * 2f);
                    Vector3 radial = normals[j] * Mathf.Cos(theta) + bin * Mathf.Sin(theta);
                    positions[k, j] = spine[j] + radial * r;
                    frames[k, j] = new Frame { Normal = radial, Tangent = Vector3.Cross(tangents[j], radial), Bitangent = tangents[j] };
                    uvs[k, j] = new Vector2(around * k / sides, length);
                    float underside = 0.62f + 0.38f * (radial.y * 0.5f + 0.5f);
                    float baseAo = Mathf.Lerp(0.5f, 1f, LookTestMath.Smooth(0f, 4f, positions[k, j].y - minY));
                    colors[k, j] = new Color(0f, 0f, 0f, underside * baseAo * Mathf.Lerp(0.8f, 1f, lobe));
                }
            }

            // Normals from the actual surface (the lobes change them), oriented outward.
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k <= sides; k++)
                {
                    Vector3 du = positions[Mathf.Min(k + 1, sides), j] - positions[Mathf.Max(k - 1, 0), j];
                    Vector3 dv = positions[k, Mathf.Min(j + 1, n - 1)] - positions[k, Mathf.Max(j - 1, 0)];
                    Vector3 nrm = Vector3.Cross(du, dv);
                    if (nrm.sqrMagnitude < 1e-10f)
                    {
                        continue;
                    }

                    nrm.Normalize();
                    if (Vector3.Dot(nrm, frames[k, j].Normal) < 0f)
                    {
                        nrm = -nrm;
                    }

                    frames[k, j].Normal = nrm;
                }
            }

            Mesh mesh = Grid(name, positions, frames, uvs, colors);
            OrientOutward(mesh, spine);
            return mesh;
        }

        /// <summary>
        /// A clump of leaf cards in an ellipsoid (radius, height) standing on the origin: bushes, framing leaf masses
        /// and canopy-roof clusters. Normals point away from the clump centre (soft, rounded lighting and backlight
        /// glow). <paramref name="tiltDown"/> 0..1 turns cards to face down (roof clusters seen from below).
        /// </summary>
        public static Mesh Clump(IRandom rng, float radius, float height, int cards, float cardSize, float tiltDown, string name)
        {
            var vertices = new List<Vector3>(cards * 4);
            var normals = new List<Vector3>(cards * 4);
            var tangents = new List<Vector4>(cards * 4);
            var uvs = new List<Vector2>(cards * 4);
            var colors = new List<Color>(cards * 4);
            var triangles = new List<int>(cards * 6);
            var center = new Vector3(0f, height * 0.45f, 0f);
            for (int i = 0; i < cards; i++)
            {
                // Points inside the ellipsoid, biased to the surface (a full silhouette, a hollow-ish core).
                Vector3 dir = new Vector3(rng.NextFloat(-1f, 1f), rng.NextFloat(-0.6f, 1f), rng.NextFloat(-1f, 1f));
                if (dir.sqrMagnitude < 1e-4f)
                {
                    dir = Vector3.up;
                }

                dir.Normalize();
                float reach = Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(rng.NextFloat()));
                var p = new Vector3(dir.x * radius * reach, center.y + dir.y * height * 0.55f * reach, dir.z * radius * reach);
                p.y = Mathf.Max(p.y, height * 0.05f);
                float size = cardSize * rng.NextFloat(0.7f, 1.25f);
                float pitch = Mathf.Lerp(rng.NextFloat(-60f, 10f), rng.NextFloat(70f, 110f), tiltDown);
                Quaternion orientation = Quaternion.Euler(pitch, rng.NextFloat(0f, 360f), rng.NextFloat(-30f, 30f));
                Vector3 right = orientation * Vector3.right * size * 0.5f;
                Vector3 up = orientation * Vector3.up * size * 0.5f;
                int start = vertices.Count;
                Vector3[] corners = { p - right - up, p + right - up, p - right + up, p + right + up };
                Vector2[] cornerUvs = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                for (int k = 0; k < 4; k++)
                {
                    Vector3 n = (corners[k] - center).normalized;
                    if (tiltDown > 0.5f)
                    {
                        n = Vector3.Slerp(n, Vector3.down, 0.5f).normalized;
                    }

                    Vector3 t = Vector3.ProjectOnPlane(right, n).normalized;
                    if (t.sqrMagnitude < 1e-6f)
                    {
                        t = Vector3.right;
                    }

                    float w = Vector3.Dot(Vector3.Cross(n, t), up) < 0f ? -1f : 1f;
                    vertices.Add(corners[k]);
                    normals.Add(n);
                    tangents.Add(new Vector4(t.x, t.y, t.z, w));
                    uvs.Add(cornerUvs[k]);
                    float ao = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(corners[k].y / Mathf.Max(0.1f, height)));
                    ao *= Mathf.Lerp(0.7f, 1f, reach);
                    colors.Add(new Color(0f, 0f, 0f, ao));
                }

                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flips triangles of a tube whose winding faces inward (front face must point away from the spine).</summary>
        private static void OrientOutward(Mesh mesh, IList<Vector3> spine)
        {
            int[] tris = mesh.triangles;
            Vector3[] v = mesh.vertices;
            if (tris.Length < 3)
            {
                return;
            }

            Vector3 a = v[tris[0]];
            Vector3 b = v[tris[1]];
            Vector3 c = v[tris[2]];
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 nearest = spine[0];
            float best = float.MaxValue;
            for (int i = 0; i < spine.Count; i++)
            {
                float dist = (spine[i] - centroid).sqrMagnitude;
                if (dist < best)
                {
                    best = dist;
                    nearest = spine[i];
                }
            }

            if (Vector3.Dot(faceNormal, centroid - nearest) >= 0f)
            {
                return;
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                int swap = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = swap;
            }

            mesh.triangles = tris;
        }

        /// <summary>Flips a flat mesh so its triangles face +y.</summary>
        private static void FaceUp(Mesh mesh)
        {
            int[] tris = mesh.triangles;
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                if (n.y < 0f)
                {
                    int swap = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = swap;
                }
            }

            mesh.triangles = tris;
        }

        private static void InsertSorted(List<float> xs, float value)
        {
            for (int i = 0; i < xs.Count; i++)
            {
                if (Mathf.Abs(xs[i] - value) < 0.05f)
                {
                    xs[i] = value;
                    return;
                }

                if (xs[i] > value)
                {
                    xs.Insert(i, value);
                    return;
                }
            }

            xs.Add(value);
        }

        /// <summary>One sheet of a layered waterfall (ADR 0008). Distances in multiples of the fall width unless noted.</summary>
        public struct FallLayer
        {
            /// <summary>Sheet width as a fraction of the fall width.</summary>
            public float Width;
            /// <summary>Lateral offset (fraction of the fall width, + = local +z).</summary>
            public float Lateral;
            /// <summary>Offset toward the viewer, m.</summary>
            public float Forward;
            /// <summary>Outward bulge at mid-height, m.</summary>
            public float Bulge;
            /// <summary>Extra widening toward the foot (fraction of the sheet width).</summary>
            public float Spread;
            /// <summary>Layer speed 0..1 (Waterfall.shader: 0.6-1.4x), vertex color G.</summary>
            public float Speed;
            /// <summary>Pattern offset 0..1, vertex color B.</summary>
            public float Seed;
            /// <summary>Peak edge coverage 0..1, vertex color A.</summary>
            public float Coverage;
            /// <summary>Where the sheet's edge softening starts (|across| in −0.5..0.5; lower = more ragged).</summary>
            public float EdgeStart;
            /// <summary>Foam at the lip (top), 0..1.</summary>
            public float LipFoam;
        }

        /// <summary>The layers of a hero fall: a dense core, a ragged front veil and two side spray streaks.</summary>
        public static readonly FallLayer[] FallLayers =
        {
            new FallLayer { Width = 0.82f, Lateral = 0f, Forward = 0f, Bulge = 0.9f, Spread = 0.15f, Speed = 0.25f, Seed = 0.13f, Coverage = 1f, EdgeStart = 0.3f, LipFoam = 0.8f },
            new FallLayer { Width = 1.0f, Lateral = 0f, Forward = 0.45f, Bulge = 1.3f, Spread = 0.25f, Speed = 0.75f, Seed = 0.61f, Coverage = 0.75f, EdgeStart = 0.16f, LipFoam = 0.5f },
            new FallLayer { Width = 0.24f, Lateral = -0.5f, Forward = 0.7f, Bulge = 1.6f, Spread = 0.9f, Speed = 1f, Seed = 0.37f, Coverage = 0.55f, EdgeStart = 0.02f, LipFoam = 0f },
            new FallLayer { Width = 0.24f, Lateral = 0.5f, Forward = 0.7f, Bulge = 1.6f, Spread = 0.9f, Speed = 0.9f, Seed = 0.83f, Coverage = 0.55f, EdgeStart = 0.02f, LipFoam = 0f },
        };

        /// <summary>Single-sheet waterfall (the core layer).</summary>
        public static Mesh Waterfall(float width, float topHeight, float topSetback)
        {
            return Waterfall(width, topHeight, topSetback, FallLayers[0]);
        }

        /// <summary>
        /// Waterfall sheet in local space: origin at the plunge point, rising along +y to the lip at
        /// <paramref name="topHeight"/> (set back by <paramref name="topSetback"/> along +x), facing −x (the viewer).
        /// UV0 in meters (x across, y height). Vertex color: R foam (lip and plunge), G layer speed, B seed, A coverage.
        /// </summary>
        public static Mesh Waterfall(float width, float topHeight, float topSetback, FallLayer layer)
        {
            const int across = 9;
            int down = Mathf.Clamp(Mathf.RoundToInt(topHeight / 3f), 8, 16);
            var positions = new Vector3[down + 1, across];
            var frames = new Frame[down + 1, across];
            var uvs = new Vector2[down + 1, across];
            var colors = new Color[down + 1, across];
            float sheetWidth = width * layer.Width;
            for (int i = 0; i <= down; i++)
            {
                float t = (float)i / down;
                for (int j = 0; j < across; j++)
                {
                    float s = (float)j / (across - 1) - 0.5f;
                    float y = topHeight * t;
                    // Short spills bulge and spread less than tall falls (the layer offsets are meant for 10 m+ sheets).
                    float scale = Mathf.Clamp01(topHeight / 10f);
                    float x = topSetback * Mathf.Pow(t, 1.6f) - layer.Bulge * scale * Mathf.Sin(Mathf.PI * t) * (1f - 0.4f * Mathf.Abs(s) * 2f) - layer.Forward * scale;
                    float z = layer.Lateral * width + s * sheetWidth * (1f + layer.Spread * (1f - t));
                    positions[i, j] = new Vector3(x, y, z);
                    uvs[i, j] = new Vector2(z, y);
                    float foam = Mathf.Clamp01(0.12f + 0.9f * (1f - LookTestMath.Smooth(0f, 0.22f, t)) + layer.LipFoam * LookTestMath.Smooth(0.86f, 1f, t));
                    float edge = 1f - LookTestMath.Smooth(layer.EdgeStart, 0.5f, Mathf.Abs(s));
                    float ends = (1f - 0.6f * LookTestMath.Smooth(0.96f, 1f, t)) * (1f - 0.5f * (1f - LookTestMath.Smooth(0f, 0.06f, t)));
                    colors[i, j] = new Color(foam, layer.Speed, layer.Seed, edge * ends * layer.Coverage);
                }
            }

            for (int i = 0; i <= down; i++)
            {
                for (int j = 0; j < across; j++)
                {
                    Vector3 up = positions[Mathf.Min(i + 1, down), j] - positions[Mathf.Max(i - 1, 0), j];
                    Vector3 side = positions[i, Mathf.Min(j + 1, across - 1)] - positions[i, Mathf.Max(j - 1, 0)];
                    Vector3 n = Vector3.Cross(side, up).normalized;
                    frames[i, j] = new Frame { Normal = n, Tangent = side.normalized, Bitangent = up.normalized };
                }
            }

            return Grid("Waterfall", positions, frames, uvs, colors);
        }

        /// <summary>
        /// Backdrop layer card: a vertical cylinder strip of radius <paramref name="distance"/> around the origin,
        /// centred on +z, covering ±<paramref name="halfAngle"/> (radians), from <paramref name="baseY"/> up by
        /// <paramref name="height"/>, facing the origin. UV0 0..1 over the strip.
        /// </summary>
        public static Mesh BackdropStrip(float distance, float halfAngle, float baseY, float height, int columns)
        {
            columns = Mathf.Max(2, columns);
            var positions = new Vector3[columns + 1, 2];
            var frames = new Frame[columns + 1, 2];
            var uvs = new Vector2[columns + 1, 2];
            var colors = new Color[columns + 1, 2];
            for (int i = 0; i <= columns; i++)
            {
                float u = (float)i / columns;
                float a = Mathf.Lerp(-halfAngle, halfAngle, u);
                var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int j = 0; j < 2; j++)
                {
                    positions[i, j] = radial * distance + Vector3.up * (baseY + height * j);
                    frames[i, j] = new Frame { Normal = -radial, Tangent = new Vector3(radial.z, 0f, -radial.x), Bitangent = Vector3.up };
                    uvs[i, j] = new Vector2(u, j);
                    colors[i, j] = Color.white;
                }
            }

            return Grid("Backdrop", positions, frames, uvs, colors);
        }

        /// <summary>Tapered, slightly bent trunk with buttress roots. Pivot at the base. u in meters around, v = height.</summary>
        public static Mesh Trunk(IRandom rng, float height, float radius, float barkTileM)
        {
            return Trunk(rng, height, radius, barkTileM, 14, 18);
        }

        /// <summary>Trunk with a chosen resolution (sides around, rings up).</summary>
        public static Mesh Trunk(IRandom rng, float height, float radius, float barkTileM, int sides, int rings)
        {
            float flare = rng.NextFloat(0.6f, 1.4f);
            int lobes = rng.NextInt(4, 7);
            float lobePhase = rng.NextFloat(0f, 6.283f);
            float bendAngle = rng.NextFloat(0f, 6.283f);
            float bend = rng.NextFloat(0.2f, 1.2f);
            float circumference = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * radius / barkTileM)) * barkTileM;

            var positions = new Vector3[sides + 1, rings + 1];
            var frames = new Frame[sides + 1, rings + 1];
            var uvs = new Vector2[sides + 1, rings + 1];
            var colors = new Color[sides + 1, rings + 1];
            for (int j = 0; j <= rings; j++)
            {
                float t = (float)j / rings;
                float y = height * t * t * 0.35f + height * t * 0.65f; // Denser rings near the base.
                float taper = Mathf.Lerp(1f, 0.45f, y / height);
                float buttress = flare * Mathf.Exp(-y / 1.3f);
                Vector3 center = new Vector3(Mathf.Cos(bendAngle), 0f, Mathf.Sin(bendAngle)) * (bend * (y / height) * (y / height));
                for (int k = 0; k <= sides; k++)
                {
                    float theta = 2f * Mathf.PI * k / sides;
                    float lobe = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(lobes * theta + lobePhase), 3f);
                    float r = radius * taper * (1f + buttress * (0.25f + 1.6f * lobe));
                    var radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
                    positions[k, j] = center + radial * r + Vector3.up * y;
                    Vector3 tangent = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta));
                    Vector3 normal = (radial + Vector3.up * (buttress * 0.5f)).normalized;
                    frames[k, j] = new Frame { Normal = normal, Tangent = tangent, Bitangent = Vector3.up };
                    uvs[k, j] = new Vector2(circumference * k / sides, y);
                    float ao = Mathf.Lerp(0.5f, 1f, LookTestMath.Smooth(0f, 3f, y)) * (1f - 0.25f * (1f - lobe) * buttress / Mathf.Max(flare, 0.01f));
                    colors[k, j] = new Color(0f, 0f, 0f, ao);
                }
            }

            return Grid("Trunk", positions, frames, uvs, colors);
        }

        /// <summary>Crown of crossed leaf cards around the trunk top. Normals point away from the crown center (soft lighting).</summary>
        public static Mesh Crown(IRandom rng, float trunkHeight, int cards, float cardSize)
        {
            var vertices = new List<Vector3>(cards * 4);
            var normals = new List<Vector3>(cards * 4);
            var tangents = new List<Vector4>(cards * 4);
            var uvs = new List<Vector2>(cards * 4);
            var colors = new List<Color>(cards * 4);
            var triangles = new List<int>(cards * 6);
            var crownCenter = new Vector3(0f, trunkHeight * 0.82f, 0f);

            // Leaves grow in clumps at the ends of a few branches (a full, broken silhouette instead of one even
            // layer). Clumps sit from about 55% of the height to the top, spreading wider higher up.
            int clumps = Mathf.Max(3, cards / 9);
            var clumpCenters = new Vector3[clumps];
            for (int c = 0; c < clumps; c++)
            {
                float h = rng.NextFloat(0.55f, 1f);
                float reach = trunkHeight * 0.26f * Mathf.Lerp(0.55f, 1f, h) * Mathf.Sqrt(rng.NextFloat(0.25f, 1f));
                float a = rng.NextFloat(0f, 6.283f);
                clumpCenters[c] = new Vector3(Mathf.Cos(a) * reach, trunkHeight * h, Mathf.Sin(a) * reach);
            }

            float spread = cardSize * 0.9f;
            for (int i = 0; i < cards; i++)
            {
                // Lower cards hang further out (branches), upper cards cluster on top.
                Vector3 clump = clumpCenters[i % clumps];
                float angle = rng.NextFloat(0f, 6.283f);
                float radial = spread * Mathf.Sqrt(rng.NextFloat(0f, 1f));
                var center = clump + new Vector3(Mathf.Cos(angle) * radial, rng.NextFloat(-0.45f, 0.45f) * cardSize, Mathf.Sin(angle) * radial);
                float size = cardSize * rng.NextFloat(0.75f, 1.2f);
                Quaternion orientation = Quaternion.Euler(rng.NextFloat(-70f, -25f), rng.NextFloat(0f, 360f), rng.NextFloat(-25f, 25f));
                Vector3 right = orientation * Vector3.right * size * 0.5f;
                Vector3 up = orientation * Vector3.up * size * 0.5f;

                int start = vertices.Count;
                Vector3[] corners = { center - right - up, center + right - up, center - right + up, center + right + up };
                Vector2[] cornerUvs = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                for (int k = 0; k < 4; k++)
                {
                    Vector3 n = (corners[k] - crownCenter).normalized;
                    Vector3 t = Vector3.ProjectOnPlane(right, n).normalized;
                    if (t.sqrMagnitude < 1e-6f)
                    {
                        t = Vector3.right;
                    }

                    float w = Vector3.Dot(Vector3.Cross(n, t), up) < 0f ? -1f : 1f;
                    vertices.Add(corners[k]);
                    normals.Add(n);
                    tangents.Add(new Vector4(t.x, t.y, t.z, w));
                    uvs.Add(cornerUvs[k]);
                    float ao = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((corners[k].y - trunkHeight * 0.5f) / (trunkHeight * 0.5f)));
                    colors.Add(new Color(0f, 0f, 0f, ao));
                }

                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            var mesh = new Mesh { name = "Crown" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Low-poly boulder (subdivided icosahedron, displaced, flattened base). Box-projected UVs in meters.</summary>
        public static Mesh Boulder(IRandom rng, float radius)
        {
            return Boulder(rng, radius, 2);
        }

        /// <summary>Boulder with 0-3 subdivisions (1 = 80 triangles: far canopy blobs and pillar-top tufts).</summary>
        public static Mesh Boulder(IRandom rng, float radius, int subdivisions)
        {
            var points = new List<Vector3>();
            var faces = new List<int>();
            Icosahedron(points, faces);
            for (int i = 0; i < Mathf.Clamp(subdivisions, 0, 3); i++)
            {
                Subdivide(points, faces);
            }

            var phases = new float[9];
            for (int i = 0; i < phases.Length; i++)
            {
                phases[i] = rng.NextFloat(0f, 6.283f);
            }

            float squash = rng.NextFloat(0.55f, 0.8f);
            float stretch = rng.NextFloat(1f, 1.4f);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 d = points[i];
                float n = 0.18f * Mathf.Sin(1.7f * d.x + phases[0]) * Mathf.Sin(1.9f * d.y + phases[1]) * Mathf.Sin(1.6f * d.z + phases[2])
                        + 0.08f * Mathf.Sin(4.1f * d.x + phases[3]) * Mathf.Sin(3.7f * d.y + phases[4]) * Mathf.Sin(4.3f * d.z + phases[5])
                        + 0.04f * Mathf.Sin(9.1f * d.x + phases[6]) * Mathf.Sin(8.3f * d.y + phases[7]) * Mathf.Sin(9.7f * d.z + phases[8]);
                Vector3 p = d * radius * (1f + n);
                p.x *= stretch;
                p.y = p.y > 0f ? p.y * squash : p.y * 0.35f;
                points[i] = p;
            }

            // Smooth normals per shared point.
            var pointNormals = new Vector3[points.Count];
            for (int f = 0; f < faces.Count; f += 3)
            {
                Vector3 a = points[faces[f]];
                Vector3 b = points[faces[f + 1]];
                Vector3 c = points[faces[f + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, a + b + c) < 0f)
                {
                    int swap = faces[f + 1];
                    faces[f + 1] = faces[f + 2];
                    faces[f + 2] = swap;
                    n = -n;
                }

                pointNormals[faces[f]] += n;
                pointNormals[faces[f + 1]] += n;
                pointNormals[faces[f + 2]] += n;
            }

            var vertices = new List<Vector3>(faces.Count);
            var normals = new List<Vector3>(faces.Count);
            var uvs = new List<Vector2>(faces.Count);
            var colors = new List<Color>(faces.Count);
            var triangles = new List<int>(faces.Count);
            for (int f = 0; f < faces.Count; f += 3)
            {
                Vector3 a = points[faces[f]];
                Vector3 b = points[faces[f + 1]];
                Vector3 c = points[faces[f + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                float ax = Mathf.Abs(fn.x);
                float ay = Mathf.Abs(fn.y);
                float az = Mathf.Abs(fn.z);
                for (int k = 0; k < 3; k++)
                {
                    int index = faces[f + k];
                    Vector3 p = points[index];
                    Vector2 uv = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : (ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                    vertices.Add(p);
                    normals.Add(pointNormals[index].normalized);
                    uvs.Add(uv);
                    float ao = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((p.y + radius * 0.2f) / (radius * 0.6f)));
                    colors.Add(new Color(0f, 0f, 0f, ao));
                    triangles.Add(vertices.Count - 1);
                }
            }

            var mesh = new Mesh { name = "Boulder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a mesh from a grid of vertices [u, v]. Quads (a, c, b) and (b, c, d) with a = [u, v],
        /// b = [u + 1, v], c = [u, v + 1], d = [u + 1, v + 1], so the front face points along cross(dP/dv, dP/du).
        /// Tangent w is derived from the given frame.
        /// </summary>
        public static Mesh Grid(string name, Vector3[,] positions, Frame[,] frames, Vector2[,] uvs, Color[,] colors)
        {
            int nu = positions.GetLength(0);
            int nv = positions.GetLength(1);
            var vertices = new Vector3[nu * nv];
            var normals = new Vector3[nu * nv];
            var tangents = new Vector4[nu * nv];
            var uv0 = new Vector2[nu * nv];
            var color = new Color[nu * nv];
            for (int u = 0; u < nu; u++)
            {
                for (int v = 0; v < nv; v++)
                {
                    int index = u * nv + v;
                    Frame frame = frames[u, v];
                    Vector3 n = frame.Normal.normalized;
                    Vector3 t = Vector3.ProjectOnPlane(frame.Tangent, n).normalized;
                    if (t.sqrMagnitude < 1e-6f)
                    {
                        t = Vector3.Cross(n, Vector3.forward).normalized;
                    }

                    float w = Vector3.Dot(Vector3.Cross(n, t), frame.Bitangent) < 0f ? -1f : 1f;
                    vertices[index] = positions[u, v];
                    normals[index] = n;
                    tangents[index] = new Vector4(t.x, t.y, t.z, w);
                    uv0[index] = uvs[u, v];
                    color[index] = colors[u, v];
                }
            }

            var triangles = new int[(nu - 1) * (nv - 1) * 6];
            int k = 0;
            for (int u = 0; u < nu - 1; u++)
            {
                for (int v = 0; v < nv - 1; v++)
                {
                    int a = u * nv + v;
                    int b = (u + 1) * nv + v;
                    int c = u * nv + v + 1;
                    int d = (u + 1) * nv + v + 1;
                    triangles[k++] = a;
                    triangles[k++] = c;
                    triangles[k++] = b;
                    triangles[k++] = b;
                    triangles[k++] = c;
                    triangles[k++] = d;
                }
            }

            var mesh = new Mesh { name = name };
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv0;
            mesh.colors = color;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Icosahedron(List<Vector3> points, List<int> faces)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] p =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f),
            };
            for (int i = 0; i < p.Length; i++)
            {
                points.Add(p[i].normalized);
            }

            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            faces.AddRange(f);
        }

        private static void Subdivide(List<Vector3> points, List<int> faces)
        {
            var cache = new Dictionary<long, int>();
            var result = new List<int>(faces.Count * 4);
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i];
                int b = faces[i + 1];
                int c = faces[i + 2];
                int ab = Midpoint(points, cache, a, b);
                int bc = Midpoint(points, cache, b, c);
                int ca = Midpoint(points, cache, c, a);
                result.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            faces.Clear();
            faces.AddRange(result);
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index))
            {
                return index;
            }

            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            index = points.Count - 1;
            cache.Add(key, index);
            return index;
        }

        /// <summary>Approximate triangle count of a mesh (all submeshes).</summary>
        public static long TriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0L;
            }

            long count = 0L;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                count += (long)mesh.GetIndexCount(i) / 3L;
            }

            return count;
        }
    }
}
