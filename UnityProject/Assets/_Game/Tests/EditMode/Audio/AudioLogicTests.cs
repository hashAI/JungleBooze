using JungleBooze.Core.Feedback;
using JungleBooze.Core.Settings;
using JungleBooze.Services.Audio;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Audio
{
    /// <summary>Haptic gate, player volumes, coin streak pitch and the adaptive music mix.</summary>
    public sealed class AudioLogicTests
    {
        [Test]
        public void HapticGateAllowsOnePer100MsAndRespectsTheToggle()
        {
            double t = 0;
            var inner = new NullHaptics();
            var gate = new HapticGate(inner, () => t);
            gate.Impact(HapticImpact.Light);
            t = 0.05;
            gate.Impact(HapticImpact.Heavy);
            t = 0.101;
            gate.Play(CueHaptic.Success);
            Assert.AreEqual(1, inner.LightCount);
            Assert.AreEqual(0, inner.HeavyCount, "dropped inside 100 ms");
            Assert.AreEqual(1, inner.MediumCount, "success maps to a medium impact");
            Assert.AreEqual(1, gate.Dropped);
            gate.Enabled = false;
            t = 1;
            gate.Impact(HapticImpact.Light);
            Assert.AreEqual(1, inner.LightCount);
        }

        [Test]
        public void VolumesUseTheSettingsKeysAndDefaults()
        {
            var store = new MemorySettingsStore();
            var v = new AudioVolumes(store);
            Assert.AreEqual(0.8f, v.Music, 1e-6f);
            Assert.AreEqual(1f, v.Sfx, 1e-6f);
            Assert.AreEqual(1f, v.Ambience, 1e-6f);
            store.SetFloat("jb.settings.music", 0.25f);
            store.SetFloat("jb.settings.sfx", 2f);
            v.Reload();
            Assert.AreEqual(0.25f, v.Linear(AudioBus.Music), 1e-6f);
            Assert.AreEqual(1f, v.Linear(AudioBus.Ui), 1e-6f, "clamped; UI follows SFX");
            v.SetAmbience(0.5f);
            Assert.AreEqual(0.5f, new AudioVolumes(store).Ambience, 1e-6f, "persisted");
            Assert.AreEqual(AudioVolumes.MinDb, AudioVolumes.ToDecibels(0f));
            Assert.AreEqual(0f, AudioVolumes.ToDecibels(1f), 1e-4f);
            Assert.AreEqual(-12.04f, AudioVolumes.ToDecibels(0.5f), 0.01f, "squared taper");
        }

        [Test]
        public void CoinStreakClimbsToAFifthAndReportsLongStreaks()
        {
            var c = new CoinStreak();
            float[] expected = { 0f, 2f, 4f, 5f, 7f, 7f, 7f };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], c.OnCoin(i * 0.2), 1e-6f);
            }

            Assert.IsFalse(c.Update(1.3));
            Assert.IsTrue(c.Update(2.0), "streak of 7 ended");
            Assert.IsFalse(c.Update(3.0));
            Assert.AreEqual(0f, c.OnCoin(5.0), "a pause resets the pitch");
            Assert.IsFalse(c.Update(6.0), "a single coin isn't a streak");
        }

        [Test]
        public void MusicLevelsFollowTheTable()
        {
            var setup = new MusicSetup();
            var mix = new MusicMix(setup) { LoopStart = 10.0 };
            Assert.AreEqual(1, mix.Level);
            Assert.AreEqual(setup.Volume(1, 0), mix.Layer(0), 1e-6f);
            mix.SetLevel(2, 10.0);
            for (int i = 0; i < 200; i++)
            {
                mix.Update(10.0 + (i * 0.02), 0.02f);
            }

            for (int l = 0; l < MusicSetup.LayerCount; l++)
            {
                Assert.AreEqual(setup.Volume(2, l), mix.Layer(l), 1e-4f);
            }

            mix.SetLevel(9, 20.0);
            Assert.AreEqual(3, mix.Level, "clamped");
        }

        [Test]
        public void DangerSwitchWaitsForTheBarLine()
        {
            var setup = new MusicSetup { BarSeconds = 2f };
            var mix = new MusicMix(setup) { LoopStart = 100.0 };
            Assert.AreEqual(104.0, mix.NextBar(102.5), 1e-9);
            Assert.AreEqual(102.0, mix.NextBar(102.0), 1e-9);
            Assert.AreEqual(100.0, mix.NextBar(50.0), 1e-9);
            mix.SetLevel(3, 102.5);
            mix.Update(103.0, 0.5f);
            Assert.AreEqual(setup.Volume(1, 0), mix.Layer(0), 1e-6f, "melody holds until the bar line");
            Assert.AreEqual(0f, mix.Layer(2), 1e-6f, "danger hasn't started");
            mix.Update(104.0, 0.25f);
            Assert.Greater(mix.Layer(2), 0f);
            mix.Update(104.3, 0.3f);
            Assert.AreEqual(setup.Volume(3, 2), mix.Layer(2), 1e-4f, "section fade completes in SectionFade");
            Assert.AreEqual(0f, mix.Layer(0), 1e-4f);
        }

        [Test]
        public void SoftStopAndDuck()
        {
            var setup = new MusicSetup { StopFade = 1f, StingDuck = 0.4f, StingDuckFade = 0.1f };
            var mix = new MusicMix(setup);
            mix.Duck(true);
            mix.Update(0, 0.2f);
            Assert.AreEqual(0.4f, mix.Master, 1e-5f);
            mix.Duck(false);
            mix.SoftStop();
            mix.Update(0, 0.5f);
            Assert.IsFalse(mix.Stopped);
            mix.Update(0, 0.6f);
            Assert.IsTrue(mix.Stopped);
            mix.Snap(1);
            Assert.AreEqual(1f, mix.Master, 1e-6f, "a new run restores the music");
        }

        [Test]
        public void VariationPickerIsUniformEnough()
        {
            var p = new VariationPicker(42);
            var counts = new int[4];
            for (int i = 0; i < 4000; i++)
            {
                counts[p.Pick(4, -1)]++;
                float f = p.Next01();
                Assert.That(f, Is.InRange(0f, 0.99999994f));
            }

            foreach (int c in counts)
            {
                Assert.That(c, Is.InRange(850, 1150));
            }
        }
    }
}
