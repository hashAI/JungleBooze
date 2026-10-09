using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// "Continue?" (GDD §11): 4 s offer with a draining ring and the seconds left, the exact crystal cost on the
    /// button, "No thanks" always visible, never mandatory. Updates only on changes; no allocation per frame.
    /// </summary>
    public sealed class ReviveView
    {
        private readonly UiFactory _f;
        private readonly Image _ring;
        private readonly Text _seconds;
        private readonly Text _short;
        private readonly NumberText _numbers = new NumberText(10, 16, null);
        private readonly string[] _labels = new string[8];
        private int _shownCost = -1;
        private int _shownSeconds = -1;
        private float _shownFill = -1f;

        public ReviveView(UiFactory f, Transform canvas, UnityAction onRevive, UnityAction onSkip)
        {
            _f = f;
            Shell = new ScreenShell(f, canvas, "Revive", new Vector2(320f, 320f), new Vector2(340f, 300f));
            Transform body = Shell.Body.transform;
            Text title = f.Label(body, "Title", f.S("revive.title"), FontKind.Display, 28, UiColors.Gold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(280f, 44f), new Vector2(0f, -14f));
            RectTransform ringRoot = UiFactory.Rect(body, "Timer");
            UiFactory.Anchor(ringRoot, new Vector2(0.5f, 1f), new Vector2(76f, 76f), new Vector2(0f, -62f));
            Image back = f.Image(ringRoot, "Back", f.Theme.Circle, new Color(1f, 1f, 1f, 0.35f));
            UiFactory.Stretch(back.rectTransform);
            _ring = f.Image(ringRoot, "Ring", f.Theme.Knob, Color.white);
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = false;
            UiFactory.Stretch(_ring.rectTransform, 4f, 4f, 4f, 4f);
            _seconds = f.Label(ringRoot, "Seconds", string.Empty, FontKind.Heavy, 30, UiColors.Ink, TextAnchor.MiddleCenter, false, false);
            UiFactory.Stretch(_seconds.rectTransform);
            Button = f.Button(body, "Continue", ButtonStyle.Primary, string.Empty, f.Theme.Crystal, 20, new Vector2(250f, 58f), onRevive);
            UiFactory.Anchor(Button.Root, new Vector2(0.5f, 0f), new Vector2(250f, 58f), new Vector2(0f, 74f));
            _short = f.Label(body, "Short", f.S("revive.short"), FontKind.Body, 14, UiColors.Mist, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_short.rectTransform, new Vector2(0.5f, 0f), new Vector2(260f, 22f), new Vector2(0f, 134f));
            Skip = f.Button(body, "Skip", ButtonStyle.Ghost, f.S("revive.skip"), null, 17, new Vector2(170f, 46f), onSkip);
            UiFactory.Anchor(Skip.Root, new Vector2(0.5f, 0f), new Vector2(170f, 46f), new Vector2(0f, 18f));
            for (int i = 1; i < _labels.Length; i++)
            {
                _labels[i] = f.Strings.Format(i == 1 ? "revive.button.one" : "revive.button.many", i);
            }
        }

        public ScreenShell Shell { get; }

        public UiButton Button { get; }

        public UiButton Skip { get; }

        public bool Visible => Shell.Visible;

        public void Show(int cost, bool affordable, float secondsLeft, float total, bool reducedMotion)
        {
            if (!Shell.Visible)
            {
                Shell.Show(true, reducedMotion);
                _shownCost = -1;
                _shownSeconds = -1;
                _shownFill = -1f;
            }

            if (cost != _shownCost)
            {
                _shownCost = cost;
                Button.SetLabel(cost > 0 && cost < _labels.Length ? _labels[cost] : _f.Strings.Format("revive.button.many", cost));
            }

            Button.Interactable = affordable;
            if (_short.gameObject.activeSelf == affordable)
            {
                _short.gameObject.SetActive(!affordable);
            }

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _seconds.text = _numbers.Get(seconds);
            }

            float fill = Mathf.Round(Mathf.Clamp01(total > 0f ? secondsLeft / total : 0f) * 60f) / 60f;
            if (fill != _shownFill)
            {
                _shownFill = fill;
                _ring.fillAmount = fill;
            }
        }

        public void Hide()
        {
            if (Shell.Visible)
            {
                Shell.Show(false, true);
            }
        }
    }
}
