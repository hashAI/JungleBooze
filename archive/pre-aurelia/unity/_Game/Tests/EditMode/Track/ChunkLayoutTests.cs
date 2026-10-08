using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Track
{
    /// <summary>Spec 002 sections 4 and 5: mirroring (AC-204), coin generation (AC-205), kit boxes (AC-206, AC-207).</summary>
    public sealed class ChunkLayoutTests
    {
        private const float Eps = 1e-4f;

        private static ChunkData MirrorFixture()
        {
            return TrackFixtures.Normal(
                "FX-MIRROR",
                new[]
                {
                    ObstaclePlacement.Full(LaneMasks.Lane0 | LaneMasks.Lane1, 8f),
                    ObstaclePlacement.Mover(1, 2, 18f),
                    ObstaclePlacement.Gap(LaneMasks.Lane0, 28f, 3f),
                },
                new[]
                {
                    CoinPattern.Line(0, 2f, 6f),
                    CoinPattern.Trail(0, 2, 10f, 18f),
                    CoinPattern.Arc(2, 20f),
                    CoinPattern.Single(1.5f, 1.0f, 34f),
                });
        }

        // ---- AC-204 ----

        [Test]
        public void AC204_MirrorMapsLanesAndX_ForObstaclesMoversAndCoins()
        {
            ChunkData a = MirrorFixture();
            ChunkData m = a.Mirrored();

            Assert.AreEqual(LaneMasks.Lane1 | LaneMasks.Lane2, m.GetObstacle(0).LaneMask);
            Assert.AreEqual(LaneMasks.Lane1, m.GetObstacle(1).LaneMask, "mover start lane 1 stays 1");
            Assert.AreEqual(0, m.GetObstacle(1).MoverToLane, "mover end lane 2 -> 0");
            Assert.AreEqual(LaneMasks.Lane2, m.GetObstacle(2).LaneMask);
            Assert.AreEqual(a.GetObstacle(2).Zc, m.GetObstacle(2).Zc);
            Assert.AreEqual(a.GetObstacle(2).GapLengthM, m.GetObstacle(2).GapLengthM);

            Assert.AreEqual(2, m.GetCoinPattern(0).Lane);
            Assert.AreEqual(2, m.GetCoinPattern(1).Lane);
            Assert.AreEqual(0, m.GetCoinPattern(1).ToLane);
            Assert.AreEqual(0, m.GetCoinPattern(2).Lane);
            Assert.AreEqual(-1.5f, m.GetCoinPattern(3).X);
            Assert.AreEqual(1.0f, m.GetCoinPattern(3).Y);
        }

        [Test]
        public void AC204_MirroringTwice_GivesTheOriginal()
        {
            ChunkData a = MirrorFixture();
            Assert.AreEqual(a.ComputeHash(), a.Mirrored().Mirrored().ComputeHash());
            Assert.AreNotEqual(a.ComputeHash(), a.Mirrored().ComputeHash());
        }

        [Test]
        public void AC204_MirroredRunChunk_PlacesCoinsAtNegatedX()
        {
            // The same chunk spawned mirrored at runtime: every coin x is negated, z unchanged.
            ChunkData chunk = TrackFixtures.Normal("FX-C", new ObstaclePlacement[0], new[] { CoinPattern.Trail(0, 2, 10f, 18f) });
            RunnerConfig runner = RunnerConfig.CreateDefault();
            CoinConfig coins = CoinConfig.CreateDefault();
            var xs = new float[16];
            var ys = new float[16];
            var zs = new double[16];
            var mxs = new float[16];
            var mys = new float[16];
            var mzs = new double[16];
            CoinPattern p = chunk.GetCoinPattern(0);
            int n = CoinLayout.Generate(p, 100.0, 10.0, coins, runner, xs, ys, zs, 0);
            int mn = CoinLayout.Generate(p.Mirrored(), 100.0, 10.0, coins, runner, mxs, mys, mzs, 0);
            Assert.AreEqual(n, mn);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(-xs[i], mxs[i], Eps);
                Assert.AreEqual(zs[i], mzs[i], 1e-9);
            }
        }

        // ---- AC-205 ----

        [Test]
        public void AC205_Line_FiveCoinsInTheMiddleLaneAtCoinHeight()
        {
            var xs = new float[16];
            var ys = new float[16];
            var zs = new double[16];
            int n = CoinLayout.Generate(CoinPattern.Line(1, 4f, 12f), 0.0, 10.0, CoinConfig.CreateDefault(), RunnerConfig.CreateDefault(), xs, ys, zs, 0);
            Assert.AreEqual(5, n);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(0f, xs[i], Eps);
                Assert.AreEqual(0.75f, ys[i], Eps);
                Assert.AreEqual(4.0 + 2.0 * i, zs[i], 1e-6);
            }
        }

        [Test]
        public void AC205_ArcAt10mps_FollowsTheRealJumpParabola()
        {
            var xs = new float[16];
            var ys = new float[16];
            var zs = new double[16];
            const float center = 20f;
            int n = CoinLayout.Generate(CoinPattern.Arc(1, center), 0.0, 10.0, CoinConfig.CreateDefault(), RunnerConfig.CreateDefault(), xs, ys, zs, 0);
            Assert.AreEqual(7, n);
            Assert.AreEqual(center - 2.25, zs[0], 0.001);
            Assert.AreEqual(center + 2.25, zs[6], 0.001);
            Assert.AreEqual(1.406, ys[0], 0.001);
            Assert.AreEqual(1.406, ys[6], 0.001);
            Assert.AreEqual(center, zs[3], 0.001);
            Assert.AreEqual(2.25, ys[3], 0.001);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(0f, xs[i], Eps);
            }
        }

        [Test]
        public void AC205_Trail_MiddleCoinIsInTheMiddleLane()
        {
            var xs = new float[16];
            var ys = new float[16];
            var zs = new double[16];
            int n = CoinLayout.Generate(CoinPattern.Trail(0, 2, 10f, 18f), 0.0, 10.0, CoinConfig.CreateDefault(), RunnerConfig.CreateDefault(), xs, ys, zs, 0);
            Assert.AreEqual(5, n);
            Assert.AreEqual(14.0, zs[2], 1e-6);
            Assert.AreEqual(0f, xs[2], Eps);
            Assert.AreEqual(-2.4f, xs[0], Eps);
            Assert.AreEqual(2.4f, xs[4], Eps);
            Assert.AreEqual(0.75f, ys[2], Eps);
        }

        [Test]
        public void AC205_RunCoins_ArcUsesTheSpeedCurveAtItsWorldZ()
        {
            // An arc at z 30 on a constant 10 m/s curve spans 4.5 m in the generated run as well.
            ChunkData start = TrackFixtures.Start(200f, new ObstaclePlacement[0], new[] { CoinPattern.Arc(1, 30f) });
            TrackSimulation track = TrackFixtures.BuildTrack(TrackFixtures.Setup(start));
            int first = TrackFixtures.FindCoin(track, 27.75);
            int last = TrackFixtures.FindCoin(track, 32.25);
            Assert.GreaterOrEqual(first, 0);
            Assert.AreEqual(first + 6, last);
            Assert.AreEqual(1.406f, track.GetCoin(first).Y, 0.001f);
            Assert.AreEqual(1, track.GetCoin(first).Lane);
        }

        // ---- AC-206 ----

        [Test]
        public void AC206_BoxesFromTheKit_MatchTable51()
        {
            ChunkData start = TrackFixtures.Start(
                200f,
                new[]
                {
                    ObstaclePlacement.Low(LaneMasks.Lane1, 20f),
                    ObstaclePlacement.High(LaneMasks.Lane1, 40f),
                    ObstaclePlacement.Full(LaneMasks.Lane1, 60f),
                    ObstaclePlacement.Mover(1, 0, 80f),
                    ObstaclePlacement.Gap(LaneMasks.Lane1, 100f, 3f),
                });
            TrackSimulation track = TrackFixtures.BuildTrack(TrackFixtures.Setup(start));
            var boxes = new ObstacleBox[16];
            int n = track.GetBoxes(0.0, 150.0, boxes);
            Assert.AreEqual(4, n, "gaps produce no box");

            AssertBox(boxes[0], ObstacleArchetype.LowBarrier, 2.04f, 0.6f, 0.0f, 0.8f, 20.0);
            AssertBox(boxes[1], ObstacleArchetype.HighBarrier, 2.04f, 0.5f, 1.1f, 3.0f, 40.0);
            AssertBox(boxes[2], ObstacleArchetype.FullBlock, 2.04f, 1.0f, 0.0f, 3.0f, 60.0);
            AssertBox(boxes[3], ObstacleArchetype.Mover, 1.90f, 1.6f, 0.0f, 1.9f, 80.0);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(0f, boxes[i].CenterX, Eps, "lane 1 centre");
                Assert.IsFalse(boxes[i].IsMoving, "no box moves before any update");
            }

            Assert.Less(boxes[0].Id, boxes[1].Id);
            Assert.Less(boxes[2].Id, boxes[3].Id);
        }

        [Test]
        public void AC206_GetBoxes_FiltersByZAndRespectsTheBufferLength()
        {
            ChunkData start = TrackFixtures.Start(
                200f,
                new[] { ObstaclePlacement.Low(LaneMasks.All, 20f), ObstaclePlacement.Full(LaneMasks.Lane0, 40f) });
            TrackSimulation track = TrackFixtures.BuildTrack(TrackFixtures.Setup(start));
            var boxes = new ObstacleBox[8];
            Assert.AreEqual(3, track.GetBoxes(19.0, 20.0, boxes), "touching the front counts (closed intervals)");
            Assert.AreEqual(0, track.GetBoxes(20.61, 39.99, boxes));
            Assert.AreEqual(1, track.GetBoxes(41.0, 41.0, boxes), "touching the back counts");
            Assert.AreEqual(2, track.GetBoxes(0.0, 100.0, new ObstacleBox[2]), "never more than the buffer");
        }

        // ---- AC-207 ----

        [Test]
        public void AC207_TwoLaneFullBlock_TwoBoxesOneIdOnLaneCentres036Apart()
        {
            ChunkData start = TrackFixtures.Start(200f, new[] { ObstaclePlacement.Full(LaneMasks.Lane0 | LaneMasks.Lane1, 30f) });
            TrackSimulation track = TrackFixtures.BuildTrack(TrackFixtures.Setup(start));
            var boxes = new ObstacleBox[8];
            int n = track.GetBoxes(0.0, 100.0, boxes);
            Assert.AreEqual(2, n);
            Assert.AreEqual(boxes[0].Id, boxes[1].Id);
            Assert.AreEqual(-2.4f, boxes[0].CenterX, Eps);
            Assert.AreEqual(0f, boxes[1].CenterX, Eps);
            Assert.AreEqual(0.36f, boxes[1].XMin - boxes[0].XMax, 1e-4f);
            Assert.AreEqual(0, boxes[0].Lane);
            Assert.AreEqual(1, boxes[1].Lane);
        }

        private static void AssertBox(ObstacleBox b, ObstacleArchetype archetype, float width, float depth, float bottom, float top, double front)
        {
            Assert.AreEqual(archetype, b.Archetype);
            Assert.AreEqual(width, b.XMax - b.XMin, Eps, archetype + " width");
            Assert.AreEqual(depth, b.ZMax - b.ZMin, 1e-6, archetype + " depth");
            Assert.AreEqual(bottom, b.YMin, Eps, archetype + " bottom");
            Assert.AreEqual(top, b.YMax, Eps, archetype + " top");
            Assert.AreEqual(front, b.ZMin, 1e-9, archetype + " front");
        }
    }
}
