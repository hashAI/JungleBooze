using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.App
{
    /// <summary>
    /// Dev aid (editor and development builds only; the bootstrap does not create it in release builds): a small label
    /// at the bottom-left showing the route mode and the dev keys, for example "Route: Generated (F4)  |  F3 hitboxes".
    /// Says "(next run)" while the chosen mode has not started yet. All strings are built once; the text is only
    /// assigned when the mode changes, so Render allocates nothing. It also shows the dev start distance (F5 = +300 m,
    /// F6 = +1000 m, see <see cref="DevStartDistance"/>) and, on the main menu only, two small buttons for the same
    /// choice on a phone.
    /// </summary>
    public sealed class DevRouteLabelView : MonoBehaviour, IRunView
    {
        private const int ModeCount = 3;
        private const int FontSize = 14;
        private const int StartOptionCount = 4;

        private SelectableRouteSource _source;
        private Text _text;
        private string[] _labels;
        private int _shown = -1;
        private GameObject _buttons;
        private bool _buttonsShown;

        /// <summary>Creates the canvas and the text once.</summary>
        public void Build(Font font, SelectableRouteSource source)
        {
            _source = source;

            // Index = ((int)mode * 2 + (pending ? 1 : 0)) * StartOptionCount + DevStartDistance.OptionIndex.
            string[] starts =
            {
                "\nStart: track begin (F5 +300 m, F6 +1000 m)",
                "\nStart: +300 m (F5 / F6)",
                "\nStart: +1000 m (F5 / F6)",
                "\nStart: custom (F5 / F6)",
            };
            _labels = new string[ModeCount * 2 * StartOptionCount];
            for (int i = 0; i < ModeCount; i++)
            {
                string head = "Route: " + ModeName((RouteMode)i);
                for (int o = 0; o < StartOptionCount; o++)
                {
                    _labels[((i * 2) * StartOptionCount) + o] = head + " (F4)  |  F3 hitboxes" + starts[o];
                    _labels[(((i * 2) + 1) * StartOptionCount) + o] = head + " (next run) (F4)  |  F3 hitboxes" + starts[o];
                }
            }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudView.ReferenceResolutionPt;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Phone buttons for the start distance, visible on the main menu only (never in the way of a run).
            _buttons = new GameObject("StartButtons", typeof(RectTransform));
            _buttons.transform.SetParent(transform, false);
            Color panel = new Color(0.97f, 0.91f, 0.78f, 1f);
            Color ink = new Color(0.12f, 0.1f, 0.14f, 1f);
            Color none = new Color(0f, 0f, 0f, 0f);
            HudFactory.CreateButton(
                _buttons.transform, "Start300", panel, new Vector2(0f, 0f), new Vector2(110f, 44f), new Vector2(12f, 70f),
                font, "Start +300 m", 16, ink, none, () => DevStartDistance.Toggle(DevStartDistance.ShortM));
            HudFactory.CreateButton(
                _buttons.transform, "Start1000", panel, new Vector2(0f, 0f), new Vector2(120f, 44f), new Vector2(132f, 70f),
                font, "Start +1000 m", 16, ink, none, () => DevStartDistance.Toggle(DevStartDistance.LongM));
            _buttons.SetActive(false);

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
            if (DevStartKeyPressed(out int meters))
            {
                DevStartDistance.Toggle(meters);
            }

            bool onMenu = session.Phase == SessionPhase.Menu;
            if (_buttons != null && onMenu != _buttonsShown)
            {
                _buttonsShown = onMenu;
                _buttons.SetActive(onMenu);
            }

            Refresh();
        }

        // F5 / F6 (Input System, as for F4 in PathFrameRunView); the Game view must have focus.
        private static bool DevStartKeyPressed(out int meters)
        {
            meters = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                meters = DevStartDistance.ShortM;
                return true;
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                meters = DevStartDistance.LongM;
                return true;
            }

            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.F5))
            {
                meters = DevStartDistance.ShortM;
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F6))
            {
                meters = DevStartDistance.LongM;
                return true;
            }

            return false;
#else
            return false;
#endif
#else
            return false;
#endif
        }

        private void Refresh()
        {
            if (_text == null || _source == null)
            {
                return;
            }

            int index = (((int)_source.Mode * 2) + (_source.Pending ? 1 : 0)) * StartOptionCount + DevStartDistance.OptionIndex;
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
