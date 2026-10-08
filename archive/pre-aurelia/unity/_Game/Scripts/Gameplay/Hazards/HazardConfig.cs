using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Hazards
{
    /// <summary>Immutable runtime <c>HazardTuning</c> (GDD 8.3) with times in whole 60 Hz ticks.</summary>
    public sealed class HazardConfig
    {
        private HazardConfig(HazardDesignValues v)
        {
            WarningTicks = RunnerConfig.MsToTicks(v.LaneStrikeWarningS * 1000f);
            ActiveTicks = RunnerConfig.MsToTicks(v.LaneStrikeActiveS * 1000f);
            int cycle = RunnerConfig.MsToTicks(v.LaneStrikeCycleS * 1000f);
            RestTicks = Math.Max(1, cycle - WarningTicks - ActiveTicks);
            TriggerLeadS = v.LaneStrikeWarningS + v.LaneStrikeActiveS * v.LaneStrikeArrivalFraction;
        }

        public int WarningTicks { get; }

        public int ActiveTicks { get; }

        public int RestTicks { get; }

        /// <summary>A dormant strike starts its warning when HERO is this many seconds away at his current speed.</summary>
        public double TriggerLeadS { get; }

        public static HazardConfig FromDesignValues(HazardDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid hazard tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new HazardConfig(values);
        }

        public static HazardConfig CreateDefault()
        {
            return FromDesignValues(HazardDesignValues.CreateDefault());
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, WarningTicks);
            h = StableHash.Mix(h, ActiveTicks);
            h = StableHash.Mix(h, RestTicks);
            return StableHash.Mix(h, TriggerLeadS);
        }
    }
}
