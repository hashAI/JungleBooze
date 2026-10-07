using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 004 AC-424, AC-425 and AC-427: the missed-vine rule, the grab take-off window and the grab by jumping.</summary>
    public sealed class MissedVineTests
    {
        private const double Rim = VineScenario.PivotZ - 4.0;

        private static int TakeoffTick(double runSpeed, double takeoffZ)
        {
            return (int)Math.Round(takeoffZ * 60.0 / runSpeed);
        }

        private static int TicksToZ(double runSpeed, double z)
        {
            return (int)(z * 60.0 / runSpeed);
        }

        /// <summary>Runs the approach (start at z = 0, constant speed) with one jump on <paramref name="jumpTick"/> (-1 = none).</summary>
        private static VineScenario Approach(double runSpeed, bool chasm, int jumpTick, int ticks)
        {
            var sc = new VineScenario(runSpeed, chasm);
            for (int i = 0; i < ticks; i++)
            {
                sc.Step(i == jumpTick ? InputCommand.Jump : InputCommand.None);
                if (sc.State.IsDead)
                {
                    break;
                }
            }

            return sc;
        }

        [Test]
        public void AC424_FallingIntoAChasmUnderAVineThatWasNotGrabbedIsMissedVine()
        {
            VineScenario sc = Approach(10.0, true, -1, 900);
            Assert.IsTrue(sc.State.IsDead);
            Assert.AreEqual(DeathCause.MissedVine, sc.Sim.DeathCause);
            Assert.AreEqual(0, sc.CountOf(RunnerEventType.VineGrabbed));
        }

        [Test]
        public void AC424_AJumpTooLateToReachTheZoneIsMissedVine()
        {
            // Too early: a take-off 6 m before the rim lands on the rim, HERO then runs into the chasm without a grab.
            VineScenario sc = Approach(10.0, true, TakeoffTick(10.0, Rim - 6.0), 900);
            Assert.IsTrue(sc.State.IsDead);
            Assert.AreEqual(DeathCause.MissedVine, sc.Sim.DeathCause);
        }

        [Test]
        public void AC424_AFallAfterASwingOnThatRowIsAPlainFell()
        {
            // Make the ground end after the vine: whatever HERO does on the rope, the landing is in a hole.
            var sc = new VineScenario(15.0, true, null, null, VineScenario.PivotZ + 80.0);
            sc.GrabAt(-1.25, 1.2, 15.0);
            sc.RunUntilReleased(47);
            for (int i = 0; i < 400 && !sc.State.IsDead; i++)
            {
                sc.Step();
            }

            Assert.IsTrue(sc.State.IsDead);
            Assert.AreEqual(DeathCause.Fell, sc.Sim.DeathCause);
        }

        [Test]
        public void AC425_AMissedVineOverGroundReportsOnceAndDoesNotKill()
        {
            VineScenario sc = Approach(10.0, false, -1, 1500);
            Assert.IsFalse(sc.State.IsDead);
            Assert.AreEqual(1, sc.CountOf(RunnerEventType.VineMissed));
            Assert.AreEqual(0, sc.CountOf(RunnerEventType.VineGrabbed));
            Assert.Greater(sc.State.Z, VineScenario.PivotZ + 20.0);
        }

        [Test]
        public void AJumpFromTheMiddleOfTheTakeoffWindowGrabsTheVine()
        {
            double[] speeds = { 8.0, 10.0, 13.0, 21.0 };
            for (int s = 0; s < speeds.Length; s++)
            {
                double v = speeds[s];
                double lo = VineScenario.PivotZ - 1.25 - (0.55 * v);
                double hi = Rim + 0.25 + (v / 12.0);
                VineScenario sc = Approach(v, true, TakeoffTick(v, 0.5 * (lo + hi)), TicksToZ(v, 125.0));
                Assert.AreEqual(1, sc.CountOf(RunnerEventType.VineGrabbed), "speed " + v);
                Assert.IsFalse(sc.State.IsDead, "speed " + v);
            }
        }

        [TestCase(8.0, 280.0)]
        [TestCase(10.0, 350.0)]
        [TestCase(13.0, 410.0)]
        public void AC427_TheGrabTakeoffWindowIsAtLeastTheSpecFloor(double runSpeed, double floorMs)
        {
            int grabbing = 0;
            int first = -1;
            int last = -1;
            double lo = VineScenario.PivotZ - 1.25 - (0.55 * runSpeed);
            double hi = Rim + 0.25 + (runSpeed / 12.0);
            int firstTick = TakeoffTick(runSpeed, lo - 2.0);
            int lastTick = TakeoffTick(runSpeed, hi + 2.0);
            for (int j = firstTick; j <= lastTick; j++)
            {
                VineScenario sc = Approach(runSpeed, true, j, TicksToZ(runSpeed, 125.0));
                if (sc.CountOf(RunnerEventType.VineGrabbed) > 0)
                {
                    grabbing++;
                    if (first < 0)
                    {
                        first = j;
                    }

                    last = j;
                }
            }

            double windowMs = grabbing * 1000.0 / 60.0;
            Assert.GreaterOrEqual(windowMs, floorMs, "speed " + runSpeed + " window " + windowMs + " ms");
            Assert.AreEqual(last - first + 1, grabbing, "the window is contiguous at " + runSpeed);
        }
    }
}
