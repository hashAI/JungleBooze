using JungleBooze.Core;
using JungleBooze.Gameplay.Views;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>
    /// Spec 004 T405: AC-429 to AC-433 as pure math (rope geometry from the real pendulum angle, rest sway with the true
    /// period, the damped swing after a release, branch tip at the pivot, anchor tree placement, draw distance). Not yet
    /// run (written without a Unity compiler).
    /// </summary>
    public sealed class SwingRigMathTests
    {
        private const float Tolerance = 0.01f;
        private const float Deg = (float)(System.Math.PI / 180.0);

        [Test]
        public void AC401_PivotIsGrabPointPlusRopeLength()
        {
            VineConfig vines = VineConfig.CreateDefault();
            Assert.AreEqual(17f, vines.PivotHeightM, Tolerance);
            Assert.AreEqual(vines.GrabPointHeightM + vines.RopeLengthM, vines.PivotHeightM, 1e-4f);
        }

        [Test]
        public void AC430_RopeEndIsExactlyOneRopeLengthFromThePivotForEveryAngle()
        {
            VineConfig vines = VineConfig.CreateDefault();
            for (float theta = -0.2f; theta <= 1.1f; theta += 0.01f)
            {
                float ahead = SwingRigMath.RopeEndOffsetS(vines.RopeLengthM, theta);
                float down = vines.PivotHeightM - SwingRigMath.RopeEndHeightM(vines.PivotHeightM, vines.RopeLengthM, theta);
                float length = (float)System.Math.Sqrt((ahead * ahead) + (down * down));
                Assert.AreEqual(vines.RopeLengthM, length, 0.02f, "rope length at theta " + theta);
            }
        }

        [Test]
        public void AC430_RopeEndMatchesTheSimulationsHand()
        {
            VineConfig vines = VineConfig.CreateDefault();
            for (float theta = -0.1f; theta <= 1.0f; theta += 0.05f)
            {
                Assert.AreEqual((float)vines.HandOffsetZ(theta), SwingRigMath.RopeEndOffsetS(vines.RopeLengthM, theta), 0.02f, "z at " + theta);
                Assert.AreEqual((float)vines.HandHeightY(theta), SwingRigMath.RopeEndHeightM(vines.PivotHeightM, vines.RopeLengthM, theta), 0.02f, "y at " + theta);
            }
        }

        [Test]
        public void RopeAtRestHangsStraightDownToTheGrabPoint()
        {
            VineConfig vines = VineConfig.CreateDefault();
            Assert.AreEqual(0f, SwingRigMath.RopeEndOffsetS(vines.RopeLengthM, 0f), 1e-5f);
            Assert.AreEqual(vines.GrabPointHeightM, SwingRigMath.RopeEndHeightM(vines.PivotHeightM, vines.RopeLengthM, 0f), 1e-4f);
        }

        [Test]
        public void AC431_RestSwayUsesTheTruePendulumPeriod()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float period = SwingRigMath.SwayPeriodSeconds(vines.RopeLengthM, vines.SwingGravityMps2);
            Assert.AreEqual(5.0f, period, 0.05f);
            for (float t = 0f; t < 12f; t += 0.37f)
            {
                float a = SwingRigMath.SwayAngleRad(t, 0.7f, 5f, period);
                float b = SwingRigMath.SwayAngleRad(t + period, 0.7f, 5f, period);
                Assert.AreEqual(a, b, 1e-4f, "periodic at " + t);
                Assert.LessOrEqual(System.Math.Abs(a), (5f * Deg) + 1e-5f);
            }
        }

        [Test]
        public void AC431_SwayFadesToHalfADegreeInTheLast30MetersAndComesBack()
        {
            Assert.AreEqual(5f, SwingRigMath.SwayAmplitudeForDistance(60f), 1e-4f);
            Assert.AreEqual(5f, SwingRigMath.SwayAmplitudeForDistance(30f), 1e-4f);
            Assert.AreEqual(0.5f, SwingRigMath.SwayAmplitudeForDistance(0f), 1e-4f);
            for (float d = 0f; d <= 30f; d += 0.5f)
            {
                Assert.GreaterOrEqual(SwingRigMath.SwayAmplitudeForDistance(d), 0.5f - 1e-4f);
                Assert.LessOrEqual(SwingRigMath.SwayAmplitudeForDistance(d), 5f + 1e-4f);
            }

            Assert.AreEqual(5f, SwingRigMath.SwayAmplitudeForDistance(-SwingRigMath.SwayRegrowM), 1e-4f);
        }

        [Test]
        public void AC431_GlowStaysInsideTheGrabZoneWhileTheRopeSways()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float halfZone = vines.GrabZoneLengthM * 0.5f;
            float nearAmplitude = SwingRigMath.SwayAmplitudeForDistance(0f);
            float bound = SwingRigMath.GlowSwayBoundM(vines.RopeLengthM, nearAmplitude);
            Assert.Less(bound, halfZone);
            Assert.LessOrEqual(bound, 0.13f, "the rope end moves at most about 0.12 m at the grab zone");
        }

        [Test]
        public void AC432_RopeKeepsSwingingAfterTheReleaseAndSettlesToTheRestSway()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float theta = 0.7f;
            float omega = 1.1f;
            float peakEarly = 0f;
            float peakLater = 0f;
            const float dt = 1f / 120f;
            int settledAt = -1;
            for (int i = 0; i < 120 * 14; i++)
            {
                SwingRigMath.StepPostRelease(ref theta, ref omega, vines.SwingGravityMps2, vines.RopeLengthM, SwingRigMath.PostReleaseDampingPerS, dt);
                float t = i * dt;
                float abs = System.Math.Abs(theta);
                if (t < 1.5f && abs > peakEarly)
                {
                    peakEarly = abs;
                }

                if (t >= 3.5f && t < 5.5f && abs > peakLater)
                {
                    peakLater = abs;
                }

                if (settledAt < 0 && SwingRigMath.PostReleaseSettled(theta, omega))
                {
                    settledAt = i;
                }
            }

            Assert.Greater(peakEarly, 0.5f, "the rope swings on right after the release");
            Assert.Less(peakLater, 0.5f * peakEarly, "decay with a time constant of about 2 s");
            Assert.Greater(settledAt, 0, "the swing dies out");
            Assert.Less(settledAt * dt, 14f);
        }

        [Test]
        public void AC429_BranchTipIsExactlyAtThePivotAndTheRootIsInsideTheTrunk()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float[] lanes = { -2.4f, 0f, 2.4f };
            for (int serial = 1; serial <= 100; serial++)
            {
                for (int row = 0; row < 3; row++)
                {
                    for (int l = 0; l < lanes.Length; l++)
                    {
                        SwingTree tree = SwingRigMath.PlaceTree(99UL, RandomStreamIds.Scenery, serial, row, lanes[l]);
                        SwingRigMath.BranchPoint(tree, lanes[l], vines.PivotHeightM, 1f, out float tipX, out float tipY);
                        Assert.AreEqual(lanes[l], tipX, 0.001f, "tip above the lane centre");
                        Assert.AreEqual(vines.PivotHeightM, tipY, 0.001f, "tip at the pivot height");

                        SwingRigMath.BranchPoint(tree, lanes[l], vines.PivotHeightM, 0f, out float rootX, out float rootY);
                        Assert.AreEqual(tree.TrunkX, rootX, 1e-4f, "root on the trunk axis, inside the trunk radius");
                        Assert.AreEqual(tree.BranchRootHeightM, rootY, 1e-4f);

                        float reach = SwingRigMath.BranchReachM(tree, lanes[l]);
                        Assert.GreaterOrEqual(reach, SwingRigMath.BranchMinReachM - 1e-3f, "a real limb, not a stub");
                        Assert.GreaterOrEqual(tree.LateralM, 6f);
                        Assert.LessOrEqual(tree.LateralM, 9f);
                        Assert.Less(reach, 11.5f, "a limb, not a bridge");
                    }
                }
            }
        }

        [Test]
        public void AC429_BranchIsContinuousAndNeverGoesBelowTheRootOrTheBough()
        {
            VineConfig vines = VineConfig.CreateDefault();
            SwingTree tree = SwingRigMath.PlaceTree(5UL, RandomStreamIds.Scenery, 3, 0, 0f);
            float lastX = 0f;
            float lastY = 0f;
            for (int k = 0; k <= 20; k++)
            {
                SwingRigMath.BranchPoint(tree, 0f, vines.PivotHeightM, k / 20f, out float x, out float y);
                Assert.GreaterOrEqual(y, System.Math.Min(tree.BranchRootHeightM, vines.PivotHeightM) - 1e-3f, "stays 15 m or more above the path (clear of the bough)");
                if (k > 0)
                {
                    float step = (float)System.Math.Sqrt(((x - lastX) * (x - lastX)) + ((y - lastY) * (y - lastY)));
                    Assert.Less(step, 1.5f, "no gap along the branch");
                }

                lastX = x;
                lastY = y;
            }

            Assert.AreEqual(0.8f, SwingRigMath.BranchDiameterM(0f), 1e-5f);
            Assert.AreEqual(0.35f, SwingRigMath.BranchDiameterM(1f), 1e-5f);
        }

        [Test]
        public void AC433_AnchorTree_IsDeterministicStatelessAndInsideTheSpecRanges()
        {
            for (int serial = 1; serial <= 200; serial++)
            {
                for (int row = 0; row < 3; row++)
                {
                    SwingTree a = SwingRigMath.PlaceTree(12345UL, RandomStreamIds.Scenery, serial, row, 2.4f);
                    SwingTree b = SwingRigMath.PlaceTree(12345UL, RandomStreamIds.Scenery, serial, row, 2.4f);
                    Assert.AreEqual(a.Side, b.Side);
                    Assert.AreEqual(a.LateralM, b.LateralM);
                    Assert.AreEqual(a.TrunkRadiusM, b.TrunkRadiusM);
                    Assert.AreEqual(a.HeightM, b.HeightM);
                    Assert.AreEqual(a.BranchRootHeightM, b.BranchRootHeightM);
                    Assert.GreaterOrEqual(a.LateralM, 6f);
                    Assert.LessOrEqual(a.LateralM, 9f);
                    Assert.GreaterOrEqual(a.HeightM, 28f);
                    Assert.LessOrEqual(a.HeightM, 40f);
                    Assert.GreaterOrEqual(a.HeightM, 25f, "trunk top at least H + 8 m");
                    Assert.GreaterOrEqual(a.BranchRootHeightM, 15f);
                    Assert.LessOrEqual(a.BranchRootHeightM, 19f);
                    Assert.GreaterOrEqual(a.TrunkRadiusM, 1.25f);
                    Assert.LessOrEqual(a.TrunkRadiusM, 1.75f);
                    Assert.AreEqual(-a.Side, a.OpenSide, "the open side is opposite the trunk");
                }
            }
        }

        [Test]
        public void AnchorTree_RowsZigzagAndSeedsDiffer()
        {
            SwingTree row0 = SwingRigMath.PlaceTree(7UL, RandomStreamIds.Scenery, 5, 0, 0f);
            SwingTree row1 = SwingRigMath.PlaceTree(7UL, RandomStreamIds.Scenery, 5, 1, 0f);
            SwingTree row2 = SwingRigMath.PlaceTree(7UL, RandomStreamIds.Scenery, 5, 2, 0f);
            Assert.AreEqual(-row0.Side, row1.Side);
            Assert.AreEqual(row0.Side, row2.Side);

            int differing = 0;
            for (int serial = 1; serial <= 50; serial++)
            {
                if (SwingRigMath.PlaceTree(1UL, RandomStreamIds.Scenery, serial, 0, 0f).LateralM
                    != SwingRigMath.PlaceTree(2UL, RandomStreamIds.Scenery, serial, 0, 0f).LateralM)
                {
                    differing++;
                }
            }

            Assert.Greater(differing, 40);
        }

        [Test]
        public void AC433_SwingCameraStaysInsideTheAnchorTreeFace()
        {
            RunnerPresentationConfig config = RunnerPresentationConfig.CreateDefault();
            var tuning = new CameraRouteTuning();
            float widest = (config.CameraLateralFollow * 2.4f) + tuning.SwingSideOffsetM;
            float trunkFace = SwingRigMath.TreeLateralMinM - SwingRigMath.TreeRadiusMaxM;
            Assert.Less(widest, trunkFace, "the camera on the open side never reaches the trunk side");
            Assert.Less(config.CameraLateralFollow * 2.4f, trunkFace, "nor on the trunk side");
        }

        [Test]
        public void AC433_RigIsDrawnAtLeastTwoSecondsBeforeTheGrabAtTopSpeed()
        {
            // VineView draws a vine from FogEnd + signpost lead (90 + 25 m); the spec wants 2.0 s ahead (42 m at 21 m/s).
            float drawnAhead = SwingRigMath.RigDrawAheadM(90f, 25f);
            Assert.GreaterOrEqual(drawnAhead, SwingRigMath.RequiredVisibleAheadM(21f, 2f));
            Assert.GreaterOrEqual(drawnAhead, SwingRigMath.RequiredVisibleAheadM(28f, 2f));
        }

        [Test]
        public void LandingGladeStartsTwelveMetersPastTheLastPivot()
        {
            Assert.AreEqual(12f, SwingRigMath.GladeStartAfterGrabM, 1e-5f);
            Assert.AreEqual(20f, SwingRigMath.GladeLengthM, 1e-5f);
        }
    }
}
