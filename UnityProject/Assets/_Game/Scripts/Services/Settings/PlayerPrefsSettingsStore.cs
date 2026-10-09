using JungleBooze.Core.Settings;
using UnityEngine;

namespace JungleBooze.Services.Settings
{
    /// <summary>Settings persisted with PlayerPrefs (fine for the feel test; the save system replaces it later).</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public float GetFloat(string key, float fallback)
        {
            return PlayerPrefs.GetFloat(key, fallback);
        }

        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }

        public int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(key, fallback);
        }

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
