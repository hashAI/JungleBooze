using System;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Smooth periodic curve through (x, value) keys over a period: cubic Hermite with Catmull-Rom tangents that
    /// wrap around, so value and slope match at both ends of the period. Used for the look-test layout (heading,
    /// height, enclosure, bank height...). Keys may come in any order; a key at x = period is the same as x = 0.
    /// Evaluation does not allocate.
    /// </summary>
    public sealed class PeriodicCurve
    {
        private readonly float _period;
        private readonly float[] _x;
        private readonly float[] _y;
        private readonly float[] _m;

        public PeriodicCurve(float period, Vector2[] keys)
        {
            if (!(period > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }

            if (keys == null || keys.Length == 0)
            {
                throw new ArgumentException("A periodic curve needs at least one key.", nameof(keys));
            }

            _period = period;
            var sorted = new Vector2[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                sorted[i] = new Vector2(Mathf.Repeat(keys[i].x, period), keys[i].y);
            }

            Array.Sort(sorted, (a, b) => a.x.CompareTo(b.x));
            int n = sorted.Length;
            _x = new float[n];
            _y = new float[n];
            _m = new float[n];
            for (int i = 0; i < n; i++)
            {
                _x[i] = sorted[i].x;
                _y[i] = sorted[i].y;
            }

            for (int i = 0; i < n; i++)
            {
                if (n == 1)
                {
                    _m[i] = 0f;
                    continue;
                }

                float xPrev = i == 0 ? _x[n - 1] - period : _x[i - 1];
                float yPrev = i == 0 ? _y[n - 1] : _y[i - 1];
                float xNext = i == n - 1 ? _x[0] + period : _x[i + 1];
                float yNext = i == n - 1 ? _y[0] : _y[i + 1];
                _m[i] = (yNext - yPrev) / Mathf.Max(1e-4f, xNext - xPrev);
            }
        }

        public float Period => _period;

        public float Evaluate(float x)
        {
            int n = _x.Length;
            if (n == 1)
            {
                return _y[0];
            }

            x = Mathf.Repeat(x, _period);
            int i1 = 0;
            while (i1 < n && _x[i1] <= x)
            {
                i1++;
            }

            int i0 = i1 - 1;
            float x0;
            float x1;
            if (i0 < 0)
            {
                i0 = n - 1;
                x0 = _x[i0] - _period;
            }
            else
            {
                x0 = _x[i0];
            }

            if (i1 >= n)
            {
                i1 = 0;
                x1 = _x[0] + _period;
            }
            else
            {
                x1 = _x[i1];
            }

            float h = Mathf.Max(1e-4f, x1 - x0);
            float t = (x - x0) / h;
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * _y[i0]
                 + (t3 - 2f * t2 + t) * h * _m[i0]
                 + (-2f * t3 + 3f * t2) * _y[i1]
                 + (t3 - t2) * h * _m[i1];
        }
    }
}
