using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// Shows a non-negative integer with one <see cref="Text"/> per digit and cached digit strings, so updating the
    /// number never allocates (ARCHITECTURE 10.3). Left-aligned; an optional suffix (for example "m") follows the
    /// last digit. Updates only when the value changes.
    /// </summary>
    public sealed class DigitCounter : MonoBehaviour
    {
        private static readonly string[] DigitStrings = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private Text[] _digits;
        private Text _suffix;
        private float _digitWidth;
        private int _maxValue;
        private int _value = -1;
        private bool _centered;
        private float _suffixWidth;

        public int Value => _value;

        /// <summary>
        /// Creates the digit slots under this object's RectTransform. <paramref name="centered"/>: the number is
        /// centered on this rect's center; otherwise it starts at the rect's left edge.
        /// </summary>
        public void Build(Font font, int fontSize, int maxDigits, Color color, Color outline, string suffix, bool centered)
        {
            _centered = centered;
            _digitWidth = fontSize * 0.6f;
            float anchorX = centered ? 0.5f : 0f;
            _digits = new Text[maxDigits];
            _maxValue = 1;
            for (int i = 0; i < maxDigits; i++)
            {
                _maxValue *= 10;
            }

            _maxValue -= 1;

            for (int i = 0; i < maxDigits; i++)
            {
                Text digit = HudFactory.CreateText(transform, "Digit" + i, font, fontSize, color, outline, TextAnchor.MiddleCenter);
                RectTransform rt = digit.rectTransform;
                rt.anchorMin = new Vector2(anchorX, 0.5f);
                rt.anchorMax = new Vector2(anchorX, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(_digitWidth, fontSize * 1.3f);
                rt.anchoredPosition = new Vector2(i * _digitWidth, 0f);
                _digits[i] = digit;
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                _suffix = HudFactory.CreateText(transform, "Suffix", font, Mathf.RoundToInt(fontSize * 0.75f), color, outline, TextAnchor.MiddleLeft);
                RectTransform rt = _suffix.rectTransform;
                rt.anchorMin = new Vector2(anchorX, 0.5f);
                rt.anchorMax = new Vector2(anchorX, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                _suffixWidth = fontSize * 0.75f * suffix.Length * 0.6f + 2f;
                rt.sizeDelta = new Vector2(_suffixWidth + fontSize, fontSize * 1.3f);
                _suffix.text = suffix;
            }

            _value = -1;
            SetValue(0);
        }

        public void SetValue(int value)
        {
            if (value < 0)
            {
                value = 0;
            }
            else if (value > _maxValue)
            {
                value = _maxValue;
            }

            if (value == _value || _digits == null)
            {
                return;
            }

            _value = value;

            int count = 1;
            for (int v = value / 10; v > 0; v /= 10)
            {
                count++;
            }

            float startX = _centered ? -(count * _digitWidth + _suffixWidth) * 0.5f : 0f;
            for (int i = _digits.Length - 1; i >= 0; i--)
            {
                bool visible = i < count;
                Text digit = _digits[i];
                if (digit.enabled != visible)
                {
                    digit.enabled = visible;
                }

                if (!visible)
                {
                    continue;
                }

                // Slot i shows the digit at position i from the left of a count-digit number.
                int power = count - 1 - i;
                int d = value;
                for (int p = 0; p < power; p++)
                {
                    d /= 10;
                }

                d %= 10;
                digit.rectTransform.anchoredPosition = new Vector2(startX + i * _digitWidth, 0f);
                if (!ReferenceEquals(digit.text, DigitStrings[d]))
                {
                    digit.text = DigitStrings[d];
                }
            }

            if (_suffix != null)
            {
                _suffix.rectTransform.anchoredPosition = new Vector2(startX + count * _digitWidth + 2f, -2f);
            }
        }
    }
}
