using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Companion
{
    /// <summary>
    /// Immutable runtime <c>CompanionTuning</c> (GDD 15.2) with simulation times in whole 60 Hz ticks. Built once
    /// per session from <see cref="CompanionDesignValues"/>; the companion simulation and views only read it.
    /// </summary>
    public sealed class CompanionConfig
    {
        /// <summary>The meter is full at 100%.</summary>
        public const float MeterFullPercent = 100f;

        private CompanionConfig(CompanionDesignValues v)
        {
            NearMissMeterPercent = v.NearMissMeterPercent;
            CoinStreakMeterPercent = v.CoinStreakMeterPercent;
            GoodReleaseMeterPercent = v.GoodReleaseMeterPercent;

            LiftTicks = RunnerConfig.MsToTicks(v.LiftDurationMs);
            LiftGlideHeightM = v.LiftGlideHeightM;
            LiftDescentTicks = RunnerConfig.MsToTicks(v.LiftDescentMs);
            LiftRiseTicks = RunnerConfig.MsToTicks(v.LiftRiseMs);
            LiftClearAfterSeconds = v.LiftClearAfterTouchdownMs / 1000.0;
            LiftLandingInvulnerableTicks = RunnerConfig.MsToTicksAllowZero(v.LiftLandingInvulnerableMs);
            LiftCoinPullM = v.LiftCoinPullM;

            VineCalloutLeadSeconds = v.VineCalloutLeadMs / 1000.0;
            HazardCalloutLeadSeconds = v.HazardCalloutLeadMs / 1000.0;
            CalloutMinGapTicks = RunnerConfig.MsToTicksAllowZero(v.CalloutMinGapMs);

            HomeAheadM = v.HomeAheadM;
            HomeHeightM = v.HomeHeightM;
            FollowSmoothSeconds = v.FollowSmoothMs / 1000f;
            SwoopHeightM = v.SwoopHeightM;
            SwoopSeconds = v.SwoopMs / 1000f;
            WingspanM = v.WingspanM;
            FlapHz = v.FlapHz;
            CalloutBubbleSeconds = v.CalloutBubbleMs / 1000f;
        }

        public float NearMissMeterPercent { get; }

        public float CoinStreakMeterPercent { get; }

        public float GoodReleaseMeterPercent { get; }

        public int LiftTicks { get; }

        public float LiftGlideHeightM { get; }

        public int LiftDescentTicks { get; }

        public int LiftRiseTicks { get; }

        public double LiftClearAfterSeconds { get; }

        public int LiftLandingInvulnerableTicks { get; }

        public float LiftCoinPullM { get; }

        public double VineCalloutLeadSeconds { get; }

        public double HazardCalloutLeadSeconds { get; }

        public int CalloutMinGapTicks { get; }

        public float HomeAheadM { get; }

        public float HomeHeightM { get; }

        public float FollowSmoothSeconds { get; }

        public float SwoopHeightM { get; }

        public float SwoopSeconds { get; }

        public float WingspanM { get; }

        public float FlapHz { get; }

        public float CalloutBubbleSeconds { get; }

        /// <summary>The Lift shape handed to <see cref="RunnerSimulation.TryStartLift"/>.</summary>
        public LiftParameters ToLiftParameters()
        {
            return new LiftParameters
            {
                TotalTicks = LiftTicks,
                RiseTicks = LiftRiseTicks,
                DescentTicks = LiftDescentTicks,
                GlideHeightM = LiftGlideHeightM,
                LandingInvulnerableTicks = LiftLandingInvulnerableTicks,
            };
        }

        public static CompanionConfig FromDesignValues(CompanionDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid companion tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new CompanionConfig(values);
        }

        public static CompanionConfig CreateDefault()
        {
            return FromDesignValues(CompanionDesignValues.CreateDefault());
        }
    }
}
