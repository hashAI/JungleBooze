using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Fixed-capacity FIFO ring of structs (spec 002 section 8.6). Items are added at the back and removed from the
    /// front; indexing is oldest first and returns a reference, so items can be updated in place. Never allocates
    /// after construction; a full ring rejects new items (<see cref="TryAdd"/> returns false).
    /// </summary>
    public sealed class FixedRing<T>
        where T : struct
    {
        private readonly T[] _items;
        private int _head;

        public FixedRing(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Must be positive.");
            }

            _items = new T[capacity];
        }

        public int Capacity => _items.Length;

        public int Count { get; private set; }

        /// <summary>Highest <see cref="Count"/> reached since the last <see cref="Clear"/>.</summary>
        public int HighWaterMark { get; private set; }

        /// <summary>Item <paramref name="index"/>, 0 = oldest.</summary>
        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return ref _items[(_head + index) % _items.Length];
            }
        }

        public bool TryAdd(in T item)
        {
            if (Count == _items.Length)
            {
                return false;
            }

            _items[(_head + Count) % _items.Length] = item;
            Count++;
            if (Count > HighWaterMark)
            {
                HighWaterMark = Count;
            }

            return true;
        }

        /// <summary>Removes the oldest item.</summary>
        public void RemoveFirst()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("The ring is empty.");
            }

            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            Count--;
        }

        public void Clear()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                _items[i] = default;
            }

            _head = 0;
            Count = 0;
            HighWaterMark = 0;
        }
    }
}
