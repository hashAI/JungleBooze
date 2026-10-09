using JungleBooze.Core;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>The streamed path (spec 102 §2–3): seams, ids and slot reuse, route splits (AC-102-08).</summary>
    public sealed class WorldPathTests
    {
        private static ChunkLibrary SmallLibrary()
        {
            var plain = new ChunkDefinition { Id = "T_Plain_01", Length = 100f };
            var v = new ChunkVariant { Name = "Default" };
            v.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.Low, SMin = 50f, SMax = 50.6f, XMin = -3.5f, XMax = 3.5f, YMax = 0.5f });
            v.Coins.Add(CoinPattern.Line(10f, 90f, 1f, 2f));
            plain.Variants.Add(v);

            var fork = new ChunkDefinition { Id = "T_Fork_01", Length = 120f, Category = ChunkCategory.Branch };
            var f = new ChunkVariant { Name = "Default" };
            f.Widths.Add(new CourseWidthKey(0f, -3.5f, 3.5f));
            f.Widths.Add(new CourseWidthKey(20f, -4.5f, 4.5f));
            f.Widths.Add(new CourseWidthKey(100f, -4.5f, 4.5f));
            f.Widths.Add(new CourseWidthKey(114f, -3.5f, 3.5f));
            f.Widths.Add(new CourseWidthKey(120f, -3.5f, 3.5f));
            f.Dividers.Add(new ChunkDivider { SFront = 40f, SMerge = 90f, CenterX = 0f, HalfWidth = 0.6f, SafeSide = 1 });
            var risky = new ChunkRoute { Name = "Risky", Type = RouteType.Risky };
            risky.Steps.Add(new RouteStep(0, -1));
            var safe = new ChunkRoute { Name = "Safe", Type = RouteType.Safe };
            safe.Steps.Add(new RouteStep(0, 1));
            f.Routes.Add(risky);
            f.Routes.Add(safe);
            fork.Variants.Add(f);
            return new ChunkLibrary(new[] { plain, fork });
        }

        private static ChunkPick Pick(int entry)
        {
            return new ChunkPick { Entry = entry, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 };
        }

        [Test]
        public void Append_PlacesChunksEndToEnd_IdsIncreaseAcrossSeams()
        {
            ChunkLibrary lib = SmallLibrary();
            var path = new WorldPath();
            path.Append(lib.GetEntry(0), Pick(0));
            path.Append(lib.GetEntry(1), Pick(1));
            path.Append(lib.GetEntry(0), Pick(0));
            Assert.AreEqual(320f, path.EndS);
            Assert.AreEqual(1, path.ChunkAt(150f));
            var ids = new int[8];
            int n = path.FindObstacles(0f, 400f, ids);
            Assert.AreEqual(2, n);
            Assert.AreEqual(50f, path.GetObstacle(ids[0]).SMin);
            Assert.AreEqual(270f, path.GetObstacle(ids[1]).SMin);
            Assert.Less(ids[0], ids[1]);
            Assert.AreEqual(1, path.ForkCount);
            Assert.AreEqual(140f, path.GetFork(0).SFront);
            path.GetLateralBounds(100f, 0f, out float a, out float b);
            Assert.AreEqual(7f, b - a, 1e-4f, "seam 7.0 m");
        }

        [Test]
        public void Retire_DropsChunksBehind_AndKeepsQueriesWorking()
        {
            ChunkLibrary lib = SmallLibrary();
            var path = new WorldPath();
            for (int i = 0; i < 6; i++)
            {
                path.Append(lib.GetEntry(0), Pick(0));
            }

            path.Retire(250f);
            Assert.AreEqual(2, path.FirstChunkSerial);
            var ids = new int[8];
            int n = path.FindObstacles(0f, 1000f, ids);
            Assert.AreEqual(4, n);
            Assert.AreEqual(250f, path.GetObstacle(ids[0]).SMin);
        }

        [Test]
        public void SlotReuse_DoesNotLeakCollectedOrResolvedState()
        {
            // Stream far beyond the coin ring capacity with a simulation running on it: every coin is collectable.
            ChunkLibrary lib = SmallLibrary();
            var path = new WorldPath();
            var sim = new RunnerSimulation(SpecConfig.Create(), path, 1f / 60f, new RunEventBuffer(64)) { MuteEvents = true };
            path.Append(lib.GetEntry(0), Pick(0));
            path.Append(lib.GetEntry(0), Pick(0));
            sim.Reset(new RunOptions { ForcedSpeed = 16f, SkipStartRamp = true, StartX = 1f });
            int appended = 2;
            int coinsPlaced = 2 * lib.GetEntry(0).CoinCount;
            while (sim.State.S < 100f * 120f)
            {
                InputCommand jump = sim.State.Grounded && path.FindObstacles(sim.State.S + 2.5f, sim.State.S + 3.0f, new int[4]) > 0 ? InputCommand.Jump : InputCommand.None;
                sim.Step(InputFrame.FromCommands(jump));
                if (path.EndS < sim.State.S + 200f)
                {
                    path.Retire(sim.State.S - 50f);
                    path.Append(lib.GetEntry(0), Pick(0));
                    appended++;
                    coinsPlaced += lib.GetEntry(0).CoinCount;
                }
            }

            Assert.Greater(path.NextCoinId, WorldPath.CoinCapacity, "ids wrapped the ring");
            Assert.AreEqual(0, sim.State.Hits, "every obstacle is fresh (no stale 'resolved' stamps)");
            Assert.Greater(sim.State.Coins, (int)(coinsPlaced * 0.9f) - 120, "coins on reused slots are collectable");
        }

        [Test]
        public void AC102_08_ForkSide_ByXAtTheDividerFront_TieIsSafe_NudgedNeverCrashed()
        {
            ChunkLibrary lib = SmallLibrary();
            ChunkRuntime c = lib.GetEntry(1);
            Assert.AreEqual(RouteType.Risky, c.RouteAt(50f, -0.01f));
            Assert.AreEqual(RouteType.Safe, c.RouteAt(50f, 0.01f));
            Assert.AreEqual(RouteType.Safe, c.RouteAt(50f, 0f), "tie → safe");

            // A runner aimed straight at the divider is nudged to a side, never crashes (divider fronts aren't obstacles).
            var path = new WorldPath();
            path.Append(lib.GetEntry(1), Pick(1));
            var events = new RunEventBuffer(256);
            var sim = new RunnerSimulation(SpecConfig.Create(), path, 1f / 60f, events);
            sim.Reset(new RunOptions { ForcedSpeed = 14f, SkipStartRamp = true, StartX = 0f });
            while (sim.State.S < 100f)
            {
                sim.Step(InputFrame.Empty);
            }

            Assert.IsFalse(sim.State.Dead);
            Assert.AreEqual(0, sim.State.Hits);
            Assert.AreEqual(1, events.CountOf(RunEventType.ForkNudge));
            Assert.Greater(System.Math.Abs(sim.State.X), 0.6f);
        }

        [Test]
        public void CoinDensity_Low_ThinsTo70Percent()
        {
            ChunkLibrary lib = SmallLibrary();
            var path = new WorldPath();
            ChunkPick low = Pick(0);
            low.CoinDensity = 0.7f;
            path.Append(lib.GetEntry(0), low);
            int full = lib.GetEntry(0).CoinCount;
            Assert.AreEqual((int)(full * 0.7f), path.NextCoinId, 1);
        }
    }
}
