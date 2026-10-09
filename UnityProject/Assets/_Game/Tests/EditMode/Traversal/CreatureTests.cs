using System.Collections.Generic;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Traversal
{
    /// <summary>The sailback (spec 103 §7, AC-103-27…29).</summary>
    public sealed class CreatureTests
    {
        private sealed class Sink : ICreatureDiscoverySink
        {
            public readonly List<long> Ticks = new List<long>();

            public void OnCreatureObserved(string entryId, int chunkSerial, long tick)
            {
                Ticks.Add(tick);
            }
        }

        private static SailbackSystem Make(float perchX, out WorldPath path, out Sink sink, float launch = 0f)
        {
            var b = new ChunkLayoutBuilder("Perch", 300f);
            b.Creature("D-02", 150f, perchX, 3f, 1, 0f, 120f, 4f, 4f, launch, false, "test");
            var def = new ChunkDefinition { Id = "T_Perch_01", Length = 300f };
            def.Variants.Add(b.Variant);
            path = new WorldPath();
            path.Append(new ChunkRuntime(def, 0, 0, 0), new ChunkPick { CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            sink = new Sink();
            return new SailbackSystem(path, new SailbackConfig(), 1f / 60f, new RunEventBuffer(256)) { Sink = sink };
        }

        private static RunnerState Runner(long tick, float s, float x)
        {
            return new RunnerState { Tick = tick, S = s, X = x, Speed = 10f };
        }

        [Test]
        public void AC103_27_StatesFollowDistance_StartleWithin2m()
        {
            SailbackSystem creatures = Make(2.8f, out _, out _);
            var seen = new List<CreatureState>();
            var at = new List<float>();
            float s = 100f;
            for (long t = 1; t < 60 * 20; t++)
            {
                s += 10f / 60f;
                creatures.Step(Runner(t, s, 0f));
                CreatureState state = creatures.Get(0).State;
                if (seen.Count == 0 || seen[seen.Count - 1] != state)
                {
                    seen.Add(state);
                    at.Add(creatures.Get(0).S - s);
                }
            }

            CollectionAssert.AreEqual(new[] { CreatureState.Perched, CreatureState.Alert, CreatureState.Launch, CreatureState.Glide, CreatureState.Gone }, seen);
            Assert.AreEqual(22f, at[1], 0.2f, "Alert at Δs 22");
            Assert.LessOrEqual(at[2], 14.2f, "Launch at Δs ≤ 14 (after ≥ 0.4 s of alert)");

            // Startle: passing within 2 m laterally alerts at once (Δs 28 here) and launches after the alert.
            SailbackSystem close = Make(1.5f, out _, out _);
            s = 120f;
            close.Step(Runner(1, s, 0f));
            close.Step(Runner(2, s + 0.1f, 0f));
            Assert.AreEqual(CreatureState.Alert, close.Get(0).State);
            for (long t = 3; t < 30; t++)
            {
                close.Step(Runner(t, s, 0f));
            }

            Assert.AreEqual(CreatureState.Launch, close.Get(0).State, "a startle launches when the alert ends");
        }

        [Test]
        public void AC103_28_DiscoveryFiresOnTheTickObservedTimeReaches48()
        {
            SailbackSystem creatures = Make(2.8f, out _, out Sink sink);
            int observed = 0;
            long expected = -1;
            float s = 80f;
            for (long t = 1; t < 60 * 30; t++)
            {
                s += 10f / 60f;
                RunnerState r = Runner(t, s, 0f);
                creatures.Step(r);
                Sailback a = creatures.Get(0);
                float ds = a.S - s;
                bool isObserved = a.State != CreatureState.Gone && a.State != CreatureState.Inactive && ds >= 2f && ds <= 35f && System.Math.Abs(a.X) <= 10f;
                if (isObserved && observed < 48)
                {
                    observed++;
                    if (observed == 48)
                    {
                        expected = t;
                    }
                }
            }

            Assert.AreEqual(1, sink.Ticks.Count, "once per spawn group");
            Assert.AreEqual(expected, sink.Ticks[0], "on the tick the cumulative observed time reaches 48 ticks");
        }

        [Test]
        public void AC103_29_NeverBelow25mOverThePath()
        {
            // A glide line that dives toward the path: clamped to 2.5 m above the floor wherever it is over the path.
            var b = new ChunkLayoutBuilder("Low", 300f);
            b.Creature("D-02", 100f, 3.2f, 3f, 3, 3f, 80f, 0f, -1f, 0f, false, "dives");
            var def = new ChunkDefinition { Id = "T_Low_01", Length = 300f };
            def.Variants.Add(b.Variant);
            var path = new WorldPath();
            path.Append(new ChunkRuntime(def, 0, 0, 0), new ChunkPick { CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            var creatures = new SailbackSystem(path, new SailbackConfig(), 1f / 60f, null);
            float s = 60f;
            for (long t = 1; t < 60 * 30; t++)
            {
                s += 10f / 60f;
                creatures.Step(Runner(t, s, 0f));
                for (int i = 0; i < SailbackSystem.Capacity; i++)
                {
                    Sailback a = creatures.Get(i);
                    if (a.State == CreatureState.Inactive)
                    {
                        continue;
                    }

                    path.GetOuterBounds(a.S, out float x0, out float x1);
                    if (a.X >= x0 && a.X <= x1 && path.TryGetFloor(a.S, a.X, out float floor))
                    {
                        Assert.GreaterOrEqual(a.Y - floor, 2.5f - 1e-4f, "tick " + t);
                    }
                }
            }
        }
    }
}
