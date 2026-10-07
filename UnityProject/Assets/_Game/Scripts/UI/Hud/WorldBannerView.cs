using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Views;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// World title banner (GDD 9): when HERO enters a new world (<see cref="WorldThemeView.SegmentChanged"/>) a comic
    /// panel with the world's name slides in from the right, holds, and slides out to the left, about 2 s in total.
    /// It sits in the top third of the screen (above the lanes' obstacles) and never takes touches. With Reduce Motion
    /// it only fades. The panel is built once; showing it assigns a constant string and moves one rect, so nothing
    /// allocates per frame. The clock advances only while the run is running, so a pause holds the banner.
    /// </summary>
    public sealed class WorldBannerView : MonoBehaviour, IRunView
    {
        public const float SlideInSeconds = 0.4f;
        public const float HoldSeconds = 1.2f;
        public const float SlideOutSeconds = 0.4f;
        public const float TotalSeconds = SlideInSeconds + HoldSeconds + SlideOutSeconds;

        private const float PanelWidthPt = 250f;
        private const float PanelHeightPt = 54f;
        private const float SlideDistancePt = 260f;
        private const float AnchorY = 0.76f;
        private const int FontSize = 28;

        private WorldThemeView _worldView;
        private WorldScheduleConfig _worlds;
        private RectTransform _panel;
        private CanvasGroup _group;
        private Text _title;
        private float _age = -1f;
        private bool _reduceMotion;

        /// <summary>Banner is on screen (tests).</summary>
        public bool Visible => _age >= 0f;

        /// <summary>Title being shown or shown last (tests).</summary>
        public string Title => _title != null ? _title.text : string.Empty;

        /// <summary>Builds the canvas and the hidden panel once. Call right after AddComponent on a RectTransform object.</summary>
        public void Build(Font font, WorldThemeView worldView, bool reduceMotion)
        {
            _reduceMotion = reduceMotion;
            _worldView = worldView;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudView.ReferenceResolutionPt;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(safe, 0f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Image fill = HudFactory.CreatePanel(
                safe, "WorldBanner", StylePalette.Parchment, new Vector2(0.5f, AnchorY), new Vector2(PanelWidthPt, PanelHeightPt), Vector2.zero);
            _panel = (RectTransform)fill.transform.parent;
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            _title = HudFactory.CreateText(
                fill.transform, "Title", font, FontSize, StylePalette.Ink, new Color(0f, 0f, 0f, 0f), TextAnchor.MiddleCenter);
            HudFactory.Stretch(_title.rectTransform, 0f);
            _title.text = WorldBannerStrings.Jungle;

            _panel.gameObject.SetActive(false);

            if (_worldView != null)
            {
                _worldView.SegmentChanged += OnSegmentChanged;
            }
        }

        public void SetReduceMotion(bool reduceMotion)
        {
            _reduceMotion = reduceMotion;
        }

        private void OnDestroy()
        {
            if (_worldView != null)
            {
                _worldView.SegmentChanged -= OnSegmentChanged;
            }
        }

        public void BeginRun(GameSession session)
        {
            _worlds = (session.World as TrackRunWorld)?.Track?.Worlds;
            Hide();
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        private void OnSegmentChanged(int segment)
        {
            if (_worlds == null || _panel == null)
            {
                return;
            }

            _title.text = WorldBannerStrings.TitleFor(_worlds.KindOfSegment(segment), _worlds.IsDuskSegment(segment));
            _age = 0f;
            _panel.gameObject.SetActive(true);
            Apply(0f);
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_age < 0f)
            {
                return;
            }

            if (session.Phase == SessionPhase.Running)
            {
                _age += realDeltaSeconds;
            }

            if (_age >= TotalSeconds)
            {
                Hide();
                return;
            }

            Apply(_age);
        }

        private void Hide()
        {
            _age = -1f;
            if (_panel != null && _panel.gameObject.activeSelf)
            {
                _panel.gameObject.SetActive(false);
            }
        }

        private void Apply(float age)
        {
            float alpha;
            float slide;
            if (age < SlideInSeconds)
            {
                float u = age / SlideInSeconds;
                float ease = 1f - ((1f - u) * (1f - u) * (1f - u));
                alpha = ease;
                slide = SlideDistancePt * (1f - ease);
            }
            else if (age < SlideInSeconds + HoldSeconds)
            {
                alpha = 1f;
                slide = 0f;
            }
            else
            {
                float u = (age - SlideInSeconds - HoldSeconds) / SlideOutSeconds;
                if (u > 1f)
                {
                    u = 1f;
                }

                float ease = u * u * u;
                alpha = 1f - ease;
                slide = -SlideDistancePt * ease;
            }

            _group.alpha = alpha;
            _panel.anchoredPosition = new Vector2(_reduceMotion ? 0f : slide, 0f);
        }
    }
}
