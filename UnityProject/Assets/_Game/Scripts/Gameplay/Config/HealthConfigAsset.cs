using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Health, i-frames, regen, shield, revive (spec 101 §4.2).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/HealthConfig", fileName = "HealthConfig")]
    public sealed class HealthConfigAsset : ScriptableObject
    {
        [SerializeField] private HealthConfig _values = new HealthConfig();

        /// <summary>Authoring values. Copy (Clone) before handing them to a run.</summary>
        public HealthConfig Values => _values;
    }
}
