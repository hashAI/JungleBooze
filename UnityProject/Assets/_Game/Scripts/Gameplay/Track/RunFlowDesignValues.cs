using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary><c>RunFlowTuning</c> (spec 002 section 3.7). Defaults are the spec start values.</summary>
    public sealed class RunFlowDesignValues
    {
        public bool StartOnFirstInput = true;
        public float GameOverInputLockMs = 400f;
        public float RestartMaxMs = 1000f;

        /// <summary>0 = off. Non-zero forces this seed for every run (development builds only).</summary>
        public ulong DevFixedSeed;

        /// <summary>
        /// Length of the Dying state in real time: hit-pause 350 ms + camera hold 800 ms (spec 002 section 12.1,
        /// spec 001 section 10.6). Must equal the presentation values [ASSUMED: stored here so RunSession is
        /// self-contained].
        /// </summary>
        public float DyingDurationMs = 1150f;

        public static RunFlowDesignValues CreateDefault()
        {
            return new RunFlowDesignValues();
        }

        public RunFlowDesignValues Clone()
        {
            return (RunFlowDesignValues)MemberwiseClone();
        }

        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "gameOverInputLockMs", GameOverInputLockMs, 0, 2000);
            ConfigChecks.Range(errors, "restartMaxMs", RestartMaxMs, 100, 2000);
            ConfigChecks.Range(errors, "dyingDurationMs", DyingDurationMs, 0, 5000);
            return errors.Count == before;
        }
    }
}
