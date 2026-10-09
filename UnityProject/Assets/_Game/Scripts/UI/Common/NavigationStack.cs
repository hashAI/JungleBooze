using System.Collections.Generic;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Screen navigation (plain C#): a stack of screen ids over a base screen. Back always pops one level and never
    /// leaves the base; Reset returns to a base screen. The view layer shows exactly <see cref="Current"/>.
    /// </summary>
    public sealed class NavigationStack
    {
        private readonly List<UiScreen> _stack = new List<UiScreen>(8);

        public NavigationStack(UiScreen root)
        {
            _stack.Add(root);
        }

        public UiScreen Current => _stack[_stack.Count - 1];

        public UiScreen Root => _stack[0];

        public int Depth => _stack.Count;

        public bool CanGoBack => _stack.Count > 1;

        public void Push(UiScreen screen)
        {
            if (screen != Current)
            {
                _stack.Add(screen);
            }
        }

        /// <summary>Pops one level; false (and nothing changes) at the base.</summary>
        public bool Back()
        {
            if (_stack.Count <= 1)
            {
                return false;
            }

            _stack.RemoveAt(_stack.Count - 1);
            return true;
        }

        public void Reset(UiScreen root)
        {
            _stack.Clear();
            _stack.Add(root);
        }

        public bool Contains(UiScreen screen)
        {
            return _stack.Contains(screen);
        }
    }
}
