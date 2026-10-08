namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Fixed-size ring buffer of <see cref="RunEvent"/>s. The simulation appends; presentation reads and clears once
    /// per frame. When full, the oldest event is overwritten and <see cref="Overflowed"/> counts it.
    /// </summary>
    public sealed class RunEventBuffer
    {
        private readonly RunEvent[] _items;
        private int _start;

        public RunEventBuffer(int capacity)
        {
            _items = new RunEvent[capacity < 1 ? 1 : capacity];
        }

        public int Capacity => _items.Length;

        public int Count { get; private set; }

        public int Overflowed { get; private set; }

        /// <summary>Events in order, 0 = oldest.</summary>
        public RunEvent this[int index] => _items[(_start + index) % _items.Length];

        public void Add(RunEvent item)
        {
            if (Count == _items.Length)
            {
                _items[_start] = item;
                _start = (_start + 1) % _items.Length;
                Overflowed++;
                return;
            }

            _items[(_start + Count) % _items.Length] = item;
            Count++;
        }

        public void Clear()
        {
            _start = 0;
            Count = 0;
        }

        /// <summary>Number of events of <paramref name="type"/> currently in the buffer.</summary>
        public int CountOf(RunEventType type)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if (this[i].Type == type)
                {
                    n++;
                }
            }

            return n;
        }
    }
}
