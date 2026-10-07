using System.Collections.Generic;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Runner tuning in designer units (ms, m, m/s), spec 001 section 3.1. Plain C# so it can be validated and
    /// converted without Unity. <c>JungleBooze.Gameplay.Config.RunnerConfigAsset</c> copies its serialized fields into one of these.
    /// Field defaults are the spec start values.
    /// </summary>
    public sealed class RunnerDesignValues
    {
        public int LaneCount = 3;
        public float LaneWidthM = 2.4f;
        public int StartLane = 1;
        public float LaneSwitchMs = 120f;
        public float LaneSwitchEaseExponent = 2f;
        public float LaneQueueStartFraction = 0.5f;
        public float EdgeForgivenessFraction = 0.6f;
        public float JumpApexHeightM = 1.5f;
        public float JumpAirtimeMs = 600f;
        public float FastFallMinSpeedMps = 15f;
        public float FastFallMaxMs = 100f;
        public float SlideMs = 650f;
        public float InputBufferMs = 150f;
        public float CoyoteMs = 80f;
        public float PlayerHitboxWidthM = 0.7f;
        public float PlayerHitboxDepthM = 0.5f;
        public float StandingHeightM = 1.8f;
        public float SlidingHeightM = 0.8f;
        public float FallDeathDepthM = 1.0f;
        public float RunStartRampMs = 500f;
        public float RunStartSpeedFraction = 0.5f;
        public float StumbleBounceMs = 150f;
        public float StumbleDazeMs = 3000f;
        public float NearMissDistanceM = 0.35f;
        public int EventBufferCapacity = 64;

        /// <summary>A new instance holding the spec 001 start values.</summary>
        public static RunnerDesignValues CreateDefault()
        {
            return new RunnerDesignValues();
        }

        public RunnerDesignValues Clone()
        {
            return (RunnerDesignValues)MemberwiseClone();
        }

        /// <summary>
        /// Checks every field against the valid ranges in spec 001 section 3.1.
        /// Appends one human-readable message per problem. Returns true when there are none.
        /// Editor/setup time only (allocates).
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;

            if (LaneCount != 3)
            {
                errors.Add("laneCount must be 3 for launch.");
            }

            CheckRange(errors, "laneWidthM", LaneWidthM, 2.0f, 3.0f);

            if (StartLane < 0 || StartLane >= LaneCount)
            {
                errors.Add("startLane must be in 0..laneCount-1.");
            }

            CheckRange(errors, "laneSwitchMs", LaneSwitchMs, 80f, 140f);
            CheckRange(errors, "laneSwitchEaseExponent", LaneSwitchEaseExponent, 1f, 4f);
            CheckRange(errors, "laneQueueStartFraction", LaneQueueStartFraction, 0.3f, 1.0f);
            CheckRange(errors, "edgeForgivenessFraction", EdgeForgivenessFraction, 0.5f, 0.8f);
            CheckRange(errors, "jumpApexHeightM", JumpApexHeightM, 1.0f, 2.5f);
            CheckRange(errors, "jumpAirtimeMs", JumpAirtimeMs, 400f, 900f);
            CheckRange(errors, "fastFallMinSpeedMps", FastFallMinSpeedMps, 8f, 30f);
            CheckRange(errors, "fastFallMaxMs", FastFallMaxMs, 50f, 120f);
            CheckRange(errors, "slideMs", SlideMs, 400f, 1000f);
            CheckRange(errors, "inputBufferMs", InputBufferMs, 50f, 250f);
            CheckRange(errors, "coyoteMs", CoyoteMs, 0f, 150f);
            CheckRange(errors, "playerHitboxWidthM", PlayerHitboxWidthM, 0.4f, 0.9f);
            CheckRange(errors, "playerHitboxDepthM", PlayerHitboxDepthM, 0.3f, 0.8f);

            if (!(StandingHeightM > 0f))
            {
                errors.Add("standingHeightM must be positive.");
            }

            if (!(SlidingHeightM > 0f) || !(SlidingHeightM < StandingHeightM))
            {
                errors.Add("slidingHeightM must be positive and less than standingHeightM.");
            }

            CheckRange(errors, "fallDeathDepthM", FallDeathDepthM, 0.5f, 3.0f);
            CheckRange(errors, "runStartRampMs", RunStartRampMs, 0f, 1500f);
            CheckRange(errors, "runStartSpeedFraction", RunStartSpeedFraction, 0f, 1f);
            CheckRange(errors, "stumbleBounceMs", StumbleBounceMs, 80f, 300f);
            CheckRange(errors, "stumbleDazeMs", StumbleDazeMs, 1000f, 6000f);
            CheckRange(errors, "nearMissDistanceM", NearMissDistanceM, 0.1f, 0.6f);

            if (EventBufferCapacity < 32 || EventBufferCapacity > 256)
            {
                errors.Add("eventBufferCapacity must be in 32..256.");
            }

            return errors.Count == before;
        }

        private static void CheckRange(List<string> errors, string name, float value, float min, float max)
        {
            // Written so that NaN fails the check.
            if (!(value >= min && value <= max))
            {
                errors.Add(name + " must be in " + min + ".." + max + " (is " + value + ").");
            }
        }
    }
}
