using System;
using JungleBooze.Core.Settings;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Player UI and audio preferences (GDD §20): music and SFX volume (hooks for the audio system), left/right
    /// handed HUD, and text size. Steering, haptics and reduced motion stay in FeelSettings (gameplay feel).
    /// Plain C#, persisted through <see cref="ISettingsStore"/>.
    /// </summary>
    public sealed class UiPreferences
    {
        public const string MusicKey = "jb.settings.music";
        public const string SfxKey = "jb.settings.sfx";
        public const string LeftHandedKey = "jb.settings.leftHanded";
        public const string TextSizeKey = "jb.settings.textSize";

        /// <summary>Text size steps (×). 200% is the GDD §20 maximum.</summary>
        public static readonly float[] TextScales = { 1f, 1.15f, 1.3f, 1.5f, 1.75f, 2f };

        /// <summary>The HUD (in-run) scales at most this much: it must never cover the play view.</summary>
        public const float HudTextScaleMax = 1.3f;

        private readonly ISettingsStore _store;

        public UiPreferences(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            MusicVolume = Clamp01(_store.GetFloat(MusicKey, 0.8f));
            SfxVolume = Clamp01(_store.GetFloat(SfxKey, 1f));
            LeftHanded = _store.GetInt(LeftHandedKey, 0) != 0;
            TextSizeIndex = ClampIndex(_store.GetInt(TextSizeKey, 0));
        }

        /// <summary>Raised after any change (audio system, HUD relayout).</summary>
        public event Action Changed;

        public float MusicVolume { get; private set; }

        public float SfxVolume { get; private set; }

        public bool LeftHanded { get; private set; }

        public int TextSizeIndex { get; private set; }

        public float TextScale => TextScales[TextSizeIndex];

        public float HudTextScale => TextScale < HudTextScaleMax ? TextScale : HudTextScaleMax;

        public void SetMusicVolume(float value)
        {
            MusicVolume = Clamp01(value);
            _store.SetFloat(MusicKey, MusicVolume);
            Commit();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Clamp01(value);
            _store.SetFloat(SfxKey, SfxVolume);
            Commit();
        }

        public void SetLeftHanded(bool left)
        {
            LeftHanded = left;
            _store.SetInt(LeftHandedKey, left ? 1 : 0);
            Commit();
        }

        public void SetTextSizeIndex(int index)
        {
            TextSizeIndex = ClampIndex(index);
            _store.SetInt(TextSizeKey, TextSizeIndex);
            Commit();
        }

        public static int Percent(int index)
        {
            return (int)Math.Round(TextScales[ClampIndex(index)] * 100f);
        }

        private void Commit()
        {
            _store.Save();
            Changed?.Invoke();
        }

        private static float Clamp01(float v)
        {
            return float.IsNaN(v) ? 1f : v < 0f ? 0f : v > 1f ? 1f : v;
        }

        private static int ClampIndex(int i)
        {
            return i < 0 ? 0 : i >= TextScales.Length ? TextScales.Length - 1 : i;
        }
    }
}
