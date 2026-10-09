using JungleBooze.Gameplay.Expedition;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>One journal entry (spec 103 §7.3).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Expedition/DiscoveryEntry", fileName = "DiscoveryEntry")]
    public sealed class DiscoveryEntryAsset : ScriptableObject
    {
        [SerializeField] private DiscoveryEntry _values = new DiscoveryEntry();

        /// <summary>Authoring values (copied by the run).</summary>
        public DiscoveryEntry Values => _values;

        /// <summary>Editor authoring: replaces the values.</summary>
        public void SetValues(DiscoveryEntry values)
        {
            _values = values;
        }
    }
}
