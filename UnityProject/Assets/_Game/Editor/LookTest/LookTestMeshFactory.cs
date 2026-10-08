using System.Collections.Generic;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Procedural meshes for the look test (editor only, allocates): ground and river per segment, the cliff, the
    /// waterfall sheet, tree trunks with buttress roots, leaf-card crowns and low-poly boulders. Ground, river and
    /// cliff take their shape from <see cref="LookTestTerrainShape"/>, so neighbouring segments meet exactly and the
    /// loop is seamless. UVs are in meters (materials set the tiling). Vertex color: R/G material layers or foam,
    /// A ambient occlusion or edge fade.
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

        public static Mesh Ground(LookTestTerrainShape shape, LookTestConfigAsset c, int segment)
        {
            float segLength = c.LoopLengthM / c.SegmentCount;
            float z0 = segment * segLength;
            List<float> xs = GroundColumns(c);
            int rows = Mathf.Max(1, Mathf.RoundToInt(segLength / c.GroundStepZM));

            var positions = new Vector3[xs.Count, rows + 1];
            var frames = new Frame[xs.Count, rows + 1];
            var uvs = new Vector2[xs.Count, rows + 1];
            var colors = new Color[xs.Count, rows + 1];
            for (int i = 0; i < xs.Count; i++)
            {
                for (int j = 0; j <= rows; j++)
                {
                    float local = segLength * j / rows;
                    float x = xs[i];
                    float z = z0 + local;
                    float h = shape.Height(x, z);
                    positions[i, j] = new Vector3(x, h, local);
                    Vector3 n = shape.Normal(x, z);
                    frames[i, j] = new Frame
                    {
                        Normal = n,
                        Tangent = new Vector3(1f, -n.x / Mathf.Max(n.y, 0.05f), 0f).normalized,
                        Bitangent = new Vector3(0f, -n.z / Mathf.Max(n.y, 0.05f), 1f).normalized,
                    };
                    uvs[i, j] = new Vector2(x, z);
                    colors[i, j] = shape.Weights(x, z);
                }
            }

            return Grid("Ground_" + segment, positions, frames, uvs, colors);
        }

        public static Mesh River(LookTestTerrainShape shape, LookTestConfigAsset c, int segment)
        {
            float segLength = c.LoopLengthM / c.SegmentCount;
            float z0 = segment * segLength;
            const int columns = 11;
            int rows = Mathf.Max(1, Mathf.RoundToInt(segLength / c.GroundStepZM));
            float half = c.RiverHalfWidthM;
            float reach = half + 1.6f;

            var positions = new Vector3[columns, rows + 1];
            var frames = new Frame[columns, rows + 1];
            var uvs = new Vector2[columns, rows + 1];
            var colors = new Color[columns, rows + 1];
            for (int i = 0; i < columns; i++)
            {
                float across = -reach + 2f * reach * i / (columns - 1);
                for (int j = 0; j <= rows; j++)
                {
                    float local = segLength * j / rows;
                    float z = z0 + local;
                    float center = shape.RiverCenterX(z);
                    float x = center + across;
                    positions[i, j] = new Vector3(x, c.WaterLevelM, local);
                    frames[i, j] = new Frame { Normal = Vector3.up, Tangent = Vector3.right, Bitangent = Vector3.forward };
                    uvs[i, j] = new Vector2(x, z);

                    float bankFoam = 0.35f * LookTestTerrainShape.Smooth(half * 0.75f, half + 1.2f, Mathf.Abs(across));
                    float plunge = shape.WaterfallBump(z, 9f) * LookTestTerrainShape.Smooth(-0.5f, half, across);
                    float edge = 1f - LookTestTerrainShape.Smooth(half + 0.2f, reach, Mathf.Abs(across));
                    colors[i, j] = new Color(Mathf.Clamp01(bankFoam + plunge), 0f, 0f, edge);
                }
            }

            return Grid("River_" + segment, positions, frames, uvs, colors);
        }

        /// <summary>Cliff wall for one segment, or null when the cliff does not reach into it.</summary>
        public static Mesh Cliff(LookTestTerrainShape shape, LookTestConfigAsset c, int segment)
        {
            float segLength = c.LoopLengthM / c.SegmentCount;
            float z0 = segment * segLength;
            if (z0 + segLength < c.CliffStartZM || z0 > c.CliffEndZM)
            {
                return null;
            }

            int columns = Mathf.Max(2, Mathf.RoundToInt(segLength / c.GroundStepZM)) + 1;
            const int faceRows = 14;
            const int lipRows = 4;
            int heightRows = faceRows + lipRows;

            var positions = new Vector3[heightRows + 1, columns];
            var frames = new Frame[heightRows + 1, columns];
            var uvs = new Vector2[heightRows + 1, columns];
            var colors = new Color[heightRows + 1, columns];
            for (int j = 0; j < columns; j++)
            {
                float local = segLength * j / (columns - 1);
                float z = z0 + local;
                for (int i = 0; i <= heightRows; i++)
                {
                    positions[i, j] = CliffPoint(shape, c, z, i, faceRows, lipRows) - new Vector3(0f, 0f, z0);
                }
            }

            for (int j = 0; j < columns; j++)
            {
                float z = z0 + segLength * j / (columns - 1);
                for (int i = 0; i <= heightRows; i++)
                {
                    Vector3 p = positions[i, j];
                    Vector3 alongZ = CliffPoint(shape, c, z + 0.3f, i, faceRows, lipRows) - CliffPoint(shape, c, z - 0.3f, i, faceRows, lipRows);
                    Vector3 up = positions[Mathf.Min(i + 1, heightRows), j] - positions[Mathf.Max(i - 1, 0), j];
                    if (up.sqrMagnitude < 1e-6f)
                    {
                        up = Vector3.up;
                    }

                    Vector3 n = Vector3.Cross(alongZ, up);
                    if (n.sqrMagnitude < 1e-8f)
                    {
                        n = Vector3.left;
                    }

                    frames[i, j] = new Frame { Normal = n.normalized, Tangent = alongZ.normalized, Bitangent = up.normalized };
                    uvs[i, j] = new Vector2(z, p.y + (i > faceRows ? (p.x - positions[faceRows, j].x) : 0f));
                    float ao = 0.7f + 0.3f * LookTestTerrainShape.Smooth(0f, 4f, p.y - positions[0, j].y);
                    colors[i, j] = new Color(0f, 0f, 0f, ao);
                }
            }

            return Grid("Cliff_" + segment, positions, frames, uvs, colors);
        }

        /// <summary>Top of the cliff face at <paramref name="z"/> (stretch space), used to place the waterfall and crowns.</summary>
        public static Vector3 CliffTop(LookTestTerrainShape shape, LookTestConfigAsset c, float z)
        {
            return CliffPoint(shape, c, z, 14, 14, 4);
        }

        public static Vector3 CliffBack(LookTestTerrainShape shape, LookTestConfigAsset c, float z)
        {
            return CliffPoint(shape, c, z, 18, 14, 4);
        }

        private static Vector3 CliffPoint(LookTestTerrainShape shape, LookTestConfigAsset c, float z, int row, int faceRows, int lipRows)
        {
            float presence = shape.CliffPresence(z);
            float foot = shape.CliffFootX(z);
            float baseY = shape.Height(foot, z) - 1.5f;
            float height = c.CliffHeightM * presence + 1.5f * presence;
            float smoothFace = 1f - 0.7f * shape.WaterfallBump(z, 5f);

            if (row <= faceRows)
            {
                float t = (float)row / faceRows;
                float y = baseY + height * t;
                float lean = 0.12f * height * t;
                float noise = presence * smoothFace * (0.9f * shape.Wave(z, 13, 2.3f * t) + 0.55f * shape.Wave(z, 31, 1.3f + 5.1f * t) + 0.35f * shape.Wave(z, 57, 0.7f + 9.3f * t));
                return new Vector3(foot + lean + noise, y, z);
            }

            Vector3 top = CliffPoint(shape, c, z, faceRows, faceRows, lipRows);
            float k = (float)(row - faceRows) / lipRows;
            float back = 10f * k * k + 1.5f * k;
            return new Vector3(top.x + back * presence + 0.01f * row, top.y + 0.6f * k * presence, z);
        }

        /// <summary>Waterfall sheet in local space: origin at the plunge point, falling along -y, facing -x.</summary>
        public static Mesh Waterfall(float width, float topHeight, float topSetback)
        {
            const int across = 9;
            const int down = 16;
            var positions = new Vector3[down + 1, across];
            var frames = new Frame[down + 1, across];
            var uvs = new Vector2[down + 1, across];
            var colors = new Color[down + 1, across];
            for (int i = 0; i <= down; i++)
            {
                float t = (float)i / down;
                for (int j = 0; j < across; j++)
                {
                    float s = (float)j / (across - 1) - 0.5f;
                    float y = topHeight * t;
                    float x = topSetback * Mathf.Pow(t, 1.6f) - 0.9f * Mathf.Sin(Mathf.PI * t) * (1f - 0.4f * Mathf.Abs(s) * 2f);
                    float z = s * width * (0.85f + 0.15f * t);
                    positions[i, j] = new Vector3(x, y, z);
                    uvs[i, j] = new Vector2(z, y);
                    float foam = Mathf.Clamp01(0.35f + 0.65f * (1f - LookTestTerrainShape.Smooth(0f, 0.3f, t)) + 0.3f * LookTestTerrainShape.Smooth(0.9f, 1f, t));
                    float edge = 1f - LookTestTerrainShape.Smooth(0.32f, 0.5f, Mathf.Abs(s));
                    colors[i, j] = new Color(foam, 0f, 0f, edge * (1f - 0.5f * LookTestTerrainShape.Smooth(0.95f, 1f, t)));
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

        /// <summary>Tapered, slightly bent trunk with buttress roots. Pivot at the base. u in meters around, v = height.</summary>
        public static Mesh Trunk(IRandom rng, float height, float radius, float barkTileM)
        {
            const int sides = 14;
            const int rings = 18;
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
                    float ao = Mathf.Lerp(0.5f, 1f, LookTestTerrainShape.Smooth(0f, 3f, y)) * (1f - 0.25f * (1f - lobe) * buttress / Mathf.Max(flare, 0.01f));
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
            var points = new List<Vector3>();
            var faces = new List<int>();
            Icosahedron(points, faces);
            Subdivide(points, faces);
            Subdivide(points, faces);

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

        private static List<float> GroundColumns(LookTestConfigAsset c)
        {
            var xs = new List<float>();
            float w = c.GroundHalfWidthM;
            float fineMin = -c.PathHalfWidthM - 8f;
            float fineMax = c.RiverCenterXM + c.RiverMeanderM * 1.4f + c.RiverHalfWidthM * 2f + 4f;
            float x = -w;
            while (x < w)
            {
                xs.Add(x);
                x += x >= fineMin && x < fineMax ? 0.75f : 3f;
            }

            xs.Add(w);

            // The path edges must be exact vertices so the path stays flat at y = 0.
            InsertSorted(xs, -c.PathHalfWidthM);
            InsertSorted(xs, c.PathHalfWidthM);
            return xs;
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
