using System;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>
    /// Camera shake (spec 101 §5): one active shake with amplitude and duration, linear fade, smooth value noise at
    /// a fixed frequency. A new shake replaces the current one only if it is stronger right now. Cosmetic only.
    /// </summary>
    public sealed class ShakeChannel
    {
        private float _amplitude;
        private float _duration;
        private float _age;
        private double _time;

        public float CurrentStrength => _duration > 0f && _age < _duration ? _amplitude * (1f - (_age / _duration)) : 0f;

        public void Trigger(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f)
            {
                return;
            }

            if (amplitude >= CurrentStrength)
            {
                _amplitude = amplitude;
                _duration = duration;
                _age = 0f;
            }
        }

        public void Clear()
        {
            _amplitude = 0f;
            _duration = 0f;
            _age = 0f;
        }

        /// <summary>Advances and returns noise in [−1, 1] per channel scaled by the current strength (m).</summary>
        public float Update(float dt, float frequency, out float nx, out float ny, out float nr)
        {
            _time += dt;
            if (_duration > 0f)
            {
                _age += dt;
            }

            float strength = CurrentStrength;
            double p = _time * frequency;
            nx = Noise(p, 11);
            ny = Noise(p, 37);
            nr = Noise(p, 73);
            return strength;
        }

        private static float Noise(double p, int seed)
        {
            double floor = Math.Floor(p);
            int i = (int)floor;
            float f = (float)(p - floor);
            float u = f * f * (3f - (2f * f));
            float a = Hash(i, seed);
            float b = Hash(i + 1, seed);
            return a + ((b - a) * u);
        }

        private static float Hash(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393) + (uint)(seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return ((h & 0xFFFFFF) / 8388607.5f) - 1f;
            }
        }
    }
}
