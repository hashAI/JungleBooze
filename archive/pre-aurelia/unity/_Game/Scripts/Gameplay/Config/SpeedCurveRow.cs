using System;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>One authored row of the speed curve: at <see cref="DistanceM"/> the speed is <see cref="SpeedMps"/>.</summary>
    [Serializable]
    public struct SpeedCurveRow
    {
        [SerializeField] private float _distanceM;
        [SerializeField] private float _speedMps;

        public SpeedCurveRow(float distanceM, float speedMps)
        {
            _distanceM = distanceM;
            _speedMps = speedMps;
        }

        public float DistanceM => _distanceM;

        public float SpeedMps => _speedMps;
    }
}
