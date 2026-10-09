using JungleBooze.Gameplay.Expedition;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Sailback behaviour and discovery tuning (spec 103 §7.3, §14 <c>Creatures/SailbackConfig</c>).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Creatures/SailbackConfig", fileName = "SailbackConfig")]
    public sealed class SailbackConfigAsset : ScriptableObject
    {
        [SerializeField] private SailbackConfig _values = new SailbackConfig();

        public SailbackConfig Values => _values;

        public void SetValues(SailbackConfig values)
        {
            _values = values;
        }
    }
}
