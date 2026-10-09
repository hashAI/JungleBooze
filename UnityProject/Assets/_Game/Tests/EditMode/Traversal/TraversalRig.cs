using System;
using JungleBooze.Core;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;

namespace JungleBooze.Tests.EditMode.Traversal
{
    /// <summary>A single code-built chunk on a <see cref="WorldPath"/> with a runner simulation (spec 103 traversal tests).</summary>
    internal sealed class TraversalRig
    {
        public const float Dt = 1f / 60f;

        private TraversalRig(ChunkLayoutBuilder layout, float length, RunOptions options, MovementConfig config)
        {
            var def = new ChunkDefinition { Id = "T_Traversal_01", Length = length };
            def.Variants.Add(layout.Variant);
            Chunk = new ChunkRuntime(def, 0, 0, 0);
            Path = new WorldPath();
            Path.Append(Chunk, new ChunkPick { Entry = 0, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            Events = new RunEventBuffer(8192);
            Sim = new RunnerSimulation(config ?? SpecConfig.Create(), Path, Dt, Events);
            Sim.Reset(options);
        }

        public ChunkRuntime Chunk { get; }

        public WorldPath Path { get; }

        public RunEventBuffer Events { get; }

        public RunnerSimulation Sim { get; }

        public ref readonly RunnerState State => ref Sim.State;

        public static TraversalRig Create(ChunkLayoutBuilder layout, float length, float speed, float startS = 0f, float startX = 0f, bool deepBreath = false, MovementConfig config = null)
        {
            return new TraversalRig(layout, length, new RunOptions
            {
                ForcedSpeed = speed,
                SkipStartRamp = true,
                StartS = startS,
                StartX = startX,
                DeepBreath = deepBreath,
                DeepDiveDepth = -2.5f,
                DeepDiveTime = 2.4f,
            }, config);
        }

        /// <summary>A 400 m pool: water everywhere (surface 0), riverbed −1.8, 9 m wide.</summary>
        public static ChunkLayoutBuilder Pool(float length = 400f)
        {
            var b = new ChunkLayoutBuilder("Pool", length);
            b.Width(0f, -4.5f, 4.5f).Width(length, -4.5f, 4.5f).Floor(0f, length, -1.8f, -1.8f).Water(0f, length, -4.5f, 4.5f);
            return b;
        }

        public void Step(InputCommand command = InputCommand.None, short mm = 0)
        {
            Sim.Step(new InputFrame(command, mm));
        }

        public void Steps(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Sim.Step(InputFrame.Empty);
            }
        }

        public bool StepUntil(Func<RunnerState, bool> condition, int max = 6000)
        {
            for (int i = 0; i < max; i++)
            {
                if (condition(Sim.State))
                {
                    return true;
                }

                Sim.Step(InputFrame.Empty);
            }

            return condition(Sim.State);
        }

        public int Count(RunEventType type)
        {
            return Events.CountOf(type);
        }

        public bool Has(RunEventType type, long tick)
        {
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Type == type && Events[i].Tick == tick)
                {
                    return true;
                }
            }

            return false;
        }

        public RunEvent Last(RunEventType type)
        {
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].Type == type)
                {
                    return Events[i];
                }
            }

            return default;
        }

        public static short Mm(float metres)
        {
            return InputFrame.ClampMm((long)Math.Round(metres * 1000.0));
        }
    }
}
