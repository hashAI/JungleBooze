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

        /// <summary>Collision stage. Flags: Side or Top; ObstacleId; Archetype.</summary>
        Stumbled = 13,

        /// <summary>Collision stage.</summary>
        DazeEnded = 14,

        /// <summary>Collision stage. ObstacleId.</summary>
        NearMiss = 15,

        /// <summary>Value = <see cref="DeathCause"/>, Archetype, ObstacleId, Flags: AfterStumble.</summary>
        Died = 16,
    }
}
