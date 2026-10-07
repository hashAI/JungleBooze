namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Simulation events for animation, VFX, audio and haptics (spec 001 section 11).</summary>
    public enum RunnerEventType : byte
    {
        None = 0,

        /// <summary>Lane = start lane.</summary>
        RunStarted = 1,

        /// <summary>Value = speed curve row index.</summary>
        SpeedStepReached = 2,

        /// <summary>Value = from lane, Lane = to lane, Dir, Flags: Reversal, Queued.</summary>
        LaneChangeStarted = 3,

        /// <summary>Dir = direction of the cancelled queued move.</summary>
        LaneChangeCancelled = 4,

        /// <summary>Dir = blocked direction.</summary>
        LaneBlocked = 5,

        /// <summary>Flags: FromSlide, Coyote, Buffered.</summary>
        JumpStarted = 6,

        JumpApex = 7,

        FastFallStarted = 8,

        /// <summary>Value = air ticks, Flags: WasFastFall.</summary>
        Landed = 9,

        /// <summary>Flags: Restart.</summary>
        SlideStarted = 10,

        /// <summary>Value = <see cref="SlideEndReason"/>.</summary>
        SlideEnded = 11,

        /// <summary>Flags: Coyote (set when a coyote window was opened).</summary>
        LeftGround = 12,

        /// <summary>Flags: Side or Top; EntityId = obstacle id; Archetype.</summary>
        Stumbled = 13,

        /// <summary>The daze window after a stumble ran out.</summary>
        DazeEnded = 14,

        /// <summary>EntityId = obstacle id; Archetype.</summary>
        NearMiss = 15,

        /// <summary>
        /// Value = <see cref="DeathCause"/>, Archetype, EntityId = obstacle id (0 for a fall), Flags: AfterStumble.
        /// </summary>
        Died = 16,

        // ---- Spec 002 section 13.1 (track, coins, score). Append-only. ----

        /// <summary>Value = chunk library index; Flags: ChunkMirrored, plus the chunk kind in the upper bits (track defines).</summary>
        ChunkEntered = 17,

        /// <summary>Value = tier.</summary>
        TierChanged = 18,

        /// <summary>EntityId = obstacle id, Lane = from lane, Value = to lane.</summary>
        MoverStarted = 19,

        /// <summary>EntityId = obstacle id, Lane = end lane.</summary>
        MoverSettled = 20,

        /// <summary>EntityId = coin id, Lane, Value = coin value.</summary>
        CoinCollected = 21,

        /// <summary>Value = streak length.</summary>
        CoinStreak = 22,

        /// <summary>
        /// Value = points; Flags: BonusNearMiss, BonusStreak or BonusVine (then Archetype holds the
        /// <c>VineReleaseGrade</c>).
        /// </summary>
        ScoreBonus = 23,

        // ---- GDD 7 vine swinging. Append-only. ----

        /// <summary>EntityId = vine id, Lane = vine lane, Value = vine row, Flags: VineOverChasm.</summary>
        VineGrabbed = 24,

        /// <summary>
        /// EntityId = vine id, Lane = aimed (landing or next-vine) lane, Value = <c>VineReleaseGrade</c>,
        /// Flags: VineChained (the launch is guided into the next vine).
        /// </summary>
        VineReleased = 25,

        /// <summary>A vine row was passed without a grab. EntityId = vine id, Lane, Value = row, Flags: VineOverChasm.</summary>
        VineMissed = 26,

        /// <summary>Left/right on a vine. Lane = aimed lane, Dir, EntityId = aimed next vine id (0 = landing pad).</summary>
        VineAimChanged = 27,

        // ---- GDD 15 companion and GDD 14.4 continue. Append-only; numbered from 60 so other features can append
        // below without clashing. ----

        /// <summary>Lift started (GDD 15.1). Lane = HERO's lane, Value = lift ticks planned.</summary>
        CompanionLiftStarted = 60,

        /// <summary>The descent of Lift started (last part of the lift). Lane = HERO's lane.</summary>
        CompanionLiftDescending = 61,

        /// <summary>Lift ended with touchdown. Lane = HERO's lane, Value = lift ticks actually flown.</summary>
        CompanionLiftEnded = 62,

        /// <summary>
        /// A companion call-out (GDD 15.1). Value = <c>CompanionCalloutId</c> (language-neutral), Lane = the lane that
        /// matters (255 = none), EntityId = the vine or obstacle id that triggered it (0 = none).
        /// </summary>
        CompanionCallout = 63,

        /// <summary>The Assist meter just became full. Value = meter percent (100).</summary>
        CompanionMeterFull = 64,

        /// <summary>HERO was brought back by a Continue (GDD 14.4). Lane = respawn lane, Value = invulnerable ticks.</summary>
        Revived = 65,
    }
}
