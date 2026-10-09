namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Simulation → presentation events (ring buffer, no delegates). Append only.</summary>
    public enum RunEventType : byte
    {
        None = 0,
        RunStarted,
        Jump,
        /// <summary>Value = fall height (m). Reason = <see cref="LandingKind"/>.</summary>
        Land,
        SlideStart,
        SlideEnd,
        FastFall,
        /// <summary>Reason = 0 left, 1 right.</summary>
        Dodge,
        EdgeBrush,
        /// <summary>Id = obstacle, Reason = <see cref="HitKind"/>.</summary>
        Hit,
        /// <summary>Id = obstacle, Reason = <see cref="HitKind"/> absorbed.</summary>
        ShieldConsumed,
        /// <summary>Id = obstacle (−1 for falls), Reason = <see cref="DeathCause"/>.</summary>
        Died,
        /// <summary>Reason = <see cref="DropReason"/>, Value = (float)command.</summary>
        InputDropped,
        LedgeAssist,
        Regen,
        /// <summary>Id = coin.</summary>
        Coin,
        /// <summary>Id = fork index.</summary>
        ForkNudge,
        Finished,
        Revived,
        /// <summary>A Low obstacle with a walkable top became floor. Id = obstacle.</summary>
        WalkableLanding,

        /// <summary>A timed shield ran out unused.</summary>
        ShieldExpired,

        // ---- World / expedition events (spec 102–103), appended by the run tracker after the simulation step. ----

        /// <summary>Id = crystal id.</summary>
        Crystal,

        /// <summary>Id = power-up id, Reason = <c>PowerUpKind</c>.</summary>
        PowerUp,

        /// <summary>Id = discovery entry index, Reason = 1 first time / 0 seen again.</summary>
        Discovery,

        /// <summary>Value = bonus coins, Id = chunk serial.</summary>
        CleanLine,

        /// <summary>Id = chunk serial, Reason = <c>RouteType</c>.</summary>
        RouteChosen,

        /// <summary>Id = chunk serial (the runner crossed its entry seam).</summary>
        ChunkEntered,
    }
}
