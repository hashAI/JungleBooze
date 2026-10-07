using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// A gentle curved route for trying the curved presentation (spec 003 task T3): the heading swings sinusoidally
    /// +-25 degrees over a 300 m period (tightest radius about 109 m), the slope rolls +-5 percent over 400 m, and the
    /// ground banks up to about 4 degrees into the turns. The route passes through the origin at s = 0 heading +z, so
    /// the run starts where the straight route starts. Deterministic and independent of the seed; not a gameplay
    /// route (the generator in T2 replaces it). No allocation after construction.
    /// </summary>
    public sealed class DebugRouteSource : IRouteSource
    {
        /// <summary>Heading amplitude in degrees.</summary>
        public const float YawAmplitudeDeg = 25f;

        /// <summary>Heading period in m.</summary>
        public const float YawPeriodM = 300f;

        /// <summary>Slope amplitude in percent.</summary>
        public const float SlopePct = 5f;

        /// <summary>Slope period in m.</summary>
        public const float SlopePeriodM = 400f;

        /// <summary>Curvature (rad/m) that gets the full bank; the bank scales linearly below it (spec 003 section 4.3).</summary>
        private const float FullBankCurvature = 1f / 75f;

        private const float MaxBankDeg = 6f;
        private const double PreRollStepM = 1.0;

        private double _x;
        private double _y;
        private double _z;
        private double _s;
        private bool _first;

        public void Begin(IRandom routeRandom, double startS)
        {
            _first = true;
            _s = startS;

            // Walk from startS to 0 with the same unit steps and start there minus the walk, so the route passes
            // through the origin at s = 0 (when startS is negative, as it is for a run).
            double px = 0.0;
            double py = 0.0;
            double pz = 0.0;
            for (double q = startS; q < 0.0; q += PreRollStepM)
            {
                Advance(q, PreRollStepM, ref px, ref py, ref pz);
            }

            _x = -px;
            _y = -py;
            _z = -pz;
        }

        public void NextSample(double s, out RouteSample sample)
        {
            if (_first)
            {
                _first = false;
            }
            else if (s != _s)
            {
                Advance(_s, s - _s, ref _x, ref _y, ref _z);
            }

            _s = s;

            float yaw = YawAt(s);
            sample = default(RouteSample);
            sample.X = _x;
            sample.Y = _y;
            sample.Z = _z;
            sample.YawRad = yaw;
            sample.PitchRad = PitchAt(s);
            sample.Curvature = CurvatureAt(s);
            float bankDeg = Math.Max(-MaxBankDeg, Math.Min(MaxBankDeg, MaxBankDeg * sample.Curvature / FullBankCurvature));
            sample.BankRad = bankDeg * (float)(Math.PI / 180.0);
            sample.HalfWidthM = StraightRouteSource.HalfWidthM;
            sample.Layer = PathLayer.Floor;
            sample.Surface = PathSurface.Trail;
        }

        /// <summary>Heading in radians at arc length <paramref name="s"/> (0 at s = 0, positive turns toward +x).</summary>
        public static float YawAt(double s)
        {
            double amplitude = YawAmplitudeDeg * Math.PI / 180.0;
            return (float)(amplitude * Math.Sin(2.0 * Math.PI * s / YawPeriodM));
        }

        /// <summary>Pitch in radians at arc length <paramref name="s"/> (positive climbs).</summary>
        public static float PitchAt(double s)
        {
            double amplitude = Math.Atan(SlopePct / 100.0);
            return (float)(amplitude * Math.Sin(2.0 * Math.PI * s / SlopePeriodM));
        }

        /// <summary>Horizontal curvature in rad/m (positive = turning right), the derivative of the heading.</summary>
        public static float CurvatureAt(double s)
        {
            double amplitude = YawAmplitudeDeg * Math.PI / 180.0;
            double w = 2.0 * Math.PI / YawPeriodM;
            return (float)(amplitude * w * Math.Cos(w * s));
        }

        /// <summary>Moves a position along the route from <paramref name="from"/> by <paramref name="length"/> m (midpoint direction).</summary>
        private static void Advance(double from, double length, ref double x, ref double y, ref double z)
        {
            double mid = from + length * 0.5;
            double yaw = YawAt(mid);
            double pitch = PitchAt(mid);
            double cp = Math.Cos(pitch);
            x += Math.Sin(yaw) * cp * length;
            y += Math.Sin(pitch) * length;
            z += Math.Cos(yaw) * cp * length;
        }
    }
}
