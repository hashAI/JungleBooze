using System;

namespace JungleBooze.Services.Persistence
{
    /// <summary>
    /// Everything the local save file holds. The public lower-case field names are the JSON format read by
    /// <c>JsonUtility</c>: never rename or reuse them; add new fields and bump <see cref="SaveCodec.CurrentVersion"/>
    /// when the meaning of existing data changes.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Format version (<see cref="SaveCodec.CurrentVersion"/> when written). 0 means "not a save".</summary>
        public int version;

        /// <summary>Best score of any finished run (GDD 13.1).</summary>
        public long bestScore;

        /// <summary>Longest run in whole meters (for the Longest Distance leaderboard later, GDD 13.5).</summary>
        public long bestDistanceM;

        /// <summary>Coin wallet: coins from every run, minus spending (shop arrives later).</summary>
        public long totalCoins;

        /// <summary>Runs recorded so far (onboarding and session-flow rules use it later, GDD 12 and 19).</summary>
        public int runsPlayed;

        /// <summary>App sessions started (1 = the first session; GDD 14.4 free continue, GDD 19 session flow).</summary>
        public int sessionsStarted;

        /// <summary>The one free first-session continue from the companion was used (GDD 14.4).</summary>
        public bool freeContinueUsed;

        /// <summary>Coins spent on continues so far (for balance checks later).</summary>
        public long coinsSpentOnContinues;

        public SettingsData settings = new SettingsData();

        // ---- Missions, daily reward and shop (GDD 13.2 to 13.4). Added without a version bump: old saves simply
        // lack these fields and get the defaults from SaveCodec.Repair. ----

        /// <summary>Completed mission sets; the score multiplier is 1 + this, capped (GDD 13.1).</summary>
        public int completedMissionSets;

        /// <summary>The current set's missions (empty until the first set is made).</summary>
        public MissionSlotData[] missions = new MissionSlotData[0];

        /// <summary>Calendar day (days since 1970) of the last daily reward claim; 0 = never.</summary>
        public int lastDailyClaimDay;

        /// <summary>Next day of the 7-day calendar to claim, 0 to 6. A missed day does not reset it (GDD 13.3).</summary>
        public int dailyCalendarIndex;

        /// <summary>Calendar day the daily challenge flag below belongs to; 0 = none.</summary>
        public int dailyChallengeDay;

        public bool dailyChallengeDone;

        /// <summary>Head Start boosts owned (GDD 13.4).</summary>
        public int headStarts;

        /// <summary>Shield starts owned (GDD 13.4).</summary>
        public int shieldStarts;

        /// <summary>Use a Head Start / Shield start in the next run.</summary>
        public bool armHeadStart;

        public bool armShieldStart;

        /// <summary>Power-up upgrade levels 1 to 5: Magnet, Shield, Speed Boost.</summary>
        public int[] powerUpLevels = { 1, 1, 1 };

        /// <summary>Exclusive outfit pieces from the day-7 reward (outfits themselves come with the characters).</summary>
        public int outfitPieces;

        /// <summary>Coins spent in the shop so far (for balance checks later).</summary>
        public long coinsSpentInShop;

        // ---- Onboarding (GDD 12). Added without a version bump: old saves get false for both. ----

        /// <summary>The first-run tutorial was finished or skipped once.</summary>
        public bool tutorialCompleted;

        /// <summary>The tutorial began at least once (so a quit halfway repeats it even after the run was recorded).</summary>
        public bool tutorialStarted;

        /// <summary>Settings asked to play the tutorial again on the next run.</summary>
        public bool tutorialReplay;

        public static SaveData CreateDefault()
        {
            return new SaveData
            {
                version = SaveCodec.CurrentVersion,
                settings = SettingsData.CreateDefault(),
            };
        }
    }
}
