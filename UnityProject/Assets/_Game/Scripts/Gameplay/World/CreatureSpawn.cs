using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Sailback spawn (spec 103 §7.2), chunk-local: a perch at (S, X, Y above the floor) with <see cref="Count"/>
    /// animals spaced <see cref="Spacing"/> m along s, gliding to the spline end (S + EndDS, EndX, EndY above the
    /// floor at the start) once launched. <see cref="LaunchDistance"/> overrides the config (&lt; 0 = never launches:
    /// roosting). <see cref="Ambient"/> animals are scenery only (never observed, never discovered).
    /// </summary>
    [Serializable]
    public struct CreatureSpawn
    {
        public string EntryId;
        public float S;
        public float X;
        public float Y;
        public int Count;
        public float Spacing;
        public float EndDS;
        public float EndX;
        public float EndY;
        public float LaunchDistance;
        public bool Ambient;
        public string Note;
    }
}
