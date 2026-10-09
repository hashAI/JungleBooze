using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Stretches its RectTransform to <see cref="Screen.safeArea"/> (notch, Dynamic Island, home indicator) and
    /// re-fits when the safe area or screen size changes (rotation, Device Simulator). Tools and tests can set
    /// <see cref="Simulated"/> (normalized 0–1 rect) to check a device's insets anywhere. No allocations.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private static bool _hasSimulated;
        private static Rect _simulated;

        private RectTransform _rect;
        private Rect _applied;
        private int _width;
        private int _height;
        private bool _appliedSimulated;

        /// <summary>The normalized safe area last applied (0–1).</summary>
        public Rect AppliedNormalized { get; private set; }

        /// <summary>Tools/tests: a normalized safe area to use instead of the screen's (null = real screen).</summary>
        public static Rect? Simulated
        {
            get => _hasSimulated ? _simulated : (Rect?)null;
            set
            {
                _hasSimulated = value.HasValue;
                _simulated = value ?? default;
            }
        }

        /// <summary>Normalized safe area of a device profile (for <see cref="Simulated"/>).</summary>
        public static Rect Normalized(in DeviceProfile device, bool landscape)
        {
            Vector2Int px = device.Pixels(landscape);
            Rect safe = device.SafeAreaPixels(landscape);
            return new Rect(safe.x / px.x, safe.y / px.y, safe.width / px.x, safe.height / px.y);
        }

        /// <summary>The normalized safe area in effect now (simulated, else the screen's).</summary>
        public static Rect CurrentNormalized()
        {
            if (_hasSimulated)
            {
                return _simulated;
            }

            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            Rect safe = Screen.safeArea;
            return Rect.MinMaxRect(safe.xMin / width, safe.yMin / height, safe.xMax / width, safe.yMax / height);
        }

        public void Refresh()
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            Vector2 min;
            Vector2 max;
            if (_hasSimulated)
            {
                min = _simulated.min;
                max = _simulated.max;
                _appliedSimulated = true;
            }
            else
            {
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
                _appliedSimulated = false;
                min = new Vector2(safe.xMin / width, safe.yMin / height);
                max = new Vector2(safe.xMax / width, safe.yMax / height);
            }

            AppliedNormalized = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
            if (_hasSimulated)
            {
                if (!_appliedSimulated || AppliedNormalized != _simulated)
                {
                    Refresh();
                }

                return;
            }

            if (_appliedSimulated || Screen.safeArea != _applied || Screen.width != _width || Screen.height != _height)
            {
                Refresh();
            }
        }
    }
}
