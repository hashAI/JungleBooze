using System;
using System.Collections.Generic;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// Tuning for missions, the daily reward and challenge, and the shop (GDD 13.2 to 13.4, 14.1). Numbers are the
    /// GDD's starting values; anything the GDD leaves open is marked [ASSUMED]. <c>MetaConfigAsset</c> serializes
    /// one of these; without the asset these code defaults are used.
    /// </summary>
    [Serializable]
    public sealed class MetaConfig
    {
        /// <summary>Calendar length (GDD 13.3).</summary>
        public const int CalendarDays = 7;

        /// <summary>Upgrade levels 2 to 5 (GDD 13.4), so four prices.</summary>
        public const int UpgradeSteps = 4;

        // ---- Missions (GDD 13.2) ----

        /// <summary>Mission reward: 50 coins in set 1, +10 per set, capped.</summary>
        public int MissionRewardBase = 50;

        public int MissionRewardStep = 10;

        public int MissionRewardCap = 250;

        /// <summary>Set reward: 200 coins + 50 per set number, capped.</summary>
        public int SetRewardBase = 200;

        public int SetRewardPerSet = 50;

        public int SetRewardCap = 1000;

        /// <summary>The score multiplier rises by 1 per completed set up to this (GDD 13.1).</summary>
        public int MaxScoreMultiplier = 30;

        /// <summary>
        /// The mission pool. [ASSUMED] Targets per tier are guesses around the GDD examples. "Reach the River" is
        /// left out until the River world exists; "Slide under branches" is "Slide N times" and "Use Shields" is
        /// "Collect Shields" (the run does not report what a slide passed under).
        /// </summary>
        public MissionTemplate[] Missions = CreateDefaultMissions();

        // ---- Daily challenge and reward (GDD 13.3) ----

        /// <summary>One of these is the challenge of the day (picked from the day number). [ASSUMED] targets.</summary>
        public MissionTemplate[] DailyChallenges = CreateDefaultChallenges();

        public int DailyChallengeReward = 300;

        /// <summary>Day 1 to 7: 100 coins, 150, Head Start, 250, Shield start, 400, 750 coins + outfit piece.</summary>
        public DailyRewardKind[] DailyKinds =
        {
            DailyRewardKind.Coins, DailyRewardKind.Coins, DailyRewardKind.HeadStart, DailyRewardKind.Coins,
            DailyRewardKind.ShieldStart, DailyRewardKind.Coins, DailyRewardKind.Coins,
        };

        public int[] DailyAmounts = { 100, 150, 1, 250, 1, 400, 750 };

        public int[] DailyOutfitPieces = { 0, 0, 0, 0, 0, 0, 1 };

        // ---- Shop (GDD 13.4) ----

        /// <summary>Price of upgrade level 2, 3, 4, 5 (coins only).</summary>
        public int[] UpgradePrices = { 500, 1500, 4000, 10000 };

        public int HeadStartPrice = 300;

        public int ShieldStartPrice = 250;

        /// <summary>Head Start: the run begins with a dash of this length (GDD 13.4).</summary>
        public int HeadStartDistanceM = 300;

        public static MetaConfig CreateDefault()
        {
            return new MetaConfig();
        }

        public MetaConfig Clone()
        {
            var copy = (MetaConfig)MemberwiseClone();
            copy.Missions = CloneTemplates(Missions);
            copy.DailyChallenges = CloneTemplates(DailyChallenges);
            copy.DailyKinds = DailyKinds == null ? null : (DailyRewardKind[])DailyKinds.Clone();
            copy.DailyAmounts = DailyAmounts == null ? null : (int[])DailyAmounts.Clone();
            copy.DailyOutfitPieces = DailyOutfitPieces == null ? null : (int[])DailyOutfitPieces.Clone();
            copy.UpgradePrices = UpgradePrices == null ? null : (int[])UpgradePrices.Clone();
            return copy;
        }

        /// <summary>Sanity checks. Appends one message per problem; returns true when there are none.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            CheckTemplates(errors, "missions", Missions, 1);
            CheckTemplates(errors, "dailyChallenges", DailyChallenges, 1);
            if (DailyKinds == null || DailyKinds.Length != CalendarDays)
            {
                errors.Add("dailyKinds needs exactly " + CalendarDays + " entries.");
            }

            if (DailyAmounts == null || DailyAmounts.Length != CalendarDays)
            {
                errors.Add("dailyAmounts needs exactly " + CalendarDays + " entries.");
            }

            if (DailyOutfitPieces == null || DailyOutfitPieces.Length != CalendarDays)
            {
                errors.Add("dailyOutfitPieces needs exactly " + CalendarDays + " entries.");
            }

            if (UpgradePrices == null || UpgradePrices.Length != UpgradeSteps)
            {
                errors.Add("upgradePrices needs exactly " + UpgradeSteps + " entries.");
            }

            if (MissionRewardBase < 0 || MissionRewardStep < 0 || MissionRewardCap < 0 || SetRewardBase < 0 ||
                SetRewardPerSet < 0 || SetRewardCap < 0 || DailyChallengeReward < 0 || HeadStartPrice < 0 ||
                ShieldStartPrice < 0)
            {
                errors.Add("rewards and prices cannot be negative.");
            }

            if (MaxScoreMultiplier < 1)
            {
                errors.Add("maxScoreMultiplier must be at least 1.");
            }

            if (HeadStartDistanceM < 1)
            {
                errors.Add("headStartDistanceM must be at least 1.");
            }

            return errors.Count == before;
        }

        private static void CheckTemplates(List<string> errors, string name, MissionTemplate[] templates, int minCount)
        {
            if (templates == null || templates.Length < minCount)
            {
                errors.Add(name + " needs at least " + minCount + " template(s).");
                return;
            }

            for (int i = 0; i < templates.Length; i++)
            {
                MissionTemplate t = templates[i];
                if (t == null || t.Targets == null || t.Targets.Length == 0)
                {
                    errors.Add(name + "[" + i + "] needs at least one target.");
                    continue;
                }

                for (int k = 0; k < t.Targets.Length; k++)
                {
                    if (t.Targets[k] < 1)
                    {
                        errors.Add(name + "[" + i + "] targets must be at least 1.");
                        break;
                    }
                }
            }
        }

        private static MissionTemplate[] CloneTemplates(MissionTemplate[] source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new MissionTemplate[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                MissionTemplate t = source[i];
                copy[i] = t == null ? null : new MissionTemplate(t.Kind, t.PerRun, t.Targets == null ? null : (int[])t.Targets.Clone());
            }

            return copy;
        }

        private static MissionTemplate[] CreateDefaultMissions()
        {
            return new[]
            {
                new MissionTemplate(MissionKind.Coins, true, 150, 300, 500, 800),
                new MissionTemplate(MissionKind.Coins, false, 500, 1000, 2000, 4000),
                new MissionTemplate(MissionKind.Distance, true, 1000, 1500, 2500, 4000),
                new MissionTemplate(MissionKind.Vines, false, 5, 10, 20, 35),
                new MissionTemplate(MissionKind.PerfectReleases, false, 3, 6, 10, 15),
                new MissionTemplate(MissionKind.Slides, false, 20, 40, 70, 100),
                new MissionTemplate(MissionKind.Shields, false, 2, 3, 5, 8),
                new MissionTemplate(MissionKind.NearMisses, false, 5, 10, 20, 30),
            };
        }

        private static MissionTemplate[] CreateDefaultChallenges()
        {
            return new[]
            {
                new MissionTemplate(MissionKind.PerfectReleases, true, 4),
                new MissionTemplate(MissionKind.Coins, true, 250),
                new MissionTemplate(MissionKind.Distance, true, 1500),
                new MissionTemplate(MissionKind.Vines, true, 3),
                new MissionTemplate(MissionKind.NearMisses, true, 5),
            };
        }
    }
}
