using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A route split (spec 102 §3.3) in chunk-local coordinates: a solid divider (root wall, waterfall pillar) from
    /// <see cref="SFront"/> to <see cref="SMerge"/>. Lanes are the free intervals between dividers inside the outer
    /// path edges; a runner is on the side of the divider centre her x is on (tie → <see cref="SafeSide"/>).
    /// </summary>
    [Serializable]
    public struct ChunkDivider
    {
        public string Name;
        public float SFront;
        public float SMerge;
        public float CenterX;
        public float HalfWidth;

        /// <summary>−1 = the left side is the safer one, +1 = right (ties and the fork nudge default).</summary>
        public int SafeSide;

        public float XMin => CenterX - HalfWidth;

        public float XMax => CenterX + HalfWidth;
    }
}
