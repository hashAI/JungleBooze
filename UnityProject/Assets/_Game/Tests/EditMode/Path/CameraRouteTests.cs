using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.RouteCamera
{
    /// <summary>
    /// Spec 003 T4: the camera on the curved route (sections 6.1, 6.2, 14.3, 14.4). Pure math through
    /// <see cref="CameraRouteRig"/>: no scene, no Unity rendering. Not yet run (written without a Unity compiler).
    /// </summary>
    public sealed class CameraRouteTests
    {
        private const float Eps = 0.02f;
        private const double RadiusM = 75.0;

        [Test]
        public void StraightRoute_CameraIsExactlyTheOldFollowCamera()
        {
            var h = new CameraHarness(new StraightRouteSource(), false, 100.0);
            for (int i = 0; i < 120; i++)
            {
                h.Step(20.0, 2.4f, false, 0f, false);
            }

            RunnerPresentationConfig c = h.Config;
            Assert.AreEqual((float)h.S - c.CameraOffsetBehindM, h.Rig.Position.z, 1e-3f);
            Assert.AreEqual(c.CameraLateralFollow * 2.4f, h.Rig.Position.x, 0.01f);
            Assert.AreEqual(c.CameraOffsetUpM, h.Rig.Position.y, 1e-3f);
            Vector3 forward = h.Rig.Rotation * Vector3.forward;
            float expectedPitchDeg = -Mathf.Atan2(c.CameraOffsetUpM - c.CameraLookAtHeightM, c.CameraOffsetBehindM + c.CameraLookAheadM) * Mathf.Rad2Deg;
            Assert.AreEqual(0f, forward.x, 1e-4f, "no yaw on a straight route");
            Assert.AreEqual(expectedPitchDeg, Mathf.Asin(forward.y) * Mathf.Rad2Deg, 1e-3f);
            Assert.AreEqual(c.CameraFovDeg, h.Rig.FovDeg, 1e-3f);
            Assert.AreEqual(0f, h.Rig.Model.RollDeg, 1e-4f);
        }

        [Test]
        public void AC305_NextFortyMetersStayOnScreenOnR75Bends()
        {
            double[] speeds = { 10.0, 33.6 };
            float[] lanes = { -2.4f, 0f, 2.4f };
            foreach (double sign in new[] { 1.0, -1.0 })
            {
                foreach (double speed in speeds)
                {
                    foreach (float lane in lanes)
                    {
                        var h = new CameraHarness(new ArcRouteSource(sign / RadiusM, 100.0, 40.0), false, 0.0);
                        float worst = 0f;
                        while (h.S < 700.0)
                        {
                            h.Step(speed, lane, false, 0f, false);
                            // Steady state: 2 s after the bend has reached its full curvature.
                            if (h.S > 140.0 + (2.0 * speed))
                            {
                                worst = Mathf.Max(worst, h.WorstEdgeRatio());
                            }
                        }

                        Assert.LessOrEqual(
                            worst,
                            1f - h.Tuning.SafetyMargin + Eps,
                            "sign " + sign + " speed " + speed + " lane " + lane + ": worst edge ratio " + worst);
                    }
                }
            }
        }

        [Test]
        public void AC305_DebugRouteKeepsTheNextFortyMetersInsideTheViewport()
        {
            float[] lanes = { -2.4f, 0f, 2.4f };
            foreach (float lane in lanes)
            {
                var h = new CameraHarness(new DebugRouteSource(), false, 0.0);
                float worst = 0f;
                while (h.S < 1500.0)
                {
                    h.Step(28.0, lane, false, 0f, false);
                    if (h.S > 50.0)
                    {
                        worst = Mathf.Max(worst, h.WorstEdgeRatio());
                    }
                }

                Assert.Less(worst, 1f, "lane " + lane + ": a probed path point left the viewport, worst ratio " + worst);
            }
        }

        [Test]
        public void AC307_LaneSwitchOnScreenTakesNoLongerOnAR75BendThanOnAStraight()
        {
            float straight = LaneSwitchOnScreenSeconds(new StraightRouteSource(), 200.0);
            float rightBend = LaneSwitchOnScreenSeconds(new ArcRouteSource(1.0 / RadiusM, 100.0, 40.0), 320.0);
            float leftBend = LaneSwitchOnScreenSeconds(new ArcRouteSource(-1.0 / RadiusM, 100.0, 40.0), 320.0);

            Assert.LessOrEqual(straight, 0.12f, "straight");
            Assert.LessOrEqual(rightBend, 0.12f, "right bend R = 75 m");
            Assert.LessOrEqual(leftBend, 0.12f, "left bend R = 75 m");
            Assert.LessOrEqual(Mathf.Abs(rightBend - straight), 0.02f);
        }

        [Test]
        public void AC314_CameraStaysWithinTheSpecLimitsInScriptedRuns()
        {
            const int runs = 1000;
            for (int run = 0; run < runs; run++)
            {
                var random = new System.Random(1000 + run);
                bool reduce = (run % 3) == 0;
                bool arc = (run % 2) == 1;
                double kappa = (random.Next(2) == 0 ? 1.0 : -1.0) / (75.0 + (random.NextDouble() * 225.0));
                IRouteSource source = arc ? (IRouteSource)new ArcRouteSource(kappa, 40.0, 30.0 + (random.NextDouble() * 30.0)) : new DebugRouteSource();
                double start = arc ? 0.0 : random.NextDouble() * 4000.0;
                var h = new CameraHarness(source, reduce, start);
                AssertRunWithinLimits(h, random, run, reduce);
            }
        }

        [Test]
        public void AC315_ReduceMotionHasNoRollNoFovShiftNoPullBackAndAtMostTwentyDegPerSecondOfYaw()
        {
            var h = new CameraHarness(new DebugRouteSource(), true, 0.0);
            float baseFov = h.Config.CameraFovDeg;
            float lastYaw = h.Rig.Model.YawRad;
            float maxLeadDeg = 0f;
            for (int i = 0; i < 60 * 40; i++)
            {
                bool swing = (i / 120) % 3 == 1;
                bool boost = (i / 200) % 2 == 1;
                h.Step(30.0, 2.4f * Mathf.Sin(i * 0.05f), swing, 0.9f * Mathf.Sin(i * 0.1f), boost);
                CameraRouteModel m = h.Rig.Model;
                Assert.AreEqual(0f, m.RollDeg, 1e-5f, "roll at frame " + i);
                Assert.AreEqual(baseFov, h.Rig.FovDeg, 1e-4f, "FOV at frame " + i);
                Assert.AreEqual(0f, m.PullBackM, 1e-5f, "pull-back at frame " + i);
                Assert.AreEqual(0f, m.SideM, 1e-5f, "side offset at frame " + i);
                float rateDeg = Mathf.Abs(m.YawRad - lastYaw) * Mathf.Rad2Deg / CameraHarness.Dt;
                Assert.LessOrEqual(rateDeg, 20.01f, "yaw rate at frame " + i);
                lastYaw = m.YawRad;

                h.Frame.Sample(h.S, out PathPose pose);
                float heroHeading = Mathf.Atan2(pose.Tangent.x, pose.Tangent.z);
                maxLeadDeg = Mathf.Max(maxLeadDeg, Mathf.Abs(CameraRouteModel.WrapPi(m.YawRad - heroHeading)) * Mathf.Rad2Deg);
            }

            Assert.Greater(maxLeadDeg, 0.5f, "the yaw lead is kept with Reduce Motion");
        }

        [Test]
        public void AC316_SwingCameraReachesNinetyPercentIn250MsAndReturnsIn400Ms()
        {
            var h = new CameraHarness(new StraightRouteSource(), false, 100.0);
            for (int i = 0; i < 30; i++)
            {
                h.Step(20.0, 0f, false, 0f, false);
            }

            float baseFov = h.Config.CameraFovDeg;
            float fovDelta = h.Config.SwingCameraFovDeg - baseFov;
            float pull = h.Tuning.SwingPullBackM;

            int grabFrames = Mathf.RoundToInt(0.25f / CameraHarness.Dt);
            for (int i = 0; i < grabFrames; i++)
            {
                h.Step(20.0, 0f, true, 0.5f, false);
            }

            Assert.GreaterOrEqual(h.Rig.FovDeg, baseFov + (0.9f * fovDelta), "FOV 250 ms after the grab");
            Assert.GreaterOrEqual(h.Rig.Model.PullBackM, 0.9f * pull, "pull-back 250 ms after the grab");
            Assert.AreEqual(1f * h.Tuning.SwingSideOffsetM, Mathf.Abs(h.Rig.Model.SideM), 0.15f * h.Tuning.SwingSideOffsetM);

            for (int i = 0; i < 20; i++)
            {
                h.Step(20.0, 0f, true, 0.5f, false);
            }

            float minFovDuringReturn = float.MaxValue;
            int returnFrames = Mathf.RoundToInt(0.4f / CameraHarness.Dt);
            for (int i = 0; i < returnFrames; i++)
            {
                h.Step(20.0, 0f, false, 0f, false);
                minFovDuringReturn = Mathf.Min(minFovDuringReturn, h.Rig.FovDeg);
            }

            Assert.LessOrEqual(Mathf.Abs(h.Rig.FovDeg - baseFov), 0.1f * fovDelta, "FOV 400 ms after landing");
            Assert.LessOrEqual(h.Rig.Model.PullBackM, 0.1f * pull, "pull-back 400 ms after landing");
            Assert.Less(minFovDuringReturn, baseFov - 0.5f, "the landing punch dips below the base FOV");

            for (int i = 0; i < 30; i++)
            {
                h.Step(20.0, 0f, false, 0f, false);
            }

            Assert.AreEqual(baseFov, h.Rig.FovDeg, 1e-3f, "settled");
        }

        [Test]
        public void AC316_SwingCameraNeverPutsTheCameraInsideTheSpanCorridor()
        {
            // Weak proxy until the span exists (T6): the swing camera (7.5 m back, 4.4 m up) stays below the pivot
            // height of 8.64 m (spec 003 AC-318) and well off the middle lane line (the span's center).
            var h = new CameraHarness(new StraightRouteSource(), false, 100.0);
            for (int i = 0; i < 90; i++)
            {
                h.Step(20.0, 0f, true, 0.6f, false);
            }

            Assert.Less(h.Rig.Position.y, 8.64f);
            Assert.GreaterOrEqual(Mathf.Abs(h.Rig.Position.x), 1f);
        }

        private static float LaneSwitchOnScreenSeconds(IRouteSource source, double startS)
        {
            var h = new CameraHarness(source, false, startS);
            const double speed = 20.0;
            for (int i = 0; i < 180; i++)
            {
                h.Step(speed, -2.4f, false, 0f, false);
            }

            float before = HeroBearingAgainstTrack(h, -2.4f);
            const int frames = 60;
            float[] sep = new float[frames + 1];
            sep[0] = before;
            for (int i = 1; i <= frames; i++)
            {
                float t = i * CameraHarness.Dt;
                float u = Mathf.Clamp01(t / 0.12f);
                float x = -2.4f + (2.4f * (1f - ((1f - u) * (1f - u))));
                h.Step(speed, x, false, 0f, false);
                sep[i] = HeroBearingAgainstTrack(h, x);
            }

            float total = sep[frames] - before;
            Assert.Greater(Mathf.Abs(total), 1e-3f, "the hero moved on screen");
            for (int i = 1; i <= frames; i++)
            {
                if (Mathf.Abs(sep[i] - before) >= 0.9f * Mathf.Abs(total))
                {
                    return i * CameraHarness.Dt;
                }
            }

            return float.MaxValue;
        }

        /// <summary>Bearing of the hero minus the bearing of the track centerline at the hero's distance, both in the camera frame.</summary>
        private static float HeroBearingAgainstTrack(CameraHarness h, float heroX)
        {
            return h.Bearing(h.S, heroX) - h.Bearing(h.S, 0f);
        }

        private static void AssertRunWithinLimits(CameraHarness h, System.Random random, int run, bool reduce)
        {
            CameraRouteModel m = h.Rig.Model;
            double speed = 8.0 + (random.NextDouble() * 25.6);
            float heroX = 0f;
            float targetX = 0f;
            int swingLeft = 0;
            int boostLeft = 0;
            float lastYaw = m.YawRad;
            float lastRoll = m.BankRollDeg;
            float lastPitch = m.FollowPitchDeg;
            float yawLimit = reduce ? 20.01f : 40.01f;

            for (int i = 0; i < 60 * 12; i++)
            {
                if (random.Next(90) == 0)
                {
                    targetX = (random.Next(3) - 1) * 2.4f;
                }

                heroX = Mathf.MoveTowards(heroX, targetX, 2.4f / 7f);
                if (swingLeft == 0 && random.Next(300) == 0)
                {
                    swingLeft = 40 + random.Next(60);
                }

                if (boostLeft == 0 && random.Next(400) == 0)
                {
                    boostLeft = 120;
                }

                bool swinging = swingLeft > 0;
                bool boost = boostLeft > 0;
                swingLeft = Mathf.Max(0, swingLeft - 1);
                boostLeft = Mathf.Max(0, boostLeft - 1);
                h.Step(speed, heroX, swinging, 0.9f * Mathf.Sin(i * 0.12f), boost);

                float dt = CameraHarness.Dt;
                string where = "run " + run + " frame " + i;
                Assert.LessOrEqual(Mathf.Abs(m.YawRad - lastYaw) * Mathf.Rad2Deg / dt, yawLimit, "yaw rate, " + where);
                Assert.LessOrEqual(Mathf.Abs(m.BankRollDeg), h.Tuning.RollMaxDeg + 1e-3f, "bank roll, " + where);
                Assert.LessOrEqual(Mathf.Abs(m.BankRollDeg - lastRoll) / dt, h.Tuning.RollRateMaxDegS + 0.01f, "bank roll rate, " + where);
                Assert.LessOrEqual(Mathf.Abs(m.FollowPitchDeg - lastPitch) / dt, h.Tuning.PitchRateMaxDegS + 0.01f, "pitch rate, " + where);
                Assert.LessOrEqual(Mathf.Abs(m.SwingRollDeg), h.Tuning.SwingRollMaxDeg + 1e-3f, "swing roll, " + where);
                if (reduce)
                {
                    Assert.AreEqual(0f, m.RollDeg, 1e-5f, "reduce motion roll, " + where);
                }

                lastYaw = m.YawRad;
                lastRoll = m.BankRollDeg;
                lastPitch = m.FollowPitchDeg;
            }
        }
    }
}
