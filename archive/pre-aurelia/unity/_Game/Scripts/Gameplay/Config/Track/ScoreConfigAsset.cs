using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Score tuning (spec 002 section 3.5). Saved as <c>Assets/_Game/Config/ScoreTuning.asset</c>. Holds designer values
    /// (<see cref="ScoreDesignValues"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "ScoreTuning", menuName = "JungleBooze/Config/Score Tuning")]
    public sealed class ScoreConfigAsset : ScriptableObject
    {
        [SerializeField] private ScoreDesignValues _values = ScoreDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public ScoreDesignValues ToDesignValues()
        {
            return (_values ?? ScoreDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public ScoreConfig ToConfig()
        {
            return ScoreConfig.FromDesignValues(ToDesignValues());
        }

        /// <summary>Range checks (spec 002 section 3.5).</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(ScoreDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (ScoreConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
