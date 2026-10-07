using System;
using JungleBooze.Services.Persistence;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// The coin shop (GDD 13.4): power-up upgrade levels 2 to 5 and the two start boosts (Head Start, Shield start).
    /// Everything is coins only. Bought boosts wait in the inventory; "arming" one makes the next run use it.
    /// Characters and outfits are the owner's call and are not sold yet. Does not write the save.
    /// </summary>
    public sealed class ShopService
    {
        public const int MaxUpgradeLevel = MetaConfig.UpgradeSteps + 1;

        private readonly PlayerSave _save;
        private readonly MetaConfig _config;

        public ShopService(PlayerSave save, MetaConfig config)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public long Coins => _save.TotalCoins;

        public int HeadStartPrice => _config.HeadStartPrice;

        public int ShieldStartPrice => _config.ShieldStartPrice;

        public int HeadStartDistanceM => _config.HeadStartDistanceM;

        public int HeadStarts => _save.Data.headStarts;

        public int ShieldStarts => _save.Data.shieldStarts;

        public bool HeadStartArmed => _save.Data.armHeadStart && _save.Data.headStarts > 0;

        public bool ShieldStartArmed => _save.Data.armShieldStart && _save.Data.shieldStarts > 0;

        /// <summary>Upgrade level 1 to 5 of a power-up.</summary>
        public int GetLevel(UpgradeTrack track)
        {
            int level = _save.Data.powerUpLevels[(int)track];
            return level < 1 ? 1 : (level > MaxUpgradeLevel ? MaxUpgradeLevel : level);
        }

        public bool IsMaxLevel(UpgradeTrack track)
        {
            return GetLevel(track) >= MaxUpgradeLevel;
        }

        /// <summary>Coin price of the next level, or -1 at the top level.</summary>
        public int NextUpgradePrice(UpgradeTrack track)
        {
            int level = GetLevel(track);
            return level >= MaxUpgradeLevel ? -1 : _config.UpgradePrices[level - 1];
        }

        public bool CanBuyUpgrade(UpgradeTrack track)
        {
            int price = NextUpgradePrice(track);
            return price >= 0 && _save.TotalCoins >= price;
        }

        public bool TryBuyUpgrade(UpgradeTrack track)
        {
            int price = NextUpgradePrice(track);
            if (price < 0 || !_save.TrySpendCoinsInShop(price))
            {
                return false;
            }

            _save.Data.powerUpLevels[(int)track] = GetLevel(track) + 1;
            return true;
        }

        public bool CanBuyHeadStart => _save.TotalCoins >= _config.HeadStartPrice;

        public bool CanBuyShieldStart => _save.TotalCoins >= _config.ShieldStartPrice;

        public bool TryBuyHeadStart()
        {
            if (_save.Data.headStarts == int.MaxValue || !_save.TrySpendCoinsInShop(_config.HeadStartPrice))
            {
                return false;
            }

            _save.Data.headStarts++;
            return true;
        }

        public bool TryBuyShieldStart()
        {
            if (_save.Data.shieldStarts == int.MaxValue || !_save.TrySpendCoinsInShop(_config.ShieldStartPrice))
            {
                return false;
            }

            _save.Data.shieldStarts++;
            return true;
        }

        /// <summary>Turns "use a Head Start in the next run" on or off. Returns false if turned on with none owned.</summary>
        public bool SetHeadStartArmed(bool armed)
        {
            if (armed && _save.Data.headStarts <= 0)
            {
                return false;
            }

            _save.Data.armHeadStart = armed;
            _save.MarkDirty();
            return true;
        }

        /// <summary>Turns "use a Shield start in the next run" on or off. Returns false if turned on with none owned.</summary>
        public bool SetShieldStartArmed(bool armed)
        {
            if (armed && _save.Data.shieldStarts <= 0)
            {
                return false;
            }

            _save.Data.armShieldStart = armed;
            _save.MarkDirty();
            return true;
        }

        /// <summary>
        /// A run is starting: uses up each armed start boost (one from the inventory) and reports which to apply.
        /// A boost stays armed while more are owned and switches off when the last one is used.
        /// </summary>
        public void ConsumeArmedForRun(out bool headStart, out bool shieldStart)
        {
            SaveData d = _save.Data;
            headStart = d.armHeadStart && d.headStarts > 0;
            shieldStart = d.armShieldStart && d.shieldStarts > 0;
            if (headStart)
            {
                d.headStarts--;
                d.armHeadStart = d.headStarts > 0;
            }

            if (shieldStart)
            {
                d.shieldStarts--;
                d.armShieldStart = d.shieldStarts > 0;
            }

            if (headStart || shieldStart)
            {
                _save.MarkDirty();
            }
        }
    }
}
