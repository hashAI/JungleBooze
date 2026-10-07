using System.Collections.Generic;
using JungleBooze.Gameplay.PowerUps;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Power-up tuning (GDD 10, config asset <c>PowerUpTuning</c> of GDD 16). Saved as
    /// <c>Assets/_Game/Config/Resources/PowerUpTuning.asset</c>, where the run bootstrap loads it; without the asset
    /// the same defaults built into <see cref="PowerUpDesignValues"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerUpTuning", menuName = "JungleBooze/Config/Power-up Tuning")]
    public sealed class PowerUpConfigAsset : ScriptableObject
    {
        [SerializeField] private PowerUpDesignValues _values = PowerUpDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public PowerUpDesignValues ToDesignValues()
        {
            return (_values ?? PowerUpDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public PowerUpConfig ToConfig()
        {
            return PowerUpConfig.FromDesignValues(ToDesignValues());
        }

        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(PowerUpDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (PowerUpConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
