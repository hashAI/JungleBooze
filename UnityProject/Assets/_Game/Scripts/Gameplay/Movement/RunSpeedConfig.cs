using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Forward speed tuning (spec 101 §2.2). Authored in <c>RunSpeedConfig.asset</c>.</summary>
    [Serializable]
    public sealed class RunSpeedConfig
    {
        /// <summary>Start speed, m/s.</summary>
        public float V0 = 10f;

        /// <summary>Asymptotic speed, m/s.</summary>
        public float VMax = 16f;

        /// <summary>Distance scale of the exponential approach, m.</summary>
        public float DScale = 3000f;

        /// <summary>0 → v0 at run start (ease-out quad), s. Inputs are live during the ramp.</summary>
        public float StartRampTime = 0.8f;

        /// <summary>Instant speed multiplier on a minor hit.</summary>
        public float StumbleSpeedFactor = 0.8f;

        /// <summary>Linear recovery back to 1.0×, s.</summary>
        public float StumbleRecoverTime = 0.8f;

        /// <summary>Speed multiplier right after a revive.</summary>
        public float ReviveRampFrom = 0.5f;

        /// <summary>Linear recovery after a revive, s.</summary>
        public float ReviveRampTime = 0.5f;

        public RunSpeedConfig Clone()
        {
            return (RunSpeedConfig)MemberwiseClone();
        }
    }
}
