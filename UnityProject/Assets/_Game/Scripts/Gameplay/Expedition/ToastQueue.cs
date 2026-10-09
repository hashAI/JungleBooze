namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Discovery toasts (spec 103 §7.3, AC-103-31): each shows for <see cref="Duration"/>; a toast arriving while one
    /// is visible waits and shows <see cref="Gap"/> after it ends. Never blocks input. Fixed capacity, no allocation.
    /// </summary>
    public sealed class ToastQueue
    {
        private readonly int[] _items;
        private int _head;
        private int _count;
        private float _timer;
        private bool _showing;

        public ToastQueue(int capacity = 8, float duration = 2f, float gap = 0.5f)
        {
            _items = new int[capacity < 1 ? 1 : capacity];
            Duration = duration;
            Gap = gap;
        }

        public float Duration { get; }

        public float Gap { get; }

        /// <summary>Item shown now, or −1.</summary>
        public int Current { get; private set; } = -1;

        /// <summary>Seconds the current toast has been visible.</summary>
        public float Elapsed => _showing ? _timer : 0f;

        public int Pending => _count;

        public void Clear()
        {
            _head = 0;
            _count = 0;
            _timer = 0f;
            _showing = false;
            Current = -1;
        }

        public void Enqueue(int item)
        {
            if (_count == _items.Length)
            {
                return;
            }

            _items[(_head + _count) % _items.Length] = item;
            _count++;
            if (!_showing && _timer <= 0f)
            {
                ShowNext();
            }
        }

        /// <summary>Advances time. Returns true when the visible toast changed.</summary>
        public bool Update(float seconds)
        {
            if (_showing)
            {
                _timer += seconds;
                if (_timer < Duration)
                {
                    return false;
                }

                _showing = false;
                Current = -1;
                _timer = _count > 0 ? Gap : 0f;
                return true;
            }

            if (_timer > 0f)
            {
                _timer -= seconds;
                if (_timer > 0f)
                {
                    return false;
                }

                _timer = 0f;
            }

            if (_count > 0)
            {
                ShowNext();
                return true;
            }

            return false;
        }

        private void ShowNext()
        {
            Current = _items[_head];
            _head = (_head + 1) % _items.Length;
            _count--;
            _showing = true;
            _timer = 0f;
        }
    }
}
