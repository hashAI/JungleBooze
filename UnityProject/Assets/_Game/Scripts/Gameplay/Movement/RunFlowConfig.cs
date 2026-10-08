using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Run lifecycle timing (ready beat, dying, finish coast). Not in spec 101's table; values follow GDD §17
    /// ("results ≤ 1.0 s after the death fade") and spec 101 §4.2 ("the fall continues visually for 0.5 s").
    /// </summary>
    [Serializable]
    public sealed class RunFlowConfig
    {
        /// <summary>Ready beat before the start ramp, s (instant restart lands here).</summary>
        public float ReadyTime = 0.35f;

        /// <summary>Crash / health-out: time from death to results, s.</summary>
        public float DyingTime = 0.8f;

        /// <summary>Fall: the fall continues visually this long before results, s.</summary>
        public float FallDyingTime = 0.5f;

        /// <summary>Finish line: Pista keeps running this long before results, s.</summary>
        public float FinishCoastTime = 1f;

        /// <summary>Pause: resume after this "ready" beat, s (spec 101 §9).</summary>
        public float ResumeReadyTime = 1f;

        public RunFlowConfig Clone()
        {
            return (RunFlowConfig)MemberwiseClone();
        }
    }
}
