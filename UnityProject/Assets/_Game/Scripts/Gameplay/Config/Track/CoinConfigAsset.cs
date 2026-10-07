using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Coin tuning (spec 002 section 3.4). Saved as <c>Assets/_Game/Config/CoinTuning.asset</c>. Holds designer values
    /// (<see cref="CoinDesignValues"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "CoinTuning", menuName = "JungleBooze/Config/Coin Tuning")]
    public sealed class CoinConfigAsset : ScriptableObject
    {
        [SerializeField] private CoinDesignValues _values = CoinDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public CoinDesignValues ToDesignValues()
        {
            return (_values ?? CoinDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public CoinConfig ToConfig()
        {
            return CoinConfig.FromDesignValues(ToDesignValues());
        }

        /// <summary>Range checks of spec 002 section 3.4.</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(CoinDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (CoinConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
