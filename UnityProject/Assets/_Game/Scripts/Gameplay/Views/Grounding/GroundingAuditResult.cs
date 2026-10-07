namespace JungleBooze.Gameplay.Views
{
    /// <summary>Result of <see cref="GroundingAuditMath.Evaluate"/>: all distances in meters, never negative.</summary>
    public struct GroundingAuditResult
    {
        /// <summary>How far behind the lethal front face the visible model begins (the ghost distance, bounds based).</summary>
        public float GhostFrontM;

        /// <summary>How far visible stone/wood sticks out in front of the lethal face (not a ghost, a proud surface).</summary>
        public float ProudFrontM;

        public float GhostTopM;
        public float GhostBottomM;
        public float GhostLeftM;
        public float GhostRightM;

        /// <summary>Depth of the lowest visible point below the ground (ground-level boxes only).</summary>
        public float EmbedM;

        /// <summary>Height of the lowest visible point above the ground (ground-level boxes only).</summary>
        public float FloatM;

        public GroundingFailures Failures;

        public bool Pass => Failures == GroundingFailures.None;
    }
}
