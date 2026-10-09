using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Remembers every label's base size so the player's text size setting (100–200 %) re-sizes them all at once.
    /// HUD labels use the capped HUD scale. Applying happens on a settings change, never per frame.
    /// </summary>
    public sealed class TextScaleRegistry
    {
        /// <summary>Smallest text size in the game (GDD §20: min 13 pt).</summary>
        public const int MinSize = 13;

        private readonly List<Text> _texts = new List<Text>(256);
        private readonly List<int> _sizes = new List<int>(256);
        private readonly List<bool> _hud = new List<bool>(256);

        public float MenuScale { get; private set; } = 1f;

        public float HudScale { get; private set; } = 1f;

        public int Count => _texts.Count;

        public void Register(Text text, int baseSize, bool hud)
        {
            _texts.Add(text);
            _sizes.Add(baseSize < MinSize ? MinSize : baseSize);
            _hud.Add(hud);
            Apply(text, _sizes[_sizes.Count - 1], hud ? HudScale : MenuScale);
        }

        public void SetScales(float menu, float hud)
        {
            MenuScale = menu;
            HudScale = hud;
            for (int i = 0; i < _texts.Count; i++)
            {
                if (_texts[i] != null)
                {
                    Apply(_texts[i], _sizes[i], _hud[i] ? hud : menu);
                }
            }
        }

        /// <summary>Smallest effective size of any registered label (tests).</summary>
        public int SmallestSize()
        {
            int min = int.MaxValue;
            for (int i = 0; i < _texts.Count; i++)
            {
                if (_texts[i] != null)
                {
                    min = Mathf.Min(min, _texts[i].resizeTextForBestFit ? _texts[i].resizeTextMinSize : _texts[i].fontSize);
                }
            }

            return min;
        }

        private static void Apply(Text text, int baseSize, float scale)
        {
            int size = Mathf.RoundToInt(baseSize * scale);
            text.fontSize = size;
            if (text.resizeTextForBestFit)
            {
                text.resizeTextMaxSize = size;
                text.resizeTextMinSize = Mathf.Min(size, Mathf.Max(MinSize, Mathf.RoundToInt(baseSize * 0.8f)));
            }
        }
    }
}
