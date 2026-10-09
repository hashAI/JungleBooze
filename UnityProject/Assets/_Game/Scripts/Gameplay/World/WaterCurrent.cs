using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>A current field (spec 103 §4.2), chunk-local over [SMin, SMax): lateral (+ = right) and forward, m/s.</summary>
    [Serializable]
    public struct WaterCurrent
    {
        public float SMin;
        public float SMax;
        public float Lateral;
        public float Forward;
    }
}
