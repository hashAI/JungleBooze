using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>Interpolated runner state handed to avatars each rendered frame (presentation only).</summary>
    public struct RunnerVisualState
    {
        /// <summary>Feet position in world space.</summary>
        public Vector3 Position;

        /// <summary>Facing (path tangent) in world space.</summary>
        public Quaternion Facing;

        public float Speed;
        public float VLat;
        public float VLatMax;
        public float Vy;
        public float HeightAboveGround;
        public bool Grounded;
        public bool Sliding;
        public bool FastFalling;
        public bool Dead;
        public DeathCause Cause;

        /// <summary>True while i-frames run (the view blinks at 8 Hz).</summary>
        public bool Invulnerable;

        /// <summary>Seconds since the last stumble (large when none).</summary>
        public float SinceStumble;

        /// <summary>Seconds since the last dodge (large when none).</summary>
        public float SinceDodge;

        public int DodgeDirection;

        // ---- Traversal (spec 103) ----

        public MoveMode Mode;

        public DivePhase Dive;

        public bool Leaping;

        public bool Submerged;

        /// <summary>Vine angle while swinging, degrees (+ = forward).</summary>
        public float SwingDeg;

        /// <summary>Hand point on the vine (world) while swinging.</summary>
        public Vector3 Hand;

        /// <summary>Airborne after a vine release.</summary>
        public bool VineAir;

        /// <summary>On canopy beams (balance run).</summary>
        public bool Canopy;

        /// <summary>Water depth over the feet while running (0 = dry), m.</summary>
        public float WadeDepth;
    }
}
