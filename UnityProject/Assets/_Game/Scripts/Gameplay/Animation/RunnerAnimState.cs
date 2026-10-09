namespace JungleBooze.Gameplay.Animation
{
    /// <summary>Base-layer states of the runner's Animator Controller (one state per value, same names).</summary>
    public enum RunnerAnimState : byte
    {
        Idle = 0,
        Locomotion,
        Jump,
        Fall,
        Slide,
        Stumble,
        LandHard,
        Death,

        // ---- Traversal (spec 103). Controllers without these states fall back (see AnimatedRunnerAvatar). ----

        /// <summary>Surface swim: Run clip at a slow rate on a body pitched forward (procedural until swim clips exist).</summary>
        Swim,

        /// <summary>Dive / deep dive: Fall loop on a body pitched head-down.</summary>
        Dive,

        /// <summary>Vine_Grab: hands reach up and close.</summary>
        Grab,

        /// <summary>Vine_Hang: hanging loop for the swing.</summary>
        Hang,

        // ---- Traversal clips (2026-10-09). Fallbacks: Leap → Jump, Underwater → Dive, VineRelease → Fall,
        // Balance → Locomotion, Wade → Locomotion. ----

        /// <summary>Swim_Leap: dolphin leap out of the water.</summary>
        Leap,

        /// <summary>Swim_Underwater: streamlined glide loop (Deep Breath passage).</summary>
        Underwater,

        /// <summary>Vine_Release: lets go of the vine into the airborne tuck.</summary>
        VineRelease,

        /// <summary>Balance_Run: arms out on the canopy beams.</summary>
        Balance,

        /// <summary>Water_Wade: high-knee run through shallow water.</summary>
        Wade,
    }
}
