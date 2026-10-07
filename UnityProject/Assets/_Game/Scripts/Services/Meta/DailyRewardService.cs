using System;
using JungleBooze.Core;
using JungleBooze.Services.Persistence;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// The 7-day daily reward calendar (GDD 13.3), claimed once per calendar day in local time. Missing a day pauses
    /// the calendar; it never resets (Pillar 5). The day comes from the injected <see cref="IDayClock"/>.
    /// A device clock set back before the last claim does not allow a second claim [ASSUMED]. The optional
    /// "double it" rewarded ad is not built (ads come later, and not in session 1). Does not write the save.
    /// </summary>
    public sealed class DailyRewardService
    {
        private readonly PlayerSave _save;
        private readonly MetaConfig _config;
        private readonly IDayClock _clock;

        public DailyRewardService(PlayerSave save, MetaConfig config, IDayClock clock)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public static int DayCount => MetaConfig.CalendarDays;

        /// <summary>A reward can be claimed today.</summary>
        public bool CanClaim => _clock.Today > _save.Data.lastDailyClaimDay;

        /// <summary>The 0-based calendar day that the next claim gives.</summary>
        public int NextDayIndex => _save.Data.dailyCalendarIndex % MetaConfig.CalendarDays;

        public DailyReward RewardFor(int dayIndex)
        {
            int i = ((dayIndex % MetaConfig.CalendarDays) + MetaConfig.CalendarDays) % MetaConfig.CalendarDays;
            return new DailyReward(i, _config.DailyKinds[i], _config.DailyAmounts[i], _config.DailyOutfitPieces[i]);
        }

        /// <summary>Gives today's reward and moves the calendar on. Returns false if it was already claimed today.</summary>
        public bool TryClaim(out DailyReward reward)
        {
            reward = RewardFor(NextDayIndex);
            if (!CanClaim)
            {
                return false;
            }

            SaveData d = _save.Data;
            switch (reward.Kind)
            {
                case DailyRewardKind.Coins:
                    _save.AddCoins(reward.Amount);
                    break;
                case DailyRewardKind.HeadStart:
                    d.headStarts = (int)Math.Min((long)d.headStarts + reward.Amount, int.MaxValue);
                    break;
                case DailyRewardKind.ShieldStart:
                    d.shieldStarts = (int)Math.Min((long)d.shieldStarts + reward.Amount, int.MaxValue);
                    break;
            }

            d.outfitPieces = (int)Math.Min((long)d.outfitPieces + reward.OutfitPieces, int.MaxValue);
            d.lastDailyClaimDay = _clock.Today;
            d.dailyCalendarIndex = (reward.DayIndex + 1) % MetaConfig.CalendarDays;
            _save.MarkDirty();
            return true;
        }
    }
}
