using System;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>What a grounding audit found wrong with one rig (bit flags).</summary>
    [Flags]
    public enum GroundingFailures
    {
        None = 0,

        /// <summary>The visible model starts too far behind the lethal front face.</summary>
        GhostFront = 1,

        /// <summary>The visible model tops out below the lethal top face.</summary>
        GhostTop = 2,

        /// <summary>The visible model leaves a lethal gap at a side.</summary>
        GhostSide = 4,

        /// <summary>The visible model starts above the lethal bottom of an elevated box.</summary>
        GhostBottom = 8,

        /// <summary>A ground-level model hovers above the ground.</summary>
        Floating = 16,

        /// <summary>A ground-level model is not embedded deep enough.</summary>
        EmbedTooShallow = 32,

        /// <summary>No renderer was found in the body.</summary>
        NoVisible = 64,
    }
}
