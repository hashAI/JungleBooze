using System.Collections.Generic;

namespace JungleBooze.Core.Settings
{
    /// <summary>In-memory settings store (tests, tools).</summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

        public int SaveCount { get; private set; }

        public bool HasKey(string key)
        {
            return _floats.ContainsKey(key) || _ints.ContainsKey(key);
        }

        public float GetFloat(string key, float fallback)
        {
            return _floats.TryGetValue(key, out float value) ? value : fallback;
        }

        public void SetFloat(string key, float value)
        {
            _floats[key] = value;
        }

        public int GetInt(string key, int fallback)
        {
            return _ints.TryGetValue(key, out int value) ? value : fallback;
        }

        public void SetInt(string key, int value)
        {
            _ints[key] = value;
        }

        public void Save()
        {
            SaveCount++;
        }
    }
}
