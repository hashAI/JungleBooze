using System.Collections.Generic;
using JungleBooze.App.LookTest;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Everything the look-test scatter and landmark builders share: config, layout, materials, CC0 model variants,
    /// the merge plan, and vertex painters. Painters write the Nature Lit vertex colors of merged meshes:
    /// R = wind weight, B = canopy cover (from the layout), A = ambient occlusion (kept from the source mesh).
    /// </summary>
    public sealed class LookTestBuildContext
    {
        public LookTestBuildContext(LookTestConfigAsset config, LookTestStretchLayout layout, LookTestBatchSet batches)
        {
            Config = config;
            Layout = layout;
            Batches = batches;
        }

        public LookTestConfigAsset Config { get; }

        public LookTestStretchLayout Layout { get; }

        public LookTestBatchSet Batches { get; }

        public Material Ground;
        public Material Rootstone;
        public Material Boulder;
        public Material Bark;
        public Material Leaves;
        public Material FarCanopy;
        public Material River;
        public Material Waterfall;
        public Material Pool;
        public Material MistCard;
        public Material LightShaft;
        public readonly List<ModelVariant> Ferns = new List<ModelVariant>();
        public readonly List<ModelVariant> Plants = new List<ModelVariant>();

        public float SegmentLength => Config.SegmentLengthM;

        /// <summary>Segment that owns path distance <paramref name="s"/> (wrapped into the loop).</summary>
        public int SegmentOf(float s)
        {
            float local = Mathf.Repeat(s, Config.LoopLengthM);
            return Mathf.Clamp(Mathf.FloorToInt(local / SegmentLength), 0, Config.SegmentCount - 1);
        }

        /// <summary>Painter for solid meshes (wood, rock): no wind, canopy cover at each vertex's height above the trail at (s, d).</summary>
        public LookTestMeshAccumulator.Painter Solid(float s, float d)
        {
            float trail = Layout.TrailHeight(s);
            return (world, local, source) => new Color(0f, 0f, Layout.CanopyCover(s, d, world.y - trail), source.a);
        }

        /// <summary>
        /// Painter for foliage: wind weight grows with height above the plant base (squared, base planted) up to
        /// <paramref name="sway"/>, canopy cover at (s, d).
        /// </summary>
        public LookTestMeshAccumulator.Painter Foliage(float s, float d, float baseY, float height, float sway)
        {
            float trail = Layout.TrailHeight(s);
            return (world, local, source) =>
            {
                float w = Mathf.Clamp01((world.y - baseY) / Mathf.Max(0.1f, height));
                return new Color(w * w * sway, 0f, Layout.CanopyCover(s, d, world.y - trail), source.a);
            };
        }

        /// <summary>Painter for far scenery (backdrop): open sky, no wind.</summary>
        public static Color Open(Vector3 world, Vector3 local, Color source)
        {
            return new Color(0f, 0f, 0f, source.a);
        }

        /// <summary>Appends a CC0 model variant standing at <paramref name="foot"/>, scaled to <paramref name="height"/>.</summary>
        public void AppendModel(LookTestMeshAccumulator target, ModelVariant variant, Vector3 foot, float yawDeg, float height, LookTestMeshAccumulator.Painter painter)
        {
            float scale = height / Mathf.Max(0.01f, variant.Bounds.size.y);
            float wide = Mathf.Max(variant.Bounds.size.x, variant.Bounds.size.z);
            if (wide * scale > height * 3f)
            {
                scale = height * 3f / wide;
            }

            Matrix4x4 pose = Matrix4x4.Translate(new Vector3(-variant.Bounds.center.x, -variant.Bounds.min.y, -variant.Bounds.center.z)) * variant.Matrix;
            Matrix4x4 matrix = Matrix4x4.TRS(foot, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale) * pose;
            target.Append(variant.Mesh, matrix, painter);
        }

        /// <summary>One mesh of a CC0 model, with its pose inside the model file.</summary>
        public struct ModelVariant
        {
            public string Name;
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 Matrix;
            public Bounds Bounds;
        }
    }
}
