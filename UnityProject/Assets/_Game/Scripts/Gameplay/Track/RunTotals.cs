namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Run totals for the HUD, Game Over and sim reports (spec 002 section 13.2). Plain value; read it from
    /// <see cref="RunScoring.Totals"/> after the frame's steps.
    /// </summary>
    public struct RunTotals
    {
        /// <summary>Distance run (m); <c>heroZ</c>, never negative. HUD shows <c>floor(DistanceM)</c>.</summary>
        public double DistanceM;

        /// <summary>Coins collected this run (sum of coin values).</summary>
        public int Coins;

        /// <summary><c>(floor(distanceM × pointsPerMeter) + bonusScore) × scoreMultiplier</c> (section 10.6).</summary>
        public long Score;

        /// <summary>Near-miss and coin-streak bonuses.</summary>
        public long BonusScore;

        /// <summary>Consecutive collected coins (resets on a miss, a stumble, or a completed streak).</summary>
        public int Streak;

        /// <summary>Completed coin streaks.</summary>
        public int StreaksCompleted;

        /// <summary>Near-misses that paid a bonus.</summary>
        public int NearMisses;

        /// <summary>Coins that passed HERO in his lane uncollected (missed-coin rule, section 10.5).</summary>
        public int CoinsMissed;

        /// <summary>Tier of the chunk HERO is in (1-based).</summary>
        public int Tier;

        /// <summary>Library index of the chunk HERO is in (-1 before the first tick).</summary>
        public int CurrentChunkIndex;
    }
}
