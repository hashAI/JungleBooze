using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Designer-facing speed curve (spec 001 section 3.2), saved as <c>Assets/_Game/Config/SpeedCurve.asset</c>.
    /// <see cref="ToSpeedCurve"/> builds the immutable <see cref="SpeedCurve"/> the simulation uses.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeedCurve", menuName = "JungleBooze/Config/Speed Curve")]
    public sealed class SpeedCurveAsset : ScriptableObject
    {
        [SerializeField] private SpeedCurveRow[] _rows =
        {
            new SpeedCurveRow(0f, 10.0f),
            new SpeedCurveRow(500f, 12.0f),
            new SpeedCurveRow(1100f, 13.5f),
            new SpeedCurveRow(2300f, 15.5f),
            new SpeedCurveRow(3600f, 17.5f),
            new SpeedCurveRow(5000f, 19.0f),
            new SpeedCurveRow(7000f, 21.0f),
        };

        [SerializeField] private float _tutorialSpeedMps = 8.0f;

        public int RowCount => _rows == null ? 0 : _rows.Length;

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when invalid.</summary>
        public SpeedCurve ToSpeedCurve()
        {
            GetArrays(out double[] distances, out double[] speeds);
            return new SpeedCurve(distances, speeds, _tutorialSpeedMps);
        }

        /// <summary>Rules from spec 001 section 3.2. Appends one message per problem.</summary>
        public bool Validate(List<string> errors)
        {
            GetArrays(out double[] distances, out double[] speeds);
            return SpeedCurve.Validate(distances, speeds, _tutorialSpeedMps, errors);
        }

        /// <summary>Overwrites the rows and tutorial speed. For tests and editor tooling.</summary>
        internal void SetValues(SpeedCurveRow[] rows, float tutorialSpeedMps)
        {
            _rows = rows == null ? null : (SpeedCurveRow[])rows.Clone();
            _tutorialSpeedMps = tutorialSpeedMps;
        }

        private void GetArrays(out double[] distances, out double[] speeds)
        {
            int count = RowCount;
            distances = new double[count];
            speeds = new double[count];
            for (int i = 0; i < count; i++)
            {
                distances[i] = _rows[i].DistanceM;
                speeds[i] = _rows[i].SpeedMps;
            }
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (SpeedCurveAsset) is invalid: " + string.Join(" ", errors), this);
            }
        }
    }
}
