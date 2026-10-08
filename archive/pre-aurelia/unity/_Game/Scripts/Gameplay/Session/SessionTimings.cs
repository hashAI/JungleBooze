using System;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Real-time durations the session uses outside the simulation (presentation timings, spec 001 sections 8.3
    /// and 10.6, spec 002 section 12.1). Built from the presentation config; plain value type.
    /// </summary>
    public readonly struct SessionTimings
    {
        /// <summary>Spec 002 section 3 / 12.1: Game Over buttons ignore input for 400 ms.</summary>
        public const double DefaultGameOverInputLockSeconds = 0.4;

        public SessionTimings(
            double hitPauseSeconds,
            double deathHoldSeconds,
            double resumeCountdownSeconds,
            double gameOverInputLockSeconds = DefaultGameOverInputLockSeconds)
        {
            if (!(hitPauseSeconds >= 0.0) || !(deathHoldSeconds >= 0.0) || !(resumeCountdownSeconds >= 0.0) ||
                !(gameOverInputLockSeconds >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(hitPauseSeconds), "Timings must not be negative.");
            }

            HitPauseSeconds = hitPauseSeconds;
            DeathHoldSeconds = deathHoldSeconds;
            ResumeCountdownSeconds = resumeCountdownSeconds;
            GameOverInputLockSeconds = gameOverInputLockSeconds;
        }

        /// <summary>Views freeze this long after <c>Died</c> (spec 10.6: 350 ms).</summary>
        public double HitPauseSeconds { get; }

        /// <summary>Camera holds on the cause this long after the hit-pause (spec 10.6: 800 ms).</summary>
        public double DeathHoldSeconds { get; }

        /// <summary>Length of the 3-2-1 countdown (spec 8.3: 1.5 s).</summary>
        public double ResumeCountdownSeconds { get; }

        /// <summary>Game Over buttons and keys are ignored this long after the panel appears (spec 002: 400 ms).</summary>
        public double GameOverInputLockSeconds { get; }

        /// <summary>Spec values: 350 ms hit-pause, 800 ms hold, 1.5 s countdown, 400 ms Game Over input lock.</summary>
        public static SessionTimings CreateDefault()
        {
            return new SessionTimings(0.35, 0.8, 1.5, DefaultGameOverInputLockSeconds);
        }
    }
}
