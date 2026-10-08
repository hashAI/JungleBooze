using System.Collections.Generic;
using JungleBooze.Gameplay.Hazards;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Signature hazard tuning (GDD 8.3, config asset <c>HazardTuning</c> of GDD 16): lane-strike warning, active
    /// time and rhythm. Saved as <c>Assets/_Game/Config/Resources/HazardTuning.asset</c>; without the asset the
    /// defaults built into <see cref="HazardDesignValues"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "HazardTuning", menuName = "JungleBooze/Config/Hazard Tuning")]
    public sealed class HazardConfigAsset : ScriptableObject
    {
        [SerializeField] private HazardDesignValues _values = HazardDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public HazardDesignValues ToDesignValues()
        {
            return (_values ?? HazardDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public HazardConfig ToConfig()
        {
            return HazardConfig.FromDesignValues(ToDesignValues());
        }

        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(HazardDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (HazardConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
