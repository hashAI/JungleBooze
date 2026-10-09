using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A Deep Breath zone (spec 103 §4.5), chunk-local: a dive that starts in [SMin, SMax] × [XMin, XMax] with the
    /// ability follows the underwater passage and surfaces at (ExitS, ExitX). Zones are ≥ 8 m long.
    /// </summary>
    [Serializable]
    public struct DeepDiveZone
    {
        public float SMin;
        public float SMax;
        public float XMin;
        public float XMax;
        public float ExitS;
        public float ExitX;
        public string Note;
    }
}
