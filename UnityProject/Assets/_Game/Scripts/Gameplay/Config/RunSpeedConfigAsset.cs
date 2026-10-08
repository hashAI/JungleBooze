using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Forward speed tuning (spec 101 §2.2).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/RunSpeedConfig", fileName = "RunSpeedConfig")]
    public sealed class RunSpeedConfigAsset : ScriptableObject
    {
        [SerializeField] private RunSpeedConfig _values = new RunSpeedConfig();

        /// <summary>Authoring values. Copy (Clone) before handing them to a run.</summary>
        public RunSpeedConfig Values => _values;
    }
}
