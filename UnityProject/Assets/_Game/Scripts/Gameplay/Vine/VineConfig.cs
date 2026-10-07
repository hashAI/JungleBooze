using System;
using System.Collections.Generic;
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

            SwingTicks = RunnerConfig.MsToTicks(v.SwingMs);
            SwingRadiusM = v.SwingRadiusM;
            SwingStartAngleRad = v.SwingStartAngleDeg * DegToRad;
            SwingEndAngleRad = v.SwingEndAngleDeg * DegToRad;
            SwingLowestFeetM = v.SwingLowestFeetM;
            HangTicks = RunnerConfig.MsToTicksAllowZero(v.HangMs);
            HangTimeScale = v.HangTimeScale;

            GoodStartPhase = v.GoodStartPhase;
            PerfectStartPhase = v.PerfectStartPhase;
            PerfectEndPhase = v.PerfectEndPhase;
            GoodStartTick = PhaseToTick(v.GoodStartPhase, SwingTicks);
            PerfectStartTick = PhaseToTick(v.PerfectStartPhase, SwingTicks);
            PerfectEndTick = PhaseToTick(v.PerfectEndPhase, SwingTicks);
            ReleaseBufferTicks = RunnerConfig.MsToTicksAllowZero(v.ReleaseBufferMs);

            LaunchGravityMps2 = v.LaunchGravityMps2;
            GoodLaunchSpeedMps = v.GoodLaunchSpeedMps;
            PerfectLaunchSpeedMps = v.PerfectLaunchSpeedMps;
            AutoLaunchSpeedMps = v.AutoLaunchSpeedMps;
            ChainArriveFeetM = v.ChainArriveFeetM;
            ChainGravityMinMps2 = v.ChainGravityMinMps2;
            ChainGravityMaxMps2 = v.ChainGravityMaxMps2;
            ChainMaxReachM = v.ChainMaxReachM;

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

        /// <summary>1.40 s = 84 ticks.</summary>
        public int SwingTicks { get; }

        public float SwingRadiusM { get; }

        public double SwingStartAngleRad { get; }

        public double SwingEndAngleRad { get; }

        public float SwingLowestFeetM { get; }

        public int HangTicks { get; }

        public float HangTimeScale { get; }

        public float GoodStartPhase { get; }

        public float PerfectStartPhase { get; }

        public float PerfectEndPhase { get; }

        /// <summary>First swing tick (ticks since the grab) that counts as Good.</summary>
        public int GoodStartTick { get; }

        /// <summary>First swing tick of the Perfect band.</summary>
        public int PerfectStartTick { get; }

        /// <summary>First swing tick after the Perfect band.</summary>
        public int PerfectEndTick { get; }

        public int ReleaseBufferTicks { get; }

        public float LaunchGravityMps2 { get; }

        public float GoodLaunchSpeedMps { get; }

        public float PerfectLaunchSpeedMps { get; }

        public float AutoLaunchSpeedMps { get; }

        public float ChainArriveFeetM { get; }

        public float ChainGravityMinMps2 { get; }

        public float ChainGravityMaxMps2 { get; }

        public float ChainMaxReachM { get; }

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

        public float LaunchSpeedFor(VineReleaseGrade grade)
        {
            switch (grade)
            {
                case VineReleaseGrade.Perfect:
                    return PerfectLaunchSpeedMps;
                case VineReleaseGrade.Good:
                    return GoodLaunchSpeedMps;
                default:
                    return AutoLaunchSpeedMps;
            }
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

        /// <summary>Swing phase 0..1 for a swing tick.</summary>
        public double PhaseAt(int swingTick)
        {
            double p = (double)swingTick / SwingTicks;
            return p < 0.0 ? 0.0 : (p > 1.0 ? 1.0 : p);
        }

        /// <summary>Pendulum angle (rad, positive = swung forward) at a swing tick.</summary>
        public double SwingAngleAt(int swingTick)
        {
            double p = PhaseAt(swingTick);
            return SwingStartAngleRad + (SwingEndAngleRad - SwingStartAngleRad) * p;
        }

        /// <summary>HERO's feet height on the swing arc at a swing tick.</summary>
        public double SwingFeetYAt(int swingTick)
        {
            return SwingLowestFeetM + SwingRadiusM * (1.0 - Math.Cos(SwingAngleAt(swingTick)));
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
            h = StableHash.Mix(h, SwingTicks);
            h = StableHash.Mix(h, SwingRadiusM);
            h = StableHash.Mix(h, SwingStartAngleRad);
            h = StableHash.Mix(h, SwingEndAngleRad);
            h = StableHash.Mix(h, SwingLowestFeetM);
            h = StableHash.Mix(h, GoodStartTick);
            h = StableHash.Mix(h, PerfectStartTick);
            h = StableHash.Mix(h, PerfectEndTick);
            h = StableHash.Mix(h, ReleaseBufferTicks);
            h = StableHash.Mix(h, LaunchGravityMps2);
            h = StableHash.Mix(h, GoodLaunchSpeedMps);
            h = StableHash.Mix(h, PerfectLaunchSpeedMps);
            h = StableHash.Mix(h, AutoLaunchSpeedMps);
            h = StableHash.Mix(h, ChainArriveFeetM);
            h = StableHash.Mix(h, ChainGravityMinMps2);
            h = StableHash.Mix(h, ChainGravityMaxMps2);
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

        private static int PhaseToTick(float phase, int swingTicks)
        {
            return (int)Math.Ceiling(phase * (double)swingTicks - 1e-6);
        }
    }
}
