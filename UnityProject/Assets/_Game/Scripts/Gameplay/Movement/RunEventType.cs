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

        // ---- Traversal (spec 103 §4–7), appended by the simulation and the run tracker. ----

        /// <summary>Started swimming. Reason = 1 splash entry (from the air), 0 waded in.</summary>
        WaterEnter,

        /// <summary>Waded out of the water.</summary>
        WaterExit,

        /// <summary>A dive started (swim). Reason = 1 when it is a deep dive.</summary>
        Dive,

        /// <summary>A dive ended at the surface.</summary>
        Surface,

        /// <summary>A leap out of the water.</summary>
        Leap,

        /// <summary>Splash-down after a leap or a fall into water (the water's landing tick).</summary>
        Splash,

        /// <summary>Deep Breath dive into a zone. Id = zone id.</summary>
        DeepDiveStart,

        /// <summary>Surfaced at the end of a deep dive. Id = zone id.</summary>
        DeepDiveEnd,

        /// <summary>Grabbed a vine. Id = vine id.</summary>
        VineGrab,

        /// <summary>Let go of a vine. Id = vine id, Reason = 1 Perfect / 0 Good, Value = swing tick of the release.</summary>
        VineRelease,

        /// <summary>A Perfect release (spec 103 §5.4). Id = vine id, Value = coins credited.</summary>
        PerfectRelease,

        /// <summary>Two Perfects on one span. Id = chunk serial, Value = bonus coins.</summary>
        PerfectSpan,

        /// <summary>Beam landing assist snapped her onto a beam (spec 103 §6).</summary>
        BeamAssist,

        /// <summary>A sailback changed state. Id = creature slot, Reason = <c>CreatureState</c>.</summary>
        CreatureState,

        /// <summary>A traversal finished (analytics, DDA). Id = obstacle (−1), Reason = <c>TraversalKind</c>, Value = 1 success / 0 fail.</summary>
        TraversalResult,

        /// <summary>Vista camera beat (spec 103 §11 beat 7). Id = discovery entry.</summary>
        Vista,

        /// <summary>The runner passed through a water curtain (spec 103 §8.3).</summary>
        CurtainPass,
    }
}
