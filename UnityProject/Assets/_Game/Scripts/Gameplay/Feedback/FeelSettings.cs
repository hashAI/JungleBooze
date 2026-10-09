using System;
using JungleBooze.Core.Settings;

namespace JungleBooze.Gameplay.Feedback
{
    /// <summary>
    /// Player settings of the feel test, persisted through <see cref="ISettingsStore"/>: steering sensitivity
    /// multiplier (spec 101 §3.3: 0.5–2.0 on top of 0.040 m/pt), haptics on/off, reduced motion (§5).
    /// </summary>
    public sealed class FeelSettings
    {
        public const string SensitivityKey = "jb.settings.sensitivity";
        public const string HapticsKey = "jb.settings.haptics";
        public const string ReducedMotionKey = "jb.settings.reducedMotion";
        public const float MinSensitivity = 0.5f;
        public const float MaxSensitivity = 2f;
        public const float SensitivityStep = 0.1f;

        private readonly ISettingsStore _store;

        public FeelSettings(ISettingsStore store, bool systemReducedMotion)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Sensitivity = Quantize(_store.GetFloat(SensitivityKey, 1f));
            HapticsEnabled = _store.GetInt(HapticsKey, 1) != 0;

            // Defaults on when iOS Reduce Motion is on, until the player picks a value (spec 101 §5).
            ReducedMotion = _store.HasKey(ReducedMotionKey) ? _store.GetInt(ReducedMotionKey, 0) != 0 : systemReducedMotion;
        }

        public float Sensitivity { get; private set; }

        public bool HapticsEnabled { get; private set; }

        public bool ReducedMotion { get; private set; }

        public void SetSensitivity(float value)
        {
            Sensitivity = Quantize(value);
            _store.SetFloat(SensitivityKey, Sensitivity);
            _store.Save();
        }

        /// <summary>Steps the sensitivity by ±<see cref="SensitivityStep"/> per unit of <paramref name="steps"/>.</summary>
        public void StepSensitivity(int steps)
        {
            SetSensitivity(Sensitivity + (steps * SensitivityStep));
        }

        public void SetHaptics(bool enabled)
        {
            HapticsEnabled = enabled;
            _store.SetInt(HapticsKey, enabled ? 1 : 0);
            _store.Save();
        }

        public void SetReducedMotion(bool enabled)
        {
            ReducedMotion = enabled;
            _store.SetInt(ReducedMotionKey, enabled ? 1 : 0);
            _store.Save();
        }

        private static float Quantize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 1f;
            }

            value = value < MinSensitivity ? MinSensitivity : value > MaxSensitivity ? MaxSensitivity : value;
            return (float)Math.Round(value / SensitivityStep) * SensitivityStep;
        }
    }
}
