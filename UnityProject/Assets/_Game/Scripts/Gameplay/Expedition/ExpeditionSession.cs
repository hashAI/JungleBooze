using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// One expedition run, deterministic and plain C# (spec 102–103): the streamed <see cref="WorldPath"/> with its
    /// <see cref="WorldDirector"/>, the run lifecycle (<see cref="RunSession"/> around the simulation), and the
    /// <see cref="RunTracker"/>. Per tick: lifecycle/simulation step → tracker → streaming. Bots, tests, the scene
    /// and the video tool all drive it the same way. No allocation per tick after <see cref="BeginRun"/>.
    /// </summary>
    public sealed class ExpeditionSession
    {
        private readonly ExpeditionContent _content;

        public ExpeditionSession(ExpeditionContent content, MovementConfig movement, ITimeSource time, RunEventBuffer events, bool requireValidated)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            if (movement == null)
            {
                throw new ArgumentNullException(nameof(movement));
            }

            Events = events ?? new RunEventBuffer(256);
            Path = new WorldPath();
            Director = new WorldDirector(content.Library, content.Director, movement.Speed, time.DeltaTime, requireValidated);
            Streamer = new WorldStreamer(Path, Director);
            Run = new RunSession(movement, Path, time, Events);
            Tracker = new RunTracker(Path, Run.Simulation, Director, content, Events);
            Creatures = new SailbackSystem(Path, content.Sailback, time.DeltaTime, Events) { Sink = Tracker };
        }

        /// <summary>The slice creature (spec 103 §7), ticked after the tracker.</summary>
        public SailbackSystem Creatures { get; }

        public ExpeditionContent Content => _content;

        public RunEventBuffer Events { get; }

        public WorldPath Path { get; }

        public WorldDirector Director { get; }

        public WorldStreamer Streamer { get; }

        public RunSession Run { get; }

        public RunTracker Tracker { get; }

        public RunnerSimulation Simulation => Run.Simulation;

        public RunPhase Phase => Run.Phase;

        public RunStats Stats => Tracker.Stats;

        public ExpeditionRunSetup Setup { get; private set; }

        public bool FirstExpedition => Setup.FirstExpedition;

        /// <summary>
        /// Optional replay recording (format 3): <see cref="BeginRun"/> restarts it with the run's seed and setup,
        /// <see cref="Step"/> records the input of Running/Finishing ticks, <see cref="Revive"/> adds a marker.
        /// </summary>
        public InputRecording Recording { get; set; }

        /// <summary>Plans the opening chunks and starts the run at Ready (instant restart).</summary>
        public void BeginRun(in ExpeditionRunSetup setup)
        {
            Setup = setup;
            Events.Clear();
            Recording?.Restart(setup.Seed, ExpeditionReplay.ToReplaySetup(setup, _content));
            var director = new DirectorRunSetup
            {
                Seed = setup.Seed,
                Script = setup.FirstExpedition ? _content.Script : null,
                ShortStart = !setup.FirstExpedition,
                Owned = setup.Owned,
                PendingShowcase = setup.PendingShowcase,
                Skill = setup.Skill,
            };
            Streamer.BeginRun(director);
            AbilityDefinition deepBreath = _content.FindAbility(AbilityFlags.DeepBreath);
            Run.Restart(new RunOptions
            {
                FirstRun = setup.FirstExpedition,
                ForcedSpeed = setup.ForcedSpeed,
                SkipStartRamp = setup.ForcedSpeed > 0f,
                DeepBreath = (setup.Owned & AbilityFlags.DeepBreath) != 0,
                DeepDiveDepth = deepBreath != null ? deepBreath.DeepDiveDepth : -2.5f,
                DeepDiveTime = deepBreath != null ? deepBreath.DeepDiveTime : 2.4f,
            });
            Tracker.BeginRun(setup.Owned, setup.PendingShowcase, setup.Discovered);
            Creatures.BeginRun();
        }

        public void Step(InputFrame frame)
        {
            RunPhase phase = Run.Phase;
            if (Recording != null && (phase == RunPhase.Running || phase == RunPhase.Finishing))
            {
                Recording.Add(Run.SessionTick, frame);
            }

            RunnerState before = Run.Simulation.State;
            Run.Step(frame);
            ref readonly RunnerState now = ref Run.Simulation.State;
            Tracker.AfterStep(before, now);
            if (now.Tick != before.Tick)
            {
                Creatures.Step(now);
            }

            Streamer.Update(now.S);
        }

        /// <summary>
        /// Revives the dead runner (no payment checks: see <see cref="ReviveRules.TryRevive"/>) and books the cost.
        /// Recorded as a replay marker on the current session tick. False when not dying.
        /// </summary>
        public bool Revive(int cost)
        {
            long tick = Run.SessionTick;
            if (!Run.TryRevive())
            {
                return false;
            }

            RunStats stats = Tracker.Stats;
            stats.ReviveCrystals += cost;
            stats.Revives++;
            stats.Dead = false;
            Tracker.OnRevived();
            Recording?.AddMarker(tick, ReplayMarkerKind.Revive);
            return true;
        }
    }
}
