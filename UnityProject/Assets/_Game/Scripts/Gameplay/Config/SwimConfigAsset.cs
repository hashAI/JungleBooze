using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Swimming, dive and leap (spec 103 §4, §14 <c>Movement/SwimConfig</c>).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/SwimConfig", fileName = "SwimConfig")]
    public sealed class SwimConfigAsset : ScriptableObject
    {
        [SerializeField] private SwimConfig _values = new SwimConfig();

        public SwimConfig Values => _values;

        public void SetValues(SwimConfig values)
        {
            _values = values;
        }
    }
}
