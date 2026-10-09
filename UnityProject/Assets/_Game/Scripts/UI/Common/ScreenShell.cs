using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// A menu screen's frame: full-screen dimming scrim (blocks touches to the game), a safe-area child, a painted
    /// panel sized per orientation, an optional header (back button + title) and an optional scroll list whose rows
    /// stack with a VerticalLayoutGroup (so any text size fits: the list scrolls). Fades in ≤ 0.18 s.
    /// </summary>
    public sealed class ScreenShell
    {
        public const float HeaderHeight = 60f;

        private readonly UiFactory _f;
        private readonly Vector2 _maxPortrait;
        private readonly Vector2 _maxLandscape;
        private readonly System.Collections.Generic.List<LayoutElement> _rows = new System.Collections.Generic.List<LayoutElement>(16);
        private readonly System.Collections.Generic.List<float> _rowHeights = new System.Collections.Generic.List<float>(16);

        public ScreenShell(UiFactory f, Transform canvasRoot, string name, Vector2 maxPortrait, Vector2 maxLandscape, bool scrim = true)
        {
            _f = f;
            _maxPortrait = maxPortrait;
            _maxLandscape = maxLandscape;
            Root = UiFactory.Rect(canvasRoot, name);
            UiFactory.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            ScrimImage = f.Image(Root, "Scrim", null, scrim ? UiColors.Scrim : new Color(0f, 0f, 0f, 0f), true);
            ScrimImage.type = Image.Type.Simple;
            UiFactory.Stretch(ScrimImage.rectTransform);
            Safe = UiFactory.Rect(Root, "SafeArea");
            UiFactory.Stretch(Safe);
            Safe.gameObject.AddComponent<SafeAreaFitter>();
            Body = f.Panel(Safe, "Panel", f.Theme.Panel);
            PanelRoot = UiFactory.PanelRoot(Body);
            UiFactory.Anchor(PanelRoot, new Vector2(0.5f, 0.5f), maxPortrait, Vector2.zero);
            PanelRoot.pivot = new Vector2(0.5f, 0.5f);
            Fader = new UiFader(Group, PanelRoot);
            Root.gameObject.SetActive(false);
        }

        public RectTransform Root { get; }

        public CanvasGroup Group { get; }

        public Image ScrimImage { get; }

        public RectTransform Safe { get; }

        public RectTransform PanelRoot { get; }

        public Image Body { get; }

        public UiFader Fader { get; }

        public Text Title { get; private set; }

        public UiButton BackButton { get; private set; }

        public RectTransform ListContent { get; private set; }

        public ScrollRect Scroll { get; private set; }

        public bool Visible => Fader.Visible;

        /// <summary>Header: back button (left, ≥ 44 pt) and the title (Cinzel).</summary>
        public void AddHeader(string title, UnityAction onBack)
        {
            RectTransform header = UiFactory.Rect(Body.transform, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, HeaderHeight);
            header.anchoredPosition = new Vector2(0f, -6f);
            if (onBack != null)
            {
                BackButton = _f.Button(header, "Back", ButtonStyle.Round, null, _f.Theme.Back, 0, new Vector2(48f, 48f), onBack);
                UiFactory.Anchor(BackButton.Root, new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(14f, 0f));
            }

            Title = _f.Label(header, "Title", title, FontKind.Display, 26, UiColors.Gold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(Title.rectTransform, 70f, 70f, 4f, 4f);
            Image divider = _f.Divider(header, "Divider");
            divider.rectTransform.anchorMin = new Vector2(0.1f, 0f);
            divider.rectTransform.anchorMax = new Vector2(0.9f, 0f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 10f);
            divider.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>A vertical scroll list under the header (RectMask2D viewport; rows use LayoutElement heights).</summary>
        public RectTransform AddList(float spacing)
        {
            RectTransform viewport = UiFactory.Rect(Body.transform, "Viewport");
            UiFactory.Stretch(viewport, 16f, 16f, Title != null ? HeaderHeight + 12f : 16f, 16f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = _f.Image(viewport, "Hit", null, new Color(0f, 0f, 0f, 0f), true);
            hit.type = Image.Type.Simple;
            UiFactory.Stretch(hit.rectTransform);
            RectTransform content = UiFactory.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(4, 4, 4, 12);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = true;
            ListContent = content;
            Scroll = scroll;
            return content;
        }

        /// <summary>A list row of a given height (grows with the text scale).</summary>
        public RectTransform Row(string name, float height)
        {
            RectTransform row = UiFactory.Rect(ListContent, name);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            _rows.Add(le);
            _rowHeights.Add(height);
            return row;
        }

        /// <summary>Row heights follow the text size (setup and settings changes only).</summary>
        public void ScaleRows(float scale)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                float h = _rowHeights[i] * (scale < 1f ? 1f : scale);
                _rows[i].minHeight = h;
                _rows[i].preferredHeight = h;
            }
        }

        /// <summary>Sizes the panel for the orientation inside the safe area (in layout units).</summary>
        public void Layout(in ScreenFrame frame)
        {
            Vector2 max = frame.Landscape ? _maxLandscape : _maxPortrait;
            float w = Mathf.Min(max.x, frame.SafeWidth - 20f);
            float h = Mathf.Min(max.y, frame.SafeHeight - 16f);
            PanelRoot.sizeDelta = new Vector2(w, h);
        }

        public void Show(bool visible, bool instant)
        {
            Fader.Show(visible, instant);
            if (visible && Scroll != null)
            {
                Scroll.verticalNormalizedPosition = 1f;
            }
        }
    }
}
