using JungleBooze.Gameplay.Views;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Menu building blocks on top of <see cref="HudFactory"/> in the style guide's button types (section 8.2):
    /// primary (pulp orange), secondary (teal), neutral (parchment); plus labels, label/value rows, sliders and
    /// the coin icon. Units are points (390 × 844 pt reference canvas). Setup-time only (allocates).
    /// </summary>
    public static class MenuFactory
    {
        public const int MinFontSize = 15;

        private static readonly Color NoOutline = new Color(0f, 0f, 0f, 0f);

        public static Button PrimaryButton(Transform parent, string name, Font font, string label, int fontSize, Vector2 anchor, Vector2 size, Vector2 offset, UnityAction onClick)
        {
            return HudFactory.CreateButton(
                parent, name, StylePalette.PulpOrange, anchor, size, offset, font, label, fontSize, StylePalette.Parchment, StylePalette.Ink, onClick);
        }

        public static Button SecondaryButton(Transform parent, string name, Font font, string label, int fontSize, Vector2 anchor, Vector2 size, Vector2 offset, UnityAction onClick)
        {
            return HudFactory.CreateButton(
                parent, name, StylePalette.PistaTealSash, anchor, size, offset, font, label, fontSize, StylePalette.Parchment, StylePalette.Ink, onClick);
        }

        public static Button NeutralButton(Transform parent, string name, Font font, string label, int fontSize, Vector2 anchor, Vector2 size, Vector2 offset, UnityAction onClick)
        {
            return HudFactory.CreateButton(
                parent, name, StylePalette.Parchment, anchor, size, offset, font, label, fontSize, StylePalette.Ink, NoOutline, onClick);
        }

        /// <summary>Ink text placed at <paramref name="anchor"/> with the given size and offset.</summary>
        public static Text Label(Transform parent, string name, Font font, int fontSize, TextAnchor alignment, Vector2 anchor, Vector2 size, Vector2 offset, string text)
        {
            Text label = HudFactory.CreateText(parent, name, font, Mathf.Max(fontSize, MinFontSize), StylePalette.Ink, NoOutline, alignment);
            HudFactory.Place(label.rectTransform, anchor, size, offset);
            label.text = text;
            return label;
        }

        /// <summary>
        /// A row anchored to the parent's top: label on the left, value on the right. Returns the value text and
        /// the row's RectTransform.
        /// </summary>
        public static Text Row(Transform parent, string name, Font font, int fontSize, string label, float width, float y, out RectTransform row)
        {
            row = HudFactory.CreateRect(parent, name);
            HudFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(width, fontSize * 1.4f), new Vector2(0f, y));

            Text labelText = HudFactory.CreateText(row, "Label", font, Mathf.Max(fontSize, MinFontSize), StylePalette.Ink, NoOutline, TextAnchor.MiddleLeft);
            HudFactory.Stretch(labelText.rectTransform, 0f);
            labelText.text = label;

            Text value = HudFactory.CreateText(row, "Value", font, Mathf.Max(fontSize, MinFontSize), StylePalette.Ink, NoOutline, TextAnchor.MiddleRight);
            HudFactory.Stretch(value.rectTransform, 0f);
            return value;
        }

        /// <summary>Gold coin with the turquoise gem (style guide 7.2), 26 pt, left-middle of the parent.</summary>
        public static void CoinIcon(Transform parent, Vector2 anchor, Vector2 offset)
        {
            Image coinRim = HudFactory.CreateImage(parent, "CoinIcon", StylePalette.CoinRim, false);
            HudFactory.Place(coinRim.rectTransform, anchor, new Vector2(26f, 26f), offset);
            Image coinFace = HudFactory.CreateImage(coinRim.transform, "Face", StylePalette.CoinGold, false);
            HudFactory.Stretch(coinFace.rectTransform, 3f);
            Image coinGem = HudFactory.CreateImage(coinFace.transform, "Gem", StylePalette.CoinGem, false);
            HudFactory.Place(coinGem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(9f, 9f), Vector2.zero);
            coinGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>A <see cref="DigitCounter"/> in ink (no per-frame allocations when its value changes).</summary>
        public static DigitCounter Counter(Transform parent, string name, Font font, int fontSize, int digits, string suffix, bool centered)
        {
            RectTransform rt = HudFactory.CreateRect(parent, name);
            DigitCounter counter = rt.gameObject.AddComponent<DigitCounter>();
            counter.Build(font, fontSize, digits, StylePalette.Ink, NoOutline, suffix, centered);
            return counter;
        }

        /// <summary>
        /// Horizontal slider from 0 to <paramref name="steps"/> in whole steps. The whole rect (at least 44 pt tall)
        /// takes touches; tapping the track jumps there. Ink track, teal fill, pulp-orange handle with ink border.
        /// </summary>
        public static Slider CreateSlider(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 offset, int steps)
        {
            const float TrackHeightPt = 12f;
            const float HandleWidthPt = 28f;
            if (size.y < 44f)
            {
                size.y = 44f;
            }

            RectTransform root = HudFactory.CreateRect(parent, name);
            HudFactory.Place(root, anchor, size, offset);

            // Invisible hit area over the whole rect.
            Image hitArea = HudFactory.CreateImage(root, "HitArea", NoOutline, true);
            HudFactory.Stretch(hitArea.rectTransform, 0f);

            Image track = HudFactory.CreateImage(root, "Track", StylePalette.Ink, false);
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.offsetMin = new Vector2(0f, -TrackHeightPt * 0.5f);
            trackRect.offsetMax = new Vector2(0f, TrackHeightPt * 0.5f);
            Image trackInner = HudFactory.CreateImage(track.transform, "Inner", StylePalette.CreamPath, false);
            HudFactory.Stretch(trackInner.rectTransform, 2f);

            RectTransform fillArea = HudFactory.CreateRect(root, "FillArea");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.offsetMin = new Vector2(2f, -TrackHeightPt * 0.5f + 2f);
            fillArea.offsetMax = new Vector2(-HandleWidthPt * 0.5f, TrackHeightPt * 0.5f - 2f);
            Image fill = HudFactory.CreateImage(fillArea, "Fill", StylePalette.PistaTealSash, false);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            RectTransform handleArea = HudFactory.CreateRect(root, "HandleArea");
            HudFactory.Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(HandleWidthPt * 0.5f, 4f);
            handleArea.offsetMax = new Vector2(-HandleWidthPt * 0.5f, -4f);
            Image handle = HudFactory.CreateImage(handleArea, "Handle", StylePalette.Ink, true);
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(HandleWidthPt, 0f);
            Image handleFill = HudFactory.CreateImage(handle.transform, "Fill", StylePalette.PulpOrange, false);
            HudFactory.Stretch(handleFill.rectTransform, 3f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = steps;
            slider.wholeNumbers = true;

            // No keyboard navigation: arrow keys stay gameplay keys.
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            return slider;
        }
    }
}
