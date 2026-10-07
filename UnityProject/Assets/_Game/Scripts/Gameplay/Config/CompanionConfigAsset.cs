using System.Collections.Generic;
using JungleBooze.Gameplay.Companion;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Companion tuning (GDD 15.2, config asset <c>CompanionTuning</c> of GDD 16). Saved as
    /// <c>Assets/_Game/Config/Resources/CompanionTuning.asset</c>, where the run bootstrap loads it; without the asset
    /// the same defaults built into <see cref="CompanionDesignValues"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "CompanionTuning", menuName = "JungleBooze/Config/Companion Tuning")]
    public sealed class CompanionConfigAsset : ScriptableObject
    {
        [SerializeField] private CompanionDesignValues _values = CompanionDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public CompanionDesignValues ToDesignValues()
        {
            return (_values ?? CompanionDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public CompanionConfig ToConfig()
        {
            return CompanionConfig.FromDesignValues(ToDesignValues());
        }

        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(CompanionDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (CompanionConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
