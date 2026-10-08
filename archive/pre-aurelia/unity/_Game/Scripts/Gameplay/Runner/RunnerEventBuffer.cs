using System;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Pre-allocated ring buffer of <see cref="RunnerEvent"/>. The simulation appends; presentation reads
    /// <see cref="Count"/> events oldest first after the frame's steps and then calls <see cref="Clear"/>.
    /// When full, the oldest event is overwritten and <see cref="OverflowCount"/> goes up; the run driver reports
    /// a non-zero overflow count as an error in development builds. No allocations after construction.
    /// </summary>
    public sealed class RunnerEventBuffer
    {
        private readonly RunnerEvent[] _items;
        private int _start;

        public RunnerEventBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Must be positive.");
            }

            _items = new RunnerEvent[capacity];
        }

        public int Capacity => _items.Length;

        public int Count { get; private set; }

        /// <summary>Events lost because the buffer was full when they were written.</summary>
        public int OverflowCount { get; private set; }

        /// <summary>Event at <paramref name="index"/>, 0 = oldest unread.</summary>
        public RunnerEvent this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _items[(_start + index) % _items.Length];
            }
        }

        public void Add(in RunnerEvent e)
        {
            if (Count == _items.Length)
            {
                _items[_start] = e;
                _start = (_start + 1) % _items.Length;
                OverflowCount++;
                return;
            }

            _items[(_start + Count) % _items.Length] = e;
            Count++;
        }

        /// <summary>Marks every event as read.</summary>
        public void Clear()
        {
            _start = 0;
            Count = 0;
        }
    }
}
