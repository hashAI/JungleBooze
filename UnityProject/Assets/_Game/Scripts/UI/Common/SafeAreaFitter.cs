using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Stretches its RectTransform to <see cref="Screen.safeArea"/> (notch, Dynamic Island, home indicator) and
    /// re-fits when the safe area or screen size changes (rotation, Device Simulator). No allocations.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied;
        private int _width;
        private int _height;

        /// <summary>The safe area last applied, in pixels.</summary>
        public Rect AppliedSafeArea => _applied;

        public void Refresh()
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            Rect safe = Screen.safeArea;
            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            _applied = safe;
            _width = width;
            _height = height;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= width;
            min.y /= height;
            max.x /= width;
            max.y /= height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _width || Screen.height != _height)
            {
                Refresh();
            }
        }
    }
}
