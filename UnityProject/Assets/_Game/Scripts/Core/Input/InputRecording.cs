using System;
using System.Collections.Generic;

namespace JungleBooze.Core
{
    /// <summary>
    /// Everything needed to reproduce a run: the seed plus every non-empty input, keyed by tick.
    /// Ticks without input are not stored. Serialization lives outside Core.
    /// </summary>
    public sealed class InputRecording
    {
        private readonly List<InputFrame> _frames;

        public InputRecording(ulong seed, int initialCapacity = 1024)
        {
            Seed = seed;
            _frames = new List<InputFrame>(initialCapacity);
        }

        /// <summary>Seed of the run's root <see cref="IRandom"/>.</summary>
        public ulong Seed { get; }

        public int Count => _frames.Count;

        public InputFrame this[int index] => _frames[index];

        /// <summary>Appends a frame. Ticks must be strictly increasing. Empty commands are ignored.</summary>
        public void Add(long tick, InputCommand commands)
        {
            if (commands == InputCommand.None)
            {
                return;
            }

            if (_frames.Count > 0 && tick <= _frames[_frames.Count - 1].Tick)
            {
                throw new ArgumentException("Ticks must be strictly increasing.", nameof(tick));
            }

            _frames.Add(new InputFrame(tick, commands));
        }
    }
}
