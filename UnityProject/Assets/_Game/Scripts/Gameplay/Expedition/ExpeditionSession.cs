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
        }

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

        /// <summary>Plans the opening chunks and starts the run at Ready (instant restart).</summary>
        public void BeginRun(in ExpeditionRunSetup setup)
        {
            Setup = setup;
            Events.Clear();
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
            Run.Restart(new RunOptions
            {
                FirstRun = setup.FirstExpedition,
                ForcedSpeed = setup.ForcedSpeed,
                SkipStartRamp = setup.ForcedSpeed > 0f,
            });
            Tracker.BeginRun(setup.Owned, setup.Discovered);
        }

        public void Step(InputFrame frame)
        {
            RunnerState before = Run.Simulation.State;
            Run.Step(frame);
            ref readonly RunnerState now = ref Run.Simulation.State;
            Tracker.AfterStep(before, now);
            Streamer.Update(now.S);
        }
    }
}
