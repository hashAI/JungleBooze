using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Immutable runtime runner tuning in whole simulation ticks (spec 001 sections 2 and 3.1).
    /// Built once per run from <see cref="RunnerDesignValues"/>; the simulation only ever reads this.
    /// </summary>
    public sealed class RunnerConfig
    {
        /// <summary>Simulation rate. The whole spec is written for 60 Hz.</summary>
        public const int TicksPerSecond = 60;

        /// <summary>Length of one tick in seconds.</summary>
        public const double TickSeconds = 1.0 / TicksPerSecond;

        private RunnerConfig(RunnerDesignValues v)
        {
            LaneCount = v.LaneCount;
            LaneWidthM = v.LaneWidthM;
            StartLane = v.StartLane;
            LaneSwitchTicks = MsToTicks(v.LaneSwitchMs);
            LaneSwitchEaseExponent = v.LaneSwitchEaseExponent;
            LaneQueueStartFraction = v.LaneQueueStartFraction;
            LaneQueueStartTick = Math.Max(1, (int)Math.Ceiling(LaneSwitchTicks * (double)v.LaneQueueStartFraction - 1e-9));
            EdgeForgivenessFraction = v.EdgeForgivenessFraction;
            JumpApexHeightM = v.JumpApexHeightM;
            JumpAirtimeTicks = MsToTicks(v.JumpAirtimeMs);
            JumpApexTick = JumpAirtimeTicks / 2;

            double airtimeSeconds = JumpAirtimeTicks * TickSeconds;
            GravityMps2 = 8.0 * v.JumpApexHeightM / (airtimeSeconds * airtimeSeconds);
            JumpVelocityMps = 4.0 * v.JumpApexHeightM / airtimeSeconds;

            FastFallMinSpeedMps = v.FastFallMinSpeedMps;
            FastFallMaxTicks = MsToTicks(v.FastFallMaxMs);
            FastFallMaxSeconds = v.FastFallMaxMs / 1000.0;
            SlideTicks = MsToTicks(v.SlideMs);
            InputBufferTicks = MsToTicks(v.InputBufferMs);
            CoyoteTicks = MsToTicksAllowZero(v.CoyoteMs);
            PlayerHitboxWidthM = v.PlayerHitboxWidthM;
            PlayerHitboxDepthM = v.PlayerHitboxDepthM;
            StandingHeightM = v.StandingHeightM;
            SlidingHeightM = v.SlidingHeightM;
            FallDeathDepthM = v.FallDeathDepthM;
            RunStartRampTicks = MsToTicksAllowZero(v.RunStartRampMs);
            RunStartSpeedFraction = v.RunStartSpeedFraction;
            StumbleBounceTicks = MsToTicks(v.StumbleBounceMs);
            StumbleDazeTicks = MsToTicks(v.StumbleDazeMs);
            NearMissDistanceM = v.NearMissDistanceM;
            EventBufferCapacity = v.EventBufferCapacity;
        }

        public int LaneCount { get; }

        public float LaneWidthM { get; }

        public int StartLane { get; }

        public int LaneSwitchTicks { get; }

        public float LaneSwitchEaseExponent { get; }

        public float LaneQueueStartFraction { get; }

        /// <summary>A queued lane move starts once the active move has completed this many ticks.</summary>
        public int LaneQueueStartTick { get; }

        public float EdgeForgivenessFraction { get; }

        public float JumpApexHeightM { get; }

        public int JumpAirtimeTicks { get; }

        /// <summary>Tick offset from the jump start at which <see cref="RunnerEventType.JumpApex"/> fires.</summary>
        public int JumpApexTick { get; }

        /// <summary>Derived: 8 h / T².</summary>
        public double GravityMps2 { get; }

        /// <summary>Derived: 4 h / T.</summary>
        public double JumpVelocityMps { get; }

        public float FastFallMinSpeedMps { get; }

        public int FastFallMaxTicks { get; }

        /// <summary>The authored fast-fall cap in seconds, used by the fall speed formula (spec 6.3.2).</summary>
        public double FastFallMaxSeconds { get; }

        public int SlideTicks { get; }

        public int InputBufferTicks { get; }

        public int CoyoteTicks { get; }

        public float PlayerHitboxWidthM { get; }

        public float PlayerHitboxDepthM { get; }

        public float StandingHeightM { get; }

        public float SlidingHeightM { get; }

        public float FallDeathDepthM { get; }

        public int RunStartRampTicks { get; }

        public float RunStartSpeedFraction { get; }

        public int StumbleBounceTicks { get; }

        public int StumbleDazeTicks { get; }

        public float NearMissDistanceM { get; }

        public int EventBufferCapacity { get; }

        /// <summary>Converts validated designer values. Throws <see cref="ArgumentException"/> when they are invalid.</summary>
        public static RunnerConfig FromDesignValues(RunnerDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid runner tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new RunnerConfig(values);
        }

        /// <summary>Config built from the spec 001 start values.</summary>
        public static RunnerConfig CreateDefault()
        {
            return FromDesignValues(RunnerDesignValues.CreateDefault());
        }

        /// <summary>Spec 001 section 2: <c>ticks = max(1, floor(ms × 60 / 1000 + 0.5))</c>.</summary>
        public static int MsToTicks(float ms)
        {
            return Math.Max(1, (int)Math.Floor(ms * (double)TicksPerSecond / 1000.0 + 0.5));
        }

        /// <summary>
        /// Same rounding as <see cref="MsToTicks"/>, but 0 ms stays 0 ticks. Used for fields whose valid range
        /// includes 0 and where 0 means "off" (coyote time, run start ramp).
        /// </summary>
        public static int MsToTicksAllowZero(float ms)
        {
            return ms <= 0f ? 0 : MsToTicks(ms);
        }

        /// <summary>Lateral center of a lane. Lane 0 is left; the middle lane is at x = 0.</summary>
        public float LaneCenterX(int lane)
        {
            return (lane - (LaneCount - 1) * 0.5f) * LaneWidthM;
        }
    }
}
