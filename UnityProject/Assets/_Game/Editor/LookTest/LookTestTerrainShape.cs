using JungleBooze.App.LookTest;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// The look-test stretch as height and material functions of (x, z) in stretch space (meters; z in
    /// [0, loop length)). Every function is periodic in z with the loop length, so the looped stretch has no seam.
    /// Layout across x (runner runs along +z at x in [-path, +path], y = 0 exactly, as the simulation assumes):
    ///   left: forest floor rising into a slope;
    ///   path: flat dirt trail (vertex color R);
    ///   right: a low bank, then the river channel (pebbles, vertex color G), the far bank and the cliff that the
    ///   waterfall falls from. Pure math (no editor API), so it can be unit-tested.
    /// </summary>
    public sealed class LookTestTerrainShape
    {
        private readonly LookTestConfigAsset _c;
        private readonly float _loop;

        public LookTestTerrainShape(LookTestConfigAsset config)
        {
            _c = config;
            _loop = config.LoopLengthM;
        }

        public float LoopLengthM => _loop;

        /// <summary>sin(2π · cycles · z / loop + phase): periodic in z for whole numbers of cycles.</summary>
        public float Wave(float z, int cycles, float phase)
        {
            return Mathf.Sin(2f * Mathf.PI * cycles * z / _loop + phase);
        }

        public float RiverCenterX(float z)
        {
            return _c.RiverCenterXM + _c.RiverMeanderM * Wave(z, 1, 0.6f) + 0.35f * _c.RiverMeanderM * Wave(z, 3, 1.9f);
        }

        /// <summary>0..1 bump centered on the waterfall (smooth, width in meters).</summary>
        public float WaterfallBump(float z, float widthM)
        {
            float d = PeriodicDistance(z, _c.WaterfallZM);
            return 1f - Smooth(0f, widthM, d);
        }

        /// <summary>0..1 cliff presence along z: rises over 15 m at both ends.</summary>
        public float CliffPresence(float z)
        {
            float start = _c.CliffStartZM;
            float end = _c.CliffEndZM;
            if (z < start || z > end)
            {
                return 0f;
            }

            return Smooth(start, start + 15f, z) * (1f - Smooth(end - 15f, end, z));
        }

        /// <summary>x of the cliff foot: set back from the far river bank, close to the water at the waterfall.</summary>
        public float CliffFootX(float z)
        {
            float setback = _c.CliffSetbackM - (_c.CliffSetbackM - 1.2f) * WaterfallBump(z, 14f);
            return RiverCenterX(z) + _c.RiverHalfWidthM + setback;
        }

        /// <summary>Ground height. Exactly 0 on the path.</summary>
        public float Height(float x, float z)
        {
            float path = _c.PathHalfWidthM;
            float ax = Mathf.Abs(x);
            if (ax <= path)
            {
                return 0f;
            }

            float bumps = Bumps(x, z);
            if (x < 0f)
            {
                float d = -x - path;
                float shoulder = 0.25f * Smooth(0f, 3f, d);
                float slope = _c.LeftSlope * Mathf.Max(0f, d - 3f);
                return shoulder + slope + bumps * Smooth(0f, 5f, d);
            }

            float right = x - path;
            float center = RiverCenterX(z);
            float r = Mathf.Abs(x - center);
            float half = _c.RiverHalfWidthM;
            float bank = 0.18f * Smooth(0f, 2f, right) + bumps * 0.4f * Smooth(0f, 4f, right) * Smooth(half * 1.3f, half * 2f, r);
            float channel = -_c.RiverBedDepthM * (1f - Smooth(half * 0.55f, half * 1.35f, r));
            float farSide = x > center ? 0.14f * Mathf.Max(0f, x - center - half * 1.2f) : 0f;
            return bank + channel + farSide + (x > center ? bumps * Smooth(half * 1.3f, half * 3f, r) : 0f);
        }

        /// <summary>Surface normal from central differences (continuous across segment borders).</summary>
        public Vector3 Normal(float x, float z)
        {
            const float e = 0.25f;
            float dx = (Height(x + e, z) - Height(x - e, z)) / (2f * e);
            float dz = (Height(x, z + e) - Height(x, z - e)) / (2f * e);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        /// <summary>Ground material weights: R = path dirt, G = river pebbles, A = vertex ambient occlusion.</summary>
        public Color Weights(float x, float z)
        {
            float edgeNoise = 0.45f * Wave(z, 23, 0.4f) + 0.3f * Wave(z, 41, 2.2f);
            float path = 1f - Smooth(_c.PathHalfWidthM - 0.4f, _c.PathHalfWidthM + 1.1f + edgeNoise, Mathf.Abs(x));
            float r = Mathf.Abs(x - RiverCenterX(z));
            float pebbles = 1f - Smooth(_c.RiverHalfWidthM * 1.05f, _c.RiverHalfWidthM * 1.6f + edgeNoise, r);
            path *= 1f - pebbles;
            float ao = 1f - 0.25f * pebbles * (1f - Smooth(0f, _c.RiverHalfWidthM, r));
            return new Color(path, pebbles, 0f, ao);
        }

        /// <summary>Gentle hummocks, periodic in z, zero mean.</summary>
        public float Bumps(float x, float z)
        {
            float b = 0.5f * Wave(z, 9, 0.3f + 0.21f * x) + 0.3f * Wave(z, 17, 1.1f - 0.13f * x) + 0.2f * Wave(z, 29, 2.7f + 0.37f * x);
            return b * _c.GroundBumpM;
        }

        /// <summary>Distance between two z values on the loop.</summary>
        public float PeriodicDistance(float a, float b)
        {
            float d = Mathf.Repeat(a - b, _loop);
            return Mathf.Min(d, _loop - d);
        }

        public static float Smooth(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
