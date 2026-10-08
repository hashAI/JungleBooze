using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// v(d) = v0 + (vMax − v0)·(1 − e^(−d/dScale)) (spec 101 §2.2), precomputed into a table at setup so the
    /// per-tick path uses no transcendental functions (ADR 0002 floating-point note). Linear interpolation every
    /// <see cref="TableStepM"/> m keeps the error below 1e-4 m/s.
    /// </summary>
    public sealed class SpeedCurve
    {
        public const float TableStepM = 25f;
        public const int TableEntries = 4001; // 0 … 100 km

        private readonly float[] _table = new float[TableEntries];

        public SpeedCurve(RunSpeedConfig config)
        {
            double v0 = config.V0;
            double gain = config.VMax - config.V0;
            double scale = Math.Max(1.0, config.DScale);
            for (int i = 0; i < TableEntries; i++)
            {
                double d = i * (double)TableStepM;
                _table[i] = (float)(v0 + (gain * (1.0 - Math.Exp(-d / scale))));
            }
        }

        public float Evaluate(float distance)
        {
            if (distance <= 0f)
            {
                return _table[0];
            }

            float position = distance / TableStepM;
            int index = (int)position;
            if (index >= TableEntries - 1)
            {
                return _table[TableEntries - 1];
            }

            float t = position - index;
            return _table[index] + ((_table[index + 1] - _table[index]) * t);
        }
    }
}
