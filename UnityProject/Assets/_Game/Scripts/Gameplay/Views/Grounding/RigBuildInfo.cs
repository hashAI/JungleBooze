using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>Everything <see cref="ObstacleRigBuilder"/> needs to lay out one rig. One instance is reused by a view.</summary>
    public sealed class RigBuildInfo
    {
        public ObstacleArchetype Kind;
        public int ObstacleId;
        public ulong RunSeed;
        public byte LaneMask;
        public int FromLane;
        public int ToLane;
        public ObstacleShape Shape;
        public float DepthM;
        public float EmbedM;
        public float LaneWidthM;
        public ObstacleVariant Variant;
        public int Skin;
        public int AnchorCount;
        public readonly ContextAnchor[] Anchors = new ContextAnchor[ContextLayout.MaxAnchors];
    }
}
