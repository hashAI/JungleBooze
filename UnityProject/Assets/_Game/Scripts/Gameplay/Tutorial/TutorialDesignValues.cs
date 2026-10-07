namespace JungleBooze.Gameplay.Tutorial
{
    /// <summary>
    /// Onboarding tuning (GDD 12). [ASSUMED] The GDD gives a timeline (3 s, 8 s, 13 s ...); here each lesson starts
    /// when the matching thing first comes up on the generated track, so the numbers are distances and durations.
    /// They move into a ScriptableObject when onboarding is tuned.
    /// </summary>
    public static class TutorialDesignValues
    {
        /// <summary>Game speed while the player must act (GDD 12: "slows to 30% until the player swipes").</summary>
        public const double WaitTimeScale = 0.3;

        /// <summary>A lane obstacle starts its lesson when its front is this close (m).</summary>
        public const double LateralTriggerM = 12.0;

        /// <summary>A low log, low branch or gap starts its lesson when this close (m). Short, so a swipe lands in time.</summary>
        public const double JumpTriggerM = 7.0;

        public const double SlideTriggerM = 7.0;

        /// <summary>A coin counts as a trail to point out when it is between these distances ahead (m).</summary>
        public const double CoinsMinAheadM = 4.0;

        public const double CoinsMaxAheadM = 14.0;

        /// <summary>"Grab coins!" stays on screen this long (s).</summary>
        public const double CoinsHintSeconds = 3.0;

        /// <summary>The vine lesson starts when the first vine is this close (m).</summary>
        public const double VineTriggerM = 22.0;

        /// <summary>The vine lesson ends this far past the vine if nothing happened (m).</summary>
        public const double VineGiveUpPastM = 30.0;

        /// <summary>The companion Assist lesson may start after this much run time (s).</summary>
        public const double AssistAfterSeconds = 30.0;

        /// <summary>The Assist hint gives up after this long (s).</summary>
        public const double AssistHintSeconds = 12.0;

        /// <summary>The tutorial ends by itself after this much run time (s), whatever was not met.</summary>
        public const double MaxSeconds = 75.0;

        /// <summary>"You're on your own!" stays this long (s).</summary>
        public const double OutroSeconds = 3.0;

        /// <summary>Pause between two lessons (s).</summary>
        public const double BetweenLessonsSeconds = 1.5;

        /// <summary>A lane-change lesson ends this long after the swipe (s).</summary>
        public const double LateralHoldSeconds = 0.3;

        /// <summary>No lesson lasts longer than this in run time (s); a safety net.</summary>
        public const double MaxLessonSeconds = 12.0;

        /// <summary>The gentle hint after a rescue stays this long (s).</summary>
        public const double RescueHintSeconds = 3.0;

        /// <summary>The obstacle is counted as passed when HERO is this far beyond its back edge (m).</summary>
        public const double PassedMarginM = 0.5;
    }
}
