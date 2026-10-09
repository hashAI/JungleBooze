using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 §2.4 and §2.7: AC-101-10 … 13, 18, 21, 22; M1.</summary>
    public sealed class JumpTests
    {
        private static void MeasureJump(float speed, out float apex, out int airTicks, out float length)
        {
            SimRig rig = SimRig.Flat(speed);
            rig.Steps(10);
            float s0 = rig.State.S;
            rig.Step(InputCommand.Jump);
            long jumpTick = rig.State.Tick;
            apex = rig.State.Y;
            for (int i = 0; i < 200 && !rig.State.Grounded; i++)
            {
                rig.Steps(1);
                apex = System.Math.Max(apex, rig.State.Y);
            }

            airTicks = (int)(rig.State.Tick - jumpTick + 1);
            length = rig.State.S - s0;
        }

        [TestCase(10f)]
        [TestCase(16f)]
        public void AC10_ApexAndAirtime(float speed)
        {
            MeasureJump(speed, out float apex, out int airTicks, out _);
            Assert.AreEqual(1.41f, apex, 0.02f, "apex");
            Assert.AreEqual(0.60f, airTicks * SimRig.Dt, 0.02f, "airtime");
        }

        [Test]
        public void AC10_ArcIdenticalAtAllSpeeds()
        {
            MeasureJump(10f, out float apex10, out int air10, out float length10);
            MeasureJump(16f, out float apex16, out int air16, out float length16);
            Assert.AreEqual(apex10, apex16);
            Assert.AreEqual(air10, air16);
            Assert.AreEqual(length10 * 1.6f, length16, 0.05f, "jump length = airtime × speed");
        }

        [Test]
        public void M1_JumpChangesStateOnTheSameTick()
        {
            SimRig rig = SimRig.Flat();
            rig.Steps(3);
            rig.Step(InputCommand.Jump);
            Assert.IsFalse(rig.State.Grounded);
            Assert.Greater(rig.State.Y, 0f);
            Assert.IsTrue(rig.HasEventAt(RunEventType.Jump, rig.State.Tick));
        }

        private static SimRig LedgeRig()
        {
            return SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, -1.0f));
        }

        [Test]
        public void AC11_CoyoteJumpWithin6Ticks()
        {
            SimRig rig = LedgeRig();
            rig.StepUntil(s => !s.Grounded);
            long airborne = rig.State.AirborneSinceTick;
            Assert.AreEqual(rig.State.Tick, airborne);
            rig.Steps(5);
            rig.Step(InputCommand.Jump);
            Assert.AreEqual(airborne + 6, rig.State.Tick);
            Assert.IsTrue(rig.State.Jumped, "6 ticks after walking off: coyote jump");
            Assert.Greater(rig.State.Vy, 8f);
        }

        [Test]
        public void AC11_At7TicksTheJumpIsBufferedNotExecuted()
        {
            SimRig rig = LedgeRig();
            rig.StepUntil(s => !s.Grounded);
            rig.Steps(6);
            rig.Step(InputCommand.Jump);
            Assert.IsFalse(rig.State.Jumped);
            Assert.AreEqual(InputCommand.Jump, rig.State.Buffered);
            Assert.AreEqual(DropReason.Buffer, rig.State.BufferedKind);
        }

        private static long LandingTickAfterJumpAt(int warmup, out SimRig rig)
        {
            rig = SimRig.Flat();
            rig.Steps(warmup);
            rig.Step(InputCommand.Jump);
            rig.StepUntil(s => s.Grounded, 200);
            return rig.State.Tick;
        }

        [Test]
        public void AC12_BufferedJump9TicksBeforeLandingExecutesOnLandingTick()
        {
            long landing = LandingTickAfterJumpAt(5, out _);
            SimRig rig = SimRig.Flat();
            rig.Steps(5);
            rig.Step(InputCommand.Jump);
            rig.StepUntil(s => s.Tick == landing - 10);
            rig.Step(InputCommand.Jump); // received on tick landing − 9
            Assert.AreEqual(landing - 9, rig.State.Tick);
            rig.StepUntil(s => s.Tick == landing);
            Assert.IsTrue(rig.HasEventAt(RunEventType.Land, landing));
            Assert.IsTrue(rig.HasEventAt(RunEventType.Jump, landing), "jump on the landing tick");
            Assert.IsTrue(rig.State.Jumped);
            Assert.AreEqual(0, rig.CountEvents(RunEventType.InputDropped));
        }

        [Test]
        public void AC12_BufferedJump10TicksBeforeLandingIsDropped()
        {
            long landing = LandingTickAfterJumpAt(5, out _);
            SimRig rig = SimRig.Flat();
            rig.Steps(5);
            rig.Step(InputCommand.Jump);
            rig.StepUntil(s => s.Tick == landing - 11);
            rig.Step(InputCommand.Jump); // received on tick landing − 10
            rig.StepUntil(s => s.Tick == landing + 1);
            Assert.IsFalse(rig.HasEventAt(RunEventType.Jump, landing));
            Assert.AreEqual(1, rig.CountEvents(RunEventType.InputDropped));
            RunEvent dropped = rig.LastEvent(RunEventType.InputDropped);
            Assert.AreEqual((byte)DropReason.Buffer, dropped.Reason);
            Assert.IsTrue(rig.State.Grounded);
        }

        [Test]
        public void AC13_NewerCommandReplacesBufferedOne()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(InputCommand.Jump);
            rig.Steps(20);
            rig.Step(InputCommand.Jump);
            long first = rig.State.BufferedTick;
            rig.Steps(2);
            rig.Step(InputCommand.Jump);
            Assert.AreEqual(rig.State.Tick, rig.State.BufferedTick, "a newer jump restarts the buffer");
            Assert.AreNotEqual(first, rig.State.BufferedTick);

            // A slide (fast-fall) replaces the buffered jump: landing slides, no jump.
            rig.Step(InputCommand.Slide);
            Assert.AreEqual(InputCommand.None, rig.State.Buffered);
            rig.StepUntil(s => s.Grounded, 200);
            Assert.IsTrue(rig.State.Sliding);
            Assert.IsFalse(rig.State.Jumped);
        }

        [Test]
        public void AC18_FastFallThenJumpBeforeLanding_JumpsOnLandingTick()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(InputCommand.Jump);
            rig.Steps(12);
            rig.Step(InputCommand.Slide);
            Assert.IsTrue(rig.State.FastFalling);
            rig.Step(InputCommand.Jump);
            Assert.IsFalse(rig.State.Grounded, "still in the air");
            rig.StepUntil(s => s.Tick > 0 && rig.Events.CountOf(RunEventType.Land) > 0, 200);
            long landing = rig.LastEvent(RunEventType.Land).Tick;
            Assert.IsTrue(rig.HasEventAt(RunEventType.SlideStart, landing), "auto-slide first");
            Assert.IsTrue(rig.HasEventAt(RunEventType.Jump, landing), "then the buffered jump");
            Assert.IsTrue(rig.State.Jumped);
            Assert.IsFalse(rig.State.Sliding);
        }

        [Test]
        public void AC21_SmallRiseWalkedUpGrounded()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, 0.35f));
            for (int i = 0; i < 300; i++)
            {
                rig.Steps(1);
                Assert.IsTrue(rig.State.Grounded, "tick " + rig.State.Tick);
            }

            Assert.AreEqual(0.35f, rig.State.Y, 1e-5f);
        }

        [Test]
        public void AC21_RiseAboveStepUpHeightIsAWallHit()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, 0.5f));
            rig.StepUntil(s => s.Y > 0f, 300);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.Hit), "one minor hit");
            Assert.AreEqual((byte)HitKind.Wall, rig.LastEvent(RunEventType.Hit).Reason);
            Assert.AreEqual(HitKind.Wall, rig.State.LastHitKind);
            Assert.AreEqual(2, rig.State.Health);
            Assert.IsTrue(rig.State.IsInvulnerable);
            Assert.IsTrue(rig.State.Grounded, "she clambers up and keeps running");
            Assert.AreEqual(0.5f, rig.State.Y, 1e-5f);
            rig.Steps(120);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.Hit));
        }

        [Test]
        public void AC21_WallDuringIFramesOrWithShieldCostsNothing()
        {
            SimRig shielded = SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, 0.5f));
            shielded.Sim.GrantShield();
            shielded.StepUntil(s => s.Y > 0f, 300);
            Assert.AreEqual(3, shielded.State.Health);
            Assert.AreEqual(1, shielded.CountEvents(RunEventType.ShieldConsumed));

            SimRig ghost = SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, 0.5f));
            RunnerState st = ghost.State;
            st.InvulnerableUntilTick = 10000;
            ghost.Sim.SetStateForTest(st);
            ghost.StepUntil(s => s.Y > 0f, 300);
            Assert.AreEqual(3, ghost.State.Health);
            Assert.AreEqual(0, ghost.CountEvents(RunEventType.Hit));
        }

        [Test]
        public void AC21_SmallDropStaysGrounded_LargeDropStartsCoyote()
        {
            SimRig small = SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, -0.35f));
            for (int i = 0; i < 300; i++)
            {
                small.Steps(1);
                Assert.IsTrue(small.State.Grounded);
            }

            SimRig large = SimRig.Flat(10f, c => SimRig.Floor(c, 20f, 500f, -0.4f));
            large.StepUntil(s => !s.Grounded, 300);
            Assert.IsFalse(large.State.Grounded);
            Assert.IsFalse(large.State.Jumped);
            Assert.AreEqual(large.State.Tick, large.State.AirborneSinceTick, "coyote window starts");
            Assert.IsTrue(large.Sim.CanJumpFromGround(large.State.Tick + 1));
        }

        private static SimRig GapRig()
        {
            return SimRig.Flat(10f, c => SimRig.Gap(c, 20f, 25f));
        }

        private static RunnerState Airborne(SimRig rig, float s, float y, float vy)
        {
            RunnerState st = rig.State;
            st.Tick = 100;
            st.S = s;
            st.Y = y;
            st.Vy = vy;
            st.Grounded = false;
            st.Jumped = true;
            st.GroundY = 0f;
            st.LastGroundY = 0f;
            st.AirPeakY = 1.4f;
            return st;
        }

        [Test]
        public void AC22_LedgeAssistSnapsOntoTheLip()
        {
            SimRig rig = GapRig();
            rig.Sim.SetStateForTest(Airborne(rig, 24.75f, -0.10f, -1.0f));
            rig.Steps(1);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.LedgeAssist));
            Assert.IsTrue(rig.State.Grounded);
            Assert.AreEqual(0f, rig.State.Y, 1e-5f);
            Assert.GreaterOrEqual(rig.State.S, 25f);
        }

        [Test]
        public void AC22_TooLowFallsAndDiesAt120BelowTheLip()
        {
            SimRig rig = GapRig();
            rig.Sim.SetStateForTest(Airborne(rig, 24.75f, -0.30f, -1.0f));
            float lastY = rig.State.Y;
            for (int i = 0; i < 120 && !rig.State.Dead; i++)
            {
                lastY = rig.State.Y;
                rig.Steps(1);
            }

            Assert.IsTrue(rig.State.Dead);
            Assert.AreEqual(DeathCause.Fall, rig.State.Cause);
            Assert.AreEqual(0, rig.CountEvents(RunEventType.LedgeAssist));
            Assert.Less(rig.State.Y, -1.2f);
            Assert.GreaterOrEqual(lastY, -1.2f, "dies on the first tick below 1.20 m");
        }

        [Test]
        public void AC22_LipOutOfReachIsNotSnappedYet()
        {
            SimRig rig = GapRig();
            rig.Sim.SetStateForTest(Airborne(rig, 24.5f, -0.05f, -1.0f));
            rig.Steps(1);
            Assert.AreEqual(0, rig.CountEvents(RunEventType.LedgeAssist), "0.5 m short is beyond the 0.30 m reach");
            Assert.IsFalse(rig.State.Grounded);
        }

        [Test]
        public void Gap_WalkingInWithoutJumpingIsAFall()
        {
            SimRig rig = GapRig();
            rig.StepUntil(s => s.Dead, 600);
            Assert.AreEqual(DeathCause.Fall, rig.State.Cause);
            Assert.AreEqual(-1, rig.State.DeathObstacle);
        }

        [Test]
        public void Gap_JumpClearsIt()
        {
            SimRig rig = GapRig();
            rig.RunTo(19.4f); // 6 m jump at 10 m/s over the 5 m gap
            rig.Step(InputCommand.Jump);
            rig.RunTo(40f);
            Assert.IsFalse(rig.State.Dead);
            Assert.IsTrue(rig.State.Grounded);
        }
    }
}
