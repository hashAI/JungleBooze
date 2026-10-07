using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 004 AC-410 to AC-417 and AC-421 to AC-422: release timing, launch, flight, landing and the speed blend.</summary>
    public sealed class VineReleaseTests
    {
        private const double Dt = 1.0 / 60.0;

        private static VineScenario Canonical(double entrySpeed, double runSpeed = 15.0)
        {
            var scenario = new VineScenario(runSpeed, true);
            scenario.GrabAt(-1.25, 1.2, entrySpeed);
            return scenario;
        }

        private static VineReleaseGrade GradeOfLastRelease(VineScenario sc)
        {
            return (VineReleaseGrade)sc.LastOf(RunnerEventType.VineReleased).Value;
        }

        [TestCase(18, 27)]
        [TestCase(22, 27)]
        [TestCase(26, 27)]
        [TestCase(27, 27)]
        [TestCase(30, 30)]
        [TestCase(41, 41)]
        public void AC410_ASwipeInTheBufferWindowFiresOnTick27AsGood(int swipeTick, int expectedReleaseTick)
        {
            VineScenario sc = Canonical(15.0);
            Assert.AreEqual(expectedReleaseTick, sc.RunUntilReleased(swipeTick));
            Assert.AreEqual(VineReleaseGrade.Good, GradeOfLastRelease(sc));
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(17)]
        public void AC410_ASwipeOnTick17OrEarlierExpiresWithNoEffect(int swipeTick)
        {
            VineScenario sc = Canonical(15.0);
            int tick = sc.RunUntilReleased(swipeTick);
            Assert.AreEqual(85, tick, "the swipe did nothing, so the apex auto release came");
            Assert.AreEqual(VineReleaseGrade.Auto, GradeOfLastRelease(sc));
        }

        [Test]
        public void AC410_TheBufferIsNineTicks()
        {
            Assert.AreEqual(9, VineConfig.CreateDefault().ReleaseBufferTicks);
        }

        [TestCase(42, VineReleaseGrade.Perfect)]
        [TestCase(47, VineReleaseGrade.Perfect)]
        [TestCase(52, VineReleaseGrade.Perfect)]
        [TestCase(53, VineReleaseGrade.Good)]
        [TestCase(70, VineReleaseGrade.Good)]
        [TestCase(84, VineReleaseGrade.Good)]
        public void AC409_GradeFollowsTheSwingTickOfTheSwipe(int swipeTick, VineReleaseGrade expected)
        {
            VineScenario sc = Canonical(15.0);
            Assert.AreEqual(swipeTick, sc.RunUntilReleased(swipeTick));
            Assert.AreEqual(expected, GradeOfLastRelease(sc));
        }

        [Test]
        public void AC409_ASwipeOnTheApexTickWinsOverTheAutoRelease()
        {
            VineScenario sc = Canonical(15.0);
            Assert.AreEqual(85, sc.RunUntilReleased(85));
            Assert.AreEqual(VineReleaseGrade.Good, GradeOfLastRelease(sc));
        }

        [Test]
        public void AC411_TheFailsafeReleasesAtSwingMaxTicksWhenTheApexIsLater()
        {
            VineDesignValues values = VineDesignValues.CreateDefault();
            values.SwingMaxMs = 1000f;
            var sc = new VineScenario(15.0, true, null, values);
            sc.GrabAt(-1.25, 1.2, 15.0);
            Assert.AreEqual(60, sc.RunUntilReleased(0));
            Assert.AreEqual(VineReleaseGrade.Auto, GradeOfLastRelease(sc));
        }

        [Test]
        public void AC412_AReleaseActsOnItsOwnTick()
        {
            VineScenario sc = Canonical(15.0);
            RunnerState before = default;
            for (int i = 1; i < 47; i++)
            {
                before = sc.Step();
            }

            Assert.AreEqual(Locomotion.Carried, before.Locomotion);
            int releasedBefore = sc.CountOf(RunnerEventType.VineReleased);
            RunnerState after = sc.Step(InputCommand.Jump);
            Assert.AreEqual(releasedBefore + 1, sc.CountOf(RunnerEventType.VineReleased), "VineReleased on the swipe tick");
            Assert.AreEqual(Locomotion.Falling, after.Locomotion);
            Assert.AreEqual(sc.Sim.LaunchForwardSpeedMps * Dt, after.Z - before.Z, 1e-9, "z advances by vx dt on the release tick");
        }

        [Test]
        public void AC413_EveryGrabAndEveryReleaseLandsClearOfTheChasmOnThePad()
        {
            int[] swipes = { 27, 35, 42, 47, 52, 60, 70, 0 };
            double minLanding = double.MaxValue;
            double maxLanding = double.MinValue;
            int runs = 0;
            for (double entry = 8.0; entry <= 28.0; entry += 0.5)
            {
                for (int z = 0; z < 6; z++)
                {
                    double zRel = -1.25 + (z * 0.5);
                    for (int s = 0; s < swipes.Length; s++)
                    {
                        var sc = new VineScenario(15.0, true);
                        sc.GrabAt(zRel, 1.2, entry);
                        sc.RunUntilReleased(swipes[s]);
                        int landed = sc.RunUntilLanded();
                        Assert.Greater(landed, 0, "entry " + entry + " z " + zRel + " swipe " + swipes[s]);
                        Assert.AreEqual(Locomotion.Running, sc.State.Locomotion, "entry " + entry + " z " + zRel + " swipe " + swipes[s]);
                        Assert.AreEqual(0, sc.CountOf(RunnerEventType.Died));
                        double landing = sc.State.Z - VineScenario.PivotZ;
                        minLanding = Math.Min(minLanding, landing);
                        maxLanding = Math.Max(maxLanding, landing);
                        runs++;
                    }
                }
            }

            Assert.AreEqual(41 * 6 * 8, runs);
            Assert.GreaterOrEqual(minLanding, 14.5, "min landing past the pivot");
            Assert.LessOrEqual(maxLanding, 30.0, "max landing past the pivot");
        }

        [TestCase(13.0)]
        [TestCase(14.0)]
        [TestCase(15.0)]
        [TestCase(16.0)]
        public void AC414_APerfectLandsAtLeastTwoAndAHalfMetresPastAPoorRelease(double catchSpeed)
        {
            double perfect = LandingPastPivot(catchSpeed, 47);
            double poor = LandingPastPivot(catchSpeed, 0);
            Assert.GreaterOrEqual(perfect - poor, 2.5, "perfect " + perfect + " poor " + poor);
        }

        [Test]
        public void T401_LandingsMatchTheReportWithinPointThreeMetres()
        {
            // Past the far edge (pivot + 12): vC 13 Good-early 2.86, Poor 3.32; vC 16 Perfect 12.76, Good-early 9.20.
            Assert.AreEqual(12.0 + 2.86, LandingPastPivot(13.0, 27), 0.3);
            Assert.AreEqual(12.0 + 3.32, LandingPastPivot(13.0, 0), 0.3);
            Assert.AreEqual(12.0 + 7.11, LandingPastPivot(13.0, 47), 0.3);
            Assert.AreEqual(12.0 + 12.76, LandingPastPivot(16.0, 47), 0.3);
            Assert.AreEqual(12.0 + 9.20, LandingPastPivot(16.0, 27), 0.3);
            Assert.AreEqual(12.0 + 5.87, LandingPastPivot(16.0, 0), 0.3);
        }

        [Test]
        public void AC415_ReleaseVelocityIsPendulumVelocityPlusImpulseWithFloors()
        {
            // The runner's launch fields for a Poor release at the apex are exactly the floors.
            VineScenario poor = Canonical(14.0);
            poor.RunUntilReleased(0);
            Assert.AreEqual(6.0, poor.Sim.LaunchForwardSpeedMps, 1e-9);
            Assert.AreEqual(2.0, poor.Sim.LaunchVelocityYMps, 1e-9);
            Assert.AreEqual(16.0, poor.Sim.LaunchGravityMps2, 1e-9);

            // A Perfect swipe at tick 47 uses the state after 46 integrations.
            VineScenario perfect = Canonical(15.0);
            for (int i = 1; i < 47; i++)
            {
                perfect.Step();
            }

            RunnerState s = perfect.State;
            perfect.Step(InputCommand.Jump);
            perfect.Sim.Vines.LaunchVelocity(s.SwingAngleRad, s.SwingOmegaRadS, VineReleaseGrade.Perfect, out double vx, out double vy);
            Assert.AreEqual(vx, perfect.Sim.LaunchForwardSpeedMps, 1e-5);
            Assert.AreEqual(vy, perfect.Sim.LaunchVelocityYMps, 1e-5);
            Assert.AreEqual(s.Y, perfect.Sim.LaunchStartY, 1e-6);
        }

        [Test]
        public void AC416_TheFlightApexIsWhereTheBonusRingExpectsIt()
        {
            VineScenario sc = Canonical(15.0);
            sc.RunUntilReleased(47);
            double vy = sc.Sim.LaunchVelocityYMps;
            double expectedApexS = vy / 16.0;
            double bestY = double.MinValue;
            int bestStep = 0;
            for (int i = 1; i <= 120; i++)
            {
                RunnerState s = sc.Step();
                if (s.Y > bestY)
                {
                    bestY = s.Y;
                    bestStep = i;
                }

                if (s.Locomotion == Locomotion.Running)
                {
                    break;
                }
            }

            // The first flight tick after the release tick is one tick after the release state.
            Assert.AreEqual(expectedApexS, (bestStep + 1) * Dt, 0.05);
            Assert.AreEqual(sc.Sim.LaunchStartY + (vy * expectedApexS) - (8.0 * expectedApexS * expectedApexS), bestY, 0.2);
        }

        [Test]
        public void AC417_FlightUsesGravity16AndLandsOnTheClosedFormTick()
        {
            int[] swipes = { 27, 47, 60, 0 };
            for (int s = 0; s < swipes.Length; s++)
            {
                VineScenario sc = Canonical(15.0);
                int released = sc.RunUntilReleased(swipes[s]);
                bool command = swipes[s] != 0;
                double y0 = sc.Sim.LaunchStartY;
                double vy = sc.Sim.LaunchVelocityYMps;
                double z0 = sc.Sim.LaunchStartZ;
                double vx = sc.Sim.LaunchForwardSpeedMps;
                double flightS = (vy + Math.Sqrt((vy * vy) + (32.0 * y0))) / 16.0;
                int landed = sc.RunUntilLanded();
                int flown = command ? landed + 1 : landed;
                Assert.AreEqual(flightS * 60.0, flown, 1.001, "swipe " + swipes[s] + " (release tick " + released + ")");
                Assert.AreEqual(z0 + (vx * flown * Dt), sc.State.Z, 1e-6, "z follows z0 + vx t");
            }
        }

        [Test]
        public void AC421_TheSpeedMultiplierDoesNotChangeZOnTheRopeOrInFlight()
        {
            VineScenario a = Canonical(15.0);
            VineScenario b = Canonical(15.0);
            b.Sim.SpeedMultiplier = 1.6;
            int steps = 0;
            for (int i = 1; i <= 160; i++)
            {
                RunnerState sa = a.Step(i == 47 ? InputCommand.Jump : InputCommand.None);
                RunnerState sb = b.Step(i == 47 ? InputCommand.Jump : InputCommand.None);
                if (sa.Locomotion == Locomotion.Running || sb.Locomotion == Locomotion.Running)
                {
                    break;
                }

                Assert.AreEqual(sa.Z, sb.Z, 1e-6, "step " + i);
                Assert.AreEqual(sa.Y, sb.Y, 1e-6f, "step " + i);
                steps++;
            }

            Assert.Greater(steps, 100);
        }

        [TestCase(1.0)]
        [TestCase(1.6)]
        public void AC422_AfterLandingTheSpeedFollowsTheSmoothstepToTheCurveSpeed(double multiplier)
        {
            // Good-early at catch 16 lands fast (about 14 m/s) while the curve says 10 m/s: the blend decelerates.
            for (int pass = 0; pass < 2; pass++)
            {
                VineScenario sc = pass == 0 ? Canonical(16.0, 10.0) : Canonical(13.0, 20.0);
                sc.Sim.SpeedMultiplier = multiplier;
                sc.RunUntilReleased(pass == 0 ? 27 : 0);
                int landed = sc.RunUntilLanded();
                Assert.Greater(landed, 0);
                double landSpeed = sc.State.Speed;
                double target = (pass == 0 ? 10.0 : 20.0) * multiplier;
                double previous = landSpeed;
                double hi = Math.Max(landSpeed, target) + 1e-4;
                for (int k = 1; k <= 40; k++)
                {
                    RunnerState s = sc.Step();
                    double u = Math.Min(1.0, k / 30.0);
                    double expected = landSpeed + ((target - landSpeed) * u * u * (3.0 - (2.0 * u)));
                    Assert.AreEqual(expected, s.Speed, 1e-3, "pass " + pass + " tick " + k);
                    Assert.LessOrEqual(s.Speed, hi);
                    if (target > landSpeed)
                    {
                        Assert.GreaterOrEqual(s.Speed, previous - 1e-4, "monotone up, tick " + k);
                    }
                    else
                    {
                        Assert.LessOrEqual(s.Speed, previous + 1e-4, "monotone down, tick " + k);
                    }

                    previous = s.Speed;
                }

                Assert.AreEqual(target, previous, 1e-3, "back on the curve after 30 ticks");
            }
        }

        [Test]
        public void AC423_ABoostedRunnerCanGrabACommittedChasmVineAndTheCatchIsClamped()
        {
            // 12 m/s x 1.6 = 19.2 m/s enters the grab zone; the catch makes it 16 m/s.
            var sc = new VineScenario(12.0, true);
            sc.Sim.SpeedMultiplier = 1.6;
            int jumpTick = (int)Math.Round((VineScenario.PivotZ - 6.0) * 60.0 / 19.2);
            bool grabbed = false;
            for (int i = 0; i < 600 && !grabbed; i++)
            {
                sc.Step(i == jumpTick ? InputCommand.Jump : InputCommand.None);
                grabbed = sc.CountOf(RunnerEventType.VineGrabbed) > 0;
            }

            Assert.IsTrue(grabbed, "a boosted runner can still grab");
            Assert.AreEqual(Locomotion.Carried, sc.State.Locomotion);
            Assert.AreEqual(16.0, sc.State.SwingOmegaRadS * 14.0, 0.001);
        }

        private static double LandingPastPivot(double catchSpeed, int swipeTick)
        {
            VineScenario sc = Canonical(catchSpeed);
            sc.RunUntilReleased(swipeTick);
            int landed = sc.RunUntilLanded();
            Assert.Greater(landed, 0);
            return sc.State.Z - VineScenario.PivotZ;
        }
    }
}
