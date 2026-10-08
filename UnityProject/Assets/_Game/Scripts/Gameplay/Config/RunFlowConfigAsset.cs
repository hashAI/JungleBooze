using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Run lifecycle timing (ready, dying, finish).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/RunFlowConfig", fileName = "RunFlowConfig")]
    public sealed class RunFlowConfigAsset : ScriptableObject
    {
        [SerializeField] private RunFlowConfig _values = new RunFlowConfig();

        /// <summary>Authoring values. Copy (Clone) before handing them to a run.</summary>
        public RunFlowConfig Values => _values;
    }
}
