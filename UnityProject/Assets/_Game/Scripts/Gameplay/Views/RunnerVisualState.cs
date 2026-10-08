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
    }
}
