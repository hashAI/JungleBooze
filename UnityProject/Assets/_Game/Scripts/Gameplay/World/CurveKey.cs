using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// View-side path curvature (spec 102 §2.1), chunk-local: curvature 1/m at <see cref="S"/> (+ = turning right),
    /// linear between keys, 0 outside. Simulation never reads it (path space stays straight); radius ≥ 60 m and
    /// seam zones straight are validator rules.
    /// </summary>
    [Serializable]
    public struct CurveKey
    {
        public float S;
        public float Curvature;

        public CurveKey(float s, float curvature)
        {
            S = s;
            Curvature = curvature;
        }
    }
}
