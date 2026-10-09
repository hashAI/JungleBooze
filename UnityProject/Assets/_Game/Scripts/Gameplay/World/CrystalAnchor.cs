using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A crystal position (spec 102 §7, spec 103 §9.1), chunk-local, height above the floor. <see cref="Always"/>
    /// crystals (secrets, script placements) are always there; the others are risky-branch candidates the director
    /// fills by chance (Pickups stream).
    /// </summary>
    [Serializable]
    public struct CrystalAnchor
    {
        public float S;
        public float X;
        public float Y;
        public bool Always;

        public CrystalAnchor(float s, float x, float y, bool always)
        {
            S = s;
            X = x;
            Y = y;
            Always = always;
        }
    }
}
