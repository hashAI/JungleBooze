using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.App
{
    /// <summary>
    /// Dev aid (editor and development builds only; the bootstrap does not create it in release builds): a small label
    /// at the bottom-left showing the route mode and the dev keys, for example "Route: Generated (F4)  |  F3 hitboxes".
    /// Says "(next run)" while the chosen mode has not started yet. All strings are built once; the text is only
    /// assigned when the mode changes, so Render allocates nothing.
    /// </summary>
    public sealed class DevRouteLabelView : MonoBehaviour, IRunView
    {
        private const int ModeCount = 3;
        private const int FontSize = 18;

        private SelectableRouteSource _source;
        private Text _text;
        private string[] _labels;
        private int _shown = -1;

        /// <summary>Creates the canvas and the text once.</summary>
        public void Build(Font font, SelectableRouteSource source)
        {
            _source = source;

            // Index = (int)mode * 2 + (pending ? 1 : 0).
            _labels = new string[ModeCount * 2];
            for (int i = 0; i < ModeCount; i++)
            {
                string head = "Route: " + ModeName((RouteMode)i);
                _labels[i * 2] = head + " (F4)  |  F3 hitboxes";
                _labels[i * 2 + 1] = head + " (next run) (F4)  |  F3 hitboxes";
            }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;

            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(12f, 12f);
            rect.sizeDelta = new Vector2(900f, 40f);

            _text = textObject.AddComponent<Text>();
            _text.font = font;
            _text.fontSize = FontSize;
            _text.color = new Color(1f, 1f, 1f, 0.85f);
            _text.alignment = TextAnchor.LowerLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;
            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            Refresh();
        }

        public void BeginRun(GameSession session)
        {
            Refresh();
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_text == null || _source == null)
            {
                return;
            }

            int index = (int)_source.Mode * 2 + (_source.Pending ? 1 : 0);
            if (index == _shown || index < 0 || index >= _labels.Length)
            {
                return;
            }

            _shown = index;
            _text.text = _labels[index];
        }

        private static string ModeName(RouteMode mode)
        {
            switch (mode)
            {
                case RouteMode.Generated:
                    return "Generated";
                case RouteMode.DebugCurve:
                    return "Debug curve";
                default:
                    return "Straight";
            }
        }
    }
}
