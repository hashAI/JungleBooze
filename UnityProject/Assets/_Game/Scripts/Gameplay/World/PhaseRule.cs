using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>One row of the difficulty phase table (spec 102 §5).</summary>
    [Serializable]
    public struct PhaseRule
    {
        public DifficultyPhase Phase;

        /// <summary>Run distance where the phase starts, m.</summary>
        public float StartDistance;

        public int RatingMin;
        public int RatingMax;

        /// <summary>Minimum time between required actions, s (V2, V11).</summary>
        public float MinActionGap;

        public float MaxActionsPerSecond;

        /// <summary>Obstacle visibility before contact, s (V7).</summary>
        public float Visibility;

        /// <summary>A Branch chunk is forced every N picks, N drawn from [min, max] (TrackGeneration stream).</summary>
        public int ForkEveryMin;

        public int ForkEveryMax;

        /// <summary>Most forks in the phase (−1 = no limit; Learning: 1).</summary>
        public int MaxForks;

        /// <summary>A Recovery chunk is forced after this many non-Recovery picks.</summary>
        public int RecoveryEvery;

        public PhaseRule(DifficultyPhase phase, float start, int ratingMin, int ratingMax, float gap, float actions, float visibility, int forkMin, int forkMax, int maxForks, int recovery)
        {
            Phase = phase;
            StartDistance = start;
            RatingMin = ratingMin;
            RatingMax = ratingMax;
            MinActionGap = gap;
            MaxActionsPerSecond = actions;
            Visibility = visibility;
            ForkEveryMin = forkMin;
            ForkEveryMax = forkMax;
            MaxForks = maxForks;
            RecoveryEvery = recovery;
        }
    }
}
