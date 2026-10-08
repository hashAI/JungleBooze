using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Runner/obstacle hitboxes and forgiveness (spec 101 §2.6, §4.1).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/HitboxConfig", fileName = "HitboxConfig")]
    public sealed class HitboxConfigAsset : ScriptableObject
    {
        [SerializeField] private HitboxConfig _values = new HitboxConfig();

        /// <summary>Authoring values. Copy (Clone) before handing them to a run.</summary>
        public HitboxConfig Values => _values;
    }
}
