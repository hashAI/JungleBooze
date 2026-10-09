namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Obstacle classes (spec 101 §4.1, water classes spec 103 §4.6). Gaps are floor, not obstacles (<see cref="IPathQuery"/>). Append only.</summary>
    public enum ObstacleClass : byte
    {
        Low = 0,
        High = 1,
        Blocker = 2,
        Thorns = 3,

        // ---- Water obstacles (spec 103 §4.6). Contact is always a minor "Bump", never Crash or Fall. ----

        /// <summary>−0.40…+0.40 m around the water surface: dive, leap or steer.</summary>
        FloatingLog = 4,

        /// <summary>Bottom +0.50 m above the water, up: dive or steer.</summary>
        LowBranch = 5,

        /// <summary>Riverbed…+0.30 m: leap or steer.</summary>
        Snag = 6,

        /// <summary>Full height: steer or dodge; contact pushes to the near free side.</summary>
        Rock = 7,
    }
}
