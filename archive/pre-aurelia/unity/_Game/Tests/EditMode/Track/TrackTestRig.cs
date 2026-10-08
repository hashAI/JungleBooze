using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Tests.EditMode.Track
{
    /// <summary>
    /// Drives a <see cref="TrackRunWorld"/> (runner + generated track + scoring) tick by tick for tests and keeps
    /// every event the runner buffer received (track, coin and score events included), like the views do.
    /// </summary>
    internal sealed class TrackTestRig
    {
        private readonly List<RunnerEvent> _events = new List<RunnerEvent>();

        public TrackTestRig(TrackRunSetup setup, ulong seed, RunnerConfig config, SpeedCurve curve)
        {
            Config = config;
            Curve = curve;
            World = new TrackRunWorld(setup, seed);
            World.CreateRunner(config, curve);
        }

        public RunnerConfig Config { get; }

        public SpeedCurve Curve { get; }

        public TrackRunWorld World { get; }

        public RunnerSimulation Sim => World.Runner;

        public TrackSimulation Track => World.Track;

        public RunScoring Scoring => World.Scoring;

        public RunnerState State => Sim.Current;

        public IReadOnlyList<RunnerEvent> Events => _events;

        /// <summary>Tick processed by the last <see cref="Step"/>.</summary>
        public long LastTick => Sim.NextTick - 1;

        /// <summary>Default library and configs, speed curve and run-start ramp as in the spec start values.</summary>
        public static TrackTestRig Default(ulong seed)
        {
            return new TrackTestRig(TrackRunSetup.CreateDefault(), seed, RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
        }

        /// <summary>Constant speed, run-start ramp off (as <c>RunnerTestHarness.ConstantSpeed</c>).</summary>
        public static TrackTestRig ConstantSpeed(TrackRunSetup setup, double speedMps, ulong seed = 1UL)
        {
            return new TrackTestRig(setup, seed, RampOffConfig(), SpeedCurve.CreateConstant(speedMps));
        }

        public static RunnerConfig RampOffConfig()
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            return RunnerConfig.FromDesignValues(values);
        }

        public static float LaneX(int lane)
        {
            return (lane - 1) * 2.4f;
        }

        public RunnerState Step(InputCommand commands = InputCommand.None)
        {
            long tick = Sim.NextTick;
            Sim.Step(commands);
            World.AfterRunnerStep(tick, Sim);
            RunnerEventBuffer buffer = Sim.Events;
            for (int i = 0; i < buffer.Count; i++)
            {
                _events.Add(buffer[i]);
            }

            buffer.Clear();
            return Sim.Current;
        }

        /// <summary>Steps with no input until <paramref name="tick"/> (inclusive) has been processed or HERO died.</summary>
        public void RunThrough(long tick)
        {
            while (Sim.NextTick <= tick && !Sim.Current.IsDead)
            {
                Step();
            }
        }

        /// <summary>Steps with no input until HERO's z reaches <paramref name="z"/> or HERO died.</summary>
        public void RunUntilZ(double z)
        {
            while (Sim.Current.Z < z && !Sim.Current.IsDead)
            {
                Step();
            }
        }

        public List<RunnerEvent> EventsOf(RunnerEventType type)
        {
            var result = new List<RunnerEvent>();
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Type == type)
                {
                    result.Add(_events[i]);
                }
            }

            return result;
        }

        public int CountOf(RunnerEventType type)
        {
            return EventsOf(type).Count;
        }

        public void ClearEvents()
        {
            _events.Clear();
        }
    }
}
