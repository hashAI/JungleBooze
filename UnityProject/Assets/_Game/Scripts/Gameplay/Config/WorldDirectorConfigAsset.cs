using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>World Director tuning (spec 102 §5–8).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/World/WorldDirectorConfig", fileName = "WorldDirectorConfig")]
    public sealed class WorldDirectorConfigAsset : ScriptableObject
    {
        [SerializeField] private WorldDirectorConfig _values = new WorldDirectorConfig();

        /// <summary>Authoring values (copied by the run).</summary>
        public WorldDirectorConfig Values => _values;

        /// <summary>Editor authoring: replaces the values.</summary>
        public void SetValues(WorldDirectorConfig values)
        {
            _values = values;
        }
    }
}
