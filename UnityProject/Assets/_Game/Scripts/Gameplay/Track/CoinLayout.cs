using System;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Turns coin patterns into coin positions (spec 002 sections 4.1, 10.2, 10.3). Static and allocation-free:
    /// positions are written into caller-owned buffers. Positions are world positions for a chunk that starts at
    /// <c>startZ</c>; mirroring is applied by the caller (<see cref="CoinPattern.Mirrored"/>) before calling.
    /// </summary>
    public static class CoinLayout
    {
        /// <summary>Tolerance for "z ≤ zEnd" so float rounding never drops the last coin of a line.</summary>
        private const float EndToleranceM = 1e-3f;

        /// <summary>Upper bound of coins one pattern can produce (buffer sizing).</summary>
        public const int MaxCoinsPerPattern = 64;

        /// <summary>
        /// Writes the coins of <paramref name="pattern"/> into the buffers starting at <paramref name="offset"/> and
        /// returns how many were written (never more than the buffers hold).
        /// <paramref name="arcSpeedMps"/> is the speed HERO will have at the arc centre (the speed curve at its world
        /// z in a run; the constant validation speed in the validator). Lines and trails ignore it.
        /// </summary>
        public static int Generate(
            in CoinPattern pattern,
            double startZ,
            double arcSpeedMps,
            CoinConfig coins,
            RunnerConfig runner,
            float[] xs,
            float[] ys,
            double[] zs,
            int offset)
        {
            int capacity = Math.Min(xs.Length, Math.Min(ys.Length, zs.Length)) - offset;
            if (capacity <= 0)
            {
                return 0;
            }

            switch (pattern.Type)
            {
                case CoinPatternType.Line:
                    return Line(pattern, startZ, coins, runner, xs, ys, zs, offset, capacity);
                case CoinPatternType.Trail:
                    return Trail(pattern, startZ, coins, runner, xs, ys, zs, offset, capacity);
                case CoinPatternType.Arc:
                    return Arc(pattern, startZ, arcSpeedMps, coins, runner, xs, ys, zs, offset, capacity);
                case CoinPatternType.Single:
                    xs[offset] = pattern.X;
                    ys[offset] = pattern.Y;
                    zs[offset] = startZ + pattern.ZStart;
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Height of the jump parabola at horizontal offset <paramref name="d"/> from the apex (spec 002 10.2):
        /// <c>Yjump(d) = apex − ½·g·(d/v)²</c>.
        /// </summary>
        public static double JumpHeightAt(double d, double speedMps, RunnerConfig runner)
        {
            double t = d / speedMps;
            return runner.JumpApexHeightM - 0.5 * runner.GravityMps2 * t * t;
        }

        /// <summary>Smoothstep 3u² − 2u³ on [0, 1].</summary>
        public static float Smoothstep(float u)
        {
            if (u <= 0f)
            {
                return 0f;
            }

            if (u >= 1f)
            {
                return 1f;
            }

            return u * u * (3f - 2f * u);
        }

        /// <summary>Lane whose centre is nearest to <paramref name="x"/>, clamped to the track.</summary>
        public static int NearestLane(float x, RunnerConfig runner)
        {
            double f = x / runner.LaneWidthM + (runner.LaneCount - 1) * 0.5;
            int lane = (int)Math.Floor(f + 0.5);
            if (lane < 0)
            {
                return 0;
            }

            return lane >= runner.LaneCount ? runner.LaneCount - 1 : lane;
        }

        private static float Spacing(in CoinPattern p, CoinConfig coins)
        {
            return p.SpacingM > 0f ? p.SpacingM : coins.LineSpacingM;
        }

        private static int Line(in CoinPattern p, double startZ, CoinConfig coins, RunnerConfig runner, float[] xs, float[] ys, double[] zs, int offset, int capacity)
        {
            float spacing = Spacing(p, coins);
            float x = runner.LaneCenterX(p.Lane);
            int n = 0;
            for (int k = 0; n < capacity; k++)
            {
                float zc = p.ZStart + k * spacing;
                if (zc > p.ZEnd + EndToleranceM)
                {
                    break;
                }

                xs[offset + n] = x;
                ys[offset + n] = coins.CoinHeightM;
                zs[offset + n] = startZ + zc;
                n++;
            }

            return n;
        }

        private static int Trail(in CoinPattern p, double startZ, CoinConfig coins, RunnerConfig runner, float[] xs, float[] ys, double[] zs, int offset, int capacity)
        {
            float spacing = Spacing(p, coins);
            float fromX = runner.LaneCenterX(p.Lane);
            float toX = runner.LaneCenterX(p.ToLane);
            float length = p.ZEnd - p.ZStart;
            int n = 0;
            for (int k = 0; n < capacity; k++)
            {
                float zc = p.ZStart + k * spacing;
                if (zc > p.ZEnd + EndToleranceM)
                {
                    break;
                }

                float u = length > 0f ? (zc - p.ZStart) / length : 1f;
                float s = Smoothstep(u);
                xs[offset + n] = fromX + (toX - fromX) * s;
                ys[offset + n] = coins.CoinHeightM;
                zs[offset + n] = startZ + zc;
                n++;
            }

            return n;
        }

        private static int Arc(in CoinPattern p, double startZ, double speedMps, CoinConfig coins, RunnerConfig runner, float[] xs, float[] ys, double[] zs, int offset, int capacity)
        {
            double v = speedMps > 0.0 ? speedMps : 1.0;
            int count = Math.Min(coins.ArcCoinCount, capacity);
            double airtimeS = runner.JumpAirtimeTicks * RunnerConfig.TickSeconds;
            double jumpLength = airtimeS * v;
            double step = coins.ArcSpanFraction * jumpLength / (coins.ArcCoinCount - 1);
            double mid = (coins.ArcCoinCount - 1) * 0.5;
            float x = runner.LaneCenterX(p.Lane);
            for (int k = 0; k < count; k++)
            {
                double d = (k - mid) * step;
                xs[offset + k] = x;
                ys[offset + k] = (float)(coins.CoinHeightM + JumpHeightAt(d, v, runner));
                zs[offset + k] = startZ + p.ZCenter + d;
            }

            return count;
        }
    }
}
