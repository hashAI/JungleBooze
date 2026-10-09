using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A water volume (spec 103 §4.2), chunk-local: the water surface over [SMin, SMax] × [XMin, XMax]. Depth is the
    /// surface minus the floor (riverbed) under Pista, so the floor patches author the depth profile. Water is never
    /// a hazard by itself.
    /// </summary>
    [Serializable]
    public struct WaterVolume
    {
        public float SMin;
        public float SMax;
        public float XMin;
        public float XMax;
        public float SurfaceY;

        public bool Contains(float s, float x)
        {
            return s >= SMin && s < SMax && x >= XMin && x <= XMax;
        }
    }
}
