using System;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Pure-math model of the portrait camera for the on-screen rule (spec 003 sections 6.1 and 14.2): in steady
    /// state the camera sits 6 m behind the hero, 70 percent of the lane offset to the side, and aims at the mean
    /// heading of the route 8 m and 22 m ahead. A point is inside when its bearing from the aim, as a share of the
    /// horizontal half-FOV, is below 1 minus the margin. Top-down check; vertical placement is not probed.
    /// [ASSUMED] These are the spec's start values, not the T4 camera's live numbers.
    /// </summary>
    public sealed class ViewportProbe
    {
        public double VerticalFovDeg = 60.0;
        public double BendFovBonusDeg = 4.0;
        public double BendFovFullRadiusM = 75.0;
        public double AspectWidthOverHeight = 9.0 / 19.5;
        public double Margin = 0.06;
        public double BehindM = 6.0;
        public double LateralFollow = 0.7;
        public double AimNearM = 8.0;
        public double AimFarM = 22.0;
        public double LaneWidthM = 2.4;

        private static readonly double[] Distances = { 10.0, 25.0, 40.0 };

        /// <summary>
        /// Probes the hero positions <c>from, from + stride, ...</c> (sample indices) for the lanes -1, 0 and +1 at 10,
        /// 25 and 40 m. Returns the number of points outside the margin; <paramref name="worstRatio"/> is the largest
        /// ratio seen (the rule holds while it stays below 1 minus the margin).
        /// </summary>
        public int Probe(RouteSample[] samples, int count, double startS, double spacing, int from, int to, int stride, out double worstRatio)
        {
            worstRatio = 0.0;
            int violations = 0;
            int step = Math.Max(1, stride);
            int behindSamples = (int)Math.Ceiling(BehindM / spacing);
            int aheadSamples = (int)Math.Ceiling(40.0 / spacing) + 2;
            int first = Math.Max(from, behindSamples + 1);
            int last = Math.Min(to, count - 1 - aheadSamples);
            for (int i = first; i <= last; i += step)
            {
                double sHero = startS + (i * spacing);
                double heroKappa = Math.Abs(Lerp(samples, count, startS, spacing, sHero, 3));
                double fov = VerticalFovDeg + (BendFovBonusDeg * Math.Min(1.0, heroKappa * BendFovFullRadiusM));
                double halfTan = Math.Tan(fov * 0.5 * Math.PI / 180.0) * AspectWidthOverHeight;
                double aim = 0.5 * (Lerp(samples, count, startS, spacing, sHero + AimNearM, 4) + Lerp(samples, count, startS, spacing, sHero + AimFarM, 4));
                double sinAim = Math.Sin(aim);
                double cosAim = Math.Cos(aim);

                double camS = sHero - BehindM;
                double camYaw = Lerp(samples, count, startS, spacing, camS, 4);
                double camX = Lerp(samples, count, startS, spacing, camS, 0);
                double camZ = Lerp(samples, count, startS, spacing, camS, 2);

                for (int lane = -1; lane <= 1; lane++)
                {
                    double lateral = lane * LaneWidthM;
                    double cx = camX + (LateralFollow * lateral * Math.Cos(camYaw));
                    double cz = camZ - (LateralFollow * lateral * Math.Sin(camYaw));
                    for (int k = 0; k < Distances.Length; k++)
                    {
                        double s = sHero + Distances[k];
                        double yaw = Lerp(samples, count, startS, spacing, s, 4);
                        double px = Lerp(samples, count, startS, spacing, s, 0) + (lateral * Math.Cos(yaw));
                        double pz = Lerp(samples, count, startS, spacing, s, 2) - (lateral * Math.Sin(yaw));
                        double vx = px - cx;
                        double vz = pz - cz;
                        double forward = (vx * sinAim) + (vz * cosAim);
                        double right = (vx * cosAim) - (vz * sinAim);
                        double ratio = forward > 1e-6 ? Math.Abs(right / forward) / halfTan : double.MaxValue;
                        if (ratio > worstRatio)
                        {
                            worstRatio = ratio;
                        }

                        if (ratio > 1.0 - Margin)
                        {
                            violations++;
                        }
                    }
                }
            }

            return violations;
        }

        /// <summary>Linear interpolation of a field at arc length <paramref name="s"/>: 0 x, 1 y, 2 z, 3 curvature, 4 yaw.</summary>
        private static double Lerp(RouteSample[] samples, int count, double startS, double spacing, double s, int field)
        {
            double u = (s - startS) / spacing;
            int i = (int)Math.Floor(u);
            if (i < 0)
            {
                i = 0;
            }
            else if (i > count - 2)
            {
                i = count - 2;
            }

            double t = u - i;
            double a = Field(samples[i], field);
            double b = Field(samples[i + 1], field);
            return a + ((b - a) * t);
        }

        private static double Field(in RouteSample sample, int field)
        {
            switch (field)
            {
                case 0:
                    return sample.X;
                case 1:
                    return sample.Y;
                case 2:
                    return sample.Z;
                case 3:
                    return sample.Curvature;
                default:
                    return sample.YawRad;
            }
        }
    }
}
