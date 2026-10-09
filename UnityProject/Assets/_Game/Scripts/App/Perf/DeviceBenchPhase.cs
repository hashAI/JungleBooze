using System;
using UnityEngine;

namespace JungleBooze.App.Perf
{
    /// <summary>One timed phase of the device benchmark: a scene, an orientation, a duration.</summary>
    [Serializable]
    public sealed class DeviceBenchPhase
    {
        [SerializeField] private string _name = "phase";
        [SerializeField] private string _scene = "HeroBasin_Painterly";
        [SerializeField] private DeviceBenchMode _mode = DeviceBenchMode.HeroView;
        [SerializeField] private bool _portrait;

        [Tooltip("Measured seconds (after the warm-up).")]
        [SerializeField] private float _seconds = 60f;

        [Tooltip("Seconds after the scene loads that are logged but kept out of the phase statistics.")]
        [SerializeField] private float _warmUpSeconds = 5f;

        public DeviceBenchPhase()
        {
        }

        public DeviceBenchPhase(string name, string scene, DeviceBenchMode mode, bool portrait, float seconds, float warmUpSeconds)
        {
            _name = name;
            _scene = scene;
            _mode = mode;
            _portrait = portrait;
            _seconds = seconds;
            _warmUpSeconds = warmUpSeconds;
        }

        public string Name => _name;
        public string Scene => _scene;
        public DeviceBenchMode Mode => _mode;
        public bool Portrait => _portrait;
        public float Seconds => Mathf.Max(1f, _seconds);
        public float WarmUpSeconds => Mathf.Max(0f, _warmUpSeconds);
    }
}
