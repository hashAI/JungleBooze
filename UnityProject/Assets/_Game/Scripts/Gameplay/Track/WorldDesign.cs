using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>One world in the run order (GDD 9): which world and how many metres the run stays in it.</summary>
    [Serializable]
    public struct WorldDesign
    {
        public WorldKind Kind;

        public float LengthM;

        public WorldDesign(WorldKind kind, float lengthM)
        {
            Kind = kind;
            LengthM = lengthM;
        }
    }
}
