using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>Immutable runtime <c>RunFlowTuning</c> (spec 002 section 3.7), used by <see cref="RunSession"/>.</summary>
    public sealed class RunFlowConfig
    {
        private RunFlowConfig(RunFlowDesignValues v)
        {
            StartOnFirstInput = v.StartOnFirstInput;
            GameOverInputLockSeconds = v.GameOverInputLockMs / 1000.0;
            RestartMaxSeconds = v.RestartMaxMs / 1000.0;
            DevFixedSeed = v.DevFixedSeed;
            DyingDurationSeconds = v.DyingDurationMs / 1000.0;
        }

        public bool StartOnFirstInput { get; }

        public double GameOverInputLockSeconds { get; }

        public double RestartMaxSeconds { get; }

        public ulong DevFixedSeed { get; }

        public double DyingDurationSeconds { get; }

        public static RunFlowConfig FromDesignValues(RunFlowDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid run flow tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new RunFlowConfig(values);
        }

        public static RunFlowConfig CreateDefault()
        {
            return FromDesignValues(RunFlowDesignValues.CreateDefault());
        }
    }
}
