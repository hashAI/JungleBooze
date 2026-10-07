using System.Collections.Generic;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Economy tuning (config asset <c>EconomyConfig</c> of GDD 16); for now the Continue rules of GDD 14.4. Saved
    /// as <c>Assets/_Game/Config/Resources/EconomyConfig.asset</c>, where the run bootstrap loads it; without the
    /// asset the same defaults built into <see cref="EconomyDesignValues"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "JungleBooze/Config/Economy Config")]
    public sealed class EconomyConfigAsset : ScriptableObject
    {
        [SerializeField] private EconomyDesignValues _values = EconomyDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public EconomyDesignValues ToDesignValues()
        {
            return (_values ?? EconomyDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public ContinueRules ToContinueRules()
        {
            return ContinueRules.FromDesignValues(ToDesignValues());
        }

        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(EconomyDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (EconomyConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
