using JungleBooze.App.LookTest;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Look test v2 layout (ENVIRONMENT_STRATEGY 4.1) as functions of path space (s along the trail, d lateral, + =
    /// right): ground height, the river that crosses the trail at the ford, canopy cover and ground material weights.
    /// Every function is periodic in s with the loop length, so the looped stretch has no seam.
    /// The world reads as one place: the trail runs on a forested plateau; on the left the ground rises into hills,
    /// on the right the plateau ends at an edge (<see cref="RightEdge"/>) and drops to the basin shelf or the valley
    /// floor. Banks and canopy enclose the trail for about 70% of the loop and open at the fork, the ford and the
    /// basin. Pure math (no editor API), unit-tested.
    /// </summary>
    public sealed class LookTestStretchLayout
    {
        private readonly LookTestConfigAsset _c;
        private readonly float _loop;
        private readonly PeriodicCurve _enclosure;
        private readonly PeriodicCurve _bankLeft;
        private readonly PeriodicCurve _bankRight;
        private readonly PeriodicCurve _rightEdge;
        private readonly PeriodicCurve _belowRight;

        public LookTestStretchLayout(LookTestConfigAsset config)
        {
            _c = config;
            _loop = config.LoopLengthM;
            Path = config.CreatePath();
            _enclosure = new PeriodicCurve(_loop, config.EnclosureKeys);
            _bankLeft = new PeriodicCurve(_loop, config.BankLeftKeys);
            _bankRight = new PeriodicCurve(_loop, config.BankRightKeys);
            _rightEdge = new PeriodicCurve(_loop, config.RightEdgeKeys);
            _belowRight = new PeriodicCurve(_loop, config.BelowRightKeys);
        }

        public LookTestPath Path { get; }

        public LookTestConfigAsset Config => _c;

        public float LoopLengthM => _loop;

        /// <summary>sin(2π · cycles · s / loop + phase): periodic in s for whole numbers of cycles.</summary>
        public float Wave(float s, int cycles, float phase)
        {
            return Mathf.Sin(2f * Mathf.PI * cycles * s / _loop + phase);
        }

        /// <summary>Canopy enclosure 0..1 at s (1 = under the roof).</summary>
        public float Enclosure(float s)
        {
            return Mathf.Clamp01(_enclosure.Evaluate(s));
        }

        public float RightEdge(float s)
        {
            return Mathf.Max(_c.RunnableHalfWidthM + 3f, _rightEdge.Evaluate(s));
        }

        public float BelowRight(float s)
        {
            return _belowRight.Evaluate(s);
        }

        public float TrailHeight(float s)
        {
            return Path.Height(s);
        }

        // ---------------------------------------------------------------- River

        /// <summary>True if the river exists at s (between its start and end, wrapped into the loop).</summary>
        public bool RiverActive(float s)
        {
            s = Mathf.Repeat(s, _loop);
            return s >= _c.RiverStartSM && s <= _c.RiverEndSM;
        }

        /// <summary>
        /// Lateral position of the river centre at s: it comes down from the hills on the left, crosses the trail at
        /// the ford and runs off to the right, toward the plateau edge.
        /// </summary>
        public float RiverD(float s)
        {
            s = Mathf.Repeat(s, _loop);
            float cross = _c.RiverCrossSM;
            float d = s < cross
                ? -62f * (cross - s) / Mathf.Max(1f, cross - _c.RiverStartSM)
                : 62f * (s - cross) / Mathf.Max(1f, _c.RiverEndSM - cross);
            return d + 2.5f * Mathf.Sin((s - cross) * 0.11f) * LookTestMath.Smooth(4f, 18f, Mathf.Abs(s - cross));
        }

        /// <summary>World height of the water surface where the river is at lateral offset <paramref name="riverD"/>.</summary>
        public float WaterY(float riverD)
        {
            return TrailHeight(_c.RiverCrossSM) + _c.FordDepthM - _c.RiverSlope * riverD;
        }

        /// <summary>s where the river reaches the plateau edge and falls (the river waterfall), or -1.</summary>
        public float RiverFallS()
        {
            for (float s = _c.RiverCrossSM; s <= _c.RiverEndSM; s += 0.25f)
            {
                if (RiverD(s) >= RightEdge(s) - 1.5f)
                {
                    return s;
                }
            }

            return -1f;
        }

        /// <summary>Distance from (s, d) to the river centre along d, or +inf if there is no river at s.</summary>
        public float RiverDistance(float s, float d)
        {
            if (!RiverActive(s))
            {
                return float.PositiveInfinity;
            }

            float rd = RiverD(s);
            if (rd > RightEdge(s) - 1f)
            {
                return float.PositiveInfinity;
            }

            return Mathf.Abs(d - rd);
        }

        // ---------------------------------------------------------------- Ground

        /// <summary>Ground height (world y) at path coordinates. Exactly the trail height on the runnable width (except in the ford).</summary>
        public float GroundY(float s, float d)
        {
            float h = TrailHeight(s);
            float run = _c.RunnableHalfWidthM;
            float ad = Mathf.Abs(d);
            float y = h;
            if (ad > run)
            {
                float verge = 0.22f * LookTestMath.Smooth(run, run + 2.5f, ad);
                float bump = Bumps(s, d) * LookTestMath.Smooth(run + 1f, run + 6f, ad);
                if (d < 0f)
                {
                    float bank = _bankLeft.Evaluate(s) * LookTestMath.Smooth(run + 2f, 13f, ad);
                    float hill = 0.3f * Mathf.Max(0f, ad - 20f) + 2.5f * LookTestMath.Smooth(30f, 50f, ad) * (1f + Wave(s, 5, 1.3f));
                    y = h + verge + bank + hill + bump;
                }
                else
                {
                    float bank = Mathf.Max(0f, _bankRight.Evaluate(s)) * LookTestMath.Smooth(run + 2f, 12f, ad);
                    y = h + verge + bank + bump;
                    float edge = RightEdge(s);
                    float drop = LookTestMath.Smooth(edge, edge + 6f, ad);
                    float below = BelowRight(s) + 1.5f * Bumps(s, d * 0.5f);
                    y = Mathf.Lerp(y, below, drop);
                    float skirt = LookTestMath.Smooth(_c.StripHalfWidthM - 7f, _c.StripHalfWidthM, ad);
                    y = Mathf.Lerp(y, Mathf.Min(y, _c.ValleyFloorY), skirt);
                }
            }

            float r = RiverDistance(s, d);
            if (r < _c.RiverHalfWidthM * 2.2f)
            {
                // Carve the channel; the banks blend back into the terrain, so nothing outside the river moves.
                float bank = LookTestMath.Smooth(_c.RiverHalfWidthM * 0.9f, _c.RiverHalfWidthM * 2.2f, r);
                y = Mathf.Min(y, Mathf.Lerp(RiverBed(s, d, r), y, bank));
            }

            return y;
        }

        /// <summary>Riverbed shape: deep in the middle, shallow (the ford) on the trail, rising above the water at the banks.</summary>
        private float RiverBed(float s, float d, float r)
        {
            float half = _c.RiverHalfWidthM;
            float run = _c.RunnableHalfWidthM;
            float water = WaterY(RiverD(s));
            float fordBlend = 1f - LookTestMath.Smooth(run, run + 3.5f, Mathf.Abs(d));
            float depth = Mathf.Lerp(_c.RiverDepthM, _c.FordDepthM, fordBlend);
            float bed = water - depth * (1f - LookTestMath.Smooth(half * 0.35f, half * 1.05f, r)) - 0.08f;
            return Mathf.Lerp(bed, water + 0.5f, LookTestMath.Smooth(half * 0.8f, half * 1.3f, r));
        }

        /// <summary>Gentle hummocks, periodic in s, zero mean.</summary>
        public float Bumps(float s, float d)
        {
            float b = 0.5f * Wave(s, 19, 0.3f + 0.21f * d) + 0.3f * Wave(s, 37, 1.1f - 0.13f * d) + 0.2f * Wave(s, 61, 2.7f + 0.37f * d);
            return b * _c.GroundBumpM;
        }

        // ---------------------------------------------------------------- Light and materials

        /// <summary>
        /// Canopy cover 0..1 over a point at (s, d), <paramref name="heightAboveTrail"/> meters above the trail height
        /// (vertex color B; JBAtmosphere.hlsl): the enclosure curve, deeper shade inside the forest walls, sunlit
        /// patches, and full sun above the roof.
        /// </summary>
        public float CanopyCover(float s, float d, float heightAboveTrail)
        {
            float ad = Mathf.Abs(d);
            float cover = Enclosure(s);
            float inWall = 0.8f * LookTestMath.Smooth(9f, 22f, ad);
            if (d > 0f)
            {
                inWall *= 1f - LookTestMath.Smooth(RightEdge(s) - 5f, RightEdge(s), ad);
            }

            cover = Mathf.Max(cover, inWall);
            Vector4[] patches = _c.SunPatches;
            if (patches != null)
            {
                for (int i = 0; i < patches.Length; i++)
                {
                    float ds = PeriodicDistance(s, patches[i].x);
                    float dd = d - patches[i].y;
                    float dist = Mathf.Sqrt(ds * ds + dd * dd);
                    cover *= 1f - patches[i].w * (1f - LookTestMath.Smooth(patches[i].z * 0.5f, patches[i].z, dist));
                }
            }

            Vector2 roof = _c.CanopyRoofHeightM;
            cover *= 1f - LookTestMath.Smooth(roof.x - 1f, roof.y + 2f, heightAboveTrail);
            if (d > 0f && ad > RightEdge(s) + 2f)
            {
                cover = 0f;
            }

            return Mathf.Clamp01(cover);
        }

        /// <summary>Ground material weights: R = trail soil, G = river pebbles and rock, B = canopy cover, A = ambient occlusion.</summary>
        public Color GroundWeights(float s, float d)
        {
            float ad = Mathf.Abs(d);
            float edgeNoise = 0.35f * Wave(s, 23, 0.4f) + 0.25f * Wave(s, 41, 2.2f);
            float trail = _c.TrailHalfWidthM + edgeNoise;
            float soil = 1f - LookTestMath.Smooth(trail - 0.5f, trail + 0.6f, ad);

            // The safe route at the fork is a wide soft moss path (ART_DIRECTION 6): less bare soil.
            float fork = 1f - LookTestMath.Smooth(8f, 22f, PeriodicDistance(s, 84f));
            soil *= 1f - 0.7f * fork;

            float rock = 0f;
            float r = RiverDistance(s, d);
            if (!float.IsInfinity(r))
            {
                rock = 1f - LookTestMath.Smooth(_c.RiverHalfWidthM * 1.0f, _c.RiverHalfWidthM * 1.7f + edgeNoise, r);
            }

            if (d > 0f)
            {
                float edge = RightEdge(s);
                rock = Mathf.Max(rock, LookTestMath.Smooth(edge - 1.5f, edge + 1f, ad));
            }

            soil *= 1f - rock;
            float cover = CanopyCover(s, d, GroundY(s, d) - TrailHeight(s));
            float ao = 1f - 0.3f * LookTestMath.Smooth(_c.RunnableHalfWidthM + 1f, 10f, ad) * Enclosure(s);
            if (!float.IsInfinity(r))
            {
                ao *= 1f - 0.2f * rock;
            }

            return new Color(soil, rock, cover, ao);
        }

        /// <summary>Distance between two s values on the loop.</summary>
        public float PeriodicDistance(float a, float b)
        {
            float d = Mathf.Repeat(a - b, _loop);
            return Mathf.Min(d, _loop - d);
        }
    }
}
