using JungleBooze.Gameplay.Views;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// Builds uGUI elements from code in the style guide's comic-panel look (section 8): parchment fill, 3 pt ink
    /// border, 4 pt hard ink shadow down-right. Units are points (the canvas scaler references 390 × 844 pt).
    /// Setup-time only (allocates).
    /// </summary>
    public static class HudFactory
    {
        /// <summary>Raised after any button made here is pressed (audio tap sound hooks in without the UI knowing about audio).</summary>
        public static event System.Action ButtonPressed;

        private static readonly UnityAction NotifyPressedAction = NotifyPressed;

        private static void NotifyPressed()
        {
            ButtonPressed?.Invoke();
        }

        public const float BorderPt = 3f;
        public const float ShadowPt = 4f;

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchors and pivots <paramref name="rt"/> at <paramref name="anchor"/> (0..1) with a size and offset.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        public static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image CreateImage(Transform parent, string name, Color color, bool raycastTarget)
        {
            RectTransform rt = CreateRect(parent, name);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>
        /// Comic panel: hard shadow, ink border, colored fill. Returns the fill image; put content under it.
        /// The root is the returned image's parent.
        /// </summary>
        public static Image CreatePanel(Transform parent, string name, Color fill, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            RectTransform root = CreateRect(parent, name);
            Place(root, anchor, size, offset);

            Image shadow = CreateImage(root, "Shadow", StylePalette.Ink, false);
            Stretch(shadow.rectTransform, 0f);
            shadow.rectTransform.offsetMin = new Vector2(ShadowPt, -ShadowPt);
            shadow.rectTransform.offsetMax = new Vector2(ShadowPt, -ShadowPt);

            Image border = CreateImage(root, "Border", StylePalette.Ink, false);
            Stretch(border.rectTransform, 0f);

            Image fillImage = CreateImage(root, "Fill", fill, false);
            Stretch(fillImage.rectTransform, BorderPt);
            return fillImage;
        }

        public static Text CreateText(Transform parent, string name, Font font, int fontSize, Color color, Color outline, TextAnchor alignment)
        {
            RectTransform rt = CreateRect(parent, name);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (outline.a > 0f)
            {
                Outline effect = rt.gameObject.AddComponent<Outline>();
                effect.effectColor = outline;
                effect.effectDistance = new Vector2(2f, -2f);
            }

            return text;
        }

        /// <summary>
        /// Comic-style button (style guide 8.2) of at least 44 pt. The fill is the raycast target. Pressed state
        /// darkens the fill. Returns the button; its label is the fill's child "Label" (if a label is given).
        /// </summary>
        public static Button CreateButton(
            Transform parent,
            string name,
            Color fill,
            Vector2 anchor,
            Vector2 size,
            Vector2 offset,
            Font font,
            string label,
            int fontSize,
            Color labelColor,
            Color labelOutline,
            UnityAction onClick)
        {
            if (size.x < 44f)
            {
                size.x = 44f;
            }

            if (size.y < 44f)
            {
                size.y = 44f;
            }

            Image fillImage = CreatePanel(parent, name, fill, anchor, size, offset);
            fillImage.raycastTarget = true;

            Button button = fillImage.gameObject.AddComponent<Button>();
            button.targetGraphic = fillImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            // No keyboard/gamepad navigation: Space must stay "Jump" and never click a selected button.
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            button.onClick.AddListener(NotifyPressedAction);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            if (!string.IsNullOrEmpty(label))
            {
                Text text = CreateText(fillImage.transform, "Label", font, fontSize, labelColor, labelOutline, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform, 0f);
                text.text = label;
            }

            return button;
        }
    }
}
