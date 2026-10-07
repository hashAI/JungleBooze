using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>
    /// <c>PowerUpTuning</c> in designer units (GDD 10 and 16). <c>PowerUpConfigAsset</c> copies its serialized fields
    /// into one of these. Field defaults are the GDD start values; values the GDD does not give are marked
    /// [ASSUMED] and are first guesses for the owner's feel check.
    /// </summary>
    [Serializable]
    public sealed class PowerUpDesignValues
    {
        /// <summary>Upgrade levels (GDD 10: level 1 to level 5, bought with coins in the shop).</summary>
        public const int LevelCount = 5;

        /// <summary>Master switch: false = the generator never places pickups.</summary>
        public bool Enabled = true;

        // ---- Placement (GDD 10: at most one on screen, on average every 25 s) ----

        /// <summary>First pickup 12–20 s of planned run time after the run start [ASSUMED].</summary>
        public float FirstPickupMinS = 12f;
        public float FirstPickupMaxS = 20f;

        /// <summary>20–30 s of planned run time between pickups (average 25 s).</summary>
        public float IntervalMinS = 20f;
        public float IntervalMaxS = 30f;

        /// <summary>Relative chance of each power-up when a pickup is placed [ASSUMED equal].</summary>
        public int MagnetWeight = 1;
        public int ShieldWeight = 1;
        public int SpeedBoostWeight = 1;

        /// <summary>Height of the pickup centre above the track (m): reached by running, missed by sliding under [ASSUMED].</summary>
        public float PickupHeightM = 1.0f;

        /// <summary>HERO's box grown by this much on every axis collects a pickup.</summary>
        public float PickupRadiusM = 0.6f;

        /// <summary>Pickups stay this far from both ends of their chunk (m).</summary>
        public float ChunkEdgeMarginM = 8f;

        /// <summary>Fair placement: no obstacle, gap or hazard in the pickup's lane this far before and after it (m).</summary>
        public float ClearBeforeM = 10f;
        public float ClearAfterM = 6f;

        /// <summary>Pickups prefer spots with no coin of the same lane this close (m).</summary>
        public float CoinClearanceM = 2f;

        // ---- Durations per level (GDD 10) ----

        public float[] MagnetDurationsS = { 10f, 12.5f, 15f, 17.5f, 20f };
        public float[] ShieldDurationsS = { 20f, 25f, 30f, 35f, 40f };
        public float[] SpeedBoostDurationsS = { 4f, 5f, 6f, 7f, 8f };

        /// <summary>Warning flash / flicker before a Magnet or Shield runs out (s).</summary>
        public float EndWarningS = 1.0f;

        // ---- Magnet ----

        /// <summary>Coins this far ahead in all 3 lanes are pulled in (m).</summary>
        public float MagnetRadiusM = 10f;

        /// <summary>Pulled coins fly to this height above HERO's feet (chest).</summary>
        public float CoinPullHeightM = 0.9f;

        // ---- Shield ----

        /// <summary>Invulnerability after the shield absorbed a hit (s).</summary>
        public float ShieldInvulnerabilityS = 1.0f;

        /// <summary>A shield that runs out on a vine stays until this long after landing (GDD 7.4).</summary>
        public float ShieldVineGraceS = 0.5f;

        // ---- Speed Boost ----

        public float SpeedBoostMultiplier = 1.6f;

        /// <summary>Slowdown with invulnerability after the dash (s).</summary>
        public float SpeedBoostSlowdownS = 1.0f;

        /// <summary>Guaranteed clear track after the slowdown (s at base run speed).</summary>
        public float SpeedBoostClearStretchS = 1.5f;

        /// <summary>Coins in HERO's lane this far ahead are auto-collected during the dash (m) [ASSUMED].</summary>
        public float SpeedBoostCollectAheadM = 10f;

        /// <summary>Extra margin after a boost's planned end before a vine section may start (m) [ASSUMED].</summary>
        public float SpeedBoostVineBlockMarginM = 30f;

        // ---- Capacity ----

        public int MaxActivePickups = 8;

        public static PowerUpDesignValues CreateDefault()
        {
            return new PowerUpDesignValues();
        }

        public PowerUpDesignValues Clone()
        {
            var copy = (PowerUpDesignValues)MemberwiseClone();
            copy.MagnetDurationsS = MagnetDurationsS == null ? null : (float[])MagnetDurationsS.Clone();
            copy.ShieldDurationsS = ShieldDurationsS == null ? null : (float[])ShieldDurationsS.Clone();
            copy.SpeedBoostDurationsS = SpeedBoostDurationsS == null ? null : (float[])SpeedBoostDurationsS.Clone();
            return copy;
        }

        /// <summary>Sanity ranges [ASSUMED]. Appends one message per problem; returns true when there are none.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "firstPickupMinS", FirstPickupMinS, 1, 300);
            ConfigChecks.Range(errors, "firstPickupMaxS", FirstPickupMaxS, FirstPickupMinS, 600);
            ConfigChecks.Range(errors, "intervalMinS", IntervalMinS, 5, 300);
            ConfigChecks.Range(errors, "intervalMaxS", IntervalMaxS, IntervalMinS, 600);
            ConfigChecks.Range(errors, "magnetWeight", MagnetWeight, 0, 100);
            ConfigChecks.Range(errors, "shieldWeight", ShieldWeight, 0, 100);
            ConfigChecks.Range(errors, "speedBoostWeight", SpeedBoostWeight, 0, 100);
            if (MagnetWeight + ShieldWeight + SpeedBoostWeight <= 0)
            {
                errors.Add("At least one power-up weight must be positive.");
            }

            ConfigChecks.Range(errors, "pickupHeightM", PickupHeightM, 0.3, 2.5);
            ConfigChecks.Range(errors, "pickupRadiusM", PickupRadiusM, 0.2, 1.5);
            ConfigChecks.Range(errors, "chunkEdgeMarginM", ChunkEdgeMarginM, 0, 20);
            ConfigChecks.Range(errors, "clearBeforeM", ClearBeforeM, 0, 40);
            ConfigChecks.Range(errors, "clearAfterM", ClearAfterM, 0, 40);
            ConfigChecks.Range(errors, "coinClearanceM", CoinClearanceM, 0, 10);
            CheckDurations(errors, "magnetDurationsS", MagnetDurationsS);
            CheckDurations(errors, "shieldDurationsS", ShieldDurationsS);
            CheckDurations(errors, "speedBoostDurationsS", SpeedBoostDurationsS);
            ConfigChecks.Range(errors, "endWarningS", EndWarningS, 0, 5);
            ConfigChecks.Range(errors, "magnetRadiusM", MagnetRadiusM, 1, 40);
            ConfigChecks.Range(errors, "coinPullHeightM", CoinPullHeightM, 0, 2);
            ConfigChecks.Range(errors, "shieldInvulnerabilityS", ShieldInvulnerabilityS, 0, 5);
            ConfigChecks.Range(errors, "shieldVineGraceS", ShieldVineGraceS, 0, 5);
            ConfigChecks.Range(errors, "speedBoostMultiplier", SpeedBoostMultiplier, 1, 3);
            ConfigChecks.Range(errors, "speedBoostSlowdownS", SpeedBoostSlowdownS, 0.1, 5);
            ConfigChecks.Range(errors, "speedBoostClearStretchS", SpeedBoostClearStretchS, 0, 10);
            ConfigChecks.Range(errors, "speedBoostCollectAheadM", SpeedBoostCollectAheadM, 0, 40);
            ConfigChecks.Range(errors, "speedBoostVineBlockMarginM", SpeedBoostVineBlockMarginM, 0, 500);
            ConfigChecks.Range(errors, "maxActivePickups", MaxActivePickups, 2, 64);
            return errors.Count == before;
        }

        private static void CheckDurations(List<string> errors, string name, float[] values)
        {
            if (values == null || values.Length != LevelCount)
            {
                errors.Add(name + " needs exactly " + LevelCount + " values (levels 1 to 5).");
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                ConfigChecks.Range(errors, name + "[" + i + "]", values[i], 0.5, 120);
            }
        }
    }
}
