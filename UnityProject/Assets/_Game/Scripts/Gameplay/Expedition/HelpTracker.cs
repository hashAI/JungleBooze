using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// First-run slow-time help (GDD §16, spec 103 [ST:move] markers): the first instance of each move slows the
    /// world <see cref="LeadSeconds"/> before contact if the player hasn't acted, until the right action happens
    /// or the marker is passed. Max once per move, first run only. The App layer applies the slowdown by stepping
    /// the simulation at <see cref="TimeScale"/> of real time, so the simulation stays deterministic. Plain C#,
    /// allocation-free per tick.
    /// </summary>
    public sealed class HelpTracker
    {
        public const float LeadSeconds = 0.6f;
        public const float TimeScale = 0.35f;

        private readonly WorldPath _path;
        private readonly RunnerSimulation _sim;
        private readonly bool[] _usedMove = new bool[8];
        private bool _enabled;
        private int _activeId = -1;
        private int _lastHandledId = -1;

        public HelpTracker(WorldPath path, RunnerSimulation sim)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
        }

        public bool Active => _activeId >= 0;

        public HelpMove ActiveMove { get; private set; }

        /// <summary>Number of help prompts shown this run.</summary>
        public int Prompts { get; private set; }

        public void BeginRun(bool enabled)
        {
            _enabled = enabled;
            _activeId = -1;
            _lastHandledId = -1;
            Prompts = 0;
            Array.Clear(_usedMove, 0, _usedMove.Length);
        }

        public void Disable()
        {
            _enabled = false;
            _activeId = -1;
        }

        /// <summary>Call after every simulation step.</summary>
        public void Update()
        {
            if (!_enabled)
            {
                return;
            }

            ref readonly RunnerState st = ref _sim.State;
            if (st.Dead)
            {
                _activeId = -1;
                return;
            }

            if (_activeId >= 0)
            {
                HelpPoint active = _path.GetHelp(_activeId);
                if (_activeId < _path.FirstHelpId || Satisfied(active, st) || st.S > active.S + 1f)
                {
                    _activeId = -1;
                }

                return;
            }

            for (int id = Math.Max(_path.FirstHelpId, _lastHandledId + 1); id < _path.NextHelpId; id++)
            {
                HelpPoint h = _path.GetHelp(id);
                if (h.S < st.S)
                {
                    _lastHandledId = id;
                    continue;
                }

                float speed = Math.Max(1f, st.Speed);
                if ((h.S - st.S) / speed > LeadSeconds)
                {
                    break;
                }

                _lastHandledId = id;
                int move = (int)h.Move;
                if (move < _usedMove.Length && _usedMove[move])
                {
                    continue;
                }

                if (move < _usedMove.Length)
                {
                    _usedMove[move] = true;
                }

                if (!Satisfied(h, st))
                {
                    _activeId = id;
                    ActiveMove = h.Move;
                    Prompts++;
                }

                break;
            }
        }

        private bool Satisfied(in HelpPoint h, in RunnerState st)
        {
            switch (h.Move)
            {
                case HelpMove.Jump:
                case HelpMove.Gap:
                case HelpMove.Leap:
                    return !st.Grounded || st.Buffered == InputCommand.Jump;
                case HelpMove.Slide:
                case HelpMove.Dive:
                    return st.Sliding;
                default:
                    float half = _sim.Config.Hitbox.Width * 0.5f;
                    return st.XTarget + half < h.XMin || st.XTarget - half > h.XMax;
            }
        }
    }
}
