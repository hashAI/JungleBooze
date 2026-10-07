using System;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>A fixed base speed at every distance. Used by the chunk validator and tests.</summary>
    public sealed class ConstantSpeedSource : ISpeedSource
    {
        public ConstantSpeedSource(double speedMps)
        {
            if (!(speedMps > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(speedMps), "Must be positive.");
            }

            SpeedMps = speedMps;
        }

        public double SpeedMps { get; }

        public double GetBaseSpeedMps(double distanceM)
        {
            return SpeedMps;
        }
    }
}
