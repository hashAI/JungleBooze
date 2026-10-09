using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Canopy beams (spec 103 §6, §14 <c>Movement/CanopyConfig</c>).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/CanopyConfig", fileName = "CanopyConfig")]
    public sealed class CanopyConfigAsset : ScriptableObject
    {
        [SerializeField] private CanopyConfig _values = new CanopyConfig();

        public CanopyConfig Values => _values;

        public void SetValues(CanopyConfig values)
        {
            _values = values;
        }
    }
}
