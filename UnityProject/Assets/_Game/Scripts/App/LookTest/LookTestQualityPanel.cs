using System;
using System.Collections.Generic;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Buttons on the right edge of the look test that switch one rendering feature at a time (post-processing,
    /// shadows, MSAA, HDR, plants, water, 30/60 fps, render scale), so the owner can see on the iPhone what each
    /// feature costs in the <see cref="FrameStatsOverlay"/>. Every switch resets the overlay statistics.
    /// The root decides what each button does (<see cref="AddToggle"/>, <see cref="AddAction"/>).
    /// Setup allocates; clicks allocate a label string (debug UI, not per frame).
    /// </summary>
    public sealed class LookTestQualityPanel : MonoBehaviour
    {
        private const int Rows = 6;
        private const float ButtonWidth = 104f;
        private const float ButtonHeight = 44f;
        private const float Gap = 6f;
        private const int LabelFontSize = 13;

        private readonly List<Entry> _entries = new List<Entry>();
        private RectTransform _area;
        private Font _font;
        private Action _onAnyChange;

        public void Init(Font font, Action onAnyChange)
        {
            _font = font;
            _onAnyChange = onAnyChange;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(844f, 390f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            _area = HudFactory.CreateRect(transform, "SafeArea");
            _area.gameObject.AddComponent<SafeAreaFitter>();
        }

        /// <summary>A button showing "<paramref name="name"/> ON/OFF" that flips the state.</summary>
        public void AddToggle(string name, Func<bool> isOn, Action<bool> set)
        {
            var entry = new Entry { Name = name, IsOn = isOn };
            entry.Label = CreateButton(name, () =>
            {
                set(!isOn());
                Changed();
            });
            _entries.Add(entry);
            RefreshLabels();
        }

        /// <summary>A button with a label from <paramref name="label"/> that runs <paramref name="action"/>.</summary>
        public void AddAction(Func<string> label, Action action)
        {
            var entry = new Entry { DynamicLabel = label };
            entry.Label = CreateButton(label(), () =>
            {
                action();
                Changed();
            });
            _entries.Add(entry);
            RefreshLabels();
        }

        private void Changed()
        {
            RefreshLabels();
            _onAnyChange?.Invoke();
        }

        private void RefreshLabels()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Label == null)
                {
                    continue;
                }

                entry.Label.text = entry.DynamicLabel != null
                    ? entry.DynamicLabel()
                    : entry.Name + (entry.IsOn() ? " ON" : " OFF");
            }
        }

        private Text CreateButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            int index = _entries.Count;
            int column = index / Rows;
            int row = index % Rows;
            var offset = new Vector2(-Gap - column * (ButtonWidth + Gap), -Gap - row * (ButtonHeight + Gap));
            Button button = HudFactory.CreateButton(
                _area,
                "Toggle" + index,
                new Color(0.96f, 0.92f, 0.82f, 1f),
                new Vector2(1f, 1f),
                new Vector2(ButtonWidth, ButtonHeight),
                offset,
                _font,
                label,
                LabelFontSize,
                new Color(0.12f, 0.10f, 0.14f, 1f),
                new Color(0f, 0f, 0f, 0f),
                onClick);
            return button.GetComponentInChildren<Text>();
        }

        private sealed class Entry
        {
            public string Name;
            public Func<bool> IsOn;
            public Func<string> DynamicLabel;
            public Text Label;
        }
    }
}
