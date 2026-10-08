using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 §2.5: AC-101-14 … 17, 19, 20.</summary>
    public sealed class SlideTests
    {
        [Test]
        public void AC14_SlideLasts39TicksWithLowHitboxFromTheFirstTick()
        {
            SimRig rig = SimRig.Flat();
            rig.Steps(5);
            rig.Step(InputCommand.Slide);
            Assert.IsTrue(rig.State.Sliding, "slide starts this tick");
            Assert.AreEqual(0.70f, rig.Sim.HitboxHeight, 1e-6f);
            long start = rig.State.Tick;
            int sliding = 1;
            while (true)
            {
                rig.Steps(1);
                if (!rig.State.Sliding)
                {
                    break;
                }

                sliding++;
            }

            Assert.AreEqual(39, sliding);
            Assert.AreEqual(39, rig.Sim.SlideTicks);
            Assert.IsTrue(rig.HasEventAt(RunEventType.SlideEnd, start + 39));
            Assert.AreEqual(1.5f, rig.Sim.HitboxHeight, 1e-6f);
        }

        [Test]
        public void AC14_SlideHitboxPassesUnderAHighObstacle()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.High(c, 20f, 20.6f));
            rig.RunTo(18f);
            rig.Step(InputCommand.Slide);
            rig.RunTo(30f);
            Assert.AreEqual(0, rig.State.Hits);
        }

        [Test]
        public void AC15_JumpWhileSlidingCancelsOnTheSameTick()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(InputCommand.Slide);
            rig.Steps(10);
            rig.Step(InputCommand.Jump);
            Assert.IsFalse(rig.State.Sliding);
            Assert.IsTrue(rig.State.Jumped);
            Assert.Greater(rig.State.Y, 0f);
            Assert.IsTrue(rig.HasEventAt(RunEventType.SlideEnd, rig.State.Tick));
            Assert.IsTrue(rig.HasEventAt(RunEventType.Jump, rig.State.Tick));
        }

        [Test]
        public void AC16_SlideAgainRestartsTheTimer()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(InputCommand.Slide);
            rig.Steps(19);
            rig.Step(InputCommand.Slide); // tick 21
            int more = 1;
            while (true)
            {
                rig.Steps(1);
                if (!rig.State.Sliding)
                {
                    break;
                }

                more++;
            }

            Assert.AreEqual(39, more, "a full 39 ticks from the restart");
            Assert.AreEqual(1, rig.CountEvents(RunEventType.SlideStart));
        }

        [Test]
        public void AC17_SwipeDownInAirFastFallsThenSlidesOnLanding()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(InputCommand.Jump);
            rig.Steps(8);
            rig.Step(InputCommand.Slide);
            Assert.LessOrEqual(rig.State.Vy, -14f);
            Assert.IsTrue(rig.State.FastFalling);
            rig.StepUntil(s => s.Grounded, 200);
            long landing = rig.State.Tick;
            Assert.IsTrue(rig.State.Sliding, "a full slide begins on the landing tick");
            Assert.IsTrue(rig.HasEventAt(RunEventType.SlideStart, landing));
            int sliding = 1;
            while (true)
            {
                rig.Steps(1);
                if (!rig.State.Sliding)
                {
                    break;
                }

                sliding++;
            }

            Assert.AreEqual(39, sliding);
        }

        [Test]
        public void AC19_CeilingGuardExtendsTheSlideUntilClear()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.High(c, 20f, 30f));
            rig.RunTo(17f);
            rig.Step(InputCommand.Slide); // 39 ticks = 6.5 m: expires near s 23.5, under the branch
            int maxSliding = 0;
            int run = 0;
            while (rig.State.S < 35f)
            {
                rig.Steps(1);
                run = rig.State.Sliding ? run + 1 : run;
                maxSliding = System.Math.Max(maxSliding, run);
                if (rig.State.S < 30f + 0.2f)
                {
                    Assert.IsTrue(rig.State.Sliding, "still sliding under the branch at s " + rig.State.S);
                }
            }

            Assert.Greater(maxSliding, 39);
            Assert.IsFalse(rig.State.Sliding);
            Assert.AreEqual(0, rig.State.Hits);
        }

        [Test]
        public void AC20_JumpUnderACeilingIsHeldAndFiresWhenClear()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.High(c, 20f, 21f));
            rig.RunTo(17f);
            rig.Step(InputCommand.Slide);
            rig.RunTo(20.5f);
            rig.Step(InputCommand.Jump);
            long received = rig.State.Tick;
            Assert.IsFalse(rig.State.Jumped, "held: the branch is over the standing hitbox");
            Assert.AreEqual(DropReason.Ceiling, rig.State.BufferedKind);
            rig.StepUntil(s => s.Jumped, 40);
            Assert.IsTrue(rig.State.Jumped);
            Assert.LessOrEqual(rig.State.Tick - received, 21);
            Assert.AreEqual(0, rig.State.Hits);
            Assert.AreEqual(0, rig.CountEvents(RunEventType.InputDropped));
        }

        [Test]
        public void AC20_HeldJumpIsDroppedAfter21Ticks()
        {
            SimRig rig = SimRig.Flat(10f, c => SimRig.High(c, 20f, 40f));
            rig.RunTo(17f);
            rig.Step(InputCommand.Slide);
            rig.RunTo(21f);
            rig.Step(InputCommand.Jump);
            long received = rig.State.Tick;
            rig.StepUntil(s => s.Tick == received + 21);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.InputDropped));
            Assert.AreEqual((byte)DropReason.Ceiling, rig.LastEvent(RunEventType.InputDropped).Reason);
            Assert.IsFalse(rig.State.Jumped);
            Assert.IsTrue(rig.State.Sliding);
            Assert.AreEqual(0, rig.State.Hits);
        }
    }
}
