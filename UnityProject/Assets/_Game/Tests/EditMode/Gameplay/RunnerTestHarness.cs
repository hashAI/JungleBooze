using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Drives a <see cref="RunnerSimulation"/> tick by tick for tests and keeps every event it emits
    /// (the event ring buffer is drained after each step, like the views do).
    /// </summary>
    internal sealed class RunnerTestHarness
    {
        private readonly List<RunnerEvent> _events = new List<RunnerEvent>();

        public RunnerTestHarness(
            RunnerDesignValues values = null,
            SpeedCurve curve = null,
            ITrackQuery track = null,
            IHeadroomQuery headroom = null)
        {
            Config = RunnerConfig.FromDesignValues(values ?? RunnerDesignValues.CreateDefault());
            Sim = new RunnerSimulation(Config, curve ?? SpeedCurve.CreateDefault(), track, headroom);
        }

        public RunnerConfig Config { get; }

        public RunnerSimulation Sim { get; }

        public IReadOnlyList<RunnerEvent> Events => _events;

        public RunnerState State => Sim.Current;

        /// <summary>Tick that was processed by the last <see cref="Step"/> call.</summary>
        public long LastTick => Sim.NextTick - 1;

        /// <summary>Start values with the run-start ramp switched off and a constant speed curve.</summary>
        public static RunnerTestHarness ConstantSpeed(double speedMps, ITrackQuery track = null, IHeadroomQuery headroom = null)
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            return new RunnerTestHarness(values, SpeedCurve.CreateConstant(speedMps), track, headroom);
        }

        public static float LaneX(int lane)
        {
            return (lane - 1) * 2.4f;
        }

        /// <summary>Processes one tick with the given commands and returns the new state.</summary>
        public RunnerState Step(InputCommand commands = InputCommand.None)
        {
            Sim.Step(commands);
            RunnerEventBuffer buffer = Sim.Events;
            for (int i = 0; i < buffer.Count; i++)
            {
                _events.Add(buffer[i]);
            }

            buffer.Clear();
            return Sim.Current;
        }

        /// <summary>Steps with no input until the next tick to process is <paramref name="tick"/>.</summary>
        public void RunUntilNextTick(long tick)
        {
            while (Sim.NextTick < tick)
            {
                Step();
            }
        }

        /// <summary>Steps with no input until <paramref name="tick"/> (inclusive) has been processed.</summary>
        public void RunThrough(long tick)
        {
            RunUntilNextTick(tick + 1);
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
    }
}
