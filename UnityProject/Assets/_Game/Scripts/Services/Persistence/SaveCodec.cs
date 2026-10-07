using System;
using UnityEngine;

namespace JungleBooze.Services.Persistence
{
    /// <summary>
    /// Turns <see cref="SaveData"/> into versioned JSON and back. Decoding never throws: anything that is not a
    /// valid save (empty, cut off, not JSON, no version) is reported as a failure so the caller can fall back to
    /// the backup or to defaults. Values that are out of range are repaired (negative counts become 0, volumes
    /// are clamped to 0..1). Allocates; call only on load and save.
    /// </summary>
    public static class SaveCodec
    {
        /// <summary>Version written by this build. Version 1 is the first format.</summary>
        public const int CurrentVersion = 1;

        public static string Encode(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.version = CurrentVersion;
            return JsonUtility.ToJson(data, false);
        }

        /// <summary>Parses, upgrades and repairs a save. Returns false (with a reason) when the text is not a save.</summary>
        public static bool TryDecode(string json, out SaveData data, out string error)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "the file is empty";
                return false;
            }

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                error = "not valid JSON (" + e.Message + ")";
                return false;
            }

            if (parsed == null)
            {
                error = "no data";
                return false;
            }

            if (parsed.version < 1)
            {
                error = "missing or invalid version " + parsed.version;
                return false;
            }

            Upgrade(parsed);
            Repair(parsed);
            data = parsed;
            error = null;
            return true;
        }

        /// <summary>
        /// Brings an older format up to <see cref="CurrentVersion"/>, one version step at a time. Version 1 is the
        /// first format, so there is nothing to upgrade yet. A file from a newer build is read as far as this
        /// build understands it (unknown fields are ignored by JsonUtility). [ASSUMED]
        /// </summary>
        private static void Upgrade(SaveData data)
        {
            // Add one step per new version here, for example:
            // if (data.version == 1) { ...convert...; data.version = 2; }
            if (data.version < CurrentVersion)
            {
                data.version = CurrentVersion;
            }
        }

        private static void Repair(SaveData data)
        {
            if (data.bestScore < 0L)
            {
                data.bestScore = 0L;
            }

            if (data.bestDistanceM < 0L)
            {
                data.bestDistanceM = 0L;
            }

            if (data.totalCoins < 0L)
            {
                data.totalCoins = 0L;
            }

            if (data.runsPlayed < 0)
            {
                data.runsPlayed = 0;
            }

            if (data.settings == null)
            {
                data.settings = SettingsData.CreateDefault();
            }

            data.settings.musicVolume = RepairVolume(data.settings.musicVolume, SettingsData.DefaultMusicVolume);
            data.settings.sfxVolume = RepairVolume(data.settings.sfxVolume, SettingsData.DefaultSfxVolume);
        }

        private static float RepairVolume(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Clamp01(value);
        }
    }
}
