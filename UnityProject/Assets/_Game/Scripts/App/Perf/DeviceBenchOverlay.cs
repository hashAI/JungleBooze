using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// On-screen readout of the device benchmark: a translucent panel in the top-left safe area. The text is replaced
    /// once per log interval (one string per second: the known, logged exception for this debug overlay).
    /// </summary>
    public sealed class DeviceBenchOverlay : MonoBehaviour
    {
        private const int FontSize = 13;
        private Text _label;
        private RectTransform _panel;
        private CanvasScaler _scaler;
        private bool _landscape = true;

        public void Init(Font font)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(844f, 390f);
            _scaler.matchWidthOrHeight = 1f;

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Image panel = HudFactory.CreateImage(safe, "BenchBackground", new Color(0f, 0f, 0f, 0.55f), false);
            _panel = panel.rectTransform;
            HudFactory.Place(_panel, new Vector2(0f, 1f), new Vector2(360f, 132f), new Vector2(6f, -6f));
            _label = HudFactory.CreateText(panel.transform, "BenchText", font, FontSize, Color.white, new Color(0f, 0f, 0f, 0f), TextAnchor.UpperLeft);
            HudFactory.Stretch(_label.rectTransform, 6f);
            _label.text = "Benchmark starting...";
        }

        public void Show(string text, bool large)
        {
            bool landscape = Screen.width >= Screen.height;
            if (landscape != _landscape)
            {
                _landscape = landscape;
                _scaler.referenceResolution = landscape ? new Vector2(844f, 390f) : new Vector2(390f, 844f);
                _scaler.matchWidthOrHeight = landscape ? 1f : 0f;
            }

            _panel.sizeDelta = large ? new Vector2(378f, 300f) : new Vector2(360f, 132f);
            _label.text = text;
        }
    }
}
