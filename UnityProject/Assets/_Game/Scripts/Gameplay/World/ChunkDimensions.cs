using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>Difficulty dimensions 0–3 (spec 102 §2, Blueprint 10.2). Speed comes from distance, not from here.</summary>
    [Serializable]
    public struct ChunkDimensions
    {
        public int Reaction;
        public int Navigation;
        public int Traversal;
        public int Risk;
        public int Complexity;

        public ChunkDimensions(int reaction, int navigation, int traversal, int risk, int complexity)
        {
            Reaction = reaction;
            Navigation = navigation;
            Traversal = traversal;
            Risk = risk;
            Complexity = complexity;
        }
    }
}
