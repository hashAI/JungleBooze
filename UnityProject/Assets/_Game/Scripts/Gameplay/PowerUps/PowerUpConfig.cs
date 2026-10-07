using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>
    /// Immutable runtime <c>PowerUpTuning</c> (GDD 10) with times in whole 60 Hz ticks. Built once per session from
    /// <see cref="PowerUpDesignValues"/>; the pickup placer and <see cref="PowerUpSystem"/> only read it.
    /// Helpers are allocation-free.
    /// </summary>
    public sealed class PowerUpConfig
    {
        private readonly int[] _magnetTicks;
        private readonly int[] _shieldTicks;
        private readonly int[] _boostTicks;
        private readonly float[] _boostSeconds;

        private PowerUpConfig(PowerUpDesignValues v)
        {
            Enabled = v.Enabled;
            FirstPickupMinS = v.FirstPickupMinS;
            FirstPickupMaxS = v.FirstPickupMaxS;
            IntervalMinS = v.IntervalMinS;
            IntervalMaxS = v.IntervalMaxS;
            MagnetWeight = v.MagnetWeight;
            ShieldWeight = v.ShieldWeight;
            SpeedBoostWeight = v.SpeedBoostWeight;
            PickupHeightM = v.PickupHeightM;
            PickupRadiusM = v.PickupRadiusM;
            ChunkEdgeMarginM = v.ChunkEdgeMarginM;
            ClearBeforeM = v.ClearBeforeM;
            ClearAfterM = v.ClearAfterM;
            CoinClearanceM = v.CoinClearanceM;
            _magnetTicks = ToTicks(v.MagnetDurationsS);
            _shieldTicks = ToTicks(v.ShieldDurationsS);
            _boostTicks = ToTicks(v.SpeedBoostDurationsS);
            _boostSeconds = (float[])v.SpeedBoostDurationsS.Clone();
            EndWarningTicks = RunnerConfig.MsToTicksAllowZero(v.EndWarningS * 1000f);
            MagnetRadiusM = v.MagnetRadiusM;
            CoinPullHeightM = v.CoinPullHeightM;
            ShieldInvulnerableTicks = RunnerConfig.MsToTicksAllowZero(v.ShieldInvulnerabilityS * 1000f);
            ShieldVineGraceTicks = RunnerConfig.MsToTicksAllowZero(v.ShieldVineGraceS * 1000f);
            SpeedBoostMultiplier = v.SpeedBoostMultiplier;
            SpeedBoostSlowdownTicks = RunnerConfig.MsToTicks(v.SpeedBoostSlowdownS * 1000f);
            SpeedBoostSlowdownS = v.SpeedBoostSlowdownS;
            SpeedBoostClearStretchS = v.SpeedBoostClearStretchS;
            SpeedBoostCollectAheadM = v.SpeedBoostCollectAheadM;
            SpeedBoostVineBlockMarginM = v.SpeedBoostVineBlockMarginM;
            MaxActivePickups = v.MaxActivePickups;
        }

        public bool Enabled { get; }

        public float FirstPickupMinS { get; }

        public float FirstPickupMaxS { get; }

        public float IntervalMinS { get; }

        public float IntervalMaxS { get; }

        public int MagnetWeight { get; }

        public int ShieldWeight { get; }

        public int SpeedBoostWeight { get; }

        public int TotalWeight => MagnetWeight + ShieldWeight + SpeedBoostWeight;

        public float PickupHeightM { get; }

        public float PickupRadiusM { get; }

        public float ChunkEdgeMarginM { get; }

        public float ClearBeforeM { get; }

        public float ClearAfterM { get; }

        public float CoinClearanceM { get; }

        public int EndWarningTicks { get; }

        public float MagnetRadiusM { get; }

        public float CoinPullHeightM { get; }

        public int ShieldInvulnerableTicks { get; }

        public int ShieldVineGraceTicks { get; }

        public float SpeedBoostMultiplier { get; }

        public int SpeedBoostSlowdownTicks { get; }

        public float SpeedBoostSlowdownS { get; }

        public float SpeedBoostClearStretchS { get; }

        public float SpeedBoostCollectAheadM { get; }

        public float SpeedBoostVineBlockMarginM { get; }

        public int MaxActivePickups { get; }

        /// <summary>Full duration of <paramref name="type"/> at <paramref name="level"/> (1–5, clamped), in ticks.</summary>
        public int DurationTicks(PowerUpType type, int level)
        {
            int i = ClampLevel(level) - 1;
            switch (type)
            {
                case PowerUpType.Magnet:
                    return _magnetTicks[i];
                case PowerUpType.Shield:
                    return _shieldTicks[i];
                case PowerUpType.SpeedBoost:
                    return _boostTicks[i];
                default:
                    return 0;
            }
        }

        /// <summary>Speed Boost dash length at <paramref name="level"/> in seconds.</summary>
        public float SpeedBoostSeconds(int level)
        {
            return _boostSeconds[ClampLevel(level) - 1];
        }

        /// <summary>Picks a power-up type from a draw in [0, <see cref="TotalWeight"/>).</summary>
        public PowerUpType TypeForDraw(int draw)
        {
            if (draw < MagnetWeight)
            {
                return PowerUpType.Magnet;
            }

            draw -= MagnetWeight;
            return draw < ShieldWeight ? PowerUpType.Shield : PowerUpType.SpeedBoost;
        }

        public static int ClampLevel(int level)
        {
            return level < 1 ? 1 : (level > PowerUpDesignValues.LevelCount ? PowerUpDesignValues.LevelCount : level);
        }

        public static PowerUpConfig FromDesignValues(PowerUpDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid power-up tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new PowerUpConfig(values);
        }

        public static PowerUpConfig CreateDefault()
        {
            return FromDesignValues(PowerUpDesignValues.CreateDefault());
        }

        /// <summary>Stable hash of every value that affects the simulation (config hash).</summary>
        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, Enabled);
            h = StableHash.Mix(h, FirstPickupMinS);
            h = StableHash.Mix(h, FirstPickupMaxS);
            h = StableHash.Mix(h, IntervalMinS);
            h = StableHash.Mix(h, IntervalMaxS);
            h = StableHash.Mix(h, MagnetWeight);
            h = StableHash.Mix(h, ShieldWeight);
            h = StableHash.Mix(h, SpeedBoostWeight);
            h = StableHash.Mix(h, PickupHeightM);
            h = StableHash.Mix(h, PickupRadiusM);
            h = StableHash.Mix(h, ChunkEdgeMarginM);
            h = StableHash.Mix(h, ClearBeforeM);
            h = StableHash.Mix(h, ClearAfterM);
            h = StableHash.Mix(h, CoinClearanceM);
            for (int i = 0; i < PowerUpDesignValues.LevelCount; i++)
            {
                h = StableHash.Mix(h, _magnetTicks[i]);
                h = StableHash.Mix(h, _shieldTicks[i]);
                h = StableHash.Mix(h, _boostTicks[i]);
            }

            h = StableHash.Mix(h, EndWarningTicks);
            h = StableHash.Mix(h, MagnetRadiusM);
            h = StableHash.Mix(h, CoinPullHeightM);
            h = StableHash.Mix(h, ShieldInvulnerableTicks);
            h = StableHash.Mix(h, ShieldVineGraceTicks);
            h = StableHash.Mix(h, SpeedBoostMultiplier);
            h = StableHash.Mix(h, SpeedBoostSlowdownTicks);
            h = StableHash.Mix(h, SpeedBoostClearStretchS);
            h = StableHash.Mix(h, SpeedBoostCollectAheadM);
            h = StableHash.Mix(h, SpeedBoostVineBlockMarginM);
            return h;
        }

        private static int[] ToTicks(float[] seconds)
        {
            var ticks = new int[seconds.Length];
            for (int i = 0; i < seconds.Length; i++)
            {
                ticks[i] = RunnerConfig.MsToTicks(seconds[i] * 1000f);
            }

            return ticks;
        }
    }
}
