namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Bit values for <see cref="RunnerEvent.Flags"/>. Meaning depends on the event type.</summary>
    public static class RunnerEventFlags
    {
        /// <summary>LaneChangeStarted: the move reverses an active move.</summary>
        public const byte Reversal = 1 << 0;

        /// <summary>LaneChangeStarted: the move was started from the lateral queue.</summary>
        public const byte Queued = 1 << 1;

        /// <summary>JumpStarted: the jump cancelled a slide.</summary>
        public const byte FromSlide = 1 << 0;

        /// <summary>JumpStarted: a coyote-time jump. LeftGround: a coyote window was opened.</summary>
        public const byte Coyote = 1 << 1;

        /// <summary>JumpStarted: fired from the input buffer.</summary>
        public const byte Buffered = 1 << 2;

        /// <summary>Landed: the landing ended a fast-fall.</summary>
        public const byte WasFastFall = 1 << 0;

        /// <summary>SlideStarted: the command restarted a running slide.</summary>
        public const byte Restart = 1 << 0;

        /// <summary>Stumbled: side contact.</summary>
        public const byte Side = 1 << 0;

        /// <summary>Stumbled: top contact.</summary>
        public const byte Top = 1 << 1;

        /// <summary>Died: second stumble while dazed.</summary>
        public const byte AfterStumble = 1 << 0;

        /// <summary>ChunkEntered: the chunk is mirrored.</summary>
        public const byte ChunkMirrored = 1 << 0;

        /// <summary>ScoreBonus: the bonus came from a near-miss.</summary>
        public const byte BonusNearMiss = 1 << 0;

        /// <summary>ScoreBonus: the bonus came from a completed coin streak.</summary>
        public const byte BonusStreak = 1 << 1;

        /// <summary>ScoreBonus: the bonus came from a vine release.</summary>
        public const byte BonusVine = 1 << 2;

        /// <summary>VineGrabbed, VineMissed: a chasm lies under the vine.</summary>
        public const byte VineOverChasm = 1 << 0;

        /// <summary>VineReleased: the launch is guided into the next vine of the chain.</summary>
        public const byte VineChained = 1 << 1;

        /// <summary>PowerUpEnded: the power-up was used up (a shield popped by a hit), not timed out.</summary>
        public const byte PowerUpUsedUp = 1 << 0;
    }
}
