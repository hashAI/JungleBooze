using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>A power-up anchor (spec 102 §7), chunk-local; Y is height above the floor. Filled by the director.</summary>
    [Serializable]
    public struct PowerUpSlot
    {
        public float S;
        public float X;
        public float Y;

        /// <summary>On a risky branch (more likely to be filled).</summary>
        public bool OnRisky;

        public PowerUpSlot(float s, float x, float y, bool onRisky)
        {
            S = s;
            X = x;
            Y = y;
            OnRisky = onRisky;
        }
    }
}
