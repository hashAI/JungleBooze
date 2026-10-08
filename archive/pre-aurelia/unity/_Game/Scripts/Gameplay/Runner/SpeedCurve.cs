using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Immutable forward speed curve (spec 001 section 3.2): linear interpolation between (distance, speed) rows,
    /// first speed below the first row, last speed beyond the last row. Evaluate does not allocate.
    /// </summary>
    public sealed class SpeedCurve
    {
        private readonly double[] _distancesM;
        private readonly double[] _speedsMps;

        /// <param name="distancesM">Row distances, strictly increasing.</param>
        /// <param name="speedsMps">Row speeds, positive and not decreasing.</param>
        /// <param name="tutorialSpeedMps">Speed while the onboarding tutorial is active.</param>
        public SpeedCurve(double[] distancesM, double[] speedsMps, double tutorialSpeedMps)
        {
            var errors = new List<string>();
            if (!Validate(distancesM, speedsMps, tutorialSpeedMps, errors))
            {
                throw new ArgumentException("Invalid speed curve: " + string.Join(" ", errors));
            }

            _distancesM = (double[])distancesM.Clone();
            _speedsMps = (double[])speedsMps.Clone();
            TutorialSpeedMps = tutorialSpeedMps;
        }

        public double TutorialSpeedMps { get; }

        public int RowCount => _distancesM.Length;

        /// <summary>The spec 001 start values.</summary>
        public static SpeedCurve CreateDefault()
        {
            return new SpeedCurve(
                new[] { 0.0, 500.0, 1100.0, 2300.0, 3600.0, 5000.0, 7000.0 },
                new[] { 10.0, 12.0, 13.5, 15.5, 17.5, 19.0, 21.0 },
                8.0);
        }

        /// <summary>A flat curve with one row. Handy for tests and bots.</summary>
        public static SpeedCurve CreateConstant(double speedMps)
        {
            return new SpeedCurve(new[] { 0.0 }, new[] { speedMps }, speedMps);
        }

        /// <summary>
        /// Checks the rules from spec 001 section 3.2: at least one row, equal lengths, distances strictly increasing
        /// and not negative, speeds positive and not decreasing, tutorial speed positive.
        /// </summary>
        public static bool Validate(double[] distancesM, double[] speedsMps, double tutorialSpeedMps, List<string> errors)
        {
            int before = errors.Count;
            if (distancesM == null || speedsMps == null || distancesM.Length == 0)
            {
                errors.Add("The speed curve needs at least one row.");
                return false;
            }

            if (distancesM.Length != speedsMps.Length)
            {
                errors.Add("Distance and speed row counts differ.");
                return false;
            }

            for (int i = 0; i < distancesM.Length; i++)
            {
                if (!(distancesM[i] >= 0.0))
                {
                    errors.Add("Row " + i + ": distance must not be negative.");
                }

                if (!(speedsMps[i] > 0.0))
                {
                    errors.Add("Row " + i + ": speed must be positive.");
                }

                if (i > 0 && !(distancesM[i] > distancesM[i - 1]))
                {
                    errors.Add("Row " + i + ": rows must be sorted by distance (strictly increasing).");
                }

                if (i > 0 && !(speedsMps[i] >= speedsMps[i - 1]))
                {
                    errors.Add("Row " + i + ": speeds must not decrease.");
                }
            }

            if (!(tutorialSpeedMps > 0.0))
            {
                errors.Add("tutorialSpeedMps must be positive.");
            }

            return errors.Count == before;
        }

        public double GetRowDistance(int index)
        {
            return _distancesM[index];
        }

        public double GetRowSpeed(int index)
        {
            return _speedsMps[index];
        }

        /// <summary>Speed in m/s at the given distance along the track.</summary>
        public double Evaluate(double distanceM)
        {
            int last = _distancesM.Length - 1;
            if (distanceM <= _distancesM[0])
            {
                return _speedsMps[0];
            }

            if (distanceM >= _distancesM[last])
            {
                return _speedsMps[last];
            }

            int i = 1;
            while (_distancesM[i] < distanceM)
            {
                i++;
            }

            double d0 = _distancesM[i - 1];
            double d1 = _distancesM[i];
            double t = (distanceM - d0) / (d1 - d0);
            return _speedsMps[i - 1] + (_speedsMps[i] - _speedsMps[i - 1]) * t;
        }
    }
}
