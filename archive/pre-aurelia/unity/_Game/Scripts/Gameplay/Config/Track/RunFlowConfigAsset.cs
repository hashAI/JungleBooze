using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Run lifecycle tuning (spec 002 section 3.7). Saved as <c>Assets/_Game/Config/RunFlowTuning.asset</c>. Holds designer values
    /// (<see cref="RunFlowDesignValues"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "RunFlowTuning", menuName = "JungleBooze/Config/Run Flow Tuning")]
    public sealed class RunFlowConfigAsset : ScriptableObject
    {
        [SerializeField] private RunFlowDesignValues _values = RunFlowDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public RunFlowDesignValues ToDesignValues()
        {
            return (_values ?? RunFlowDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public RunFlowConfig ToConfig()
        {
            return RunFlowConfig.FromDesignValues(ToDesignValues());
        }

        /// <summary>Range checks (spec 002 section 3.7).</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(RunFlowDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (RunFlowConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
