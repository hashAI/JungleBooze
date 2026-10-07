using System.Collections.Generic;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Generated low-poly meshes for the scenery system: the far LOD of every prop (fitted to the bounds of the real
    /// art, so the swap does not change the size) and the gray-box stand-in when an art slot is missing. All have
    /// pivots at the base (y = 0 of the bounds' minimum), outward normals. Submesh 0 is bark / body,
    /// submesh 1 (trees and trunks only) is leaves.
    /// <para>Contact shading without a vertex-colour shader: UV.y of every mesh (except the light-shaft card, which keeps
    /// its real UVs) is the height above the base divided by a per-shape gradient height, so a material that uses the
    /// shared dark-to-light ramp texture (see <see cref="GradientTexture"/>) is darkest at the foot of a trunk, under a leaf
    /// mass and where a rock meets the ground. Flat shapes (stubs, vines, litter) get UV.y = 1 (no darkening).</para>
    /// Setup-time only (allocates).
    /// </summary>
    public static class SceneryMeshes
    {
        /// <summary>Detail 1: far LOD (about 30 to 80 triangles). Detail 2: gray-box near stand-in (about 100 to 200).</summary>
        public const int DetailFar = 1;

        public const int DetailNear = 2;

        /// <summary>Number of submeshes (and materials) <see cref="Build"/> makes for <paramref name="shape"/>.</summary>
        public static int SubMeshCount(SceneryShape shape)
        {
            return shape == SceneryShape.Tree || shape == SceneryShape.Trunk ? 2 : 1;
        }

        /// <summary>Darkest value of the ramp (at UV.y = 0), a fraction of the material colour.</summary>
        public const float RampDark = 0.38f;

        /// <summary>Builds the mesh for <paramref name="shape"/> fitted to <paramref name="bounds"/> (leaf clump variant 0).</summary>
        public static Mesh Build(SceneryShape shape, Bounds bounds, int detail)
        {
            return Build(shape, bounds, detail, 0);
        }

        /// <summary>
        /// The dark-to-light ramp the generated materials sample with UV.y: 2 x 16 pixels, <see cref="RampDark"/> at the
        /// bottom easing to white at the top. The caller owns (and destroys) the texture.
        /// </summary>
        public static Texture2D GradientTexture()
        {
            const int width = 2;
            const int height = 16;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float eased = v * v * (3f - (2f * v));
                byte level = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(RampDark, 1f, eased) * 255f), 0, 255);
                for (int x = 0; x < width; x++)
                {
                    pixels[(y * width) + x] = new Color32(level, level, level, 255);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Scenery_ContactRamp",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Builds the mesh for <paramref name="shape"/> fitted to <paramref name="bounds"/>; <paramref name="variant"/> (0 to 2) picks the leaf clump arrangement.</summary>
        public static Mesh Build(SceneryShape shape, Bounds bounds, int detail, int variant)
        {
            var data = new MeshData(SubMeshCount(shape));
            bool near = detail >= DetailNear;
            Vector3 size = bounds.size;
            float width = Mathf.Max(size.x, size.z);
            float height = size.y;
            float baseY = bounds.min.y;
            float cx = bounds.center.x;
            float cz = bounds.center.z;
            int sides = near ? 8 : 5;
            int rings = near ? 4 : 3;
            int segs = near ? 8 : 6;
            data.GradientBaseY = baseY;

            switch (shape)
            {
                case SceneryShape.Tree:
                    data.GradientHeightM = 0.35f * height;
                    AddCylinder(data, 0, new Vector3(cx, baseY, cz), new Vector3(cx, baseY + (0.7f * height), cz), 0.09f * width, 0.05f * width, sides);
                    AddEllipsoid(data, 1, new Vector3(cx, baseY + (0.7f * height), cz), new Vector3(0.5f * width, 0.3f * height, 0.5f * width), rings + 1, segs + 2);
                    break;
                case SceneryShape.Trunk:
                    data.GradientHeightM = 0.12f * height;
                    float trunkRadius = 0.5f * width * 0.45f;
                    AddCylinder(data, 0, new Vector3(cx, baseY, cz), new Vector3(cx, baseY + (0.93f * height), cz), trunkRadius, trunkRadius * 0.6f, sides + 2);
                    AddEllipsoid(data, 1, new Vector3(cx, baseY + (0.95f * height), cz), new Vector3(0.45f * width, 0.06f * height, 0.45f * width), rings, segs + 2);
                    break;
                case SceneryShape.Blob:
                    data.GradientHeightM = 0.8f * height;
                    AddEllipsoid(data, 0, new Vector3(cx, baseY + (0.5f * height), cz), size * 0.5f, rings, segs);
                    break;
                case SceneryShape.Root:
                    data.GradientHeightM = 0.9f * height;
                    var legA = new Vector3(bounds.min.x, baseY, cz);
                    var top = new Vector3(cx, baseY + (0.8f * height), cz);
                    var legB = new Vector3(bounds.max.x, baseY, cz);
                    AddCylinder(data, 0, legA, top, 0.25f * height, 0.2f * height, sides);
                    AddCylinder(data, 0, top, legB, 0.2f * height, 0.25f * height, sides);
                    break;
                case SceneryShape.Vine:
                    AddCylinder(
                        data, 0, new Vector3(cx, bounds.max.y, cz), new Vector3(cx, bounds.min.y, cz), 0.15f * width, 0.1f * width, near ? 6 : 4);
                    break;
                case SceneryShape.WallTrunk:
                    BuildWallTrunk(data, bounds, near);
                    break;
                case SceneryShape.LeafClump:
                    data.GradientHeightM = 0.9f * height;
                    BuildLeafClump(data, bounds, near, variant);
                    break;
                case SceneryShape.Stub:
                    AddCylinder(data, 0, new Vector3(cx, baseY, cz), new Vector3(cx, baseY + height, cz), 0.5f * width, 0.3f * width, near ? 6 : 4);
                    break;
                case SceneryShape.FarTree:
                    data.GradientHeightM = 0.2f * height;
                    AddCylinder(data, 0, new Vector3(cx, baseY, cz), new Vector3(cx, baseY + (0.7f * height), cz), 0.08f * width, 0.05f * width, 5);
                    AddEllipsoid(data, 0, new Vector3(cx, baseY + (0.62f * height), cz), new Vector3(0.45f * width, 0.18f * height, 0.45f * width), 2, 6);
                    AddEllipsoid(data, 0, new Vector3(cx, baseY + (0.82f * height), cz), new Vector3(0.33f * width, 0.16f * height, 0.33f * width), 2, 6);
                    break;
                case SceneryShape.Litter:
                    BuildLitter(data, bounds);
                    break;
                default:
                    data.KeepUvs = true;
                    AddQuad(data, 0, bounds.min.y, bounds.max.y);
                    break;
            }

            return data.ToMesh(shape.ToString() + "Detail" + detail);
        }

        private sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int>[] Triangles;

            /// <summary>Height of the contact ramp above <see cref="GradientBaseY"/>; 0 = flat (UV.y 1).</summary>
            public float GradientHeightM;

            public float GradientBaseY;

            /// <summary>The mesh keeps the UVs the builder wrote (the light-shaft card).</summary>
            public bool KeepUvs;

            public MeshData(int subMeshes)
            {
                Triangles = new List<int>[subMeshes];
                for (int i = 0; i < subMeshes; i++)
                {
                    Triangles[i] = new List<int>();
                }
            }

            public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Vertices.Add(position);
                Normals.Add(normal);
                Uvs.Add(uv);
                return Vertices.Count - 1;
            }

            /// <summary>Adds a triangle wound so its front faces <paramref name="outward"/> (same rule as PrimitiveMeshes). Degenerate ones are skipped.</summary>
            public void AddTriangle(int sub, int a, int b, int c, Vector3 outward)
            {
                Vector3 cross = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                if (cross.sqrMagnitude < 1e-10f)
                {
                    return;
                }

                List<int> list = Triangles[sub];
                list.Add(a);
                if (Vector3.Dot(cross, outward) >= 0f)
                {
                    list.Add(b);
                    list.Add(c);
                }
                else
                {
                    list.Add(c);
                    list.Add(b);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = "Scenery" + name };
                if (!KeepUvs)
                {
                    for (int i = 0; i < Uvs.Count; i++)
                    {
                        float v = GradientHeightM > 1e-4f ? Mathf.Clamp01((Vertices[i].y - GradientBaseY) / GradientHeightM) : 1f;
                        Uvs[i] = new Vector2(Uvs[i].x, v);
                    }
                }

                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, Uvs);
                mesh.subMeshCount = Triangles.Length;
                for (int i = 0; i < Triangles.Length; i++)
                {
                    mesh.SetTriangles(Triangles[i], i);
                }

                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// Tall trunk with a flared foot: four rings (foot, shoulder, trunk, top) at 0, 5, 16 and 100 percent of the height;
        /// 8 sides near (48 triangles) and 5 sides, two rings far (10 triangles). The width of the bounds is the foot diameter.
        /// </summary>
        private static void BuildWallTrunk(MeshData data, Bounds bounds, bool near)
        {
            float height = bounds.size.y;
            float baseY = bounds.min.y;
            float footRadius = 0.5f * Mathf.Max(bounds.size.x, bounds.size.z);
            data.GradientHeightM = 0.1f * height;
            if (near)
            {
                AddLoft(data, bounds.center.x, bounds.center.z, baseY, height, new[] { 0f, 0.05f, 0.16f, 1f }, new[] { 1f, 0.66f, 0.53f, 0.38f }, footRadius, 8);
            }
            else
            {
                AddLoft(data, bounds.center.x, bounds.center.z, baseY, height, new[] { 0f, 1f }, new[] { 0.75f, 0.38f }, footRadius, 5);
            }
        }

        private static void AddLoft(MeshData data, float cx, float cz, float baseY, float height, float[] heights, float[] radii, float scale, int sides)
        {
            int first = data.Vertices.Count;
            for (int ring = 0; ring < heights.Length; ring++)
            {
                float y = baseY + (heights[ring] * height);
                float r = radii[ring] * scale;
                for (int side = 0; side < sides; side++)
                {
                    float phi = 2f * Mathf.PI * side / sides;
                    var normal = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    data.AddVertex(new Vector3(cx + (normal.x * r), y, cz + (normal.z * r)), normal, new Vector2((float)side / sides, ring));
                }
            }

            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = first + (ring * sides) + side;
                    int b = first + (ring * sides) + next;
                    int c = first + ((ring + 1) * sides) + side;
                    int d = first + ((ring + 1) * sides) + next;
                    Vector3 outward = data.Normals[a] + data.Normals[b];
                    data.AddTriangle(0, a, b, c, outward);
                    data.AddTriangle(0, b, d, c, outward);
                }
            }
        }

        // Leaf clump arrangements (centre x, y, z and radii x, y, z as fractions of the bounds; the first blob rests on y = 0).
        private static readonly float[][] ClumpBlobs =
        {
            new[] { 0f, 0.5f, 0f, 0.5f, 0.5f, 0.5f, 0.25f, 0.42f, 0.1f, 0.32f, 0.38f, 0.3f, -0.22f, 0.4f, -0.15f, 0.3f, 0.36f, 0.32f },
            new[] { 0f, 0.5f, 0f, 0.46f, 0.5f, 0.46f, -0.28f, 0.38f, 0.12f, 0.3f, 0.34f, 0.3f, 0.2f, 0.55f, -0.22f, 0.28f, 0.36f, 0.3f },
            new[] { 0.05f, 0.48f, 0f, 0.5f, 0.5f, 0.46f, 0.3f, 0.35f, -0.1f, 0.26f, 0.32f, 0.28f, -0.18f, 0.58f, 0.2f, 0.3f, 0.36f, 0.3f },
        };

        /// <summary>
        /// Opaque leaf mass: three overlapping blobs (near: 3 rings x 7 segments each, about 84 triangles; far: one blob,
        /// 3 x 6, about 24). No alpha: the silhouette comes from the lumpy union, which costs no fill-rate.
        /// </summary>
        private static void BuildLeafClump(MeshData data, Bounds bounds, bool near, int variant)
        {
            float[] blobs = ClumpBlobs[Mathf.Clamp(variant, 0, ClumpBlobs.Length - 1)];
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            float height = bounds.size.y;
            int blobCount = near ? 3 : 1;
            int rings = 3;
            int segs = near ? 7 : 6;
            for (int blob = 0; blob < blobCount; blob++)
            {
                int o = blob * 6;
                var center = new Vector3(bounds.center.x + (blobs[o] * width), bounds.min.y + (blobs[o + 1] * height), bounds.center.z + (blobs[o + 2] * width));
                var radii = new Vector3(blobs[o + 3] * width, blobs[o + 4] * height, blobs[o + 5] * width);
                AddEllipsoid(data, 0, center, radii, rings, segs);
            }
        }

        /// <summary>A flat irregular fan of six triangles lying on y = base, normals up.</summary>
        private static void BuildLitter(MeshData data, Bounds bounds)
        {
            float radius = 0.5f * Mathf.Max(bounds.size.x, bounds.size.z);
            float y = bounds.min.y;
            var up = new Vector3(0f, 1f, 0f);
            int hub = data.AddVertex(new Vector3(bounds.center.x, y, bounds.center.z), up, new Vector2(0.5f, 0.5f));
            var rim = new int[6];
            for (int i = 0; i < rim.Length; i++)
            {
                float phi = 2f * Mathf.PI * i / rim.Length;
                float r = radius * (i % 2 == 0 ? 1f : 0.72f);
                rim[i] = data.AddVertex(
                    new Vector3(bounds.center.x + (Mathf.Cos(phi) * r), y, bounds.center.z + (Mathf.Sin(phi) * r)), up, new Vector2(0.5f, 0.5f));
            }

            for (int i = 0; i < rim.Length; i++)
            {
                data.AddTriangle(0, hub, rim[i], rim[(i + 1) % rim.Length], up);
            }
        }

        private static void AddEllipsoid(MeshData data, int sub, Vector3 center, Vector3 radii, int rings, int segs)
        {
            int first = data.Vertices.Count;
            for (int ring = 0; ring <= rings; ring++)
            {
                float theta = Mathf.PI * ring / rings;
                float y = Mathf.Cos(theta);
                float ringRadius = Mathf.Sin(theta);
                for (int seg = 0; seg < segs; seg++)
                {
                    float phi = 2f * Mathf.PI * seg / segs;
                    var unit = new Vector3(ringRadius * Mathf.Cos(phi), y, ringRadius * Mathf.Sin(phi));
                    var normal = new Vector3(unit.x / Mathf.Max(radii.x, 1e-4f), unit.y / Mathf.Max(radii.y, 1e-4f), unit.z / Mathf.Max(radii.z, 1e-4f));
                    data.AddVertex(
                        center + Vector3.Scale(unit, radii), normal.normalized, new Vector2((float)seg / segs, (float)ring / rings));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segs; seg++)
                {
                    int next = (seg + 1) % segs;
                    int a = first + (ring * segs) + seg;
                    int b = first + (ring * segs) + next;
                    int c = first + ((ring + 1) * segs) + seg;
                    int d = first + ((ring + 1) * segs) + next;
                    Vector3 outward = data.Normals[a] + data.Normals[b] + data.Normals[c] + data.Normals[d];
                    data.AddTriangle(sub, a, b, c, outward);
                    data.AddTriangle(sub, b, d, c, outward);
                }
            }
        }

        private static void AddCylinder(MeshData data, int sub, Vector3 p0, Vector3 p1, float r0, float r1, int sides)
        {
            Vector3 axis = (p1 - p0).normalized;
            Vector3 helper = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            int first = data.Vertices.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                Vector3 p = ring == 0 ? p0 : p1;
                float r = ring == 0 ? r0 : r1;
                for (int side = 0; side < sides; side++)
                {
                    float phi = 2f * Mathf.PI * side / sides;
                    Vector3 normal = (u * Mathf.Cos(phi)) + (v * Mathf.Sin(phi));
                    data.AddVertex(p + (normal * r), normal, new Vector2((float)side / sides, ring));
                }
            }

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = first + side;
                int b = first + next;
                int c = first + sides + side;
                int d = first + sides + next;
                Vector3 outward = data.Normals[a] + data.Normals[b];
                data.AddTriangle(sub, a, b, c, outward);
                data.AddTriangle(sub, b, d, c, outward);
            }
        }

        private static void AddQuad(MeshData data, int sub, float yMin, float yMax)
        {
            var normal = new Vector3(0f, 0f, -1f);
            int a = data.AddVertex(new Vector3(-0.5f, yMin, 0f), normal, new Vector2(0f, 0f));
            int b = data.AddVertex(new Vector3(0.5f, yMin, 0f), normal, new Vector2(1f, 0f));
            int c = data.AddVertex(new Vector3(0.5f, yMax, 0f), normal, new Vector2(1f, 1f));
            int d = data.AddVertex(new Vector3(-0.5f, yMax, 0f), normal, new Vector2(0f, 1f));
            data.AddTriangle(sub, a, b, c, normal);
            data.AddTriangle(sub, a, c, d, normal);
        }
    }
}
