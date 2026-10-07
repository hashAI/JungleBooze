using System.Collections.Generic;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Generated low-poly meshes for the scenery system: the far LOD of every prop (fitted to the bounds of the real
    /// art, so the swap does not change the size) and the gray-box stand-in when an art slot is missing. All have
    /// pivots at the base (y = 0 of the bounds' minimum), outward normals and no UV detail. Submesh 0 is bark / body,
    /// submesh 1 (trees and trunks only) is leaves. Setup-time only (allocates).
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

        /// <summary>Builds the mesh for <paramref name="shape"/> fitted to <paramref name="bounds"/>.</summary>
        public static Mesh Build(SceneryShape shape, Bounds bounds, int detail)
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

            switch (shape)
            {
                case SceneryShape.Tree:
                    AddCylinder(data, 0, new Vector3(cx, baseY, cz), new Vector3(cx, baseY + (0.7f * height), cz), 0.09f * width, 0.05f * width, sides);
                    AddEllipsoid(data, 1, new Vector3(cx, baseY + (0.7f * height), cz), new Vector3(0.5f * width, 0.3f * height, 0.5f * width), rings + 1, segs + 2);
                    break;
                case SceneryShape.Trunk:
                    float trunkRadius = 0.5f * width * 0.45f;
                    AddCylinder(data, 0, new Vector3(cx, baseY, cz), new Vector3(cx, baseY + (0.93f * height), cz), trunkRadius, trunkRadius * 0.6f, sides + 2);
                    AddEllipsoid(data, 1, new Vector3(cx, baseY + (0.95f * height), cz), new Vector3(0.45f * width, 0.06f * height, 0.45f * width), rings, segs + 2);
                    break;
                case SceneryShape.Blob:
                    AddEllipsoid(data, 0, new Vector3(cx, baseY + (0.5f * height), cz), size * 0.5f, rings, segs);
                    break;
                case SceneryShape.Root:
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
                default:
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
