using UnityEngine;

namespace JungleBooze.Editor.Perf
{
    /// <summary>One draw (renderer + submesh + material) of a measured view and the fragments it shades.</summary>
    public sealed class PerfDrawSample
    {
        public const string Opaque = "opaque";
        public const string Cutout = "cutout";
        public const string Blended = "blended";
        public const string Sky = "sky";

        public PerfDrawSample(Renderer renderer, int submesh, Material material, string category, string layer, long triangles, float distance)
        {
            Renderer = renderer;
            Submesh = submesh;
            Material = material;
            Category = category;
            Layer = layer;
            Triangles = triangles;
            Distance = distance;
        }

        public Renderer Renderer { get; }
        public int Submesh { get; }
        public Material Material { get; }
        public string Category { get; }
        public string Layer { get; }
        public long Triangles { get; }
        public float Distance { get; }

        /// <summary>Fragments shaded by this draw (pixels of the measured resolution).</summary>
        public double Fragments { get; set; }

        /// <summary>Pixels this draw touches at least once.</summary>
        public long Covered { get; set; }

        public string Name => Renderer != null ? Renderer.name : "sky";
        public string MaterialName => Material != null ? Material.name : "-";
        public string ShaderName => Material != null && Material.shader != null ? Material.shader.name : "-";
    }
}
