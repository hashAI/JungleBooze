namespace JungleBooze.Services.Meta
{
    /// <summary>What one banked run earned from missions and the daily challenge (Game Over panel).</summary>
    public struct MetaRunOutcome
    {
        public int MissionsCompleted;

        /// <summary>Coins paid for the missions completed in this run.</summary>
        public int MissionCoins;

        /// <summary>All 3 missions of the set are done: the set pays and the score multiplier rises.</summary>
        public bool SetCompleted;

        public int SetCoins;

        /// <summary>The score multiplier after a completed set (0 when no set was completed).</summary>
        public int NewScoreMultiplier;

        public bool DailyChallengeCompleted;

        public int DailyChallengeCoins;

        public bool HasAnything => MissionsCompleted > 0 || SetCompleted || DailyChallengeCompleted;
    }
}
