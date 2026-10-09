using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Collects many meshes into one (ENVIRONMENT_STRATEGY 4.3, ADR 0004 fix 1: merge per segment at build time).
    /// Each appended mesh is transformed into the accumulator's space, and a painter can rewrite its vertex colors
    /// (R wind weight, G free, B canopy cover, A ambient occlusion for Nature Lit). All submeshes go into one, so a
    /// merged mesh is one draw call with one material. Editor only; allocates.
    /// </summary>
    public sealed class LookTestMeshAccumulator
    {
        /// <summary>Returns the vertex color for a vertex at world position <paramref name="world"/>.</summary>
        public delegate Color Painter(Vector3 world, Vector3 local, Color source);

        private static readonly Dictionary<Mesh, SourceData> Cache = new Dictionary<Mesh, SourceData>();

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector4> _tangents = new List<Vector4>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector4> _uv1 = new List<Vector4>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _indices = new List<int>();
        private bool _missingTangents;
        private bool _hasUv1;

        public int VertexCount => _vertices.Count;

        public int TriangleCount => _indices.Count / 3;

        public bool IsEmpty => _indices.Count == 0;

        /// <summary>Forgets cached source mesh data (call once per build).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        /// <summary>Appends <paramref name="mesh"/> transformed by <paramref name="matrix"/>.</summary>
        public void Append(Mesh mesh, Matrix4x4 matrix, Painter painter)
        {
            if (mesh == null)
            {
                return;
            }

            SourceData source = Source(mesh);
            int start = _vertices.Count;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            float sign = matrix.determinant < 0f ? -1f : 1f;
            for (int i = 0; i < source.Vertices.Length; i++)
            {
                Vector3 local = source.Vertices[i];
                Vector3 world = matrix.MultiplyPoint3x4(local);
                _vertices.Add(world);
                Vector3 n = source.Normals.Length > i ? normalMatrix.MultiplyVector(source.Normals[i]).normalized : Vector3.up;
                _normals.Add(n);
                if (source.Tangents.Length > i)
                {
                    Vector4 t = source.Tangents[i];
                    Vector3 tw = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                    _tangents.Add(new Vector4(tw.x, tw.y, tw.z, t.w * sign));
                }
                else
                {
                    _tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                    _missingTangents = true;
                }

                _uv0.Add(source.Uv0.Length > i ? source.Uv0[i] : Vector2.zero);
                _uv1.Add(source.Uv1.Count > i ? source.Uv1[i] : Vector4.zero);
                _hasUv1 |= source.Uv1.Count > i;
                Color c = source.Colors.Length > i ? source.Colors[i] : Color.white;
                _colors.Add(painter != null ? painter(world, local, c) : c);
            }

            bool flip = sign < 0f;
            for (int i = 0; i < source.Indices.Length; i += 3)
            {
                _indices.Add(start + source.Indices[i]);
                _indices.Add(start + (flip ? source.Indices[i + 2] : source.Indices[i + 1]));
                _indices.Add(start + (flip ? source.Indices[i + 1] : source.Indices[i + 2]));
            }
        }

        /// <summary>
        /// Appends one camera-facing card for the Atmos Card shader: four vertices sharing <paramref name="center"/>
        /// and <paramref name="axis"/>; corners in UV0, (width, length, seed, kind) in UV1. Kind: 0 = mist or shaft,
        /// 1 = waterfall spray (Atmos Card shader, mist mode).
        /// </summary>
        public void AppendCard(Vector3 center, Vector3 axis, float width, float length, float seed)
        {
            AppendCard(center, axis, width, length, seed, 0f);
        }

        /// <summary>Camera-facing card of a given kind (see the overload above).</summary>
        public void AppendCard(Vector3 center, Vector3 axis, float width, float length, float seed, float kind)
        {
            int start = _vertices.Count;
            var corners = new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-0.5f, 1f), new Vector2(0.5f, 1f) };
            for (int k = 0; k < 4; k++)
            {
                _vertices.Add(center);
                _normals.Add(axis.normalized);
                _tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                _uv0.Add(corners[k]);
                _uv1.Add(new Vector4(width, length, seed, kind));
                _colors.Add(Color.white);
            }

            _hasUv1 = true;
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 1);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTangents(_tangents);
            mesh.SetUVs(0, _uv0);
            if (_hasUv1)
            {
                mesh.SetUVs(1, _uv1);
            }

            mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0, true);
            if (_missingTangents && !_hasUv1)
            {
                mesh.RecalculateTangents();
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static SourceData Source(Mesh mesh)
        {
            if (Cache.TryGetValue(mesh, out SourceData data))
            {
                return data;
            }

            var indices = new List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) == MeshTopology.Triangles)
                {
                    indices.AddRange(mesh.GetIndices(sub));
                }
            }

            var uv1 = new List<Vector4>();
            mesh.GetUVs(1, uv1);
            data = new SourceData
            {
                Vertices = mesh.vertices,
                Normals = mesh.normals,
                Tangents = mesh.tangents,
                Uv0 = mesh.uv,
                Uv1 = uv1,
                Colors = mesh.colors,
                Indices = indices.ToArray(),
            };
            Cache[mesh] = data;
            return data;
        }

        private sealed class SourceData
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] Uv0;
            public List<Vector4> Uv1;
            public Color[] Colors;
            public int[] Indices;
        }
    }
}
