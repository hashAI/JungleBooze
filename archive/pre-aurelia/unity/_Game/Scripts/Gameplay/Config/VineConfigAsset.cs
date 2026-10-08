using System.Collections.Generic;
using JungleBooze.Gameplay.Vine;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Vine tuning (GDD 7.5, config asset <c>VineTuning</c> of GDD 16). Saved as
    /// <c>Assets/_Game/Config/Resources/VineTuning.asset</c>, where the run bootstrap loads it; without the asset the
    /// same defaults built into <see cref="VineDesignValues"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "VineTuning", menuName = "JungleBooze/Config/Vine Tuning")]
    public sealed class VineConfigAsset : ScriptableObject
    {
        [SerializeField] private VineDesignValues _values = VineDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public VineDesignValues ToDesignValues()
        {
            return (_values ?? VineDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public VineConfig ToConfig()
        {
            return VineConfig.FromDesignValues(ToDesignValues());
        }

        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(VineDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (VineConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
