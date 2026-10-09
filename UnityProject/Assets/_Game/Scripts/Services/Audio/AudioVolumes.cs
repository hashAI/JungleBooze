using System;
using JungleBooze.Core.Settings;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Player volumes per bus (GDD §20: separate music / SFX / ambience), read from the same settings keys as the
    /// settings screen (<c>UiPreferences</c>: music, sfx). Ambience has its own key (default 100%) until the settings
    /// screen shows a slider for it; UI sounds follow the SFX volume.
    /// </summary>
    public sealed class AudioVolumes
    {
        public const string MusicKey = "jb.settings.music";
        public const string SfxKey = "jb.settings.sfx";
        public const string AmbienceKey = "jb.settings.ambience";
        public const float DefaultMusic = 0.8f;
        public const float DefaultSfx = 1f;
        public const float DefaultAmbience = 1f;

        /// <summary>Mixer floor (silence), dB.</summary>
        public const float MinDb = -80f;

        private readonly ISettingsStore _store;

        public AudioVolumes(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Reload();
        }

        public float Music { get; private set; }

        public float Sfx { get; private set; }

        public float Ambience { get; private set; }

        /// <summary>Re-reads the settings (call after the settings screen changed them).</summary>
        public void Reload()
        {
            Music = Clamp01(_store.GetFloat(MusicKey, DefaultMusic));
            Sfx = Clamp01(_store.GetFloat(SfxKey, DefaultSfx));
            Ambience = Clamp01(_store.GetFloat(AmbienceKey, DefaultAmbience));
        }

        public void SetAmbience(float value)
        {
            Ambience = Clamp01(value);
            _store.SetFloat(AmbienceKey, Ambience);
            _store.Save();
        }

        public float Linear(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music:
                    return Music;
                case AudioBus.Ambience:
                    return Ambience;
                default:
                    return Sfx;
            }
        }

        /// <summary>Slider value (0…1) → mixer dB. Squared taper so the slider feels even; 0 is silence.</summary>
        public static float ToDecibels(float linear)
        {
            if (linear <= 0.0001f)
            {
                return MinDb;
            }

            float db = 20f * (float)Math.Log10(linear * linear);
            return db < MinDb ? MinDb : db;
        }

        private static float Clamp01(float v)
        {
            return float.IsNaN(v) ? 1f : v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
