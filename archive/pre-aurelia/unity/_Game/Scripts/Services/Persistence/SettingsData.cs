using System;

namespace JungleBooze.Services.Persistence
{
    /// <summary>
    /// Player settings as stored in the save file (GDD sections 19 and 20). The public lower-case field names are
    /// the JSON format read by <c>JsonUtility</c>: never rename them; add new fields instead.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        /// <summary>[ASSUMED] Music starts a little below full so sound effects stay on top.</summary>
        public const float DefaultMusicVolume = 0.8f;

        public const float DefaultSfxVolume = 1f;

        /// <summary>Music volume 0..1 (audio arrives later; stored now).</summary>
        public float musicVolume = DefaultMusicVolume;

        /// <summary>Sound effect volume 0..1 (audio arrives later; stored now).</summary>
        public float sfxVolume = DefaultSfxVolume;

        /// <summary>Haptics on/off (GDD 5.4: every haptic can be switched off).</summary>
        public bool hapticsEnabled = true;

        /// <summary>Reduce Motion (GDD 20): no slow-motion, no camera tilt, smaller FOV change.</summary>
        public bool reduceMotion;

        public static SettingsData CreateDefault()
        {
            return new SettingsData();
        }
    }
}
