using JungleBooze.Gameplay.Expedition;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Pickups, Clean Line, Shield (spec 103 §9).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Expedition/PickupConfig", fileName = "PickupConfig")]
    public sealed class PickupConfigAsset : ScriptableObject
    {
        [SerializeField] private PickupConfig _values = new PickupConfig();

        /// <summary>Authoring values (copied by the run).</summary>
        public PickupConfig Values => _values;

        /// <summary>Editor authoring: replaces the values.</summary>
        public void SetValues(PickupConfig values)
        {
            _values = values;
        }
    }
}
