using JungleBooze.Gameplay.Animation;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Animation
{
    /// <summary>
    /// Animator mapping for the rigged Pista (presentation): every simulation event that changes the pose starts its
    /// state on the same frame (no added latency), states return to the run, lean signs and limits, foot anchor.
    /// </summary>
    public sealed class RunnerAnimationModelTests
    {
        private const float Dt = 1f / 60f;
        private const float Airtime = 0.6f;

        private static RunnerAnimationConfig Cfg()
        {
            return new RunnerAnimationConfig();
        }

        private static RunnerVisualState Running(float speed = 12f)
        {
            return new RunnerVisualState { Position = Vector3.zero, Facing = Quaternion.identity, Speed = speed, Grounded = true, VLatMax = 11f };
        }

        private static RunnerAnimationModel RunningModel(RunnerAnimationConfig c = null)
        {
            var m = new RunnerAnimationModel(c ?? Cfg(), Airtime);
            RunnerVisualState s = Running();
            for (int i = 0; i < 30; i++)
            {
                m.Update(s, Dt, 0.5f);
            }

            Assert.AreEqual(RunnerAnimState.Locomotion, m.State);
            return m;
        }

        private static RunEvent Ev(RunEventType type, byte reason = 0, float value = 0f)
        {
            return new RunEvent(type, 0, -1, reason, value);
        }

        [Test]
        public void StartsIdleAndRunsOnceMoving()
        {
            var m = new RunnerAnimationModel(Cfg(), Airtime);
            RunnerVisualState s = Running(0f);
            m.Update(s, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Idle, m.State);

            s.Speed = 1f;
            m.Update(s, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Locomotion, m.State);
            Assert.IsTrue(m.Output.Changed);
        }

        [Test]
        public void JumpStartsOnTheEventFrameAtTakeoffAndSpansTheSimAirtime()
        {
            RunnerAnimationConfig c = Cfg();
            RunnerAnimationModel m = RunningModel(c);
            m.OnRunEvent(Ev(RunEventType.Jump));
            RunnerVisualState s = Running();
            s.Grounded = false;
            s.Vy = 9f;
            m.Update(s, Dt, 0.5f);

            Assert.AreEqual(RunnerAnimState.Jump, m.State);
            Assert.IsTrue(m.Output.Changed);
            Assert.AreEqual(c.JumpTakeoffFrame / 30f / c.JumpClipLength, m.Output.StartNormalized, 1e-5f);
            float clipAir = (c.JumpTouchdownFrame - c.JumpTakeoffFrame) / 30f;
            Assert.AreEqual(clipAir, m.Output.StateRate * Airtime, 1e-4f, "touch-down frame lines up with the simulation's landing");
            Assert.LessOrEqual(m.Output.Fade, 0.05f, "snappy");
        }

        [Test]
        public void LandingReturnsToTheRunOnTheLandingFrame()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.Jump));
            RunnerVisualState air = Running();
            air.Grounded = false;
            for (int i = 0; i < 36; i++)
            {
                m.Update(air, Dt, -1f);
            }

            m.OnRunEvent(Ev(RunEventType.Land, (byte)LandingKind.Soft, 1.41f));
            m.Update(Running(), Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Locomotion, m.State);
            Assert.IsTrue(m.Output.Changed);
            Assert.AreEqual(m.Config.LandingRunPhase, m.Output.StartNormalized, 1e-6f);
        }

        [Test]
        public void HardLandingAbsorbsThenRuns()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.Land, (byte)LandingKind.Hard, 3f));
            m.Update(Running(), Dt, -1f);
            Assert.AreEqual(RunnerAnimState.LandHard, m.State);
            int frames = Mathf.CeilToInt(m.Config.LandHardTime / Dt) + 1;
            for (int i = 0; i < frames; i++)
            {
                m.Update(Running(), Dt, -1f);
            }

            Assert.AreEqual(RunnerAnimState.Locomotion, m.State);
        }

        [Test]
        public void LongAirSwitchesFromJumpToFall()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.Jump));
            RunnerVisualState air = Running();
            air.Grounded = false;
            for (int i = 0; i < 60; i++)
            {
                m.Update(air, Dt, -1f);
            }

            Assert.AreEqual(RunnerAnimState.Fall, m.State);
        }

        [Test]
        public void FastFallGoesToFallImmediately()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.Jump));
            RunnerVisualState air = Running();
            air.Grounded = false;
            m.Update(air, Dt, -1f);
            air.FastFalling = true;
            m.Update(air, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Fall, m.State);
        }

        [Test]
        public void WalkOffFallsOnlyAfterTheDelay()
        {
            RunnerAnimationModel m = RunningModel();
            RunnerVisualState air = Running();
            air.Grounded = false;
            m.Update(air, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Locomotion, m.State, "a small step-down keeps running");
            int frames = Mathf.CeilToInt(m.Config.WalkOffFallDelay / Dt) + 1;
            for (int i = 0; i < frames; i++)
            {
                m.Update(air, Dt, -1f);
            }

            Assert.AreEqual(RunnerAnimState.Fall, m.State);
        }

        [Test]
        public void SlideStartsOnTheEventFrameAndHoldsWhileExtended()
        {
            RunnerAnimationConfig c = Cfg();
            RunnerAnimationModel m = RunningModel(c);
            m.OnRunEvent(Ev(RunEventType.SlideStart));
            RunnerVisualState slide = Running();
            slide.Sliding = true;
            m.Update(slide, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Slide, m.State);
            Assert.IsTrue(m.Output.Changed);
            Assert.AreEqual((c.SlideLowFrame - c.SlideStartFrame) / 30f / c.SlideDropTime, m.Output.StateRate, 1e-4f, "drops in SlideDropTime");

            // Ceiling guard keeps the slide going well past its normal length: the low pose holds (rate 0).
            for (int i = 0; i < 120; i++)
            {
                m.Update(slide, Dt, -1f);
            }

            Assert.AreEqual(RunnerAnimState.Slide, m.State);
            Assert.AreEqual(0f, m.Output.StateRate);

            m.Update(Running(), Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Locomotion, m.State);
        }

        [Test]
        public void SlideToJumpCancelPlaysTheJump()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.SlideStart));
            RunnerVisualState slide = Running();
            slide.Sliding = true;
            m.Update(slide, Dt, -1f);
            m.OnRunEvent(Ev(RunEventType.SlideEnd));
            m.OnRunEvent(Ev(RunEventType.Jump));
            RunnerVisualState air = Running();
            air.Grounded = false;
            m.Update(air, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Jump, m.State);
        }

        [Test]
        public void MinorHitStumblesThenRecovers()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.Hit, (byte)HitKind.Trip));
            m.Update(Running(), Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Stumble, m.State);
            int frames = Mathf.CeilToInt(m.Config.StumbleTime / Dt) + 1;
            for (int i = 0; i < frames; i++)
            {
                m.Update(Running(), Dt, -1f);
            }

            Assert.AreEqual(RunnerAnimState.Locomotion, m.State);
        }

        [Test]
        public void DeathPicksTheClipByCause()
        {
            RunnerAnimationModel crash = RunningModel();
            RunnerVisualState dead = Running(0f);
            dead.Dead = true;
            dead.Cause = DeathCause.Crash;
            crash.OnRunEvent(Ev(RunEventType.Hit, (byte)HitKind.Crash));
            crash.Update(dead, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Death, crash.State);

            RunnerAnimationModel fall = RunningModel();
            dead.Cause = DeathCause.Fall;
            dead.Grounded = false;
            fall.Update(dead, Dt, -1f);
            Assert.AreEqual(RunnerAnimState.Fall, fall.State);

            // Death holds: no restart every frame.
            RunnerVisualState still = Running(0f);
            still.Dead = true;
            still.Cause = DeathCause.Crash;
            crash.Update(still, Dt, -1f);
            Assert.IsFalse(crash.Output.Changed);
        }

        [Test]
        public void LeansAndYawsTowardTheSteeringWithinLimits()
        {
            RunnerAnimationModel m = RunningModel();
            RunnerVisualState s = Running(10f);
            s.VLat = 11f;
            for (int i = 0; i < 60; i++)
            {
                m.Update(s, Dt, -1f);
            }

            Assert.Greater(m.Output.YawDeg, 5f);
            Assert.LessOrEqual(m.Output.YawDeg, m.Config.MaxYawDeg + 1e-3f);
            Assert.AreEqual(m.Config.RollAtMaxLateral, m.Output.RollDeg, 0.05f);

            s.VLat = -11f;
            for (int i = 0; i < 60; i++)
            {
                m.Update(s, Dt, -1f);
            }

            Assert.Less(m.Output.YawDeg, -5f);
            Assert.Less(m.Output.RollDeg, 0f);
        }

        [Test]
        public void LeanRespondsWithinAFewFrames()
        {
            RunnerAnimationModel m = RunningModel();
            RunnerVisualState s = Running(10f);
            s.VLat = 11f;
            for (int i = 0; i < 3; i++)
            {
                m.Update(s, Dt, -1f);
            }

            Assert.Greater(m.Output.RollDeg, 0.5f * m.Config.RollAtMaxLateral, "half the lean within 3 frames (50 ms)");
        }

        [Test]
        public void DodgeAndEdgeBrushAddBank()
        {
            RunnerAnimationModel m = RunningModel();
            m.OnRunEvent(Ev(RunEventType.Dodge, 1));
            for (int i = 0; i < 4; i++)
            {
                m.Update(Running(), Dt, -1f);
            }

            Assert.Greater(m.Output.RollDeg, 1f, "dodge right banks right");

            RunnerAnimationModel e = RunningModel();
            for (int i = 0; i < 6; i++)
            {
                e.OnRunEvent(Ev(RunEventType.EdgeBrush, 1));
                e.Update(Running(), Dt, -1f);
            }

            Assert.Less(e.Output.RollDeg, -0.5f, "brushing the right edge leans away (left)");
        }

        [Test]
        public void FootAnchorWeightFollowsAirborne()
        {
            RunnerAnimationModel m = RunningModel();
            RunnerVisualState air = Running();
            air.Grounded = false;
            for (int i = 0; i < 12; i++)
            {
                m.Update(air, Dt, -1f);
            }

            Assert.Greater(m.Output.AnchorWeight, 0.95f);
            for (int i = 0; i < 12; i++)
            {
                m.Update(Running(), Dt, -1f);
            }

            Assert.Less(m.Output.AnchorWeight, 0.05f);
        }

        [Test]
        public void ResetReturnsToIdle()
        {
            RunnerAnimationModel m = RunningModel();
            m.Reset();
            Assert.AreEqual(RunnerAnimState.Idle, m.State);
            Assert.AreEqual(0f, m.Output.YawDeg);
        }
    }
}
