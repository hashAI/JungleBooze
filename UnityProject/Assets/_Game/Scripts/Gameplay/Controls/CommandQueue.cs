using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Fixed-size FIFO of recognized commands (spec 001 section 12.5). One entry is handed to the simulation per
    /// tick, so two fast swipes are never merged. When full, new commands are dropped. No allocations after
    /// construction.
    /// </summary>
    public sealed class CommandQueue
    {
        private readonly InputCommand[] _items;
        private int _start;

        public CommandQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Must be positive.");
            }

            _items = new InputCommand[capacity];
        }

        public int Capacity => _items.Length;

        public int Count { get; private set; }

        /// <summary>Commands dropped because the queue was full.</summary>
        public int DroppedCount { get; private set; }

        public bool TryEnqueue(InputCommand command)
        {
            if (command == InputCommand.None)
            {
                return false;
            }

            if (Count == _items.Length)
            {
                DroppedCount++;
                return false;
            }

            _items[(_start + Count) % _items.Length] = command;
            Count++;
            return true;
        }

        /// <summary>Oldest command, or <see cref="InputCommand.None"/> when empty.</summary>
        public InputCommand Dequeue()
        {
            if (Count == 0)
            {
                return InputCommand.None;
            }

            InputCommand command = _items[_start];
            _start = (_start + 1) % _items.Length;
            Count--;
            return command;
        }

        public void Clear()
        {
            _start = 0;
            Count = 0;
        }
    }
}
