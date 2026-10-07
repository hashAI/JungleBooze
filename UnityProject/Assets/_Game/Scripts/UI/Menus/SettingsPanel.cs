using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Settings (GDD 19 and 20; placeholder look): music and sound-effect volume sliders in 10% steps, haptics
    /// on/off and Reduce Motion on/off, and Back. Changes go straight into <see cref="PlayerSave"/>; Back writes
    /// the save if anything changed. On/off states are shown as text ("On"/"Off") and fill color, so they never
    /// rely on color alone. Audio and haptics arrive later; their settings are stored now.
    /// Later rows from GDD 19 (Companion voice, left-handed, color-blind mode, replay tutorial, Restore Purchases,
    /// privacy policy, credits) go here when those features exist.
    /// </summary>
    public sealed class SettingsPanel
    {
        private const int VolumeSteps = 10;
        private const float WidthPt = 320f;
        private const float HeightPt = 520f;
        private const float ContentWidthPt = 270f;
        private const int TitleFontSize = 34;
        private const int RowFontSize = 20;
        private const int ToggleFontSize = 20;
        private const int BackFontSize = 24;

        private static readonly string[] PercentStrings = BuildPercentStrings();

        private readonly PlayerSave _save;
        private readonly UnityAction _onClosed;
        private readonly GameObject _root;
        private readonly Slider _music;
        private readonly Slider _sfx;
        private readonly Text _musicValue;
        private readonly Text _sfxValue;
        private readonly Button _haptics;
        private readonly Button _reduceMotion;
        private readonly Button _replayTutorial;
        private readonly Text _replayTutorialLabel;

        public SettingsPanel(Transform safeArea, Font font, PlayerSave save, UnityAction onClosed)
        {
            _save = save;
            _onClosed = onClosed;

            Image fill = HudFactory.CreatePanel(
                safeArea, "SettingsPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(WidthPt, HeightPt), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;
            Vector2 top = new Vector2(0.5f, 1f);

            MenuFactory.Label(panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 56f), new Vector2(0f, -10f), MenuStrings.Settings);

            _musicValue = MenuFactory.Row(panel, "MusicRow", font, RowFontSize, MenuStrings.Music, ContentWidthPt, -74f, out _);
            _music = MenuFactory.CreateSlider(panel, "MusicSlider", top, new Vector2(ContentWidthPt, 44f), new Vector2(0f, -104f), VolumeSteps);
            _music.onValueChanged.AddListener(OnMusicChanged);

            _sfxValue = MenuFactory.Row(panel, "SfxRow", font, RowFontSize, MenuStrings.SoundEffects, ContentWidthPt, -160f, out _);
            _sfx = MenuFactory.CreateSlider(panel, "SfxSlider", top, new Vector2(ContentWidthPt, 44f), new Vector2(0f, -190f), VolumeSteps);
            _sfx.onValueChanged.AddListener(OnSfxChanged);

            _haptics = CreateToggleRow(panel, "Haptics", font, MenuStrings.Haptics, -252f, OnHapticsClicked);
            _reduceMotion = CreateToggleRow(panel, "ReduceMotion", font, MenuStrings.ReduceMotion, -316f, OnReduceMotionClicked);

            _replayTutorial = MenuFactory.NeutralButton(
                panel, "ReplayTutorialButton", font, MenuStrings.ReplayTutorial, ToggleFontSize, top, new Vector2(ContentWidthPt, 48f), new Vector2(0f, -376f), OnReplayTutorialClicked);
            _replayTutorialLabel = _replayTutorial.GetComponentInChildren<Text>();

            BackButton = MenuFactory.NeutralButton(
                panel, "BackButton", font, MenuStrings.Back, BackFontSize, new Vector2(0.5f, 0f), new Vector2(220f, 60f), new Vector2(0f, 20f), Close);

            _root.SetActive(false);
        }

        public Button BackButton { get; }

        public Button HapticsButton => _haptics;

        public Button ReduceMotionButton => _reduceMotion;

        /// <summary>"Replay tutorial" (GDD 12): the next run teaches again.</summary>
        public Button ReplayTutorialButton => _replayTutorial;

        public Slider MusicSlider => _music;

        public Slider SfxSlider => _sfx;

        public bool Visible => _root.activeSelf;

        /// <summary>Shows the panel with the current settings (no change events fire).</summary>
        public void Open()
        {
            Refresh();
            _root.SetActive(true);
        }

        /// <summary>Back: hides the panel, writes the save if a setting changed, then notifies the owner.</summary>
        public void Close()
        {
            if (!_root.activeSelf)
            {
                return;
            }

            _root.SetActive(false);
            _save?.SaveIfDirty();
            _onClosed?.Invoke();
        }

        private static string[] BuildPercentStrings()
        {
            var strings = new string[VolumeSteps + 1];
            for (int i = 0; i <= VolumeSteps; i++)
            {
                strings[i] = (i * 100 / VolumeSteps).ToString(System.Globalization.CultureInfo.InvariantCulture) + MenuStrings.PercentSuffix;
            }

            return strings;
        }

        private static int StepOf(float volume)
        {
            int step = Mathf.RoundToInt(volume * VolumeSteps);
            return step < 0 ? 0 : (step > VolumeSteps ? VolumeSteps : step);
        }

        private static void ShowToggle(Button button, bool on)
        {
            ((Image)button.targetGraphic).color = on ? StylePalette.PistaTealSash : StylePalette.Parchment;
            Text label = button.GetComponentInChildren<Text>();
            label.text = on ? MenuStrings.On : MenuStrings.Off;
            label.color = on ? StylePalette.Parchment : StylePalette.Ink;
        }

        private Button CreateToggleRow(Transform panel, string name, Font font, string label, float y, UnityAction onClick)
        {
            MenuFactory.Row(panel, name + "Row", font, RowFontSize, label, ContentWidthPt, y - 10f, out _);
            return MenuFactory.NeutralButton(
                panel, name + "Toggle", font, MenuStrings.Off, ToggleFontSize, new Vector2(0.5f, 1f), new Vector2(96f, 48f), new Vector2(ContentWidthPt * 0.5f - 48f, y), onClick);
        }

        private void Refresh()
        {
            if (_save == null)
            {
                return;
            }

            int music = StepOf(_save.MusicVolume);
            int sfx = StepOf(_save.SfxVolume);
            _music.SetValueWithoutNotify(music);
            _sfx.SetValueWithoutNotify(sfx);
            _musicValue.text = PercentStrings[music];
            _sfxValue.text = PercentStrings[sfx];
            ShowToggle(_haptics, _save.HapticsEnabled);
            ShowToggle(_reduceMotion, _save.ReduceMotion);
            _replayTutorialLabel.text = _save.TutorialReplayRequested ? MenuStrings.TutorialQueued : MenuStrings.ReplayTutorial;
        }

        private void OnReplayTutorialClicked()
        {
            if (_save != null)
            {
                _save.RequestTutorialReplay();
                _replayTutorialLabel.text = MenuStrings.TutorialQueued;
            }
        }

        private void OnMusicChanged(float value)
        {
            int step = StepOf(value / VolumeSteps);
            _musicValue.text = PercentStrings[step];
            if (_save != null)
            {
                _save.MusicVolume = (float)step / VolumeSteps;
            }
        }

        private void OnSfxChanged(float value)
        {
            int step = StepOf(value / VolumeSteps);
            _sfxValue.text = PercentStrings[step];
            if (_save != null)
            {
                _save.SfxVolume = (float)step / VolumeSteps;
            }
        }

        private void OnHapticsClicked()
        {
            if (_save != null)
            {
                _save.HapticsEnabled = !_save.HapticsEnabled;
                ShowToggle(_haptics, _save.HapticsEnabled);
            }
        }

        private void OnReduceMotionClicked()
        {
            if (_save != null)
            {
                _save.ReduceMotion = !_save.ReduceMotion;
                ShowToggle(_reduceMotion, _save.ReduceMotion);
            }
        }
    }
}
