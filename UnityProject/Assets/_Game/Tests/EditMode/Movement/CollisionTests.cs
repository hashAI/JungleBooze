using JungleBooze.Core;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 §4: AC-101-23 … AC-101-31.</summary>
    public sealed class CollisionTests
    {
        private static SimRig RunInto(System.Action<CourseData> build, float startX = 0f, bool firstRun = false, float speed = 10f)
        {
            SimRig rig = SimRig.Flat(speed, build, 4f, startX, null, firstRun);
            rig.RunTo(40f);
            return rig;
        }

        [Test]
        public void AC23_LowIsTrip()
        {
            SimRig rig = RunInto(c => SimRig.Low(c, 20f, 20.5f));
            Assert.AreEqual(2, rig.State.Health);
            Assert.AreEqual(HitKind.Trip, rig.State.LastHitKind);
            Assert.AreEqual((byte)HitKind.Trip, rig.LastEvent(RunEventType.Hit).Reason);
            Assert.AreEqual(0, rig.LastEvent(RunEventType.Hit).Id, "the hit names its obstacle");
        }

        [Test]
        public void AC23_HighIsHeadClip()
        {
            SimRig rig = RunInto(c => SimRig.High(c, 20f, 20.6f));
            Assert.AreEqual(2, rig.State.Health);
            Assert.AreEqual(HitKind.HeadClip, rig.State.LastHitKind);
        }

        [Test]
        public void AC23_ThornsCostOneSegment()
        {
            SimRig rig = RunInto(c => SimRig.Thorns(c, 20f, 22f));
            Assert.AreEqual(2, rig.State.Health);
            Assert.AreEqual(HitKind.Thorns, rig.State.LastHitKind);
        }

        [Test]
        public void AC23_BlockerFrontalIsCrash()
        {
            SimRig rig = RunInto(c => SimRig.Blocker(c, 20f, 21.2f, -0.6f, 0.6f));
            Assert.IsTrue(rig.State.Dead);
            Assert.AreEqual(DeathCause.Crash, rig.State.Cause);
            Assert.AreEqual(0, rig.State.DeathObstacle);
        }

        [Test]
        public void AC23_GapIsFall()
        {
            SimRig rig = RunInto(c => SimRig.Gap(c, 20f, 23f));
            Assert.IsTrue(rig.State.Dead);
            Assert.AreEqual(DeathCause.Fall, rig.State.Cause);
        }

        [Test]
        public void AC23_HitsAreAvoidable()
        {
            SimRig low = SimRig.Flat(10f, c => SimRig.Low(c, 20f, 20.5f));
            low.RunTo(18.3f);
            low.Step(InputCommand.Jump);
            low.RunTo(30f);
            Assert.AreEqual(0, low.State.Hits, "jump clears a 0.6 m root");

            SimRig steer = SimRig.Flat(10f, c => SimRig.Blocker(c, 20f, 21.2f, -0.6f, 0.6f), 4f);
            steer.Step(new InputFrame(InputCommand.None, 1500));
            steer.RunTo(30f);
            Assert.AreEqual(0, steer.State.Hits, "steering clears a blocker");
        }

        [TestCase(0.0f, HitKind.Crash)]
        [TestCase(0.15f, HitKind.Crash)]
        [TestCase(0.29f, HitKind.Crash)]
        [TestCase(-0.29f, HitKind.Crash)]
        [TestCase(0.31f, HitKind.SideClip)]
        [TestCase(0.5f, HitKind.SideClip)]
        [TestCase(0.74f, HitKind.SideClip)]
        [TestCase(-0.74f, HitKind.SideClip)]
        [TestCase(0.76f, HitKind.None)]
        [TestCase(-0.76f, HitKind.None)]
        [TestCase(1.2f, HitKind.None)]
        public void AC24_BlockerCrashCore(float offset, HitKind expected)
        {
            SimRig rig = RunInto(c => SimRig.Blocker(c, 20f, 21.2f, -0.6f, 0.6f), offset);
            if (expected == HitKind.None)
            {
                Assert.AreEqual(0, rig.State.Hits);
                Assert.IsFalse(rig.State.Dead);
                return;
            }

            Assert.AreEqual(expected, rig.State.LastHitKind);
            if (expected == HitKind.Crash)
            {
                Assert.IsTrue(rig.State.Dead);
            }
            else
            {
                Assert.IsFalse(rig.State.Dead);
                Assert.AreEqual(2, rig.State.Health);
                float free = offset > 0f ? 0.6f + 0.3f : -0.6f - 0.3f;
                Assert.AreEqual(free, rig.State.XTarget, 1e-4f, "xT moved to the free side");
            }
        }

        [Test]
        public void AC25_LandingOnAWalkableTopRunsAcross()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Low(c, 30f, 33f, 0.9f, -10f, 10f, true));
            RunnerState st = rig.State;
            st.Tick = 50;
            st.S = 29.6f;
            st.Y = 0.75f; // effective top 0.80: within 0.15 m
            st.Vy = -2f;
            st.Grounded = false;
            st.Jumped = true;
            st.AirPeakY = 1.4f;
            rig.Sim.SetStateForTest(st);
            rig.RunTo(32f);
            Assert.AreEqual(0, rig.State.Hits);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.WalkableLanding));
            Assert.IsTrue(rig.State.Grounded);
            Assert.AreEqual(0.9f, rig.State.Y, 1e-5f, "runs across the top");
            rig.RunTo(40f);
            Assert.AreEqual(0, rig.State.Hits);
            Assert.AreEqual(0f, rig.State.Y, 1e-5f, "drops back to the path");
        }

        [Test]
        public void AC25_TooLowOnAWalkableTopIsATrip()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Low(c, 30f, 33f, 0.9f, -10f, 10f, true));
            RunnerState st = rig.State;
            st.Tick = 50;
            st.S = 29.6f;
            st.Y = 0.6f; // 0.20 m under the effective top
            st.Vy = -2f;
            st.Grounded = false;
            st.Jumped = true;
            rig.Sim.SetStateForTest(st);
            rig.RunTo(32f);
            Assert.AreEqual(1, rig.State.Hits);
            Assert.AreEqual(HitKind.Trip, rig.State.LastHitKind);
        }

        [Test]
        public void AC25_JumpOntoTheFeelCourseLogFromTheGround()
        {
            // Sweep take-off points: every take-off either clears the log, lands on it, or trips; landing on it
            // never costs health.
            int landed = 0;
            for (float takeoff = 24f; takeoff < 30f; takeoff += 0.1f)
            {
                SimRig rig = SimRig.Flat(10f, c => SimRig.Low(c, 30f, 31f, 0.9f, -10f, 10f, true));
                rig.RunTo(takeoff);
                rig.Step(InputCommand.Jump);
                rig.RunTo(40f);
                if (rig.CountEvents(RunEventType.WalkableLanding) > 0)
                {
                    landed++;
                    Assert.AreEqual(0, rig.State.Hits, "landing on top is never a hit (take-off " + takeoff + ")");
                }
            }

            Assert.Greater(landed, 0);
        }

        [Test]
        public void AC26_IFramesIgnoreObstaclesForExactly72Ticks_GapsStillKill()
        {
            SimRig rig = SimRig.Flat(10f, c =>
            {
                SimRig.Low(c, 20f, 20.5f);
                SimRig.Low(c, 30f, 30.5f); // reached ~60 ticks after the first hit: ignored
            });
            rig.RunTo(40f);
            Assert.AreEqual(1, rig.State.Hits);
            Assert.AreEqual(72, rig.Sim.InvulnerableTicks);
            Assert.IsTrue(rig.Sim.IsObstacleResolved(1), "ghosted through");

            // 72 ticks at 10 m/s = 12 m: an obstacle 13 m later is hit again.
            SimRig after = SimRig.Flat(10f, c =>
            {
                SimRig.Low(c, 20f, 20.5f);
                SimRig.Low(c, 33.5f, 34f);
            });
            after.RunTo(40f);
            Assert.AreEqual(2, after.State.Hits);

            SimRig gap = SimRig.Flat(10f, c =>
            {
                SimRig.Low(c, 20f, 20.5f);
                SimRig.Gap(c, 24f, 27f);
            });
            gap.RunTo(40f);
            Assert.IsTrue(gap.State.Dead);
            Assert.AreEqual(DeathCause.Fall, gap.State.Cause);
        }

        [Test]
        public void AC26_InvulnerableWindowLength()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Low(c, 20f, 20.5f));
            rig.StepUntil(s => s.Hits > 0);
            long hit = rig.State.Tick;
            Assert.AreEqual(hit + 72, rig.State.InvulnerableUntilTick);
            rig.StepUntil(s => s.Tick == hit + 72);
            Assert.IsTrue(rig.State.IsInvulnerable);
            rig.Steps(1);
            Assert.IsFalse(rig.State.IsInvulnerable);
        }

        [Test]
        public void AC27_AnObstacleResolvesOnce()
        {
            SimRig rig = RunInto(c => SimRig.Low(c, 15f, 35f));
            Assert.AreEqual(1, rig.State.Hits, "20 m inside one obstacle (well past the i-frames) costs one segment");
            Assert.AreEqual(2, rig.State.Health);
        }

        [Test]
        public void AC28_TwoMinorsOnOneTickCostOneSegment()
        {
            SimRig rig = RunInto(c =>
            {
                SimRig.Low(c, 20f, 20.5f, 0.6f, -2f, 0.1f);
                SimRig.High(c, 20f, 20.6f, 1.0f, -0.1f, 2f);
            });
            Assert.AreEqual(1, rig.State.Hits);
            Assert.AreEqual(2, rig.State.Health);
            Assert.IsTrue(rig.Sim.IsObstacleResolved(0));
            Assert.IsTrue(rig.Sim.IsObstacleResolved(1));
        }

        [Test]
        public void AC28_MajorBeatsMinorOnTheSameTick()
        {
            SimRig rig = RunInto(c =>
            {
                SimRig.Low(c, 20f, 20.5f);
                SimRig.Blocker(c, 20f, 21.2f, -0.6f, 0.6f);
            });
            Assert.IsTrue(rig.State.Dead);
            Assert.AreEqual(DeathCause.Crash, rig.State.Cause);
        }

        private static void ThreeRoots(CourseData c)
        {
            SimRig.Low(c, 20f, 20.5f);
            SimRig.Low(c, 40f, 40.5f);
            SimRig.Low(c, 60f, 60.5f);
        }

        [Test]
        public void AC29_ThirdMinorHitIsDeath()
        {
            SimRig rig = SimRig.Flat(10f, ThreeRoots);
            rig.RunTo(70f);
            Assert.IsTrue(rig.State.Dead);
            Assert.AreEqual(DeathCause.Health, rig.State.Cause);
            Assert.AreEqual(3, rig.State.Hits);
            Assert.AreEqual(2, rig.State.DeathObstacle);
        }

        [Test]
        public void AC29_FirstRunFloorsHealthAtOne()
        {
            SimRig rig = SimRig.Flat(10f, ThreeRoots, firstRun: true);
            rig.RunTo(70f);
            Assert.IsFalse(rig.State.Dead);
            Assert.AreEqual(1, rig.State.Health);
            Assert.AreEqual(3, rig.State.Hits);
        }

        [Test]
        public void AC29_FirstRunFloorEndsAfter60Seconds()
        {
            SimRig rig = SimRig.Flat(10f, c =>
            {
                SimRig.Low(c, 610f, 610.5f); // all reached after 60 s
                SimRig.Low(c, 630f, 630.5f);
                SimRig.Low(c, 650f, 650.5f);
            }, firstRun: true);
            rig.RunTo(660f);
            Assert.IsTrue(rig.State.Dead);
        }

        [Test]
        public void AC30_RegenAfter350mWithoutDamage_NeverAbove3()
        {
            SimRig rig = SimRig.Flat(16f, c => SimRig.Low(c, 20f, 20.5f));
            rig.RunTo(25f);
            Assert.AreEqual(2, rig.State.Health);
            float hitS = 20.5f;
            rig.RunTo(hitS + 348f);
            Assert.AreEqual(2, rig.State.Health);
            rig.RunTo(hitS + 352f);
            Assert.AreEqual(3, rig.State.Health);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.Regen));
            rig.RunTo(hitS + 1200f);
            Assert.AreEqual(3, rig.State.Health);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.Regen));
        }

        [Test]
        public void AC31_ShieldAbsorbsMinor()
        {
            SimRig rig = SimRig.Flat(12f, c => SimRig.Low(c, 20f, 20.5f));
            rig.Sim.GrantShield();
            rig.StepUntil(s => !s.Shield);
            long tick = rig.State.Tick;
            Assert.AreEqual(3, rig.State.Health);
            Assert.AreEqual(12f, rig.State.Speed, 1e-5f, "no stumble");
            Assert.AreEqual(tick + 60, rig.State.InvulnerableUntilTick, "60 ticks of i-frames");
            Assert.AreEqual(1, rig.CountEvents(RunEventType.ShieldConsumed));
            Assert.AreEqual(0, rig.CountEvents(RunEventType.Hit));
        }

        [Test]
        public void AC31_ShieldAbsorbsCrash()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Blocker(c, 20f, 21.2f, -0.6f, 0.6f));
            rig.Sim.GrantShield();
            rig.RunTo(30f);
            Assert.IsFalse(rig.State.Dead);
            Assert.AreEqual(3, rig.State.Health);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.ShieldConsumed));
        }

        [Test]
        public void AC31_ShieldDoesNotAbsorbFall()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Gap(c, 20f, 24f));
            rig.Sim.GrantShield();
            rig.RunTo(40f);
            Assert.IsTrue(rig.State.Dead);
            Assert.AreEqual(DeathCause.Fall, rig.State.Cause);
            Assert.IsTrue(rig.State.Shield, "not consumed");
        }

        [Test]
        public void AC31_ShieldNotConsumedDuringIFrames()
        {
            SimRig rig = SimRig.Flat(10f, c =>
            {
                SimRig.Low(c, 20f, 20.5f);
                SimRig.Low(c, 25f, 25.5f);
            });
            rig.RunTo(21f);
            Assert.AreEqual(1, rig.State.Hits);
            rig.Sim.GrantShield();
            rig.RunTo(30f);
            Assert.IsTrue(rig.State.Shield);
            Assert.AreEqual(2, rig.State.Health);
        }

        [Test]
        public void Revive_RestoresHealthAtSafeGroundAndClearsObstacles()
        {
            SimRig rig = SimRig.Flat(10f, c =>
            {
                SimRig.Blocker(c, 50f, 51.2f, -0.6f, 0.6f);
                SimRig.Low(c, 60f, 60.5f);
            });
            rig.StepUntil(s => s.Dead);
            rig.Steps(30);
            Assert.IsTrue(rig.State.Dead);
            Assert.IsTrue(rig.Sim.Revive());
            Assert.IsFalse(rig.State.Dead);
            Assert.AreEqual(3, rig.State.Health);
            Assert.LessOrEqual(rig.State.S, 50f - 6f + 1e-3f);
            Assert.IsTrue(rig.Sim.IsObstacleRemoved(0));
            Assert.IsTrue(rig.Sim.IsObstacleRemoved(1));
            Assert.AreEqual(rig.State.Tick + 120, rig.State.InvulnerableUntilTick);
            rig.Steps(1);
            Assert.AreEqual(5f, rig.State.Speed, 0.2f, "revive ramp starts at 0.5×");
            rig.RunTo(80f);
            Assert.IsFalse(rig.State.Dead);
            Assert.AreEqual(10f, rig.State.Speed, 1e-4f);
        }
    }
}
