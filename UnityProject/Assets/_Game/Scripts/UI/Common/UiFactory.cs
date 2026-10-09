using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Builds painterly uGUI elements from code with the <see cref="UiTheme"/>: sliced panels, labels registered for
    /// text scaling, buttons (≥ 44 pt touch targets, press scale, tap feedback hook), chips, toggles, steppers and
    /// sliders. Setup-time only (allocates); per-frame updates go through the views' cached setters.
    /// </summary>
    public sealed class UiFactory
    {
        public const float MinTouch = 44f;

        public UiFactory(UiTheme theme, StringTable strings, TextScaleRegistry texts)
        {
            Theme = theme;
            Strings = strings;
            Texts = texts;
        }

        public UiTheme Theme { get; }

        public StringTable Strings { get; }

        public TextScaleRegistry Texts { get; }

        /// <summary>
        /// While true, new labels use the capped HUD scale (≤ 130 %): fixed-layout screens (home, results, pause,
        /// revive, ability card). Scrolling screens (settings, journal, abilities, credits) scale to 200 %.
        /// </summary>
        public bool CappedScale { get; set; }

        public string S(string key)
        {
            return Strings.Get(key);
        }

        // ---- rects ----

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Top-left placement in the parent (layout units, y down).</summary>
        public static void Place(RectTransform rt, in UiRect r)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(r.W, r.H);
            rt.anchoredPosition = new Vector2(r.X + (r.W * 0.5f), -(r.Y + (r.H * 0.5f)));
        }

        /// <summary>Anchor + pivot at <paramref name="anchor"/>, size and offset.</summary>
        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        public static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Stretch(RectTransform rt)
        {
            Stretch(rt, 0f, 0f, 0f, 0f);
        }

        // ---- images ----

        public Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2f;
            }
            else
            {
                img.preserveAspect = true;
            }

            return img;
        }

        public Image Icon(Transform parent, string name, Sprite sprite, float size)
        {
            Image img = Image(parent, name, sprite, Color.white);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Painted panel with a soft drop shadow; returns the panel image (content goes under it).</summary>
        public Image Panel(Transform parent, string name, Sprite sprite, bool shadow = true)
        {
            RectTransform root = Rect(parent, name);
            if (shadow && Theme.Shadow != null)
            {
                Image sh = Image(root, "Shadow", Theme.Shadow, Color.white);
                sh.type = UnityEngine.UI.Image.Type.Sliced;
                Stretch(sh.rectTransform, -22f, -22f, -16f, -30f);
            }

            Image body = Image(root, "Body", sprite, Color.white);
            Stretch(body.rectTransform);
            return body;
        }

        /// <summary>Root of a <see cref="Panel"/> (the shadow's parent).</summary>
        public static RectTransform PanelRoot(Image panelBody)
        {
            return (RectTransform)panelBody.transform.parent;
        }

        // ---- text ----

        public Font FontOf(FontKind kind)
        {
            switch (kind)
            {
                case FontKind.Heavy:
                    return Theme.Heavy != null ? Theme.Heavy : Theme.Body;
                case FontKind.Display:
                    return Theme.Display != null ? Theme.Display : Theme.Heavy;
                default:
                    return Theme.Body;
            }
        }

        /// <summary>A label (registered for text scaling). <paramref name="fit"/> shrinks to fit (never below 80 %).</summary>
        public Text Label(Transform parent, string name, string text, FontKind font, int size, Color color, TextAnchor align, bool hud = false, bool fit = true, bool shadow = false)
        {
            RectTransform rt = Rect(parent, name);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = FontOf(font);
            t.fontStyle = FontStyle.Normal;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = fit ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = fit;
            t.lineSpacing = 1f;
            t.text = text ?? string.Empty;
            if (shadow)
            {
                Shadow s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = UiColors.TextShadow;
                s.effectDistance = new Vector2(1.5f, -2f);
            }

            Texts.Register(t, size, hud || CappedScale);
            return t;
        }

        // ---- buttons ----

        public Sprite ButtonSprite(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary:
                    return Theme.ButtonPrimary;
                case ButtonStyle.Secondary:
                    return Theme.ButtonSecondary;
                case ButtonStyle.Round:
                    return Theme.Circle;
                default:
                    return Theme.Chip;
            }
        }

        public static Color LabelColor(ButtonStyle style)
        {
            return style == ButtonStyle.Primary ? UiColors.Ink : UiColors.Cream;
        }

        /// <summary>
        /// A button. Size is clamped to ≥ 44 × 44. With an icon, the icon sits left of the label (or alone, centred).
        /// </summary>
        public UiButton Button(Transform parent, string name, ButtonStyle style, string label, Sprite icon, int fontSize, Vector2 size, UnityAction onClick)
        {
            size.x = Mathf.Max(size.x, MinTouch);
            size.y = Mathf.Max(size.y, MinTouch);
            RectTransform root = Rect(parent, name);
            root.sizeDelta = size;
            Image bg = Image(root, "Background", ButtonSprite(style), Color.white, true);
            Stretch(bg.rectTransform);
            if (style == ButtonStyle.Round)
            {
                bg.type = UnityEngine.UI.Image.Type.Simple;
                bg.preserveAspect = false;
            }

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.75f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None; // Space stays "jump"; never clicks a selected button.
            button.navigation = nav;
            root.gameObject.AddComponent<PressScale>();
            button.onClick.AddListener(UiFeedback.OnTap);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var result = new UiButton { Root = root, Button = button, Background = bg };
            bool hasLabel = label != null; // "" = a label filled in later; null = icon-only
            float iconSize = style == ButtonStyle.Round ? size.y * 0.5f : Mathf.Min(size.y * 0.56f, 34f);
            if (icon != null)
            {
                result.Icon = Icon(root, "Icon", icon, iconSize);
                if (hasLabel)
                {
                    Anchor(result.Icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(iconSize, iconSize), new Vector2(16f, 0f));
                }
                else
                {
                    Anchor(result.Icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(iconSize, iconSize), Vector2.zero);
                }
            }

            if (hasLabel)
            {
                result.Label = Label(root, "Label", label, FontKind.Heavy, fontSize, LabelColor(style), TextAnchor.MiddleCenter);
                float left = icon != null ? 16f + iconSize + 4f : 12f;
                Stretch(result.Label.rectTransform, left, 12f, 4f, 4f);
            }

            return result;
        }

        /// <summary>HUD chip: dark pill with an icon and a number (no best-fit: sized by HudLayout).</summary>
        public Text Chip(Transform parent, string name, Sprite icon, out RectTransform root, out Image iconImage)
        {
            root = Rect(parent, name);
            Image bg = Image(root, "Background", Theme.Chip, Color.white);
            Stretch(bg.rectTransform);
            iconImage = Icon(root, "Icon", icon, HudLayout.IconSize);
            Anchor(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(HudLayout.IconSize, HudLayout.IconSize), new Vector2(12f, 0f));
            Text value = Label(root, "Value", string.Empty, FontKind.Heavy, (int)HudLayout.NumberFont, UiColors.Cream, TextAnchor.MiddleLeft, true, false, true);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(value.rectTransform, 12f + HudLayout.IconSize + 6f, 10f, 0f, 0f);
            return value;
        }

        /// <summary>Horizontal ornamental divider.</summary>
        public Image Divider(Transform parent, string name)
        {
            Image d = Image(parent, name, Theme.Divider, new Color(1f, 1f, 1f, 0.9f));
            d.preserveAspect = false;
            return d;
        }

        /// <summary>A uGUI slider in the painted style (track, turquoise fill, gold knob), 44 pt tall hit area.</summary>
        public Slider Slider(Transform parent, string name, float value, UnityAction<float> onChange)
        {
            RectTransform root = Rect(parent, name);
            root.sizeDelta = new Vector2(180f, MinTouch);
            Image hit = Image(root, "Hit", null, new Color(0f, 0f, 0f, 0f), true);
            Stretch(hit.rectTransform);
            Image track = Image(root, "Track", Theme.Track, Color.white);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 14f);
            RectTransform fillArea = Rect(root, "FillArea");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 14f);
            Image fill = Image(fillArea, "Fill", Theme.TrackFill, Color.white);
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = Rect(root, "HandleArea");
            Stretch(handleArea, 12f, 12f, 0f, 0f);
            Image knob = Image(handleArea, "Knob", Theme.Knob, Color.white);
            knob.rectTransform.sizeDelta = new Vector2(28f, 28f);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            Navigation nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            slider.SetValueWithoutNotify(value);
            if (onChange != null)
            {
                slider.onValueChanged.AddListener(onChange);
            }

            return slider;
        }
    }
}
