using JungleBooze.Gameplay.Expedition;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Results timing and next-objective thresholds (spec 103 §9.2, GDD §17).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Expedition/ResultsConfig", fileName = "ResultsConfig")]
    public sealed class ResultsConfigAsset : ScriptableObject
    {
        [SerializeField] private ResultsConfig _values = new ResultsConfig();

        /// <summary>Authoring values (copied by the run).</summary>
        public ResultsConfig Values => _values;

        /// <summary>Editor authoring: replaces the values.</summary>
        public void SetValues(ResultsConfig values)
        {
            _values = values;
        }
    }
}
