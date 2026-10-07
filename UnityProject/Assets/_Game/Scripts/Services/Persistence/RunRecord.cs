namespace JungleBooze.Services.Persistence
{
    /// <summary>What <see cref="PlayerSave.RecordRun"/> stored for one finished run (Game Over panel).</summary>
    public readonly struct RunRecord
    {
        public RunRecord(long score, long distanceM, int coins, long previousBestScore, long totalCoins)
        {
            Score = score;
            DistanceM = distanceM;
            Coins = coins;
            PreviousBestScore = previousBestScore;
            TotalCoins = totalCoins;
            IsValid = true;
        }

        /// <summary>False for the default value (no run recorded yet).</summary>
        public bool IsValid { get; }

        public long Score { get; }

        public long DistanceM { get; }

        /// <summary>Coins collected in the run (added to the wallet).</summary>
        public int Coins { get; }

        /// <summary>Best score before this run.</summary>
        public long PreviousBestScore { get; }

        /// <summary>Wallet after this run's coins were added.</summary>
        public long TotalCoins { get; }

        public bool IsNewBestScore => IsValid && Score > PreviousBestScore;
    }
}
