using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Traversal data for Part B (spec 103 §10.3: water volumes, deep-dive zones, vines, beams, creature spawns),
    /// chunk-local. Part A keeps the data and draws a tinted gray-box stand-in; nothing reads it in the simulation yet.
    /// <see cref="Value"/> is mode-specific (current m/s, anchor height, …).
    /// </summary>
    [Serializable]
    public struct TraversalZone
    {
        public TraversalMode Mode;
        public float SMin;
        public float SMax;
        public float XMin;
        public float XMax;
        public float Value;
        public string Note;
    }
}
