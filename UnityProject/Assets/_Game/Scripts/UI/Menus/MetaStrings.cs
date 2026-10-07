using System.Globalization;
using JungleBooze.Services.Meta;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// English strings for missions, the daily reward and the shop. [ASSUMED] Placeholder string table until the
    /// localization system exists, like <see cref="MenuStrings"/>: every label here is read from this one file.
    /// Formats use <c>{0}</c> placeholders and are filled with the invariant culture.
    /// </summary>
    public static class MetaStrings
    {
        // Main menu buttons.
        public const string Missions = "Missions";
        public const string Daily = "Daily";
        public const string DailyReady = "Daily!";
        public const string Shop = "Shop";

        // Missions panel.
        public const string MissionsTitle = "Missions";
        public const string ScoreMultiplierFormat = "Score multiplier x{0}";
        public const string SetFormat = "Set {0}";
        public const string MissionDone = "Done";
        public const string MissionRewardFormat = "+{0} coins";
        public const string SetRewardFormat = "All 3: +{0} coins and +1 multiplier";

        // Daily panel.
        public const string DailyTitle = "Daily reward";
        public const string DayFormat = "Day {0}";
        public const string DayClaimed = "claimed";
        public const string DayToday = "today";
        public const string DayNext = "next";
        public const string Claim = "Claim";
        public const string ComeBackTomorrow = "Come back tomorrow";
        public const string RewardCoinsFormat = "{0} coins";
        public const string RewardHeadStart = "Head Start";
        public const string RewardShieldStart = "Shield start";
        public const string OutfitPiece = "+ outfit piece";
        public const string ChallengeTitle = "Daily challenge";
        public const string ChallengeRewardFormat = "Reward: {0} coins";
        public const string ChallengeDone = "Done";
        public const string ChallengeOpen = "Not done yet";

        // Shop panel.
        public const string ShopTitle = "Shop";
        public const string UpgradesHeader = "Power-up upgrades (last longer)";
        public const string UpgradeFormat = "{0}  Lv {1}/{2}";
        public const string MaxLevel = "Max";
        public const string MagnetName = "Magnet";
        public const string ShieldName = "Shield";
        public const string SpeedBoostName = "Speed Boost";
        public const string StartBoostsHeader = "Start boosts";
        public const string HeadStartFormat = "Head Start: {0} m dash (own {1})";
        public const string ShieldStartFormat = "Shield start (own {0})";
        public const string BuyFormat = "Buy {0}";
        public const string UseNextRunFormat = "Use next run: {0}";
        public const string ComingSoon = "More characters and outfits soon";

        // Game Over line.
        public const string OutcomeMissionFormat = "Mission done! +{0} coins";
        public const string OutcomeSetFormat = "Set complete! Score x{0}, +{1} coins";
        public const string OutcomeDailyFormat = "Daily challenge done! +{0} coins";

        public static string Format(string format, int a)
        {
            return string.Format(CultureInfo.InvariantCulture, format, a);
        }

        public static string Format(string format, string a)
        {
            return string.Format(CultureInfo.InvariantCulture, format, a);
        }

        public static string Format(string format, int a, int b)
        {
            return string.Format(CultureInfo.InvariantCulture, format, a, b);
        }

        public static string Format(string format, string a, int b, int c)
        {
            return string.Format(CultureInfo.InvariantCulture, format, a, b, c);
        }

        /// <summary>The mission sentence, for example "Swing on 5 vines".</summary>
        public static string Describe(MissionKind kind, bool perRun, int target)
        {
            string text;
            switch (kind)
            {
                case MissionKind.Coins:
                    text = perRun ? "Collect {0} coins in one run" : "Collect {0} coins in total";
                    break;
                case MissionKind.Distance:
                    text = perRun ? "Run {0} m in one run" : "Run {0} m in total";
                    break;
                case MissionKind.Vines:
                    text = perRun ? "Swing on {0} vines in one run" : "Swing on {0} vines";
                    break;
                case MissionKind.PerfectReleases:
                    text = perRun ? "Get {0} Perfect releases in one run" : "Get {0} Perfect releases";
                    break;
                case MissionKind.Slides:
                    text = perRun ? "Slide {0} times in one run" : "Slide {0} times";
                    break;
                case MissionKind.Shields:
                    text = perRun ? "Collect {0} Shields in one run" : "Collect {0} Shields";
                    break;
                case MissionKind.NearMisses:
                    text = perRun ? "Get {0} near misses in one run" : "Get {0} near misses";
                    break;
                default:
                    text = "{0}";
                    break;
            }

            return Format(text, target);
        }

        /// <summary>The reward of one calendar day, for example "100 coins" or "Head Start".</summary>
        public static string Describe(DailyReward reward)
        {
            string text;
            switch (reward.Kind)
            {
                case DailyRewardKind.HeadStart:
                    text = reward.Amount > 1 ? RewardHeadStart + " x" + reward.Amount.ToString(CultureInfo.InvariantCulture) : RewardHeadStart;
                    break;
                case DailyRewardKind.ShieldStart:
                    text = reward.Amount > 1 ? RewardShieldStart + " x" + reward.Amount.ToString(CultureInfo.InvariantCulture) : RewardShieldStart;
                    break;
                default:
                    text = Format(RewardCoinsFormat, reward.Amount);
                    break;
            }

            return reward.OutfitPieces > 0 ? text + " " + OutfitPiece : text;
        }

        /// <summary>The Game Over lines for what a run earned (empty when nothing).</summary>
        public static string Describe(MetaRunOutcome outcome)
        {
            string first = null;
            if (outcome.SetCompleted)
            {
                first = Format(OutcomeSetFormat, outcome.NewScoreMultiplier, outcome.MissionCoins + outcome.SetCoins);
            }
            else if (outcome.MissionsCompleted > 0)
            {
                first = Format(OutcomeMissionFormat, outcome.MissionCoins);
            }

            string second = outcome.DailyChallengeCompleted ? Format(OutcomeDailyFormat, outcome.DailyChallengeCoins) : null;
            if (first == null)
            {
                return second ?? string.Empty;
            }

            return second == null ? first : first + "\n" + second;
        }

        public static string UpgradeName(UpgradeTrack track)
        {
            switch (track)
            {
                case UpgradeTrack.Magnet:
                    return MagnetName;
                case UpgradeTrack.Shield:
                    return ShieldName;
                default:
                    return SpeedBoostName;
            }
        }
    }
}
