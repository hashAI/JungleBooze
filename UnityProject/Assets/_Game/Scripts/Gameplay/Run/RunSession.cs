using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Run
{
    /// <summary>
    /// Deterministic run lifecycle around <see cref="RunnerSimulation"/>: a short ready beat, the run, a dying or
    /// finishing coast, then results. Instant restart goes straight back to Ready. Stepped once per fixed tick
    /// with that tick's input; inputs outside Running are ignored. Plain C#, no allocation per step.
    /// </summary>
    public sealed class RunSession
    {
        private readonly int _readyTicks;
        private readonly int _dyingTicks;
        private readonly int _fallDyingTicks;
        private readonly int _finishTicks;
        private readonly int _reviveReadyTicks;
        private int _readyLimit;
        private RunOptions _options;

        public RunSession(MovementConfig config, IPathQuery path, ITimeSource time, RunEventBuffer events)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            Simulation = new RunnerSimulation(config, path, time.DeltaTime, events);
            float dt = time.DeltaTime;
            RunFlowConfig flow = config.Flow;
            _readyTicks = Math.Max(0, (int)Math.Round(flow.ReadyTime / dt));
            _dyingTicks = Math.Max(1, (int)Math.Round(flow.DyingTime / dt));
            _fallDyingTicks = Math.Max(1, (int)Math.Round(flow.FallDyingTime / dt));
            _finishTicks = Math.Max(1, (int)Math.Round(flow.FinishCoastTime / dt));
            _reviveReadyTicks = Math.Max(0, (int)Math.Round(flow.ReviveReadyTime / dt));
            Phase = RunPhase.Results;
        }

        public RunnerSimulation Simulation { get; }

        public RunPhase Phase { get; private set; }

        /// <summary>Ticks spent in the current phase.</summary>
        public int PhaseTicks { get; private set; }

        /// <summary>Session steps since the last restart (the replay clock).</summary>
        public long SessionTick { get; private set; }

        /// <summary>Number of runs started.</summary>
        public int RunCount { get; private set; }

        public bool Finished => Simulation.State.Finished;

        /// <summary>Run time in seconds up to the finish or death (or now).</summary>
        public float RunSeconds
        {
            get
            {
                ref readonly RunnerState s = ref Simulation.State;
                long end = s.Dead ? s.DeathTick : s.Finished ? s.FinishTick : s.Tick;
                return end * Simulation.StepSeconds;
            }
        }

        /// <summary>Starts a new run at Ready (instant restart).</summary>
        public void Restart(RunOptions options)
        {
            _options = options;
            Simulation.Reset(options);
            _readyLimit = _readyTicks;
            Phase = _readyTicks > 0 ? RunPhase.Ready : RunPhase.Running;
            PhaseTicks = 0;
            SessionTick = 0;
            RunCount++;
        }

        public void Step(InputFrame frame)
        {
            SessionTick++;
            PhaseTicks++;
            switch (Phase)
            {
                case RunPhase.Ready:
                    if (PhaseTicks >= _readyLimit)
                    {
                        SetPhase(RunPhase.Running);
                    }

                    break;

                case RunPhase.Running:
                    Simulation.Step(frame);
                    if (Simulation.State.Dead)
                    {
                        SetPhase(RunPhase.Dying);
                    }
                    else if (Simulation.State.Finished)
                    {
                        SetPhase(RunPhase.Finishing);
                    }

                    break;

                case RunPhase.Finishing:
                    // Coast: Pista keeps running; steering stays live, actions are ignored.
                    Simulation.Step(new InputFrame(InputCommand.None, frame.LateralDeltaMm));
                    if (PhaseTicks >= _finishTicks)
                    {
                        SetPhase(RunPhase.Results);
                    }

                    break;

                case RunPhase.Dying:
                    Simulation.Step(InputFrame.Empty);
                    int limit = Simulation.State.Cause == DeathCause.Fall ? _fallDyingTicks : _dyingTicks;
                    if (PhaseTicks >= limit)
                    {
                        SetPhase(RunPhase.Results);
                    }

                    break;
            }
        }

        /// <summary>
        /// Revive after death (GDD §11), only while dying (review N2: once the results show, the run is banked).
        /// The run resumes after the revive ready beat (<see cref="RunFlowConfig.ReviveReadyTime"/>, Ready phase).
        /// Returns false when not dying.
        /// </summary>
        public bool TryRevive()
        {
            if (Phase != RunPhase.Dying || !Simulation.Revive())
            {
                return false;
            }

            _readyLimit = _reviveReadyTicks;
            SetPhase(_reviveReadyTicks > 0 ? RunPhase.Ready : RunPhase.Running);
            return true;
        }

        private void SetPhase(RunPhase phase)
        {
            Phase = phase;
            PhaseTicks = 0;
        }
    }
}
