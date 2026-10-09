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
    }
}
