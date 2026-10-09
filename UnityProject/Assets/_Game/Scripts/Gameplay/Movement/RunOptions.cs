namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Per-run switches (not tuning). Bots and tests use <see cref="ForcedSpeed"/> for constant-speed runs.</summary>
    public struct RunOptions
    {
        /// <summary>When &gt; 0, replaces the speed curve with this constant speed, m/s.</summary>
        public float ForcedSpeed;

        /// <summary>Start at full speed (no 0 → v0 ramp).</summary>
        public bool SkipStartRamp;

        /// <summary>First run of the player's life: health floors at 1 during the FTUE window (spec 101 §4.2).</summary>
        public bool FirstRun;

        public float StartS;

        public float StartX;

        /// <summary>Owns Deep Breath: a dive started in a Deep Breath zone follows its passage (spec 103 §4.5).</summary>
        public bool DeepBreath;

        /// <summary>Deep dive depth below the swim line (m, negative) and duration (s), from the ability definition.</summary>
        public float DeepDiveDepth;

        public float DeepDiveTime;

        public static RunOptions Default => default;
    }
}
