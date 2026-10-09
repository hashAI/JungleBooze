using System;
using System.Collections.Generic;
using JungleBooze.Services.Audio;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Audio
{
    /// <summary>One-shot rules: cooldown, per-cue voice limit, stealing by priority, no-repeat variations, jitter bounds.</summary>
    public sealed class SfxEngineTests
    {
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _made)
            {
                UnityEngine.Object.DestroyImmediate(o);
            }

            _made.Clear();
        }

        [Test]
        public void CooldownDropsRepeats()
        {
            AudioCatalog cat = Catalog(Cue("a", 1, cooldown: 0.1f));
            var voices = new FakeVoices(4);
            var e = new SfxEngine(cat, voices, 1);
            Assert.GreaterOrEqual(e.Play(0, 0.0), 0);
            Assert.AreEqual(-1, e.Play(0, 0.05), "inside the cooldown");
            Assert.GreaterOrEqual(e.Play(0, 0.11), 0);
            Assert.AreEqual(2, voices.Starts);
        }

        [Test]
        public void VoiceLimitReusesTheCuesOldestVoice()
        {
            AudioCatalog cat = Catalog(Cue("a", 1, cooldown: 0f, maxVoices: 2));
            var voices = new FakeVoices(8);
            var e = new SfxEngine(cat, voices, 1);
            int v0 = e.Play(0, 0.0);
            int v1 = e.Play(0, 0.01);
            int v2 = e.Play(0, 0.02);
            Assert.AreNotEqual(v0, v1);
            Assert.AreEqual(v0, v2, "third play takes the oldest of the cue's two voices");
            Assert.AreEqual(2, e.ActiveVoices(0));
        }

        [Test]
        public void FullPoolStealsLowestPriorityNeverHigher()
        {
            AudioCatalog cat = Catalog(Cue("low", 1, priority: 50, maxVoices: 4), Cue("high", 1, priority: 200, maxVoices: 4), Cue("mid", 1, priority: 100));
            var voices = new FakeVoices(2);
            var e = new SfxEngine(cat, voices, 1);
            int low = e.Play(0, 0.0);
            int high = e.Play(1, 0.0);
            Assert.AreEqual(low, e.Play(2, 0.1), "mid steals the low-priority voice");
            Assert.AreEqual(-1, e.Play(0, 0.2), "low can't steal mid or high");
            Assert.IsTrue(voices.Playing[high]);
        }

        [Test]
        public void VariationsNeverRepeatBackToBack()
        {
            AudioCatalog cat = Catalog(Cue("a", 3, cooldown: 0f, maxVoices: 8));
            var voices = new FakeVoices(8);
            var e = new SfxEngine(cat, voices, 7);
            int last = -1;
            var seen = new HashSet<int>();
            for (int i = 0; i < 200; i++)
            {
                int v = e.Play(0, i);
                voices.Finish(v);
                int clip = e.LastClip(0);
                Assert.AreNotEqual(last, clip);
                seen.Add(clip);
                last = clip;
            }

            Assert.AreEqual(3, seen.Count, "all variations get used");
        }

        [Test]
        public void JitterStaysInsideItsBoundsAndVolumeIsClamped()
        {
            AudioCue c = Cue("a", 2, cooldown: 0f, maxVoices: 8);
            c.PitchJitter = 1f;
            c.VolumeJitterDb = 2f;
            c.Volume = 0.5f;
            AudioCatalog cat = Catalog(c);
            var voices = new FakeVoices(8);
            var e = new SfxEngine(cat, voices, 3) { BusGain = b => 0.5f };
            float lo = (float)Math.Pow(2, -1.0 / 12) - 1e-4f;
            float hi = (float)Math.Pow(2, 1.0 / 12) + 1e-4f;
            for (int i = 0; i < 100; i++)
            {
                int v = e.Play(0, i);
                Assert.That(voices.Pitch[v], Is.InRange(lo, hi));
                float expected = 0.5f * 0.5f;
                Assert.That(voices.Volume[v], Is.InRange(expected * 0.79f, expected * 1.26f));
                voices.Finish(v);
            }

            int loud = e.Play(0, 1000, 12f, 10f);
            Assert.LessOrEqual(voices.Volume[loud], 1f);
            Assert.That(voices.Pitch[loud], Is.InRange(1.8f, 2.2f), "+12 semitones ≈ ×2");
        }

        [Test]
        public void UnknownCuesAndEmptyCuesAreIgnored()
        {
            AudioCatalog cat = Catalog(Cue("empty", 0));
            var e = new SfxEngine(cat, new FakeVoices(2), 1);
            Assert.AreEqual(-1, e.Play(-1, 0));
            Assert.AreEqual(-1, e.Play(5, 0));
            Assert.AreEqual(-1, e.Play(0, 0));
        }

        private AudioCue Cue(string id, int clips, float cooldown = 0f, int maxVoices = 2, int priority = 128)
        {
            var c = new AudioCue { Id = id, Cooldown = cooldown, MaxVoices = maxVoices, Priority = priority, PitchJitter = 0f, VolumeJitterDb = 0f, Clips = new AudioClip[clips] };
            for (int i = 0; i < clips; i++)
            {
                c.Clips[i] = AudioClip.Create(id + i, 441, 1, 44100, false);
                _made.Add(c.Clips[i]);
            }

            return c;
        }

        private AudioCatalog Catalog(params AudioCue[] cues)
        {
            var cat = ScriptableObject.CreateInstance<AudioCatalog>();
            cat.Cues = new List<AudioCue>(cues);
            _made.Add(cat);
            return cat;
        }
    }
}
