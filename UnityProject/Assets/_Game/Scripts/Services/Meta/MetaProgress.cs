using System;
using JungleBooze.Core;
using JungleBooze.Services.Persistence;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// Everything that happens outside a run (GDD 13): missions and the daily challenge, the daily reward and the
    /// shop, all on one <see cref="PlayerSave"/>. The composition root builds one and hands it to the menus and the
    /// run driver.
    /// </summary>
    public sealed class MetaProgress
    {
        public MetaProgress(PlayerSave save, MetaConfig config, IDayClock clock)
        {
            Save = save ?? throw new ArgumentNullException(nameof(save));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            Missions = new MissionService(save, config, clock);
            Daily = new DailyRewardService(save, config, clock);
            Shop = new ShopService(save, config);
        }

        public PlayerSave Save { get; }

        public MetaConfig Config { get; }

        public MissionService Missions { get; }

        public DailyRewardService Daily { get; }

        public ShopService Shop { get; }

        /// <summary>What the last banked run earned (shown on the Game Over panel).</summary>
        public MetaRunOutcome LastOutcome { get; private set; }

        /// <summary>Banks one finished run into the missions and the daily challenge.</summary>
        public MetaRunOutcome ApplyRun(in RunStats stats)
        {
            LastOutcome = Missions.ApplyRun(stats);
            return LastOutcome;
        }
    }
}
