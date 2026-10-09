using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>One authored waterfall of the hero basin: lip position, foot height, width, layers, and what it lands in.</summary>
    [Serializable]
    public struct HeroFall
    {
        public Vector3 Top;
        public float FootY;
        public float WidthM;
        [Range(1, 4)] public int Layers;
        [Tooltip("Lands in a pool (impact foam) rather than out of sight.")]
        public bool IntoPool;

        public HeroFall(Vector3 top, float footY, float widthM, int layers, bool intoPool)
        {
            Top = top;
            FootY = footY;
            WidthM = widthM;
            Layers = layers;
            IntoPool = intoPool;
        }
    }
}
