using System;
using JungleBooze.Gameplay.Session;
using JungleBooze.Services.Meta;

namespace JungleBooze.App
{
    /// <summary>
    /// Turns the player's shop and mission progress into the <see cref="RunLoadout"/> of the run that is about to
    /// start (GDD 13). Uses up the start boosts the player armed, so call it once per run start.
    /// </summary>
    public static class RunLoadoutBuilder
    {
        public static RunLoadout Build(MetaProgress meta)
        {
            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            ShopService shop = meta.Shop;
            shop.ConsumeArmedForRun(out bool headStart, out bool shieldStart);
            return new RunLoadout(
                shop.GetLevel(UpgradeTrack.Magnet),
                shop.GetLevel(UpgradeTrack.Shield),
                shop.GetLevel(UpgradeTrack.SpeedBoost),
                shieldStart,
                headStart ? shop.HeadStartDistanceM : 0,
                meta.Missions.ScoreMultiplier);
        }
    }
}
