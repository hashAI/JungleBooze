using JungleBooze.Core.Settings;
using JungleBooze.Gameplay.Feedback;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Feedback
{
    /// <summary>Persisted player settings: sensitivity 0.5–2.0 in 0.1 steps, haptics, reduced motion default from iOS.</summary>
    public sealed class FeelSettingsTests
    {
        [Test]
        public void DefaultsWithAnEmptyStore()
        {
            var s = new FeelSettings(new MemorySettingsStore(), false);
            Assert.AreEqual(1f, s.Sensitivity, 1e-6f);
            Assert.IsTrue(s.HapticsEnabled);
            Assert.IsFalse(s.ReducedMotion);
            Assert.IsTrue(new FeelSettings(new MemorySettingsStore(), true).ReducedMotion, "follows iOS Reduce Motion until set");
        }

        [Test]
        public void SensitivityIsClampedQuantizedAndPersisted()
        {
            var store = new MemorySettingsStore();
            var s = new FeelSettings(store, false);
            s.StepSensitivity(3);
            Assert.AreEqual(1.3f, s.Sensitivity, 1e-5f);
            s.SetSensitivity(9f);
            Assert.AreEqual(2f, s.Sensitivity, 1e-5f);
            s.SetSensitivity(0.1f);
            Assert.AreEqual(0.5f, s.Sensitivity, 1e-5f);
            s.SetSensitivity(1.26f);
            Assert.GreaterOrEqual(store.SaveCount, 4);

            var reloaded = new FeelSettings(store, false);
            Assert.AreEqual(1.3f, reloaded.Sensitivity, 1e-5f);
        }

        [Test]
        public void TogglesPersist()
        {
            var store = new MemorySettingsStore();
            var s = new FeelSettings(store, true);
            s.SetHaptics(false);
            s.SetReducedMotion(false);
            var reloaded = new FeelSettings(store, true);
            Assert.IsFalse(reloaded.HapticsEnabled);
            Assert.IsFalse(reloaded.ReducedMotion, "an explicit choice beats the system default");
        }

        [Test]
        public void GarbageInTheStoreFallsBackToDefault()
        {
            var store = new MemorySettingsStore();
            store.SetFloat(FeelSettings.SensitivityKey, float.NaN);
            Assert.AreEqual(1f, new FeelSettings(store, false).Sensitivity, 1e-6f);
        }
    }
}
