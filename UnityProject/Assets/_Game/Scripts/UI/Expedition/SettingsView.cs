using System.Globalization;
using JungleBooze.Gameplay.Feedback;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// Settings (GDD §20): steering sensitivity 0.5–2.0×, left/right hand, music and SFX volume (hooks), haptics,
    /// reduced motion, text size 100–200 %, privacy policy, credits, version. On/off states show a word and an icon
    /// (check / cross), never colour alone. A scroll list, so every text size fits.
    /// </summary>
    public sealed class SettingsView
    {
        private const float RowH = 56f;

        private readonly UiFactory _f;
        private readonly FeelSettings _feel;
        private readonly UiPreferences _prefs;
        private readonly UnityAction _changed;
        private readonly Text _sensitivityValue;
        private readonly Text _textSizeValue;
        private readonly UiButton _handRight;
        private readonly UiButton _handLeft;
        private readonly UiButton _haptics;
        private readonly UiButton _reducedMotion;
        private readonly Slider _music;
        private readonly Slider _sfx;
        private readonly Text _musicValue;
        private readonly Text _sfxValue;
        private readonly UiButton _privacy;
        private readonly string _url;

        public SettingsView(UiFactory f, Transform canvas, FeelSettings feel, UiPreferences prefs, string version, string privacyUrl, UnityAction onBack, UnityAction onCredits, UnityAction changed)
        {
            _f = f;
            _feel = feel;
            _prefs = prefs;
            _changed = changed;
            _url = privacyUrl;
            Shell = new ScreenShell(f, canvas, "Settings", new Vector2(400f, 720f), new Vector2(620f, 360f));
            Shell.AddHeader(f.S("settings.title"), onBack);
            Shell.AddList(4f);

            Section("settings.section.controls");
            RectTransform row = Row("Sensitivity", "settings.sensitivity");
            _sensitivityValue = Stepper(row, () => { _feel.StepSensitivity(-1); Changed(); }, () => { _feel.StepSensitivity(1); Changed(); });
            row = Row("Hand", "settings.handed");
            _handLeft = f.Button(row, "Left", ButtonStyle.Ghost, f.S("settings.handed.left"), f.Theme.Check, 16, new Vector2(96f, 44f), () => { _prefs.SetLeftHanded(true); Changed(); });
            _handRight = f.Button(row, "Right", ButtonStyle.Ghost, f.S("settings.handed.right"), f.Theme.Check, 16, new Vector2(96f, 44f), () => { _prefs.SetLeftHanded(false); Changed(); });
            UiFactory.Anchor(_handRight.Root, new Vector2(1f, 0.5f), new Vector2(96f, 44f), Vector2.zero);
            UiFactory.Anchor(_handLeft.Root, new Vector2(1f, 0.5f), new Vector2(96f, 44f), new Vector2(-102f, 0f));

            Section("settings.section.sound");
            row = Row("Music", "settings.music");
            _music = Slider(row, prefs.MusicVolume, v => { _prefs.SetMusicVolume(v); Changed(); }, out _musicValue);
            row = Row("Sfx", "settings.sfx");
            _sfx = Slider(row, prefs.SfxVolume, v => { _prefs.SetSfxVolume(v); Changed(); }, out _sfxValue);

            Section("settings.section.display");
            row = Row("Haptics", "settings.haptics");
            _haptics = Toggle(row, () => { _feel.SetHaptics(!_feel.HapticsEnabled); Changed(); });
            row = Row("ReducedMotion", "settings.reducedMotion");
            _reducedMotion = Toggle(row, () => { _feel.SetReducedMotion(!_feel.ReducedMotion); Changed(); });
            row = Row("TextSize", "settings.textSize");
            _textSizeValue = Stepper(row, () => { _prefs.SetTextSizeIndex(_prefs.TextSizeIndex - 1); Changed(); }, () => { _prefs.SetTextSizeIndex(_prefs.TextSizeIndex + 1); Changed(); });

            Section("settings.section.about");
            row = Shell.Row("PrivacyRow", RowH);
            _privacy = f.Button(row, "Privacy", ButtonStyle.Ghost, f.S("settings.privacy"), f.Theme.Next, 17, new Vector2(200f, 48f), OpenPrivacy);
            UiFactory.Stretch(_privacy.Root, 0f, 0f, 4f, 4f);
            row = Shell.Row("CreditsRow", RowH);
            UiButton credits = f.Button(row, "Credits", ButtonStyle.Ghost, f.S("settings.credits"), f.Theme.Next, 17, new Vector2(200f, 48f), onCredits);
            UiFactory.Stretch(credits.Root, 0f, 0f, 4f, 4f);
            row = Shell.Row("VersionRow", 32f);
            Text versionText = f.Label(row, "Version", f.Strings.Format("settings.version", version), FontKind.Body, 14, UiColors.Mist, TextAnchor.MiddleCenter);
            UiFactory.Stretch(versionText.rectTransform);
            Refresh();
        }

        public ScreenShell Shell { get; }

        public void Refresh()
        {
            _sensitivityValue.text = _feel.Sensitivity.ToString("0.0", CultureInfo.InvariantCulture) + "×";
            _textSizeValue.text = UiPreferences.Percent(_prefs.TextSizeIndex).ToString(CultureInfo.InvariantCulture) + "%";
            SetSegment(_handLeft, _prefs.LeftHanded);
            SetSegment(_handRight, !_prefs.LeftHanded);
            SetToggle(_haptics, _feel.HapticsEnabled);
            SetToggle(_reducedMotion, _feel.ReducedMotion);
            _music.SetValueWithoutNotify(_prefs.MusicVolume);
            _sfx.SetValueWithoutNotify(_prefs.SfxVolume);
            _musicValue.text = Percent(_prefs.MusicVolume);
            _sfxValue.text = Percent(_prefs.SfxVolume);
            _privacy.Interactable = !string.IsNullOrEmpty(_url);
        }

        private void Changed()
        {
            Refresh();
            _changed?.Invoke();
        }

        private void OpenPrivacy()
        {
            if (!string.IsNullOrEmpty(_url))
            {
                Application.OpenURL(_url);
            }
        }

        private void Section(string key)
        {
            RectTransform row = Shell.Row("Section", 34f);
            Text t = _f.Label(row, "Label", _f.S(key).ToUpperInvariant(), FontKind.Heavy, 14, UiColors.Gold, TextAnchor.LowerLeft);
            UiFactory.Stretch(t.rectTransform, 6f, 6f, 0f, 4f);
        }

        private RectTransform Row(string name, string labelKey)
        {
            RectTransform row = Shell.Row(name, RowH);
            Image line = _f.Image(row, "Line", null, new Color(0.89f, 0.75f, 0.43f, 0.18f));
            line.type = Image.Type.Simple;
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 1f);
            Text label = _f.Label(row, "Label", _f.S(labelKey), FontKind.Body, 17, UiColors.Cream, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            return row;
        }

        private Text Stepper(RectTransform row, UnityAction minus, UnityAction plus)
        {
            UiButton up = _f.Button(row, "Plus", ButtonStyle.Round, "+", null, 24, new Vector2(44f, 44f), plus);
            UiFactory.Anchor(up.Root, new Vector2(1f, 0.5f), new Vector2(44f, 44f), Vector2.zero);
            Text value = _f.Label(row, "Value", string.Empty, FontKind.Heavy, 18, UiColors.Cream, TextAnchor.MiddleCenter);
            UiFactory.Anchor(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(76f, 44f), new Vector2(-46f, 0f));
            UiButton down = _f.Button(row, "Minus", ButtonStyle.Round, "−", null, 24, new Vector2(44f, 44f), minus);
            UiFactory.Anchor(down.Root, new Vector2(1f, 0.5f), new Vector2(44f, 44f), new Vector2(-124f, 0f));
            return value;
        }

        private UiButton Toggle(RectTransform row, UnityAction onClick)
        {
            UiButton b = _f.Button(row, "Toggle", ButtonStyle.Ghost, _f.S("settings.on"), _f.Theme.Check, 16, new Vector2(104f, 44f), onClick);
            UiFactory.Anchor(b.Root, new Vector2(1f, 0.5f), new Vector2(104f, 44f), Vector2.zero);
            return b;
        }

        private Slider Slider(RectTransform row, float value, UnityAction<float> onChange, out Text valueText)
        {
            Slider s = _f.Slider(row, "Slider", value, onChange);
            RectTransform rt = (RectTransform)s.transform;
            rt.anchorMin = new Vector2(0.48f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(-58f, 44f);
            rt.anchoredPosition = new Vector2(-58f, 0f);
            valueText = _f.Label(row, "Value", string.Empty, FontKind.Heavy, 15, UiColors.Mist, TextAnchor.MiddleRight);
            UiFactory.Anchor(valueText.rectTransform, new Vector2(1f, 0.5f), new Vector2(54f, 40f), Vector2.zero);
            return s;
        }

        private void SetToggle(UiButton b, bool on)
        {
            b.SetLabel(_f.S(on ? "settings.on" : "settings.off"));
            b.Icon.sprite = on ? _f.Theme.Check : _f.Theme.Close;
            b.Background.sprite = on ? _f.Theme.ButtonSecondary : _f.Theme.Chip;
        }

        private void SetSegment(UiButton b, bool selected)
        {
            // Selected = turquoise fill + a check mark (shape cue, not colour alone).
            b.Background.sprite = selected ? _f.Theme.ButtonSecondary : _f.Theme.Chip;
            b.Icon.gameObject.SetActive(selected);
            b.Label.color = selected ? UiColors.Cream : UiColors.Mist;
        }

        private static string Percent(float v)
        {
            return Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
