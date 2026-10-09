namespace JungleBooze.Core.Settings
{
    /// <summary>Key/value storage for player settings (PlayerPrefs on device, memory in tests).</summary>
    public interface ISettingsStore
    {
        bool HasKey(string key);

        float GetFloat(string key, float fallback);

        void SetFloat(string key, float value);

        int GetInt(string key, int fallback);

        void SetInt(string key, int value);

        void Save();
    }
}
