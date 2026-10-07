using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Path;

namespace JungleBooze.Tests.EditMode.RouteCamera
{
    /// <summary>
    /// Test route: straight until <c>startArcS</c>, then the curvature ramps to a constant value over <c>rampM</c>
    /// (a steady bend, radius 1 / |kappa|), banked like the debug route (6 deg at R = 75 m). Flat.
    /// </summary>
    public sealed class ArcRouteSource : IRouteSource
    {
        private const double FullBankKappa = 1.0 / 75.0;
        private const double MaxBankRad = 6.0 * Math.PI / 180.0;

        private readonly double _kappa;
        private readonly double _startArcS;
        private readonly double _rampM;

        private double _x;
        private double _z;
        private double _yaw;
        private double _s;
        private bool _first;

        public ArcRouteSource(double kappa, double startArcS, double rampM)
        {
            _kappa = kappa;
            _startArcS = startArcS;
            _rampM = Math.Max(1.0, rampM);
        }

        public void Begin(IRandom routeRandom, double startS)
        {
            _x = 0.0;
            _z = 0.0;
            _yaw = 0.0;
            _s = startS;
            _first = true;
        }

        public void NextSample(double s, out RouteSample sample)
        {
            if (_first)
            {
                _first = false;
            }
            else if (s != _s)
            {
                double ds = s - _s;
                double yawEnd = _yaw + (CurvatureAt(_s + (0.5 * ds)) * ds);
                double yawMid = 0.5 * (_yaw + yawEnd);
                _x += Math.Sin(yawMid) * ds;
                _z += Math.Cos(yawMid) * ds;
                _yaw = yawEnd;
            }

            _s = s;
            double kappa = CurvatureAt(s);
            sample = default(RouteSample);
            sample.X = _x;
            sample.Z = _z;
            sample.YawRad = (float)_yaw;
            sample.Curvature = (float)kappa;
            double bank = MaxBankRad * kappa / FullBankKappa;
            sample.BankRad = (float)Math.Max(-MaxBankRad, Math.Min(MaxBankRad, bank));
            sample.HalfWidthM = StraightRouteSource.HalfWidthM;
            sample.Layer = PathLayer.Floor;
            sample.Surface = PathSurface.Trail;
        }

        private double CurvatureAt(double s)
        {
            double u = (s - _startArcS) / _rampM;
            return _kappa * Math.Max(0.0, Math.Min(1.0, u));
        }
    }
}
