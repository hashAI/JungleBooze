using System.Collections.Generic;
using System.Text;
using JungleBooze.Core;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Scenery
{
    /// <summary>
    /// The environment assets that have landed under <see cref="EnvironmentAssetRules.Root"/> (ADR 0008): model
    /// pieces by role, their texture sets, and the backdrop layers. The look-test builder asks the kit for a piece
    /// at each placement hook (stiltwoods, basin pillars, hero arch, far range, backdrop) and falls back to its
    /// procedural stand-in when the kit has none, so art can land piece by piece. Everything a piece contributes is
    /// appended to the same per-segment merge batches as the stand-ins (merged per segment, one draw per material).
    /// Editor only.
    /// </summary>
    public sealed class EnvironmentKit
    {
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly List<BackdropLayer> _backdrops = new List<BackdropLayer>();

        /// <summary>One model file: its meshes with their pose inside the file, bounds, role and texture set.</summary>
        public sealed class Piece
        {
            public string Name;
            public string Path;
            public EnvironmentRole Role;
            public readonly List<Part> Parts = new List<Part>();
            public Bounds Bounds;
            public TextureSet Textures;

            /// <summary>Named empties in the model (for example "WaterfallMouth"), position in the piece's space.</summary>
            public readonly Dictionary<string, Vector3> Anchors = new Dictionary<string, Vector3>();

            /// <summary>Own Nature Lit material (from <see cref="Textures"/>), or null to use the stand-in material.</summary>
            public Material Material;

            public int TriangleCount
            {
                get
                {
                    long count = 0;
                    for (int i = 0; i < Parts.Count; i++)
                    {
                        for (int sub = 0; sub < Parts[i].Mesh.subMeshCount; sub++)
                        {
                            count += Parts[i].Mesh.GetIndexCount(sub) / 3;
                        }
                    }

                    return (int)count;
                }
            }
        }

        public struct Part
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
        }

        public struct TextureSet
        {
            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Arm;
            public Texture2D Alpha;

            public bool HasAny => Albedo != null || Normal != null || Arm != null;
        }

        public sealed class BackdropLayer
        {
            public int Order;
            public string Name;
            public Texture2D Texture;
        }

        public IReadOnlyList<Piece> Pieces => _pieces;

        /// <summary>Backdrop layers, farthest (lowest order) first.</summary>
        public IReadOnlyList<BackdropLayer> Backdrops => _backdrops;

        public bool IsEmpty => _pieces.Count == 0 && _backdrops.Count == 0;

        /// <summary>An empty kit (no environment assets): every hook uses its stand-in.</summary>
        public static EnvironmentKit Empty()
        {
            return new EnvironmentKit();
        }

        /// <summary>Scans the environment folder (missing folders are fine).</summary>
        public static EnvironmentKit Scan()
        {
            var kit = new EnvironmentKit();
            string root = EnvironmentAssetRules.Root.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(root))
            {
                return kit;
            }

            var sets = new Dictionary<string, TextureSet>();
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                EnvironmentTextureKind kind = EnvironmentAssetRules.TextureKindOf(path);
                if (kind == EnvironmentTextureKind.Backdrop)
                {
                    int order = EnvironmentAssetRules.BackdropOrder(path);
                    if (order >= 0)
                    {
                        kit._backdrops.Add(new BackdropLayer
                        {
                            Order = order,
                            Name = System.IO.Path.GetFileNameWithoutExtension(path),
                            Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                        });
                    }

                    continue;
                }

                string key = SetKey(path, EnvironmentAssetRules.TextureSetName(path));
                sets.TryGetValue(key, out TextureSet set);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                switch (kind)
                {
                    case EnvironmentTextureKind.Albedo:
                        set.Albedo = texture;
                        break;
                    case EnvironmentTextureKind.Normal:
                        set.Normal = texture;
                        break;
                    case EnvironmentTextureKind.Arm:
                        set.Arm = texture;
                        break;
                    case EnvironmentTextureKind.Alpha:
                        set.Alpha = texture;
                        break;
                }

                sets[key] = set;
            }

            kit._backdrops.Sort((a, b) => a.Order.CompareTo(b.Order));

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { root });
            for (int i = 0; i < modelGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                EnvironmentRole role = EnvironmentAssetRules.RoleOf(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (role == EnvironmentRole.Unknown || model == null)
                {
                    continue;
                }

                var piece = new Piece { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = path, Role = role };
                Matrix4x4 toRoot = model.transform.worldToLocalMatrix;
                bool first = true;
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || IsLowerLod(filter.name))
                    {
                        continue;
                    }

                    Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                    piece.Parts.Add(new Part { Mesh = filter.sharedMesh, Matrix = matrix });
                    Bounds b = TransformBounds(matrix, filter.sharedMesh.bounds);
                    if (first)
                    {
                        piece.Bounds = b;
                        first = false;
                    }
                    else
                    {
                        piece.Bounds.Encapsulate(b);
                    }
                }

                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponent<MeshFilter>() == null && t != model.transform)
                    {
                        string anchor = t.name;
                        int cut = anchor.LastIndexOf('_');
                        piece.Anchors[cut >= 0 ? anchor.Substring(cut + 1) : anchor] = toRoot.MultiplyPoint3x4(t.position);
                    }
                }

                if (piece.Parts.Count == 0 || piece.Bounds.size.y <= 0.001f)
                {
                    continue;
                }

                string ownName = piece.Name.ToLowerInvariant();
                if (role == EnvironmentRole.PlantClump && ownName.EndsWith("_clump", System.StringComparison.Ordinal))
                {
                    ownName = ownName.Substring(0, ownName.Length - "_clump".Length);
                }

                string own = SetKey(path, ownName);
                string kitSet = SetKey(path, EnvironmentAssetRules.KitSetName(role) ?? string.Empty);
                if (sets.TryGetValue(own, out TextureSet ownSet) && ownSet.HasAny)
                {
                    piece.Textures = ownSet;
                }
                else if (sets.TryGetValue(kitSet, out TextureSet shared) && shared.HasAny)
                {
                    piece.Textures = shared;
                }
                else if (role == EnvironmentRole.PlantClump && sets.TryGetValue(SetKey(path, ownName.Replace("fern", "fronds")), out TextureSet fronds) && fronds.HasAny)
                {
                    // Plants/README: the fern clump uses the FP_Fronds atlas.
                    piece.Textures = fronds;
                }

                kit._pieces.Add(piece);
            }

            kit._pieces.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return kit;
        }

        public List<Piece> Of(EnvironmentRole role)
        {
            var list = new List<Piece>();
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].Role == role)
                {
                    list.Add(_pieces[i]);
                }
            }

            return list;
        }

        /// <summary>A pick among <paramref name="role"/>, else among <paramref name="fallback"/>, or null.</summary>
        public Piece Pick(EnvironmentRole role, EnvironmentRole fallback, IRandom rng)
        {
            return Of(role).Count > 0 ? Pick(role, rng) : Pick(fallback, rng);
        }

        /// <summary>A seeded pick among the pieces of <paramref name="role"/>, or null if there are none.</summary>
        public Piece Pick(EnvironmentRole role, IRandom rng)
        {
            List<Piece> list = Of(role);
            return list.Count == 0 ? null : list[rng.NextInt(0, list.Count)];
        }

        /// <summary>
        /// Placement that stands a piece on <paramref name="foot"/> (bounds base centre), turned by
        /// <paramref name="yawDeg"/>, scaled to <paramref name="height"/>; the footprint is scaled toward
        /// <paramref name="footprintRadius"/> (0 = uniform), within 0.6-1.6x of the uniform scale so shapes are not smeared.
        /// </summary>
        public static Matrix4x4 Stand(Piece piece, Vector3 foot, float yawDeg, float height, float footprintRadius)
        {
            Bounds b = piece.Bounds;
            float sy = height / Mathf.Max(0.01f, b.size.y);
            float sxz = sy;
            if (footprintRadius > 0f)
            {
                float wide = Mathf.Max(b.size.x, b.size.z);
                sxz = Mathf.Clamp(2f * footprintRadius / Mathf.Max(0.01f, wide), sy * 0.6f, sy * 1.6f);
            }

            Matrix4x4 center = Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z));
            return Matrix4x4.TRS(foot, Quaternion.Euler(0f, yawDeg, 0f), new Vector3(sxz, sy, sxz)) * center;
        }

        /// <summary>
        /// Placement that spans an arch piece (local X between its feet) from <paramref name="footA"/> to
        /// <paramref name="footB"/> with its top at <paramref name="topY"/>.
        /// </summary>
        public static Matrix4x4 Span(Piece piece, Vector3 footA, Vector3 footB, float topY)
        {
            Bounds b = piece.Bounds;
            Vector3 across = footB - footA;
            across.y = 0f;
            float span = Mathf.Max(0.01f, across.magnitude);
            float baseY = Mathf.Min(footA.y, footB.y);
            float sx = span / Mathf.Max(0.01f, b.size.x);
            float sy = Mathf.Max(0.01f, topY - baseY) / Mathf.Max(0.01f, b.size.y);
            float sz = 0.5f * (sx + sy);
            Quaternion yaw = Quaternion.FromToRotation(Vector3.right, across / span);
            Vector3 mid = (footA + footB) * 0.5f;
            mid.y = baseY;
            Matrix4x4 center = Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z));
            return Matrix4x4.TRS(mid, yaw, new Vector3(sx, sy, sz)) * center;
        }

        public string Summary()
        {
            var text = new StringBuilder();
            text.Append("Environment kit (").Append(EnvironmentAssetRules.Root).Append("): ");
            if (IsEmpty)
            {
                text.Append("nothing yet, procedural stand-ins used.");
                return text.ToString();
            }

            text.Append(_pieces.Count).Append(" pieces, ").Append(_backdrops.Count).Append(" backdrop layers.");
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece p = _pieces[i];
                text.AppendLine().Append("  ").Append(p.Role).Append(": ").Append(p.Name).Append(" (")
                    .Append(p.TriangleCount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Append(" tris, ")
                    .Append(p.Textures.HasAny ? "own textures" : "stand-in material").Append(")");
            }

            for (int i = 0; i < _backdrops.Count; i++)
            {
                text.AppendLine().Append("  Backdrop ").Append(_backdrops[i].Order).Append(": ").Append(_backdrops[i].Name);
            }

            return text.ToString();
        }

        private static string SetKey(string assetPath, string setName)
        {
            return EnvironmentAssetRules.SetFolder(assetPath) + "/" + setName;
        }

        private static bool IsLowerLod(string meshName)
        {
            int index = meshName.LastIndexOf("_LOD", System.StringComparison.OrdinalIgnoreCase);
            return index >= 0 && index + 4 < meshName.Length && meshName.Substring(index + 4) != "0";
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var result = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }

            return result;
        }
    }
}
