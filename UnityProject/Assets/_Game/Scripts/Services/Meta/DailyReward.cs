namespace JungleBooze.Services.Meta
{
    /// <summary>One day of the 7-day calendar (GDD 13.3).</summary>
    public readonly struct DailyReward
    {
        public DailyReward(int dayIndex, DailyRewardKind kind, int amount, int outfitPieces)
        {
            DayIndex = dayIndex;
            Kind = kind;
            Amount = amount;
            OutfitPieces = outfitPieces;
        }

        /// <summary>0-based day of the calendar (0 = day 1).</summary>
        public int DayIndex { get; }

        public DailyRewardKind Kind { get; }

        /// <summary>Coins, or the number of Head Starts / Shield starts.</summary>
        public int Amount { get; }

        /// <summary>Exclusive outfit pieces given on top (day 7).</summary>
        public int OutfitPieces { get; }
    }
}
