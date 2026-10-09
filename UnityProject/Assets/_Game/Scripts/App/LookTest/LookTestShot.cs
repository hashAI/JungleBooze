using System;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>One frame of the look-test shot list (ENVIRONMENT_STRATEGY section 10), rendered by the screenshot tool.</summary>
    [Serializable]
    public struct LookTestShot
    {
        [Tooltip("File name, for example F1_out_of_the_roots.")]
        public string Name;
        [Tooltip("Runner's path distance, m.")]
        public float DistanceM;
        public bool Portrait;

        public LookTestShot(string name, float distanceM, bool portrait)
        {
            Name = name;
            DistanceM = distanceM;
            Portrait = portrait;
        }
    }
}
