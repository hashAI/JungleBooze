using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// The chunk's centreline in its local horizontal frame (x right, z forward at s = 0), integrated from
    /// <see cref="CurveKey"/>s and sampled every metre. Heading is the yaw from +z toward +x (radians). Straight when
    /// there are no keys. Built once at setup; <see cref="Evaluate"/> is allocation-free.
    /// </summary>
    public sealed class PathCurve
    {
        private const float Step = 1f;

        private readonly float[] _x;
        private readonly float[] _z;
        private readonly float[] _heading;
        private readonly float _length;

        public PathCurve(IList<CurveKey> keys, float length)
        {
            _length = Math.Max(0f, length);
            Straight = keys == null || keys.Count == 0;
            int n = (int)Math.Ceiling(_length / Step) + 1;
            _x = new float[n];
            _z = new float[n];
            _heading = new float[n];
            MaxCurvature = 0f;
            double x = 0.0;
            double z = 0.0;
            double h = 0.0;
            for (int i = 0; i < n; i++)
            {
                _x[i] = (float)x;
                _z[i] = (float)z;
                _heading[i] = (float)h;
                if (Straight)
                {
                    z += Step;
                    continue;
                }

                // Midpoint integration over [s, s + step].
                float s = i * Step;
                double k0 = CurvatureAt(keys, s);
                double k1 = CurvatureAt(keys, s + Step);
                MaxCurvature = Math.Max(MaxCurvature, (float)Math.Max(Math.Abs(k0), Math.Abs(k1)));
                double hm = h + (0.25 * (k0 + k1) * Step);
                x += Math.Sin(hm) * Step;
                z += Math.Cos(hm) * Step;
                h += 0.5 * (k0 + k1) * Step;
            }

            EndX = Sample(_x, _length);
            EndZ = Sample(_z, _length);
            EndHeading = Sample(_heading, _length);
        }

        public bool Straight { get; }

        public float MaxCurvature { get; }

        public float EndX { get; }

        public float EndZ { get; }

        public float EndHeading { get; }

        /// <summary>Curvature at s (1/m) from keys: linear between keys, 0 before the first and after the last.</summary>
        public static double CurvatureAt(IList<CurveKey> keys, float s)
        {
            if (keys == null || keys.Count == 0 || s < keys[0].S || s > keys[keys.Count - 1].S)
            {
                return 0.0;
            }

            for (int i = 1; i < keys.Count; i++)
            {
                if (s <= keys[i].S)
                {
                    float t = (s - keys[i - 1].S) / Math.Max(1e-6f, keys[i].S - keys[i - 1].S);
                    return keys[i - 1].Curvature + ((keys[i].Curvature - keys[i - 1].Curvature) * t);
                }
            }

            return keys[keys.Count - 1].Curvature;
        }

        /// <summary>Local centreline point and heading at s (extrapolated straight beyond the ends).</summary>
        public void Evaluate(float s, out float x, out float z, out float heading)
        {
            if (Straight)
            {
                x = 0f;
                z = s;
                heading = 0f;
                return;
            }

            if (s <= 0f)
            {
                heading = _heading[0];
                x = _x[0] + ((float)Math.Sin(heading) * s);
                z = _z[0] + ((float)Math.Cos(heading) * s);
                return;
            }

            if (s >= _length)
            {
                heading = EndHeading;
                float over = s - _length;
                x = EndX + ((float)Math.Sin(heading) * over);
                z = EndZ + ((float)Math.Cos(heading) * over);
                return;
            }

            x = Sample(_x, s);
            z = Sample(_z, s);
            heading = Sample(_heading, s);
        }

        private static float Sample(float[] values, float s)
        {
            float f = s / Step;
            int i = (int)f;
            if (i >= values.Length - 1)
            {
                return values[values.Length - 1];
            }

            float t = f - i;
            return values[i] + ((values[i + 1] - values[i]) * t);
        }
    }
}
