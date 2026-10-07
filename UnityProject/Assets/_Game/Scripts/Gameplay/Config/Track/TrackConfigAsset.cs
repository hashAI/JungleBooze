using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Track tuning (spec 002 section 3.1). Saved as <c>Assets/_Game/Config/TrackTuning.asset</c>. Holds designer values
    /// (<see cref="TrackDesignValues"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "TrackTuning", menuName = "JungleBooze/Config/Track Tuning")]
    public sealed class TrackConfigAsset : ScriptableObject
    {
        [SerializeField] private TrackDesignValues _values = TrackDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public TrackDesignValues ToDesignValues()
        {
            return (_values ?? TrackDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public TrackConfig ToConfig()
        {
            return TrackConfig.FromDesignValues(ToDesignValues());
        }

        /// <summary>Range checks of spec 002 section 3.1.</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(TrackDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (TrackConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
