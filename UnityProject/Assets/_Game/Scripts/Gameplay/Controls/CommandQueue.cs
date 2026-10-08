using JungleBooze.Core;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>Fixed-size FIFO of recognized commands. Full queue drops the newest (counted). No allocation.</summary>
    public sealed class CommandQueue
    {
        private readonly InputCommand[] _items;
        private int _head;

        public CommandQueue(int capacity)
        {
            _items = new InputCommand[capacity < 1 ? 1 : capacity];
        }

        public int Count { get; private set; }

        public int Capacity => _items.Length;

        public int Dropped { get; private set; }

        public bool TryEnqueue(InputCommand command)
        {
            if (Count == _items.Length)
            {
                Dropped++;
                return false;
            }

            _items[(_head + Count) % _items.Length] = command;
            Count++;
            return true;
        }

        public InputCommand Dequeue()
        {
            if (Count == 0)
            {
                return InputCommand.None;
            }

            InputCommand item = _items[_head];
            _head = (_head + 1) % _items.Length;
            Count--;
            return item;
        }

        public InputCommand Peek(int index)
        {
            return index < Count ? _items[(_head + index) % _items.Length] : InputCommand.None;
        }

        public void Clear()
        {
            _head = 0;
            Count = 0;
        }
    }
}
