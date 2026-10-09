using System;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// The look-test v2 trail as a curve in the world (ENVIRONMENT_STRATEGY 4.1): an S-curve that climbs and drops,
    /// defined by periodic keys of heading (degrees, + = turning right) and height (m) over the loop length.
    /// Path space: <c>s</c> = distance along the trail (m), <c>d</c> = lateral offset (m, + = right), as spec 101.
    /// Heading and height repeat every loop, so one loop's world is the previous loop's world shifted by
    /// <see cref="LoopOffset"/> (the segments are moved by whole loop offsets; no seam).
    /// Pure math, sampled once into tables at construction (no allocations when evaluating).
    /// Heading 0 runs along +z; the right vector of heading h is (cos h, 0, -sin h).
    /// </summary>
    public sealed class LookTestPath
    {
        private readonly float _loop;
        private readonly float _step;
        private readonly int _count;
        private readonly Vector3[] _positions;
        private readonly float[] _headings;

        /// <param name="loopLengthM">Loop length in meters.</param>
        /// <param name="headingKeys">(s, heading in degrees) keys, any order, s in [0, loop).</param>
        /// <param name="heightKeys">(s, height in m) keys, any order, s in [0, loop).</param>
        /// <param name="stepM">Table resolution in meters.</param>
        public LookTestPath(float loopLengthM, Vector2[] headingKeys, Vector2[] heightKeys, float stepM = 0.25f)
        {
            if (!(loopLengthM > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(loopLengthM));
            }

            _loop = loopLengthM;
            _count = Mathf.Max(8, Mathf.CeilToInt(loopLengthM / Mathf.Max(0.01f, stepM)));
            _step = loopLengthM / _count;
            var heading = new PeriodicCurve(loopLengthM, headingKeys);
            var height = new PeriodicCurve(loopLengthM, heightKeys);

            _positions = new Vector3[_count + 1];
            _headings = new float[_count + 1];
            float x = 0f;
            float z = 0f;
            for (int i = 0; i <= _count; i++)
            {
                float s = i * _step;
                _headings[i] = heading.Evaluate(s) * Mathf.Deg2Rad;
                _positions[i] = new Vector3(x, height.Evaluate(s), z);

                // Midpoint rule for the next sample.
                float mid = heading.Evaluate(s + 0.5f * _step) * Mathf.Deg2Rad;
                x += Mathf.Sin(mid) * _step;
                z += Mathf.Cos(mid) * _step;
            }

            Vector3 offset = _positions[_count] - _positions[0];
            offset.y = 0f;
            LoopOffset = offset;
        }

        public float LoopLengthM => _loop;

        /// <summary>World shift from one loop to the next (heading and height repeat, so y is 0).</summary>
        public Vector3 LoopOffset { get; }

        /// <summary>Index of the loop that contains <paramref name="s"/>.</summary>
        public long LoopIndex(double s)
        {
            return (long)Math.Floor(s / _loop);
        }

        /// <summary>Trail centre at distance <paramref name="s"/> (any value; whole loops add <see cref="LoopOffset"/>).</summary>
        public Vector3 Position(double s)
        {
            long loop = LoopIndex(s);
            float local = (float)(s - loop * (double)_loop);
            Sample(local, out int i, out float t);
            Vector3 p = Vector3.LerpUnclamped(_positions[i], _positions[i + 1], t);
            return p + LoopOffset * loop;
        }

        /// <summary>Heading in radians at <paramref name="s"/> (periodic).</summary>
        public float HeadingRad(double s)
        {
            long loop = LoopIndex(s);
            float local = (float)(s - loop * (double)_loop);
            Sample(local, out int i, out float t);
            return Mathf.LerpUnclamped(_headings[i], _headings[i + 1], t);
        }

        /// <summary>Height of the trail floor at <paramref name="s"/>.</summary>
        public float Height(double s)
        {
            return Position(s).y;
        }

        public Vector3 Forward(double s)
        {
            float h = HeadingRad(s);
            return new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h));
        }

        public Vector3 Right(double s)
        {
            float h = HeadingRad(s);
            return new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h));
        }

        /// <summary>World point at path distance <paramref name="s"/>, lateral <paramref name="d"/>, at <paramref name="y"/> (world height).</summary>
        public Vector3 World(double s, float d, float y)
        {
            Vector3 p = Position(s) + Right(s) * d;
            p.y = y;
            return p;
        }

        /// <summary>World point on the trail plane (trail height) at (s, d).</summary>
        public Vector3 World(double s, float d)
        {
            return Position(s) + Right(s) * d;
        }

        private void Sample(float local, out int index, out float t)
        {
            float f = Mathf.Clamp(local / _step, 0f, _count - 0.0001f);
            index = (int)f;
            t = f - index;
        }
    }
}
