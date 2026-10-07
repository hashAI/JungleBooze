using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Track presentation tuning (spec 002 section 3.6), presentation only. Saved as <c>Assets/_Game/Config/TrackPresentationTuning.asset</c>. Holds designer values
    /// (<see cref="TrackPresentationConfig"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "TrackPresentationTuning", menuName = "JungleBooze/Config/Track Presentation Tuning")]
    public sealed class TrackPresentationConfigAsset : ScriptableObject
    {
        [SerializeField] private TrackPresentationConfig _values = TrackPresentationConfig.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public TrackPresentationConfig ToDesignValues()
        {
            return (_values ?? TrackPresentationConfig.CreateDefault()).Clone();
        }

        /// <summary>The presentation values (a copy). Views treat it as read-only.</summary>
        public TrackPresentationConfig ToConfig()
        {
            return ToDesignValues();
        }

        /// <summary>Range checks without the runner fog rule (the views check that against the runner presentation).</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors, float.NaN);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(TrackPresentationConfig values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (TrackPresentationConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
