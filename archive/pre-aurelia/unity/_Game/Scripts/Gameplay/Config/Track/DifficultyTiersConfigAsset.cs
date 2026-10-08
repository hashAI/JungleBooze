using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Difficulty tiers and pool weights (spec 002 sections 3.3 and 7.3). Saved as <c>Assets/_Game/Config/DifficultyTiers.asset</c>. Holds designer values
    /// (<see cref="DifficultyTiersDesignValues"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyTiers", menuName = "JungleBooze/Config/Difficulty Tiers")]
    public sealed class DifficultyTiersConfigAsset : ScriptableObject
    {
        [SerializeField] private DifficultyTiersDesignValues _values = DifficultyTiersDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public DifficultyTiersDesignValues ToDesignValues()
        {
            return (_values ?? DifficultyTiersDesignValues.CreateDefault()).Clone();
        }

        /// <summary>
        /// Validates against the library and derives vMaxMps / minRowSpacingM from <paramref name="curve"/>.
        /// Throws <see cref="System.ArgumentException"/> when invalid.
        /// </summary>
        public DifficultyTiersConfig ToConfig(JungleBooze.Gameplay.Runner.SpeedCurve curve, ChunkLibrary library)
        {
            return DifficultyTiersConfig.FromDesignValues(ToDesignValues(), curve, library);
        }

        /// <summary>Checks that do not need the library (spec 002 section 3.3).</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(DifficultyTiersDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (DifficultyTiersConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
