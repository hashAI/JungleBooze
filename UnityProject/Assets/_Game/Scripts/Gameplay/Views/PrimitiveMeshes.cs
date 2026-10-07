using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Procedural stand-ins for Unity's built-in primitives, built once in code and cached. GameObject.CreatePrimitive is
    /// not usable in a player: it adds a collider, and the physics module is stripped from the iOS build, so every call
    /// logs "Can't add component ... doesn't exist" and its material is the built-in Standard material (magenta under URP).
    /// Sizes match the built-ins: cube 1 m, sphere diameter 1, capsule height 2 and diameter 1, cylinder height 2 and
    /// diameter 1, quad 1 x 1 facing -Z. Objects get a MeshFilter and MeshRenderer only (no collider, shadows off).
    /// Setup-time only (allocates).
    /// </summary>
    public static class PrimitiveMeshes
    {
        private const int Segments = 24;
        private const int HemisphereRings = 8;
        private const float Radius = 0.5f;

        private static readonly Mesh[] Cache = new Mesh[6];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < Cache.Length; i++)
            {
                Cache[i] = null;
            }
        }

        /// <summary>The cached mesh for <paramref name="type"/> (Cube, Sphere, Capsule, Cylinder or Quad).</summary>
        public static Mesh Get(PrimitiveType type)
        {
            int index = (int)type;
            if (index < 0 || index >= Cache.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(type), "Unsupported primitive " + type);
            }

            if (Cache[index] == null)
            {
                Cache[index] = Build(type);
                Cache[index].hideFlags = HideFlags.HideAndDontSave;
            }

            return Cache[index];
        }

        /// <summary>A GameObject named <paramref name="name"/> under <paramref name="parent"/> (local pose identity) drawing the primitive.</summary>
        public static GameObject Create(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Get(type);
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return go;
        }

        private static Mesh Build(PrimitiveType type)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            switch (type)
            {
                case PrimitiveType.Cube:
                    BuildCube(vertices, normals, uvs, triangles);
                    break;
                case PrimitiveType.Quad:
                    BuildQuad(vertices, normals, uvs, triangles);
                    break;
                case PrimitiveType.Sphere:
                    BuildRevolution(vertices, normals, uvs, triangles, 0f);
                    break;
                case PrimitiveType.Capsule:
                    BuildRevolution(vertices, normals, uvs, triangles, 0.5f);
                    break;
                case PrimitiveType.Cylinder:
                    BuildCylinder(vertices, normals, uvs, triangles);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Unsupported primitive " + type);
            }

            var mesh = new Mesh { name = "Procedural" + type };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Adds a triangle wound so its front (clockwise as seen in Unity) faces <paramref name="outward"/>. Degenerate
        /// triangles (sphere poles) are skipped.
        /// </summary>
        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3 outward)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (cross.sqrMagnitude < 1e-12f)
            {
                return;
            }

            triangles.Add(a);
            if (Vector3.Dot(cross, outward) >= 0f)
            {
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        private static void AddQuad(
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 center, Vector3 u, Vector3 v, Vector3 normal)
        {
            int start = vertices.Count;
            vertices.Add(center - u - v);
            vertices.Add(center + u - v);
            vertices.Add(center + u + v);
            vertices.Add(center - u + v);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }

            AddTriangle(vertices, triangles, start, start + 1, start + 2, normal);
            AddTriangle(vertices, triangles, start, start + 2, start + 3, normal);
        }

        private static void BuildQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            AddQuad(vertices, normals, uvs, triangles, Vector3.zero, new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0f, -1f));
        }

        private static void BuildCube(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            const float h = 0.5f;
            AddQuad(vertices, normals, uvs, triangles, new Vector3(h, 0f, 0f), new Vector3(0f, 0f, h), new Vector3(0f, h, 0f), Vector3.right);
            AddQuad(vertices, normals, uvs, triangles, new Vector3(-h, 0f, 0f), new Vector3(0f, 0f, h), new Vector3(0f, h, 0f), Vector3.left);
            AddQuad(vertices, normals, uvs, triangles, new Vector3(0f, h, 0f), new Vector3(h, 0f, 0f), new Vector3(0f, 0f, h), Vector3.up);
            AddQuad(vertices, normals, uvs, triangles, new Vector3(0f, -h, 0f), new Vector3(h, 0f, 0f), new Vector3(0f, 0f, h), Vector3.down);
            AddQuad(vertices, normals, uvs, triangles, new Vector3(0f, 0f, h), new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), Vector3.forward);
            AddQuad(vertices, normals, uvs, triangles, new Vector3(0f, 0f, -h), new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), Vector3.back);
        }

        /// <summary>
        /// Sphere (<paramref name="centerOffset"/> 0) or capsule (0.5: two hemispheres 1 m apart, total height 2).
        /// Rings run pole to pole; the equator ring is listed twice, which makes the capsule's straight section.
        /// </summary>
        private static void BuildRevolution(
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, float centerOffset)
        {
            int ringCount = HemisphereRings * 2 + 2;
            for (int ring = 0; ring < ringCount; ring++)
            {
                bool top = ring <= HemisphereRings;
                int step = top ? ring : ring - HemisphereRings - 1;
                float theta = (Mathf.PI * 0.5f) * step / HemisphereRings + (top ? 0f : Mathf.PI * 0.5f);
                float offset = top ? centerOffset : -centerOffset;
                float sin = Mathf.Sin(theta);
                float cos = Mathf.Cos(theta);
                float v = 1f - (float)ring / (ringCount - 1);
                for (int s = 0; s <= Segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / Segments;
                    var normal = new Vector3(sin * Mathf.Cos(phi), cos, sin * Mathf.Sin(phi));
                    vertices.Add(new Vector3(normal.x * Radius, normal.y * Radius + offset, normal.z * Radius));
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)s / Segments, v));
                }
            }

            int stride = Segments + 1;
            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int a = ring * stride + s;
                    int b = a + 1;
                    int c = a + stride + 1;
                    int d = a + stride;
                    Vector3 outward = normals[a] + normals[b] + normals[c] + normals[d];
                    AddTriangle(vertices, triangles, a, b, c, outward);
                    AddTriangle(vertices, triangles, a, c, d, outward);
                }
            }
        }

        private static void BuildCylinder(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            const float halfHeight = 1f;
            for (int ring = 0; ring < 2; ring++)
            {
                float y = ring == 0 ? halfHeight : -halfHeight;
                for (int s = 0; s <= Segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / Segments;
                    var normal = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    vertices.Add(new Vector3(normal.x * Radius, y, normal.z * Radius));
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)s / Segments, ring == 0 ? 1f : 0f));
                }
            }

            int stride = Segments + 1;
            for (int s = 0; s < Segments; s++)
            {
                int a = s;
                int b = s + 1;
                int c = stride + s + 1;
                int d = stride + s;
                Vector3 outward = normals[a] + normals[b];
                AddTriangle(vertices, triangles, a, b, c, outward);
                AddTriangle(vertices, triangles, a, c, d, outward);
            }

            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? halfHeight : -halfHeight;
                Vector3 normal = cap == 0 ? Vector3.up : Vector3.down;
                int center = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= Segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / Segments;
                    vertices.Add(new Vector3(Mathf.Cos(phi) * Radius, y, Mathf.Sin(phi) * Radius));
                    normals.Add(normal);
                    uvs.Add(new Vector2(0.5f + Mathf.Cos(phi) * 0.5f, 0.5f + Mathf.Sin(phi) * 0.5f));
                }

                for (int s = 0; s < Segments; s++)
                {
                    AddTriangle(vertices, triangles, center, center + 1 + s, center + 2 + s, normal);
                }
            }
        }
    }
}
