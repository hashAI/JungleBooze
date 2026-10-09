using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>The Expedition 1 script (spec 103 §10.1).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/World/ExpeditionScript", fileName = "ExpeditionScript")]
    public sealed class ExpeditionScriptAsset : ScriptableObject
    {
        [SerializeField] private ExpeditionScript _values = new ExpeditionScript();

        /// <summary>Authoring values (copied by the run).</summary>
        public ExpeditionScript Values => _values;

        /// <summary>Editor authoring: replaces the values.</summary>
        public void SetValues(ExpeditionScript values)
        {
            _values = values;
        }
    }
}
