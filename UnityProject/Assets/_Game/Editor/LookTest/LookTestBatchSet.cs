using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// The merge plan of the look test (ENVIRONMENT_STRATEGY 4.3): one mesh per (segment, budget layer, material,
    /// shadow mode). Everything the builder places goes into a batch; <see cref="Emit"/> turns each batch into one
    /// renderer named "&lt;layer&gt; &lt;label&gt; (&lt;material&gt;)", so each batch is one draw call and the
    /// screenshot tool can add up draws and triangles per budget layer. Segment −1 is the backdrop.
    /// </summary>
    public sealed class LookTestBatchSet
    {
        public const int Backdrop = -1;

        private readonly Dictionary<Key, LookTestMeshAccumulator> _batches = new Dictionary<Key, LookTestMeshAccumulator>();
        private readonly List<Key> _order = new List<Key>();

        /// <summary>Which toggle group of the quality panel a batch belongs to.</summary>
        public enum Group
        {
            Ground,
            Water,
            Trees,
            Rocks,
            Plants,
            Detail,
        }

        public int Count => _order.Count;

        /// <summary>The accumulator for a batch (created on first use).</summary>
        public LookTestMeshAccumulator Get(int segment, string layer, string label, Material material, bool castShadows, Group group)
        {
            var key = new Key(segment, layer, label, material, castShadows, group);
            if (!_batches.TryGetValue(key, out LookTestMeshAccumulator accumulator))
            {
                accumulator = new LookTestMeshAccumulator();
                _batches.Add(key, accumulator);
                _order.Add(key);
            }

            return accumulator;
        }

        /// <summary>Visits every non-empty batch in creation order: (segment, layer, label, material, accumulator).</summary>
        public void ForEach(System.Action<int, string, string, Material, LookTestMeshAccumulator> visit)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                Key key = _order[i];
                LookTestMeshAccumulator accumulator = _batches[key];
                if (!accumulator.IsEmpty)
                {
                    visit(key.Segment, key.Layer, key.Label, key.Material, accumulator);
                }
            }
        }

        /// <summary>Renderer name of a batch: the budget layer code comes first.</summary>
        public static string RendererName(string layer, string label, string materialName)
        {
            return layer + " " + label + " (" + materialName + ")";
        }

        /// <summary>Budget layer code of a renderer name ("L1 Walls (Leaves)" → "L1"), or "?".</summary>
        public static string LayerOf(string rendererName)
        {
            if (string.IsNullOrEmpty(rendererName))
            {
                return "?";
            }

            int space = rendererName.IndexOf(' ');
            return space > 0 ? rendererName.Substring(0, space) : "?";
        }

        /// <summary>
        /// Creates the merged meshes (saved through <paramref name="saveMesh"/>) and their renderers under the
        /// segment groups (<paramref name="groupParent"/>(segment, group)). Returns a per-segment summary for the log.
        /// </summary>
        public string Emit(System.Func<Mesh, Mesh> saveMesh, System.Func<int, Group, Transform> groupParent)
        {
            var summary = new SortedDictionary<int, Vector3Int>();
            var text = new StringBuilder();
            for (int i = 0; i < _order.Count; i++)
            {
                Key key = _order[i];
                LookTestMeshAccumulator accumulator = _batches[key];
                if (accumulator.IsEmpty)
                {
                    continue;
                }

                string rendererName = RendererName(key.Layer, key.Label, key.Material.name);
                string meshName = (key.Segment < 0 ? "Backdrop" : "Seg" + key.Segment.ToString("00", CultureInfo.InvariantCulture)) + "_" + Sanitize(rendererName);
                Mesh mesh = saveMesh(accumulator.ToMesh(meshName));
                var go = new GameObject(rendererName);
                go.transform.SetParent(groupParent(key.Segment, key.Group), false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = key.Material;
                meshRenderer.shadowCastingMode = key.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                meshRenderer.receiveShadows = true;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

                summary.TryGetValue(key.Segment, out Vector3Int s);
                summary[key.Segment] = new Vector3Int(s.x + 1, s.y + (key.CastShadows ? 1 : 0), s.z + accumulator.TriangleCount);
            }

            text.AppendLine("Merged renderers per segment (draws, shadow casters, triangles):");
            foreach (KeyValuePair<int, Vector3Int> pair in summary)
            {
                text.Append("  ").Append(pair.Key < 0 ? "Backdrop" : "Segment " + pair.Key).Append(": ")
                    .Append(pair.Value.x).Append(" draws, ").Append(pair.Value.y).Append(" casters, ")
                    .Append(pair.Value.z.ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" tris");
            }

            return text.ToString();
        }

        private static string Sanitize(string name)
        {
            var b = new StringBuilder(name.Length);
            foreach (char ch in name)
            {
                b.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return b.ToString();
        }

        private readonly struct Key : System.IEquatable<Key>
        {
            public Key(int segment, string layer, string label, Material material, bool castShadows, Group group)
            {
                Segment = segment;
                Layer = layer;
                Label = label;
                Material = material;
                CastShadows = castShadows;
                Group = group;
            }

            public int Segment { get; }

            public string Layer { get; }

            public string Label { get; }

            public Material Material { get; }

            public bool CastShadows { get; }

            public Group Group { get; }

            public bool Equals(Key other)
            {
                return Segment == other.Segment && Layer == other.Layer && Label == other.Label && Material == other.Material &&
                       CastShadows == other.CastShadows && Group == other.Group;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Segment;
                    h = (h * 397) ^ (Layer != null ? Layer.GetHashCode() : 0);
                    h = (h * 397) ^ (Label != null ? Label.GetHashCode() : 0);
                    h = (h * 397) ^ (Material != null ? Material.GetInstanceID() : 0);
                    h = (h * 397) ^ (CastShadows ? 1 : 0);
                    return (h * 397) ^ (int)Group;
                }
            }
        }
    }
}
