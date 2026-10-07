using System;
using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Vine
{
    /// <summary>
    /// Immutable runtime <c>VineTuning</c> (GDD 7.5) with times in whole 60 Hz ticks. Built once per session from
    /// <see cref="VineDesignValues"/>; the runner, the track generator and scoring only read it. Helpers are
    /// allocation-free.
    /// </summary>
    public sealed class VineConfig
    {
        private const double DegToRad = Math.PI / 180.0;

        private readonly float[] _chainMultipliers;

        private VineConfig(VineDesignValues v)
        {
            Enabled = v.Enabled;
            FirstSectionMinS = v.FirstSectionMinS;
            FirstSectionMaxS = v.FirstSectionMaxS;
            SectionIntervalMinS = v.SectionIntervalMinS;
            SectionIntervalMaxS = v.SectionIntervalMaxS;
            ChasmVinesFromM = v.ChasmVinesFromM;

            GrabPointHeightM = v.GrabPointHeightM;
            GrabZoneLengthM = v.GrabZoneLengthM;
            GrabZoneWidthM = v.GrabZoneWidthM;
            GrabZoneBottomM = v.GrabZoneBottomM;
            GrabZoneTopM = v.GrabZoneTopM;
            GrabEarlinessS = v.GrabEarlinessMs / 1000.0;
            GrabBlendTicks = RunnerConfig.MsToTicksAllowZero(v.GrabBlendMs);

            RopeLengthM = v.RopeLengthM;
            SwingGravityMps2 = v.SwingGravityMps2;
            GravityOverRope = (double)v.SwingGravityMps2 / v.RopeLengthM;
            CatchMinSpeedMps = v.CatchMinSpeedMps;
            CatchMaxSpeedMps = v.CatchMaxSpeedMps;
            MaxSwingAngleDeg = v.MaxSwingAngleDeg;
            GrabMaxAngleDeg = v.GrabMaxAngleDeg;
            GrabMaxAngleSin = DeterministicMath.Sin(v.GrabMaxAngleDeg * DegToRad);
            HandToFeetM = v.HandToFeetM;
            SwingMaxTicks = RunnerConfig.MsToTicks(v.SwingMaxMs);
            HangTicks = RunnerConfig.MsToTicksAllowZero(v.HangMs);
            HangTimeScale = v.HangTimeScale;

            GoodStartTick = RunnerConfig.MsToTicks(v.GoodStartMs);
            PerfectStartTick = RunnerConfig.MsToTicks(v.PerfectStartMs);
            PerfectEndTick = PerfectStartTick + RunnerConfig.MsToTicks(v.PerfectWidthMs);
            ReleaseBufferTicks = RunnerConfig.MsToTicksAllowZero(v.ReleaseBufferMs);

            LaunchGravityMps2 = v.LaunchGravityMps2;
            PerfectImpulseMps = v.PerfectImpulseMps;
            GoodImpulseMps = v.GoodImpulseMps;
            PoorImpulseMps = v.PoorImpulseMps;
            ImpulseAngleDeg = v.ImpulseAngleDeg;
            ImpulseCos = DeterministicMath.Cos(v.ImpulseAngleDeg * DegToRad);
            ImpulseSin = DeterministicMath.Sin(v.ImpulseAngleDeg * DegToRad);
            ReleaseMinForwardMps = v.ReleaseMinForwardMps;
            ReleaseMinUpMps = v.ReleaseMinUpMps;
            LandingBlendTicks = RunnerConfig.MsToTicksAllowZero(v.LandingSpeedBlendMs);
            ChainArriveFeetM = v.ChainArriveFeetM;
            ChainFlightMinS = v.ChainFlightMinS;
            ChainFlightMaxS = v.ChainFlightMaxS;
            ChainMaxReachM = v.ChainMaxReachM;
            ApexTicksEstimate = EstimateApexTicks();

            GoodScore = v.GoodScore;
            PerfectScore = v.PerfectScore;
            AutoScore = v.AutoScore;
            _chainMultipliers = (float[])v.ChainMultipliers.Clone();
            GoodCoins = v.GoodCoins;
            PerfectCoins = v.PerfectCoins;
            PerfectRingCoins = v.PerfectRingCoins;
            RingRadiusM = v.RingRadiusM;
            PerfectCompanionMeterPercent = v.PerfectCompanionMeterPercent;
            MaxActiveVines = v.MaxActiveVines;
            MaxBonusCoins = v.MaxBonusCoins;
        }

        public bool Enabled { get; }

        public float FirstSectionMinS { get; }

        public float FirstSectionMaxS { get; }

        public float SectionIntervalMinS { get; }

        public float SectionIntervalMaxS { get; }

        public float ChasmVinesFromM { get; }

        public float GrabPointHeightM { get; }

        public float GrabZoneLengthM { get; }

        public float GrabZoneWidthM { get; }

        public float GrabZoneBottomM { get; }

        public float GrabZoneTopM { get; }

        public double GrabEarlinessS { get; }

        public int GrabBlendTicks { get; }

        /// <summary>Rope length L (m): pivot to the hand.</summary>
        public float RopeLengthM { get; }

        /// <summary>Height of the fixed pivot above the path: <c>GrabPointHeightM + RopeLengthM</c> (17.0 m). Never stored.</summary>
        public float PivotHeightM => GrabPointHeightM + RopeLengthM;

        public float SwingGravityMps2 { get; }

        /// <summary><c>SwingGravityMps2 / RopeLengthM</c> in double (1/s^2).</summary>
        public double GravityOverRope { get; }

        public float CatchMinSpeedMps { get; }

        public float CatchMaxSpeedMps { get; }

        public float MaxSwingAngleDeg { get; }

        public float GrabMaxAngleDeg { get; }

        /// <summary>sin(GrabMaxAngleDeg): the clamp of the start angle argument.</summary>
        public double GrabMaxAngleSin { get; }

        /// <summary>Hand to feet distance while hanging (m).</summary>
        public float HandToFeetM { get; }

        /// <summary>Failsafe automatic release, in ticks since the grab (96).</summary>
        public int SwingMaxTicks { get; }

        public int HangTicks { get; }

        public float HangTimeScale { get; }

        /// <summary>First swing tick (ticks since the grab) that counts as Good (27).</summary>
        public int GoodStartTick { get; }

        /// <summary>First swing tick of the Perfect band (42).</summary>
        public int PerfectStartTick { get; }

        /// <summary>First swing tick after the Perfect band (53, exclusive end).</summary>
        public int PerfectEndTick { get; }

        public int ReleaseBufferTicks { get; }

        public float LaunchGravityMps2 { get; }

        public float PerfectImpulseMps { get; }

        public float GoodImpulseMps { get; }

        public float PoorImpulseMps { get; }

        public float ImpulseAngleDeg { get; }

        public double ImpulseCos { get; }

        public double ImpulseSin { get; }

        public float ReleaseMinForwardMps { get; }

        public float ReleaseMinUpMps { get; }

        /// <summary>Length of the speed blend after landing from a vine, in ticks (30).</summary>
        public int LandingBlendTicks { get; }

        public float ChainArriveFeetM { get; }

        public float ChainFlightMinS { get; }

        public float ChainFlightMaxS { get; }

        public float ChainMaxReachM { get; }

        /// <summary>
        /// Tick since the grab at which an unreleased swing reaches its apex (first <c>omega &lt;= 0</c>) for a grab at
        /// the middle of the catch range and the canonical grab point (1.25 m before the pivot): 85 with the start
        /// values. Used for the HUD ring phase.
        /// </summary>
        public int ApexTicksEstimate { get; }

        public int GoodScore { get; }

        public int PerfectScore { get; }

        public int AutoScore { get; }

        public int GoodCoins { get; }

        public int PerfectCoins { get; }

        public int PerfectRingCoins { get; }

        public float RingRadiusM { get; }

        public float PerfectCompanionMeterPercent { get; }

        public int MaxActiveVines { get; }

        public int MaxBonusCoins { get; }

        public static VineConfig FromDesignValues(VineDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid vine tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new VineConfig(values);
        }

        public static VineConfig CreateDefault()
        {
            return FromDesignValues(VineDesignValues.CreateDefault());
        }

        /// <summary>
        /// Grade of a release swipe made <paramref name="swingTick"/> ticks after the grab;
        /// <see cref="VineReleaseGrade.None"/> = too early (buffer it).
        /// </summary>
        public VineReleaseGrade GradeForSwingTick(int swingTick)
        {
            if (swingTick < GoodStartTick)
            {
                return VineReleaseGrade.None;
            }

            if (swingTick >= PerfectStartTick && swingTick < PerfectEndTick)
            {
                return VineReleaseGrade.Perfect;
            }

            return VineReleaseGrade.Good;
        }

        /// <summary>Impulse (m/s) the grade adds to the pendulum velocity at the release (Auto = Poor).</summary>
        public float ImpulseFor(VineReleaseGrade grade)
        {
            switch (grade)
            {
                case VineReleaseGrade.Perfect:
                    return PerfectImpulseMps;
                case VineReleaseGrade.Good:
                    return GoodImpulseMps;
                default:
                    return PoorImpulseMps;
            }
        }

        /// <summary>Catch speed (m/s): the entry speed clamped to [CatchMinSpeedMps, CatchMaxSpeedMps] (spec 004 4.2).</summary>
        public double CatchSpeedFor(double entrySpeedMps)
        {
            if (entrySpeedMps < CatchMinSpeedMps)
            {
                return CatchMinSpeedMps;
            }

            return entrySpeedMps > CatchMaxSpeedMps ? CatchMaxSpeedMps : entrySpeedMps;
        }

        /// <summary>
        /// Start angle (rad) of a grab <paramref name="zRelativeToPivotM"/> metres from the pivot (negative = before it):
        /// <c>asin(clamp(z / L, +-sin(GrabMaxAngleDeg)))</c> with the deterministic series.
        /// </summary>
        public double StartAngleFor(double zRelativeToPivotM)
        {
            double a = zRelativeToPivotM / RopeLengthM;
            if (a > GrabMaxAngleSin)
            {
                a = GrabMaxAngleSin;
            }
            else if (a < -GrabMaxAngleSin)
            {
                a = -GrabMaxAngleSin;
            }

            return DeterministicMath.AsinSmall(a);
        }

        /// <summary>
        /// Release velocity (spec 004 4.5) for the pendulum state <paramref name="theta"/>, <paramref name="omega"/>
        /// and the grade's impulse, with the floors applied: <c>vx = max(vt cos(theta) + I cos(a), minForward)</c>,
        /// <c>vy = max(vt sin(theta) + I sin(a), minUp)</c>, <c>vt = L omega</c>.
        /// </summary>
        public void LaunchVelocity(double theta, double omega, VineReleaseGrade grade, out double vx, out double vy)
        {
            double vt = (double)RopeLengthM * omega;
            double impulse = ImpulseFor(grade);
            vx = (vt * DeterministicMath.Cos(theta)) + (impulse * ImpulseCos);
            vy = (vt * DeterministicMath.Sin(theta)) + (impulse * ImpulseSin);
            if (vx < ReleaseMinForwardMps)
            {
                vx = ReleaseMinForwardMps;
            }

            if (vy < ReleaseMinUpMps)
            {
                vy = ReleaseMinUpMps;
            }
        }

        /// <summary>Hand z, relative to the pivot, for a rope angle: <c>L sin(theta)</c>.</summary>
        public double HandOffsetZ(double theta)
        {
            return RopeLengthM * DeterministicMath.Sin(theta);
        }

        /// <summary>Hand height above the path for a rope angle: <c>PivotHeightM - L cos(theta)</c>.</summary>
        public double HandHeightY(double theta)
        {
            return PivotHeightM - (RopeLengthM * DeterministicMath.Cos(theta));
        }

        public int ScoreFor(VineReleaseGrade grade)
        {
            switch (grade)
            {
                case VineReleaseGrade.Perfect:
                    return PerfectScore;
                case VineReleaseGrade.Good:
                    return GoodScore;
                case VineReleaseGrade.Auto:
                    return AutoScore;
                default:
                    return 0;
            }
        }

        public int CoinsFor(VineReleaseGrade grade)
        {
            switch (grade)
            {
                case VineReleaseGrade.Perfect:
                    return PerfectCoins;
                case VineReleaseGrade.Good:
                    return GoodCoins;
                default:
                    return 0;
            }
        }

        /// <summary>Score multiplier after <paramref name="perfectsEarlierInChain"/> Perfects in the current chain.</summary>
        public float GetChainMultiplier(int perfectsEarlierInChain)
        {
            int i = perfectsEarlierInChain < 0 ? 0 : perfectsEarlierInChain;
            return _chainMultipliers[i < _chainMultipliers.Length ? i : _chainMultipliers.Length - 1];
        }

        /// <summary>Swing phase 0..1 for the HUD ring: <c>swingTick / ApexTicksEstimate</c>, clamped.</summary>
        public double PhaseAt(int swingTick)
        {
            double p = (double)swingTick / ApexTicksEstimate;
            return p < 0.0 ? 0.0 : (p > 1.0 ? 1.0 : p);
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, Enabled);
            h = StableHash.Mix(h, FirstSectionMinS);
            h = StableHash.Mix(h, FirstSectionMaxS);
            h = StableHash.Mix(h, SectionIntervalMinS);
            h = StableHash.Mix(h, SectionIntervalMaxS);
            h = StableHash.Mix(h, ChasmVinesFromM);
            h = StableHash.Mix(h, GrabPointHeightM);
            h = StableHash.Mix(h, GrabZoneLengthM);
            h = StableHash.Mix(h, GrabZoneWidthM);
            h = StableHash.Mix(h, GrabZoneBottomM);
            h = StableHash.Mix(h, GrabZoneTopM);
            h = StableHash.Mix(h, GrabEarlinessS);
            h = StableHash.Mix(h, GrabBlendTicks);
            h = StableHash.Mix(h, RopeLengthM);
            h = StableHash.Mix(h, SwingGravityMps2);
            h = StableHash.Mix(h, CatchMinSpeedMps);
            h = StableHash.Mix(h, CatchMaxSpeedMps);
            h = StableHash.Mix(h, MaxSwingAngleDeg);
            h = StableHash.Mix(h, GrabMaxAngleDeg);
            h = StableHash.Mix(h, HandToFeetM);
            h = StableHash.Mix(h, SwingMaxTicks);
            h = StableHash.Mix(h, GoodStartTick);
            h = StableHash.Mix(h, PerfectStartTick);
            h = StableHash.Mix(h, PerfectEndTick);
            h = StableHash.Mix(h, ReleaseBufferTicks);
            h = StableHash.Mix(h, LaunchGravityMps2);
            h = StableHash.Mix(h, PerfectImpulseMps);
            h = StableHash.Mix(h, GoodImpulseMps);
            h = StableHash.Mix(h, PoorImpulseMps);
            h = StableHash.Mix(h, ImpulseAngleDeg);
            h = StableHash.Mix(h, ReleaseMinForwardMps);
            h = StableHash.Mix(h, ReleaseMinUpMps);
            h = StableHash.Mix(h, LandingBlendTicks);
            h = StableHash.Mix(h, ChainArriveFeetM);
            h = StableHash.Mix(h, ChainFlightMinS);
            h = StableHash.Mix(h, ChainFlightMaxS);
            h = StableHash.Mix(h, ChainMaxReachM);
            h = StableHash.Mix(h, GoodScore);
            h = StableHash.Mix(h, PerfectScore);
            h = StableHash.Mix(h, AutoScore);
            for (int i = 0; i < _chainMultipliers.Length; i++)
            {
                h = StableHash.Mix(h, _chainMultipliers[i]);
            }

            h = StableHash.Mix(h, GoodCoins);
            h = StableHash.Mix(h, PerfectCoins);
            h = StableHash.Mix(h, PerfectRingCoins);
            h = StableHash.Mix(h, RingRadiusM);
            return h;
        }

        /// <summary>Runs an unreleased swing (catch speed mid-range, grab 1.25 m before the pivot) to its apex.</summary>
        private int EstimateApexTicks()
        {
            double theta = StartAngleFor(-1.25);
            double omega = CatchSpeedFor(0.5 * ((double)CatchMinSpeedMps + CatchMaxSpeedMps)) / RopeLengthM;
            for (int n = 1; n < SwingMaxTicks; n++)
            {
                omega -= GravityOverRope * DeterministicMath.Sin(theta) * RunnerConfig.TickSeconds;
                theta += omega * RunnerConfig.TickSeconds;
                if (omega <= 0.0)
                {
                    return n;
                }
            }

            return SwingMaxTicks;
        }
    }
}
