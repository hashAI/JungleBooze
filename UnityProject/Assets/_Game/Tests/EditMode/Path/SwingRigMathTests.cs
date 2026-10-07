using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Views;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>Spec 003 T6: AC-318 to AC-320 as pure math (rope geometry, span coverage, sway, anchor tree placement).</summary>
    public sealed class SwingRigMathTests
    {
        private const float Tolerance = 0.01f;

        private static float SwingSeconds(VineConfig vines)
        {
            return vines.SwingTicks * (float)RunnerConfig.TickSeconds;
        }

        [Test]
        public void AC318_RestPivotHeightComesFromTheConfig()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float pivotY = SwingRigMath.RestPivotHeightM(vines.GrabPointHeightM, vines.SwingRadiusM, vines.SwingStartAngleRad);
            float expected = vines.GrabPointHeightM + (vines.SwingRadiusM * (float)System.Math.Cos(vines.SwingStartAngleRad));
            Assert.AreEqual(expected, pivotY, Tolerance);
            Assert.AreEqual(8.64f, pivotY, Tolerance);
            Assert.AreEqual(2.05f, SwingRigMath.RestPivotAheadM(vines.SwingRadiusM, vines.SwingStartAngleRad), Tolerance);
        }

        [Test]
        public void AC318_PivotOffsetIsExactlyOneRopeLengthFromTheHand()
        {
            VineConfig vines = VineConfig.CreateDefault();
            for (int tick = 0; tick <= vines.SwingTicks; tick++)
            {
                double angle = vines.SwingAngleAt(tick);
                SwingRigMath.PivotOffsetFromHand(vines.SwingRadiusM, angle, out float up, out float ahead);
                float length = (float)System.Math.Sqrt((up * up) + (ahead * ahead));
                Assert.AreEqual(vines.SwingRadiusM, length, 0.02f, "rope end must coincide with the hand at tick " + tick);
            }
        }

        [Test]
        public void AC319_SpanCoversPivotTravelAtMaxSpeedPlusMarginForEverySpeed()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float ahead = SwingRigMath.RestPivotAheadM(vines.SwingRadiusM, vines.SwingStartAngleRad);
            for (float v = 8f; v <= SwingRigMath.DefaultMaxSpeedMps; v += 0.5f)
            {
                float length = SwingRigMath.SpanLengthM(ahead, SwingRigMath.DefaultMaxSpeedMps, SwingSeconds(vines)) + SwingRigMath.SpanLeadM;
                float travel = SwingRigMath.SwingTravelM(v, SwingSeconds(vines));
                Assert.IsTrue(SwingRigMath.SpanCoversTravel(SwingRigMath.SpanLeadM, length, ahead, travel), "speed " + v);
            }
        }

        [Test]
        public void AC319_SpanLengthMatchesTheSpecExamples()
        {
            Assert.AreEqual(35.4f, SwingRigMath.SpanLengthM(2.05f, 21f, 1.4f), 0.1f);
            Assert.AreEqual(20.05f, SwingRigMath.SpanLengthM(2.05f, 10f, 1.4f), 0.1f);
        }

        [Test]
        public void AC320_SwayFadesToHalfADegreeInTheLast30MetersAndComesBack()
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
        public void AC320_GlowStaysInsideTheGrabZoneWhileTheRopeSways()
        {
            VineConfig vines = VineConfig.CreateDefault();
            float halfZone = vines.GrabZoneLengthM * 0.5f;
            float nearAmplitude = SwingRigMath.SwayAmplitudeForDistance(0f);
            Assert.Less(SwingRigMath.GlowSwayBoundM(vines.SwingRadiusM, nearAmplitude), halfZone);
        }

        [Test]
        public void Sway_NeverExceedsItsAmplitude()
        {
            for (float t = 0f; t < 10f; t += 0.1f)
            {
                float angle = SwingRigMath.SwayAngleRad(t, 1.3f, 5f);
                Assert.LessOrEqual(System.Math.Abs(angle), 5f * (float)(System.Math.PI / 180.0) + 1e-5f);
            }
        }

        [Test]
        public void AnchorTree_IsDeterministicStatelessAndInsideTheSpecRanges()
        {
            for (int serial = 1; serial <= 200; serial++)
            {
                for (int row = 0; row < 3; row++)
                {
                    SwingTree a = SwingRigMath.PlaceTree(12345UL, RandomStreamIds.Scenery, serial, row);
                    SwingTree b = SwingRigMath.PlaceTree(12345UL, RandomStreamIds.Scenery, serial, row);
                    Assert.AreEqual(a.Side, b.Side);
                    Assert.AreEqual(a.LateralM, b.LateralM);
                    Assert.AreEqual(a.TrunkRadiusM, b.TrunkRadiusM);
                    Assert.AreEqual(a.HeightM, b.HeightM);
                    Assert.GreaterOrEqual(a.LateralM, 6f);
                    Assert.LessOrEqual(a.LateralM, 9f);
                    Assert.GreaterOrEqual(a.HeightM, 28f);
                    Assert.LessOrEqual(a.HeightM, 40f);
                    Assert.GreaterOrEqual(a.TrunkRadiusM * 2f, 2.5f);
                    Assert.LessOrEqual(a.TrunkRadiusM * 2f, 3.5f);
                }
            }
        }

        [Test]
        public void AnchorTree_RowsZigzagAndSeedsDiffer()
        {
            SwingTree row0 = SwingRigMath.PlaceTree(7UL, RandomStreamIds.Scenery, 5, 0);
            SwingTree row1 = SwingRigMath.PlaceTree(7UL, RandomStreamIds.Scenery, 5, 1);
            SwingTree row2 = SwingRigMath.PlaceTree(7UL, RandomStreamIds.Scenery, 5, 2);
            Assert.AreEqual(-row0.Side, row1.Side);
            Assert.AreEqual(row0.Side, row2.Side);

            int differing = 0;
            for (int serial = 1; serial <= 50; serial++)
            {
                if (SwingRigMath.PlaceTree(1UL, RandomStreamIds.Scenery, serial, 0).LateralM
                    != SwingRigMath.PlaceTree(2UL, RandomStreamIds.Scenery, serial, 0).LateralM)
                {
                    differing++;
                }
            }

            Assert.Greater(differing, 40);
        }

        [Test]
        public void AnchorTree_RigIsDrawnAtLeastTwoSecondsBeforeTheGrabAtTopSpeed()
        {
            // VineView draws a vine from FogEnd + signpost lead (90 + 25 m); AC-317's 2.0 s at the top speed range is 56 m.
            float drawnAhead = 90f + 25f;
            Assert.GreaterOrEqual(drawnAhead, SwingRigMath.RequiredVisibleAheadM(28f, 2f));
        }
    }
}
