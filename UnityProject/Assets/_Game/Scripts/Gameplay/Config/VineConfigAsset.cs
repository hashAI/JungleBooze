using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Vine swing (spec 103 §5, §14 <c>Movement/VineConfig</c>).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/VineConfig", fileName = "VineConfig")]
    public sealed class VineConfigAsset : ScriptableObject
    {
        [SerializeField] private VineConfig _values = new VineConfig();

        public VineConfig Values => _values;

        public void SetValues(VineConfig values)
        {
            _values = values;
        }
    }
}
