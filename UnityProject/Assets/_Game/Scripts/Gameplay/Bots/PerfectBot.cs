using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Bots
{
    /// <summary>
    /// The "Perfect" bot of spec 101 §6.2 (0 ms reaction, exact). It drives the real simulation through
    /// <see cref="IInputProvider"/>, so it proves a course is clearable with the shipped mechanics.
    ///
    /// Steering: a stateless policy picks a free x around the nearest steer-only obstacle (blockers, thorns), else
    /// follows the next coin, else holds the chosen fork branch. Jumps and slides: each tick with nothing scheduled
    /// it looks ahead with a probe copy of the simulation; if doing nothing leads to a hit or death, it tries the
    /// best action (jump or slide) at every delay up to that moment and schedules the action in the middle of the
    /// first window that passes the threat cleanly (maximum timing margin). Allocation-free after construction.
    /// Cost: one planning tick can run up to ~90 × 240 probe steps, so a frame where the bot (re)plans can spike on a
    /// phone. It is a test/tool driver (CI, videos, the B key): keep it out of frame-pacing measurements (M7).
    /// </summary>
    public sealed class PerfectBot : IInputProvider
    {
        private const int Horizon = 150;

        /// <summary>Plan only when the threat is this close (ticks), so the whole action window is searched.</summary>
        private const int PlanLead = 90;
        private const float SteerMargin = 0.15f;
        private const float ThreatPassMargin = 0.6f;

        private readonly RunnerSimulation _live;
        private readonly RunnerSimulation _probe;
        private readonly IPathQuery _path;
        private readonly int[] _ids = new int[32];
        private readonly int[] _overlapIds = new int[32];
        private InputCommand _scheduled;
        private long _scheduledTick = -1;
        private float _gaveUpThreat = float.NaN;

        public PerfectBot(RunnerSimulation live, bool preferRiskyBranch)
        {
            _live = live ?? throw new ArgumentNullException(nameof(live));
            _path = live.Path;
            _probe = new RunnerSimulation(live.Config, live.Path, live.StepSeconds, new RunEventBuffer(4)) { MuteEvents = true };
            PreferRiskyBranch = preferRiskyBranch;
        }

        public bool PreferRiskyBranch { get; set; }

        /// <summary>Optional decision log for debugging (allocates only when set).</summary>
        public Action<string> Trace { get; set; }

        /// <summary>Probe steps run so far (cost metric).</summary>
        public long ProbeSteps { get; private set; }

        /// <summary>Threats for which no clean action was found.</summary>
        public int GaveUp { get; private set; }

        public void Reset()
        {
            _scheduled = InputCommand.None;
            _scheduledTick = -1;
            _gaveUpThreat = float.NaN;
            GaveUp = 0;
        }

        public InputFrame ReadInput(long tick)
        {
            ref readonly RunnerState state = ref _live.State;
            if (state.Dead)
            {
                return InputFrame.Empty;
            }

            long next = state.Tick + 1;
            short mm = SteerDelta(_live);
            InputCommand command = InputCommand.None;
            if (_scheduledTick >= 0)
            {
                if (next >= _scheduledTick)
                {
                    command = _scheduled;
                    _scheduledTick = -1;
                }
            }
            else
            {
                command = Plan(next);
            }

            return new InputFrame(command, mm);
        }

        private InputCommand Plan(long next)
        {
            int failAt = Rollout(-1, InputCommand.None, float.PositiveInfinity, Horizon, out float threatEnd, out ObstacleClass threatClass, out bool isFall);
            if (failAt < 0 || failAt > PlanLead)
            {
                return InputCommand.None;
            }

            InputCommand first = !isFall && threatClass == ObstacleClass.High ? InputCommand.Slide : InputCommand.Jump;
            InputCommand second = first == InputCommand.Jump ? InputCommand.Slide : InputCommand.Jump;
            if (TryWindow(first, failAt, threatEnd, out int delay) || TryWindow(second, failAt, threatEnd, out delay))
            {
                InputCommand action = _scheduled;
                Trace?.Invoke("plan at s " + _live.State.S + ": " + action + " in " + delay + " ticks (failAt " + failAt + ", fall " + isFall + ", end " + threatEnd + ")");
                if (delay == 0)
                {
                    return action;
                }

                _scheduledTick = next + delay;
                return InputCommand.None;
            }

            if (!(threatEnd == _gaveUpThreat))
            {
                _gaveUpThreat = threatEnd;
                GaveUp++;
            }

            Trace?.Invoke("gave up at s " + _live.State.S + ": failAt " + failAt + " fall " + isFall + " class " + threatClass + " end " + threatEnd);
            return InputCommand.None;
        }

        private bool TryWindow(InputCommand action, int failAt, float threatEnd, out int delay)
        {
            int maxDelay = failAt;
            int start = -1;
            int end = -1;
            for (int d = 0; d <= maxDelay; d++)
            {
                bool ok = Rollout(d, action, threatEnd, Horizon + d, out _, out _, out _) < 0;
                if (ok && start < 0)
                {
                    start = d;
                }

                if (start >= 0)
                {
                    if (ok)
                    {
                        end = d;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (start < 0)
            {
                delay = 0;
                return false;
            }

            delay = (start + end) / 2;
            _scheduled = action;
            return true;
        }

        /// <summary>
        /// Simulates from the live state with the steering policy and <paramref name="action"/> at
        /// <paramref name="delay"/> (−1 = none). Returns the tick offset of the first hit/death before s passes
        /// <paramref name="passS"/>, or −1 if clean.
        /// </summary>
        private int Rollout(int delay, InputCommand action, float passS, int horizon, out float threatEnd, out ObstacleClass threatClass, out bool isFall)
        {
            threatEnd = 0f;
            threatClass = ObstacleClass.Low;
            isFall = false;
            _probe.CopyFrom(_live);
            int hits = _probe.State.Hits;
            for (int k = 0; k < horizon; k++)
            {
                if (_probe.State.S > passS || _probe.State.Finished)
                {
                    return -1;
                }

                InputCommand command = k == delay ? action : InputCommand.None;
                _probe.Step(new InputFrame(command, SteerDelta(_probe)));
                ProbeSteps++;
                ref readonly RunnerState s = ref _probe.State;
                if (s.Hits > hits || s.Dead)
                {
                    int obstacle = s.Dead && s.Cause == DeathCause.Fall ? -1 : s.LastHitObstacle;
                    if (obstacle >= 0)
                    {
                        ObstacleBox box = _path.GetObstacle(obstacle);
                        threatEnd = box.SMax + ThreatPassMargin;
                        threatClass = box.Class;
                    }
                    else
                    {
                        isFall = true;
                        threatEnd = _path.TryFindFloorAhead(s.S, s.X, 40f, out float lip, out _) ? lip + ThreatPassMargin : s.S + 6f;
                    }

                    return k;
                }
            }

            return -1;
        }

        private short SteerDelta(RunnerSimulation sim)
        {
            float target = SteerTarget(sim);
            double mm = Math.Round((target - sim.State.XTarget) * 1000.0);
            return InputFrame.ClampMm((long)mm);
        }

        /// <summary>Stateless steering policy.</summary>
        private float SteerTarget(RunnerSimulation sim)
        {
            ref readonly RunnerState st = ref sim.State;
            float margin = sim.Config.Lateral.EdgeMargin;
            float halfWidth = sim.Config.Hitbox.Width * 0.5f;
            float speed = Math.Max(8f, st.Speed);
            float s = st.S;

            // Preferred x: fork branch, then the next coin, else hold.
            float preferred = st.XTarget;
            for (int i = 0; i < _path.ForkCount; i++)
            {
                ForkPoint fork = _path.GetFork(i);
                if (s > fork.SFront - 60f && s < fork.SMerge)
                {
                    int side = PreferRiskyBranch ? -fork.SafeSide : fork.SafeSide;
                    float probeX = fork.DividerCenterX + (side * (fork.DividerHalfWidth + 1f));
                    if (s >= fork.SFront)
                    {
                        // Inside: stay in the branch we are in.
                        probeX = st.X;
                    }

                    _path.GetLateralBounds(Math.Max(s, fork.SFront) + 0.5f, probeX, out float bMin, out float bMax);
                    preferred = (bMin + bMax) * 0.5f;
                }
            }

            int coins = _path.FindCoins(s + 0.5f, s + (speed * 0.9f), _ids);
            for (int i = 0; i < coins; i++)
            {
                int id = _ids[i];
                if (!sim.IsCoinCollected(id))
                {
                    CoinPoint coin = _path.GetCoin(id);
                    _path.GetLateralBounds(coin.S, st.X, out float cMin, out float cMax);
                    if (coin.X >= cMin && coin.X <= cMax)
                    {
                        preferred = coin.X;
                    }

                    break;
                }
            }

            _path.GetLateralBounds(s, st.X, out float xMin, out float xMax);
            float limMin = xMin + margin;
            float limMax = xMax - margin;

            // Nearest steer-only obstacle not yet passed.
            int nearest = -1;
            int n = _path.FindObstacles(s - 1f, s + (speed * 1.1f) + 2f, _ids);
            float nearestS = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                ObstacleBox box = _path.GetObstacle(_ids[i]);
                if (sim.IsObstacleRemoved(box.Id) || box.SMax < s - 0.45f)
                {
                    continue;
                }

                if ((box.Class == ObstacleClass.Blocker || box.Class == ObstacleClass.Thorns) && box.SMin < nearestS)
                {
                    nearestS = box.SMin;
                    nearest = box.Id;
                }
            }

            if (nearest < 0)
            {
                return Clamp(preferred, limMin, Math.Max(limMin, limMax));
            }

            ObstacleBox target = _path.GetObstacle(nearest);
            _path.GetLateralBounds(target.SMin, st.X, out float oMin, out float oMax);
            limMin = Math.Max(limMin, oMin + margin);
            limMax = Math.Min(limMax, oMax - margin);

            float best = float.NaN;
            float bestCost = float.MaxValue;
            Consider(sim, preferred, target, halfWidth, limMin, limMax, preferred, ref best, ref bestCost);
            for (int i = 0; i < n; i++)
            {
                ObstacleBox box = _path.GetObstacle(_ids[i]);
                if (!Overlaps(box, target) || (box.Class != ObstacleClass.Blocker && box.Class != ObstacleClass.Thorns))
                {
                    continue;
                }

                sim.EffectiveBox(box, out float ex0, out float ex1, out _, out _);
                Consider(sim, ex0 - halfWidth - SteerMargin, target, halfWidth, limMin, limMax, preferred, ref best, ref bestCost);
                Consider(sim, ex1 + halfWidth + SteerMargin, target, halfWidth, limMin, limMax, preferred, ref best, ref bestCost);
            }

            Consider(sim, limMin, target, halfWidth, limMin, limMax, preferred, ref best, ref bestCost);
            Consider(sim, limMax, target, halfWidth, limMin, limMax, preferred, ref best, ref bestCost);
            return float.IsNaN(best) ? Clamp(preferred, limMin, Math.Max(limMin, limMax)) : best;
        }

        private void Consider(RunnerSimulation sim, float x, in ObstacleBox target, float halfWidth, float limMin, float limMax, float preferred, ref float best, ref float bestCost)
        {
            if (x < limMin - 1e-4f || x > limMax + 1e-4f)
            {
                return;
            }

            int n = _path.FindObstacles(target.SMin - 1f, target.SMax + 1f, _overlapIds);
            for (int i = 0; i < n; i++)
            {
                ObstacleBox box = _path.GetObstacle(_overlapIds[i]);
                if (box.Class != ObstacleClass.Blocker && box.Class != ObstacleClass.Thorns)
                {
                    continue;
                }

                sim.EffectiveBox(box, out float ex0, out float ex1, out _, out _);
                if (x + halfWidth + SteerMargin - 1e-3f > ex0 && x - halfWidth - SteerMargin + 1e-3f < ex1)
                {
                    return;
                }
            }

            float cost = Math.Abs(x - preferred);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = x;
            }
        }

        private static bool Overlaps(in ObstacleBox a, in ObstacleBox b)
        {
            return a.SMin < b.SMax + 1f && a.SMax > b.SMin - 1f;
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }
    }
}
