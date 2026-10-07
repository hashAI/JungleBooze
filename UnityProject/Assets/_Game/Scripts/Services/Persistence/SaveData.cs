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

        public SettingsData settings = new SettingsData();

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
