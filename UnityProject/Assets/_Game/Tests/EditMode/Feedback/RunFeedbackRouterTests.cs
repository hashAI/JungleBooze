using System.Collections.Generic;
using JungleBooze.Core.Feedback;
using JungleBooze.Gameplay.Feedback;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Feedback
{
    /// <summary>Haptics and audio hooks from simulation events (spec 101 §2.3, §2.4).</summary>
    public sealed class RunFeedbackRouterTests
    {
        private sealed class Listener : IRunFeedbackListener
        {
            public readonly List<string> Calls = new List<string>();

            public void OnEdgeBrush(int side, bool started) => Calls.Add("brush " + side + (started ? " start" : string.Empty));

            public void OnJump() => Calls.Add("jump");

            public void OnLand(LandingKind kind, float fallHeight) => Calls.Add("land " + kind);

            public void OnSlide() => Calls.Add("slide");

            public void OnDodge(int direction) => Calls.Add("dodge " + direction);

            public void OnHit(HitKind kind) => Calls.Add("hit " + kind);

            public void OnDied(DeathCause cause) => Calls.Add("died " + cause);
        }

        private static RunEvent Ev(RunEventType type, long tick = 0, byte reason = 0, float value = 0f)
        {
            return new RunEvent(type, tick, -1, reason, value);
        }

        [Test]
        public void LandingsVibrateLightOrMediumByHeight()
        {
            var h = new NullHaptics();
            var r = new RunFeedbackRouter(h, null, 1f);
            r.OnRunEvent(Ev(RunEventType.Land, 0, (byte)LandingKind.Soft, 1.41f));
            r.OnRunEvent(Ev(RunEventType.Land, 0, (byte)LandingKind.Light, 0.3f));
            r.OnRunEvent(Ev(RunEventType.Land, 0, (byte)LandingKind.Hard, 3f));
            Assert.AreEqual(1, h.LightCount, "normal jump landing (1.41 m) is light; a 0.3 m step is nothing");
            Assert.AreEqual(1, h.MediumCount, "hard landing is medium");
        }

        [Test]
        public void HitsVibrateMediumAndCrashHeavy()
        {
            var h = new NullHaptics();
            var r = new RunFeedbackRouter(h, null, 1f);
            r.OnRunEvent(Ev(RunEventType.Hit, 0, (byte)HitKind.Trip));
            r.OnRunEvent(Ev(RunEventType.Hit, 0, (byte)HitKind.Crash));
            Assert.AreEqual(1, h.MediumCount);
            Assert.AreEqual(1, h.HeavyCount);
        }

        [Test]
        public void EdgeBrushNeverVibratesAndIsThrottled()
        {
            var h = new NullHaptics();
            var l = new Listener();
            var r = new RunFeedbackRouter(h, l, 1f);
            for (long tick = 0; tick < 30; tick++)
            {
                r.OnRunEvent(Ev(RunEventType.EdgeBrush, tick, 1));
            }

            Assert.AreEqual(0, h.LightCount + h.MediumCount + h.HeavyCount);
            Assert.AreEqual("brush 1 start", l.Calls[0]);
            Assert.AreEqual(4, l.Calls.Count, "ticks 0, 9, 18, 27");

            r.OnRunEvent(Ev(RunEventType.EdgeBrush, 40, 0));
            Assert.AreEqual("brush -1 start", l.Calls[4]);
        }

        [Test]
        public void HapticsSettingMutesEverything()
        {
            var h = new NullHaptics();
            var r = new RunFeedbackRouter(h, null, 1f) { HapticsEnabled = false };
            r.OnRunEvent(Ev(RunEventType.Hit, 0, (byte)HitKind.Crash));
            r.OnRunEvent(Ev(RunEventType.Land, 0, (byte)LandingKind.Hard, 3f));
            Assert.AreEqual(0, h.LightCount + h.MediumCount + h.HeavyCount);
        }

        [Test]
        public void AudioHooksFire()
        {
            var l = new Listener();
            var r = new RunFeedbackRouter(null, l, 1f);
            r.OnRunEvent(Ev(RunEventType.Jump));
            r.OnRunEvent(Ev(RunEventType.SlideStart));
            r.OnRunEvent(Ev(RunEventType.Dodge, 0, 1));
            r.OnRunEvent(Ev(RunEventType.Died, 0, (byte)DeathCause.Fall));
            CollectionAssert.AreEqual(new[] { "jump", "slide", "dodge 1", "died Fall" }, l.Calls);
        }
    }
}
