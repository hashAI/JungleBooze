using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Look test v2 layout (ENVIRONMENT_STRATEGY 4.1): the curved, climbing path is seamless across the loop, the
    /// runnable width is flat, the river crosses at the ford, the enclose/reveal cadence holds, and the world view
    /// shifts segments by whole loop offsets.
    /// </summary>
    public sealed class LookTestLayoutTests
    {
        private LookTestConfigAsset _config;
        private LookTestStretchLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
            _layout = new LookTestStretchLayout(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void PeriodicCurvePassesThroughItsKeysAndWraps()
        {
            var curve = new PeriodicCurve(100f, new[] { new Vector2(10f, 2f), new Vector2(60f, -1f), new Vector2(90f, 5f) });
            Assert.AreEqual(2f, curve.Evaluate(10f), 1e-4f);
            Assert.AreEqual(-1f, curve.Evaluate(60f), 1e-4f);
            Assert.AreEqual(5f, curve.Evaluate(90f), 1e-4f);
            Assert.AreEqual(curve.Evaluate(3f), curve.Evaluate(103f), 1e-4f);
            Assert.AreEqual(curve.Evaluate(99.999f), curve.Evaluate(0f), 1e-2f, "continuous across the period");
            float slopeEnd = (curve.Evaluate(99.99f) - curve.Evaluate(99.98f)) / 0.01f;
            float slopeStart = (curve.Evaluate(0.01f) - curve.Evaluate(0f)) / 0.01f;
            Assert.AreEqual(slopeEnd, slopeStart, 0.05f, "smooth across the period");
        }

        [Test]
        public void PathIsContinuousAcrossTheLoopAndAdvances()
        {
            LookTestPath path = _layout.Path;
            float loop = _config.LoopLengthM;
            Assert.AreEqual(0f, path.LoopOffset.y, 1e-4f);
            Assert.Greater(path.LoopOffset.z, loop * 0.9f, "the loop moves forward");
            Vector3 before = path.Position(loop - 0.01);
            Vector3 after = path.Position(loop + 0.01);
            Assert.Less(Vector3.Distance(before, after), 0.05f);
            Assert.AreEqual(path.HeadingRad(loop - 0.01), path.HeadingRad(loop + 0.01), 1e-3f);
            Vector3 p = path.Position(37.0);
            Assert.Less(Vector3.Distance(p + path.LoopOffset * 2f, path.Position(37.0 + 2 * loop)), 1e-2f);
        }

        [Test]
        public void PathMovesOneMeterPerMeterHorizontally()
        {
            LookTestPath path = _layout.Path;
            for (double s = 0.0; s < _config.LoopLengthM; s += 7.0)
            {
                Vector3 a = path.Position(s);
                Vector3 b = path.Position(s + 1.0);
                float horizontal = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                Assert.AreEqual(1f, horizontal, 0.01f, "s " + s);
            }
        }

        [Test]
        public void PathBendsAndClimbsAsTheStrategyAsks()
        {
            float minHeading = float.MaxValue;
            float maxHeading = float.MinValue;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            for (float s = 0f; s < _config.LoopLengthM; s += 0.5f)
            {
                float h = _layout.Path.HeadingRad(s) * Mathf.Rad2Deg;
                minHeading = Mathf.Min(minHeading, h);
                maxHeading = Mathf.Max(maxHeading, h);
                minHeight = Mathf.Min(minHeight, _layout.TrailHeight(s));
                maxHeight = Mathf.Max(maxHeight, _layout.TrailHeight(s));
            }

            Assert.That(maxHeading - minHeading, Is.InRange(12f, 42f), "bends of 8-15° each way, the basin turn up to ~38°");
            Assert.That(maxHeight - minHeight, Is.InRange(3f, 8f), "climbs and drops");
        }

        [Test]
        public void RunnableWidthIsFlatExceptAtTheFord()
        {
            float run = _config.RunnableHalfWidthM;
            for (float s = 0f; s < _config.LoopLengthM; s += 3.1f)
            {
                if (_layout.PeriodicDistance(s, _config.RiverCrossSM) < 12f)
                {
                    continue;
                }

                for (float d = -run; d <= run; d += 0.7f)
                {
                    Assert.AreEqual(_layout.TrailHeight(s), _layout.GroundY(s, d), 1e-4f, "s " + s + ", d " + d);
                }
            }
        }

        [Test]
        public void GroundIsContinuousAcrossTheLoopSeam()
        {
            float loop = _config.LoopLengthM;
            for (float d = -_config.StripHalfWidthM; d <= _config.StripHalfWidthM; d += 2.3f)
            {
                Vector3 end = LookTestMeshFactory.GroundPoint(_layout, loop, d);
                Vector3 start = LookTestMeshFactory.GroundPoint(_layout, 0f, d) + _layout.Path.LoopOffset;
                Assert.Less(Vector3.Distance(end, start), 0.02f, "d " + d);
                Vector4 a = _layout.GroundWeights(0f, d);
                Vector4 b = _layout.GroundWeights(loop, d);
                Assert.Less((a - b).magnitude, 1e-3f, "d " + d);
            }
        }

        [Test]
        public void RiverCrossesTheTrailAtTheFordAnkleDeep()
        {
            float cross = _config.RiverCrossSM;
            Assert.AreEqual(0f, _layout.RiverD(cross), 1e-3f);
            Assert.Less(_layout.RiverD(cross - 20f), -5f, "comes from the left");
            Assert.Greater(_layout.RiverD(cross + 20f), 5f, "runs off to the right");
            float water = _layout.WaterY(0f);
            float bed = _layout.GroundY(cross, 0f);
            Assert.That(water - bed, Is.InRange(0.05f, 0.4f));
            Assert.Greater(_layout.RiverFallS(), cross, "the river falls off the plateau edge after the ford");
        }

        [Test]
        public void EnclosedAboutSeventyPercentOpenAtTheShots()
        {
            int enclosed = 0;
            int samples = 0;
            for (float s = 0f; s < _config.LoopLengthM; s += 0.5f, samples++)
            {
                if (_layout.Enclosure(s) > 0.5f)
                {
                    enclosed++;
                }
            }

            Assert.That(enclosed / (float)samples, Is.InRange(0.55f, 0.8f));
            Assert.Greater(_layout.Enclosure(20f), 0.9f, "F1 starts under the roots");
            Assert.Less(_layout.Enclosure(_config.RiverCrossSM), 0.3f, "the ford is open");
            Assert.Less(_layout.Enclosure(190f), 0.1f, "the basin is open");
        }

        [Test]
        public void CanopyCoverIsZeroAboveTheRoofAndOverTheBasinDrop()
        {
            Assert.AreEqual(0f, _layout.CanopyCover(20f, 0f, _config.CanopyRoofHeightM.y + 5f), 1e-4f);
            Assert.Greater(_layout.CanopyCover(20f, 0f, 0f), 0.8f);
            Assert.AreEqual(0f, _layout.CanopyCover(190f, 30f, 0f), 1e-4f);
        }

        [Test]
        public void TrailSoilIsNarrowerThanTheRunnableWidth()
        {
            Color center = _layout.GroundWeights(30f, 0f);
            Color edge = _layout.GroundWeights(30f, _config.RunnableHalfWidthM);
            Assert.Greater(center.r, 0.9f);
            Assert.Less(edge.r, 0.2f, "moss and roots cover the runnable edge");
        }

        [Test]
        public void WorldViewShiftsSegmentsByWholeLoops()
        {
            float segment = _config.SegmentLengthM;
            float loop = _config.LoopLengthM;
            float behind = _config.RecycleBehindM;
            for (double s = 0.0; s < 3 * loop; s += 11.0)
            {
                for (int i = 0; i < _config.SegmentCount; i++)
                {
                    long k = LookTestWorldView.LoopFor(i, s, segment, loop, behind);
                    double start = i * segment + k * (double)loop;
                    Assert.GreaterOrEqual(start + segment, s - behind - 1e-6, "segment not entirely behind the window");
                    Assert.Less(start, s - behind + loop, "segment starts inside the window");
                }
            }
        }

        [Test]
        public void NearDetailShowsOnlyAheadWithinRange()
        {
            Assert.IsTrue(LookTestWorldView.DetailVisible(100.0, 60.0, 60f));
            Assert.IsFalse(LookTestWorldView.DetailVisible(125.0, 60.0, 60f));
            Assert.IsTrue(LookTestWorldView.DetailVisible(40.0, 60.0, 60f), "segments around and behind the runner keep their detail");
        }

        [Test]
        public void SegmentsCoverTheWholeLoopInTheConfiguredLength()
        {
            Assert.AreEqual(25f, _config.SegmentLengthM, 1e-4f);
            Vector3 tile = _config.GroundTileM;
            foreach (float t in new[] { tile.x, tile.y, tile.z })
            {
                float repeats = _config.LoopLengthM / t;
                Assert.AreEqual(Mathf.Round(repeats), repeats, 1e-3f, "ground texture tiles a whole number of times per loop");
            }
        }
    }
}
