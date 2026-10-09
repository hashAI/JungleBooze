using JungleBooze.Core.Save;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// Home / expedition camp (GDD §18): painted backdrop (cover-cropped per orientation), the AURELIA wordmark,
    /// START EXPEDITION as the one dominant action, best distance, coins and crystals, and Journal / Abilities /
    /// Settings. Portrait: wordmark top, actions at the bottom (thumb). Landscape: a left column over the calm side of
    /// the painting. Post-MVP slots (map, gear, daily) are not shown as empty buttons.
    /// </summary>
    public sealed class HomeView
    {
        private readonly UiFactory _f;
        private readonly RawImage _backdrop;
        private readonly Image _topShade;
        private readonly Image _bottomShade;
        private readonly Image _sideShade;
        private readonly RectTransform _brand;
        private readonly Text _wordmark;
        private readonly Text _tagline;
        private readonly Image _divider;
        private readonly RectTransform _bestRoot;
        private readonly Text _best;
        private readonly RectTransform _coinsRoot;
        private readonly Text _coins;
        private readonly RectTransform _crystalsRoot;
        private readonly Text _crystals;
        private readonly UiButton _journal;
        private readonly UiButton _abilities;
        private readonly UiButton _settings;
        private bool _landscape;

        public HomeView(UiFactory f, Transform canvas, UnityAction onStart, UnityAction onJournal, UnityAction onAbilities, UnityAction onSettings)
        {
            _f = f;
            Root = UiFactory.Rect(canvas, "Home");
            UiFactory.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            Fader = new UiFader(Group, null);
            _backdrop = Root.gameObject.AddComponent<RawImage>();
            _backdrop.raycastTarget = true; // blocks touches to the world behind
            _backdrop.color = Color.white;

            _topShade = Shade("TopShade", new Vector2(0f, 1f), 180f, false);
            _bottomShade = Shade("BottomShade", new Vector2(0f, 0f), 300f, true);
            _sideShade = f.Image(Root, "SideShade", f.Theme.Scrim, new Color(1f, 1f, 1f, 0.55f));
            _sideShade.preserveAspect = false;
            _sideShade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Safe = UiFactory.Rect(Root, "SafeArea");
            UiFactory.Stretch(Safe);
            Safe.gameObject.AddComponent<SafeAreaFitter>();

            _brand = UiFactory.Rect(Safe, "Brand");
            _wordmark = f.Label(_brand, "Wordmark", f.S("app.wordmark"), FontKind.Display, 60, UiColors.Cream, TextAnchor.MiddleCenter, false, true, true);
            _wordmark.GetComponent<Shadow>().effectDistance = new Vector2(2f, -3f);
            Outline glow = _wordmark.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(0.36f, 0.22f, 0.05f, 0.45f);
            glow.effectDistance = new Vector2(1.5f, -1.5f);
            _divider = f.Divider(_brand, "Divider");
            _tagline = f.Label(_brand, "Tagline", f.S("app.tagline"), FontKind.Heavy, 15, UiColors.Gold, TextAnchor.MiddleCenter, false, true, true);

            _best = f.Chip(Safe, "Best", f.Theme.Record, out _bestRoot, out _);
            _best.fontSize = 18;
            _best.resizeTextForBestFit = true;
            _best.resizeTextMinSize = 13;
            _best.resizeTextMaxSize = 18;
            _best.horizontalOverflow = HorizontalWrapMode.Wrap;
            _coins = f.Chip(Safe, "Coins", f.Theme.Coin, out _coinsRoot, out _);
            _crystals = f.Chip(Safe, "Crystals", f.Theme.Crystal, out _crystalsRoot, out _);

            Start = f.Button(Safe, "Start", ButtonStyle.Primary, f.S("home.start"), f.Theme.Play, 24, new Vector2(320f, 68f), onStart);
            _journal = f.Button(Safe, "Journal", ButtonStyle.Secondary, f.S("home.journal"), f.Theme.Journal, 15, new Vector2(104f, 56f), onJournal);
            _abilities = f.Button(Safe, "Abilities", ButtonStyle.Secondary, f.S("home.abilities"), f.Theme.Abilities, 15, new Vector2(104f, 56f), onAbilities);
            _settings = f.Button(Safe, "Settings", ButtonStyle.Secondary, f.S("home.settings"), f.Theme.Settings, 15, new Vector2(104f, 56f), onSettings);
            Tile(_journal);
            Tile(_abilities);
            Tile(_settings);
            Root.gameObject.SetActive(false);
        }

        public RectTransform Root { get; }

        public RectTransform Safe { get; }

        public CanvasGroup Group { get; }

        public UiFader Fader { get; }

        public UiButton Start { get; }

        public UiButton JournalButton => _journal;

        public UiButton AbilitiesButton => _abilities;

        public UiButton SettingsButton => _settings;

        public bool Visible => Fader.Visible;

        public string BestText => _best.text;

        public void Refresh(SaveData profile)
        {
            if (profile == null)
            {
                return;
            }

            _best.text = profile.bestDistance > 0f
                ? _f.Strings.Format("home.best", JungleBooze.UI.Expedition.ObjectiveText.Metres(_f.Strings, (int)profile.bestDistance))
                : _f.S("home.bestNone");
            _coins.text = NumberText.Group(profile.coins);
            _crystals.text = NumberText.Group(profile.crystals);
        }

        public void Layout(in ScreenFrame frame, float scale)
        {
            _landscape = frame.Landscape;
            Texture2D tex = _landscape ? _f.Theme.HomeLandscape : _f.Theme.HomePortrait;
            if (tex == null)
            {
                tex = _landscape ? _f.Theme.HomePortrait : _f.Theme.HomeLandscape;
            }

            _backdrop.texture = tex;
            _backdrop.uvRect = CoverUv(tex, frame.Width, frame.Height, _landscape ? 0.5f : 0.45f);
            float w = frame.SafeWidth;
            float h = frame.SafeHeight;
            float chipH = 40f;
            float coinW = HudLayout.ChipWidth(6, 1f);
            float crysW = HudLayout.ChipWidth(4, 1f);
            UiFactory.Place(_crystalsRoot, new UiRect(w - 12f - crysW, 8f, crysW, chipH));
            UiFactory.Place(_coinsRoot, new UiRect(w - 12f - crysW - 8f - coinW, 8f, coinW, chipH));
            float tile = 104f;
            float tileH = 66f * Mathf.Min(scale, 1.2f);
            _sideShade.gameObject.SetActive(_landscape);
            if (_landscape)
            {
                // Left column over the calm, sunlit third of the painting.
                float colW = Mathf.Min(360f, w * 0.48f);
                float x = 18f;
                float brandH = 116f;
                float y = Mathf.Max(8f, (h - (brandH + 18f + 40f + 14f + 68f + 12f + tileH)) * 0.5f);
                UiFactory.Place(_brand, new UiRect(x, y, colW, brandH));
                LayoutBrand(colW, brandH, 56f);
                y += brandH + 18f;
                UiFactory.Place(_bestRoot, new UiRect(x + ((colW - 250f) * 0.5f), y, 250f, chipH));
                y += chipH + 14f;
                UiFactory.Place(Start.Root, new UiRect(x, y, colW, 68f));
                y += 68f + 12f;
                float gap = (colW - (3f * tile)) * 0.5f;
                UiFactory.Place(_journal.Root, new UiRect(x, y, tile, tileH));
                UiFactory.Place(_abilities.Root, new UiRect(x + tile + gap, y, tile, tileH));
                UiFactory.Place(_settings.Root, new UiRect(x + (2f * (tile + gap)), y, tile, tileH));
                RectTransform side = _sideShade.rectTransform;
                side.anchorMin = new Vector2(0f, 0.5f);
                side.anchorMax = new Vector2(0f, 0.5f);
                side.pivot = new Vector2(0.5f, 1f); // the sprite's opaque top edge, rotated to the screen's left edge
                side.sizeDelta = new Vector2(frame.Height + 40f, frame.Left + colW + 180f);
                side.anchoredPosition = Vector2.zero;
                _topShade.gameObject.SetActive(false);
            }
            else
            {
                _topShade.gameObject.SetActive(true);
                float colW = Mathf.Min(w - 32f, 360f);
                float x = (w - colW) * 0.5f;
                float brandH = 120f;
                UiFactory.Place(_brand, new UiRect(x, 56f, colW, brandH));
                LayoutBrand(colW, brandH, 62f);
                float bottom = h - 16f;
                float gap = (colW - (3f * tile)) * 0.5f;
                float tileY = bottom - tileH;
                UiFactory.Place(_journal.Root, new UiRect(x, tileY, tile, tileH));
                UiFactory.Place(_abilities.Root, new UiRect(x + tile + gap, tileY, tile, tileH));
                UiFactory.Place(_settings.Root, new UiRect(x + (2f * (tile + gap)), tileY, tile, tileH));
                float startY = tileY - 14f - 68f;
                UiFactory.Place(Start.Root, new UiRect(x, startY, colW, 68f));
                UiFactory.Place(_bestRoot, new UiRect((w - 250f) * 0.5f, startY - 14f - chipH, 250f, chipH));
            }
        }

        public void Show(bool visible, bool instant)
        {
            Fader.Show(visible, instant);
        }

        /// <summary>Home's own controls hide while a screen opened from Home (settings, journal…) is on top.</summary>
        public void SetContentVisible(bool visible)
        {
            if (Safe.gameObject.activeSelf != visible)
            {
                Safe.gameObject.SetActive(visible);
            }
        }

        /// <summary>UV rect that covers a W×H area with the texture (crop, no stretch), focus x in 0–1.</summary>
        public static Rect CoverUv(Texture tex, float width, float height, float focusX)
        {
            if (tex == null || width <= 0f || height <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float texAspect = tex.width / (float)tex.height;
            float aspect = width / height;
            if (aspect > texAspect)
            {
                float h = texAspect / aspect;
                return new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }

            float w = aspect / texAspect;
            float x = Mathf.Clamp((focusX - (w * 0.5f)), 0f, 1f - w);
            return new Rect(x, 0f, w, 1f);
        }

        private void LayoutBrand(float w, float h, float size)
        {
            _wordmark.rectTransform.anchorMin = new Vector2(0f, 1f);
            _wordmark.rectTransform.anchorMax = new Vector2(1f, 1f);
            _wordmark.rectTransform.pivot = new Vector2(0.5f, 1f);
            _wordmark.rectTransform.sizeDelta = new Vector2(0f, size + 18f);
            _wordmark.rectTransform.anchoredPosition = Vector2.zero;
            _divider.rectTransform.anchorMin = new Vector2(0.12f, 1f);
            _divider.rectTransform.anchorMax = new Vector2(0.88f, 1f);
            _divider.rectTransform.sizeDelta = new Vector2(0f, 12f);
            _divider.rectTransform.anchoredPosition = new Vector2(0f, -(size + 22f));
            _tagline.rectTransform.anchorMin = new Vector2(0f, 1f);
            _tagline.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tagline.rectTransform.pivot = new Vector2(0.5f, 1f);
            _tagline.rectTransform.sizeDelta = new Vector2(0f, 24f);
            _tagline.rectTransform.anchoredPosition = new Vector2(0f, -(size + 36f));
        }

        /// <summary>Tile look: icon on top, one-line label below (never broken mid-word).</summary>
        private static void Tile(UiButton b)
        {
            UiFactory.Anchor(b.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(30f, 30f), new Vector2(0f, -5f));
            b.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.Label.resizeTextForBestFit = false;
            b.Label.alignment = TextAnchor.LowerCenter;
            UiFactory.Stretch(b.Label.rectTransform, 4f, 4f, 30f, 6f);
        }

        private Image Shade(string name, Vector2 edge, float height, bool fromBottom)
        {
            Image img = _f.Image(Root, name, _f.Theme.Scrim, new Color(1f, 1f, 1f, fromBottom ? 0.75f : 0.45f));
            img.preserveAspect = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, edge.y);
            rt.anchorMax = new Vector2(1f, edge.y);
            rt.pivot = new Vector2(0.5f, edge.y);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            if (fromBottom)
            {
                rt.localScale = new Vector3(1f, -1f, 1f);
            }

            return img;
        }
    }
}
