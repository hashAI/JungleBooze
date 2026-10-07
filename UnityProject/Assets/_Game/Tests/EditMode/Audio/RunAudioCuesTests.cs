using System;
using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Services.Audio;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Audio
{
    public sealed class RunAudioCuesTests
    {
        [Test]
        public void EventNumbers_MatchRunnerEventType()
        {
            Assert.AreEqual((byte)RunnerEventType.JumpStarted, RunAudioCues.JumpStarted);
            Assert.AreEqual((byte)RunnerEventType.SlideStarted, RunAudioCues.SlideStarted);
            Assert.AreEqual((byte)RunnerEventType.LaneChangeStarted, RunAudioCues.LaneChangeStarted);
            Assert.AreEqual((byte)RunnerEventType.CoinCollected, RunAudioCues.CoinCollected);
            Assert.AreEqual((byte)RunnerEventType.Stumbled, RunAudioCues.Stumbled);
            Assert.AreEqual((byte)RunnerEventType.NearMiss, RunAudioCues.NearMiss);
            Assert.AreEqual((byte)RunnerEventType.Died, RunAudioCues.Died);
            Assert.AreEqual((byte)RunnerEventType.VineGrabbed, RunAudioCues.VineGrabbed);
            Assert.AreEqual((byte)RunnerEventType.PowerUpCollected, RunAudioCues.PowerUpCollected);
            Assert.AreEqual((byte)RunnerEventType.ShieldAbsorbed, RunAudioCues.ShieldAbsorbed);
            Assert.AreEqual((byte)RunnerEventType.CompanionCallout, RunAudioCues.CompanionCallout);
            Assert.AreEqual((short)CompanionCalloutId.Vine, RunAudioCues.CallVine);
            Assert.AreEqual((short)CompanionCalloutId.Danger, RunAudioCues.CallDanger);
            Assert.AreEqual((short)CompanionCalloutId.Cheer1, RunAudioCues.CallCheer1);
            Assert.AreEqual((short)PowerUpType.Magnet, RunAudioCues.PowerMagnet);
            Assert.AreEqual((short)PowerUpType.Shield, RunAudioCues.PowerShield);
            Assert.AreEqual((short)PowerUpType.SpeedBoost, RunAudioCues.PowerSpeedBoost);
        }

        [Test]
        public void Actions_MapToClips()
        {
            Expect(RunAudioCues.JumpStarted, 0, 0, 0, 1f, AudioClipId.Jump, false);
            Expect(RunAudioCues.SlideStarted, 0, 0, 0, 1f, AudioClipId.Slide, false);
            Expect(RunAudioCues.LaneChangeStarted, 0, 0, 0, 1f, AudioClipId.LaneSwitch, false);
            Expect(RunAudioCues.Stumbled, 0, 0, 0, 1f, AudioClipId.Stumble, false);
            Expect(RunAudioCues.NearMiss, 0, 0, 0, 1f, AudioClipId.NearMiss, false);
            Expect(RunAudioCues.Died, 0, 0, 0, 1f, AudioClipId.Death, false);
            Expect(RunAudioCues.VineGrabbed, 0, 0, 0, 1f, AudioClipId.VineGrab, false);
            Expect(RunAudioCues.PowerUpCollected, RunAudioCues.PowerMagnet, 0, 0, 1f, AudioClipId.PowerUp, false);
            Expect(RunAudioCues.PowerUpCollected, RunAudioCues.PowerShield, 0, 0, 1f, AudioClipId.PowerUp, false);
            Expect(RunAudioCues.PowerUpCollected, RunAudioCues.PowerSpeedBoost, 0, 0, 1f, AudioClipId.SpeedBoost, false);
            Expect(RunAudioCues.ShieldAbsorbed, 0, 0, 0, 1f, AudioClipId.ShieldPop, false);
        }

        [Test]
        public void Coin_VariesPitchByEntity_AndRepeatsForTheSameId()
        {
            Assert.IsTrue(RunAudioCues.TryGet(RunAudioCues.CoinCollected, 1, 0, 3, 1f, out AudioCue a));
            Assert.IsTrue(RunAudioCues.TryGet(RunAudioCues.CoinCollected, 1, 0, 3, 1f, out AudioCue again));
            Assert.IsTrue(RunAudioCues.TryGet(RunAudioCues.CoinCollected, 1, 0, 4, 1f, out AudioCue other));
            Assert.AreEqual(AudioClipId.Coin, a.Clip);
            Assert.AreEqual(a.Pitch, again.Pitch);
            Assert.AreEqual(a.Gain, again.Gain);
            Assert.AreNotEqual(a.Pitch, other.Pitch);
            Assert.IsFalse(a.Spoken);
        }

        [Test]
        public void Callouts_UseWordsUntilVoiceIsZero()
        {
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallVine, 0, 0, 1f, AudioClipId.CallVine, true);
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallDanger, 0, 0, 1f, AudioClipId.CallDanger, true);
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallCheer1, 0, 0, 1f, AudioClipId.CallCheerShiny, true);
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallCheer2, 0, 0, 1f, AudioClipId.CallCheerWow, true);
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallCheer3, 0, 0, 1f, AudioClipId.CallCheerWoohoo, true);

            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallVine, 0, 0, 0f, AudioClipId.SquawkVine, false);
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallDanger, 0, 0, 0f, AudioClipId.SquawkDanger, false);
            Expect(RunAudioCues.CompanionCallout, RunAudioCues.CallCheer1, 0, 0, 0f, AudioClipId.SquawkCheer, false);
        }

        [Test]
        public void UnmappedEvents_AreSilent()
        {
            Assert.IsFalse(RunAudioCues.TryGet((byte)RunnerEventType.Landed, 0, 0, 0, 1f, out _));
            Assert.IsFalse(RunAudioCues.TryGet((byte)RunnerEventType.PowerUpEnded, 0, 0, 0, 1f, out _));
            Assert.IsFalse(RunAudioCues.TryGet(RunAudioCues.CompanionCallout, 0, 0, 0, 1f, out _));
        }

        [Test]
        public void TryGet_DoesNotAllocate()
        {
            RunAudioCues.TryGet(RunAudioCues.CoinCollected, 1, 0, 9, 1f, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                RunAudioCues.TryGet(RunAudioCues.CoinCollected, 1, 0, i, 1f, out _);
                RunAudioCues.TryGet(RunAudioCues.CompanionCallout, RunAudioCues.CallVine, 0, 0, 0f, out _);
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static void Expect(byte type, short value, byte flags, int entityId, float voice, AudioClipId clip, bool spoken)
        {
            Assert.IsTrue(RunAudioCues.TryGet(type, value, flags, entityId, voice, out AudioCue cue));
            Assert.AreEqual(clip, cue.Clip);
            Assert.AreEqual(spoken, cue.Spoken);
        }
    }
}
