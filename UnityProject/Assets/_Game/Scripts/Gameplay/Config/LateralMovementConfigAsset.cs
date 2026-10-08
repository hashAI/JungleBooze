using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Steering, soft edges, dodge and fork nudge (spec 101 §2.3, spec 102 §3.3).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/LateralMovementConfig", fileName = "LateralMovementConfig")]
    public sealed class LateralMovementConfigAsset : ScriptableObject
    {
        [SerializeField] private LateralMovementConfig _values = new LateralMovementConfig();

        /// <summary>Authoring values. Copy (Clone) before handing them to a run.</summary>
        public LateralMovementConfig Values => _values;
    }
}
