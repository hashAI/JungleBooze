using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Turns one rendered frame of player input into per-tick <see cref="InputFrame"/>s (spec 101 §3.3 rule 6):
    /// discrete commands are delivered one per tick in recognition order (two swipes in one frame land on
    /// consecutive ticks); the frame's steering drag is quantized to whole millimetres, split evenly across the
    /// frame's ticks (remainder on the last) and the sub-millimetre rest is carried to the next frame. TouchBegan
    /// is delivered on the first tick. No allocation.
    /// </summary>
    public sealed class FrameInputDispatcher : IInputProvider
    {
        private readonly GestureRecognizer _gestures;
        private readonly CommandQueue _extra;
        private double _carryMm;
        private double _frameLateralM;
        private int _steps;
        private int _delivered;
        private long _perTickMm;
        private long _lastTickMm;
        private bool _touchBegan;

        public FrameInputDispatcher(GestureRecognizer gestures, int extraQueueSize)
        {
            _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
            _extra = new CommandQueue(Math.Max(1, extraQueueSize));
        }

        /// <summary>Keyboard and other non-touch commands (keyboard dodges carry TouchBegan themselves).</summary>
        public CommandQueue ExtraCommands => _extra;

        /// <summary>Carry below one millimetre waiting for the next frame.</summary>
        public double CarryMm => _carryMm;

        /// <summary>Adds non-touch steering (keyboard) for this frame, metres.</summary>
        public void AddLateral(double metres)
        {
            _frameLateralM += metres;
        }

        /// <summary>
        /// Call once per rendered frame after input was read and before the simulation steps.
        /// <paramref name="steps"/> = ticks this frame. With 0 steps everything waits for the next frame.
        /// </summary>
        public void BeginFrame(int steps)
        {
            _delivered = 0;
            _steps = steps;
            if (steps <= 0)
            {
                return;
            }

            double totalMm = ((_frameLateralM + _gestures.ConsumeLateralM()) * 1000.0) + _carryMm;
            _frameLateralM = 0.0;
            long whole = (long)totalMm; // toward zero
            _carryMm = totalMm - whole;
            _perTickMm = whole / steps;
            _lastTickMm = whole - (_perTickMm * (steps - 1));
            _touchBegan |= _gestures.ConsumeTouchBegan();
        }

        /// <summary>Drops pending output (new run, resume from pause).</summary>
        public void Clear()
        {
            _carryMm = 0.0;
            _frameLateralM = 0.0;
            _steps = 0;
            _delivered = 0;
            _touchBegan = false;
            _extra.Clear();
            _gestures.ConsumeLateralM();
            _gestures.ConsumeTouchBegan();
            _gestures.Commands.Clear();
        }

        public InputFrame ReadInput(long tick)
        {
            long mm = 0;
            if (_delivered < _steps)
            {
                mm = _delivered == _steps - 1 ? _lastTickMm : _perTickMm;
            }

            _delivered++;

            InputCommand commands = InputCommand.None;
            if (_touchBegan)
            {
                commands |= InputCommand.TouchBegan;
                _touchBegan = false;
            }

            InputCommand discrete = _gestures.Commands.Count > 0 ? _gestures.Commands.Dequeue() : _extra.Dequeue();
            commands |= discrete;
            return new InputFrame(commands, InputFrame.ClampMm(mm));
        }
    }
}
