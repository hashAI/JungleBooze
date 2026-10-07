using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Pre-allocated ring buffer of <see cref="TrackEvent"/> (same contract as the runner's event buffer): the
    /// simulation appends; presentation reads <see cref="Count"/> events oldest first after the frame's steps and
    /// then calls <see cref="Clear"/>. When full, the oldest event is overwritten and <see cref="OverflowCount"/>
    /// goes up (an error in development builds).
    /// </summary>
    public sealed class TrackEventBuffer
    {
        private readonly TrackEvent[] _items;
        private int _start;

        public TrackEventBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Must be positive.");
            }

            _items = new TrackEvent[capacity];
        }

        public int Capacity => _items.Length;

        public int Count { get; private set; }

        public int OverflowCount { get; private set; }

        /// <summary>Event at <paramref name="index"/>, 0 = oldest unread.</summary>
        public TrackEvent this[int index]
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

        public void Add(in TrackEvent e)
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

        /// <summary>Clears unread events and the overflow counter (new run).</summary>
        public void Reset()
        {
            Clear();
            OverflowCount = 0;
        }
    }
}
