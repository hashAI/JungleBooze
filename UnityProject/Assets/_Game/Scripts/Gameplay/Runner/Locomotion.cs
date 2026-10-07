namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Locomotion states, spec 001 section 6.1. Stumbling is a flag, not a state.</summary>
    public enum Locomotion : byte
    {
        /// <summary>Grounded, standing hitbox.</summary>
        Running = 0,

        /// <summary>Grounded, short hitbox.</summary>
        Sliding = 1,

        /// <summary>On the analytic jump arc.</summary>
        Airborne = 2,

        /// <summary>Fixed-speed drop after a Slide command in the air.</summary>
        FastFalling = 3,

        /// <summary>Just left the ground without jumping; a Jump is still a ground jump.</summary>
        Coyote = 4,

        /// <summary>No ground: past coyote time, a landing without ground, or below the surface.</summary>
        Falling = 5,

        Dead = 6,

        /// <summary>Hook for later specs (vine, companion). Not entered by this simulation yet.</summary>
        Carried = 7,
    }
}
