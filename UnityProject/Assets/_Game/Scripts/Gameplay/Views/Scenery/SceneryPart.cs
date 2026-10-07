using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// One drawable piece of a scenery model: a mesh submesh with its material and its offset inside the model. All
    /// instances of the part in view are drawn with one Graphics.RenderMeshInstanced
    /// call (one draw call). The instance array is allocated once; filling it never allocates.
    /// </summary>
    public sealed class SceneryPart
    {
        /// <summary>Instance matrices (world space), the first <see cref="Count"/> are valid.</summary>
        public readonly Matrix4x4[] Instances;

        public readonly Mesh Mesh;

        public readonly int SubMesh;

        /// <summary>Offset of this part inside the model (identity for generated meshes).</summary>
        public readonly Matrix4x4 Local;

        public readonly bool LocalIsIdentity;

        public readonly int Triangles;

        public RenderParams Params;

        public int Count;

        public SceneryPart(Mesh mesh, int subMesh, Material material, Matrix4x4 local, int capacity)
        {
            Mesh = mesh;
            SubMesh = subMesh;
            Local = local;
            LocalIsIdentity = local == Matrix4x4.identity;
            Triangles = (int)(mesh.GetIndexCount(subMesh) / 3u);
            Instances = new Matrix4x4[capacity];
            Params = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };
        }
    }
}
