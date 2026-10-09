using JungleBooze.Gameplay.Expedition;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>One ability (GDD §13).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Expedition/AbilityDefinition", fileName = "AbilityDefinition")]
    public sealed class AbilityDefinitionAsset : ScriptableObject
    {
        [SerializeField] private AbilityDefinition _values = new AbilityDefinition();

        /// <summary>Authoring values (copied by the run).</summary>
        public AbilityDefinition Values => _values;

        /// <summary>Editor authoring: replaces the values.</summary>
        public void SetValues(AbilityDefinition values)
        {
            _values = values;
        }
    }
}
