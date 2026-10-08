using JungleBooze.Gameplay.Controls;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Touch gestures, keyboard and mouse (spec 101 §3.3–3.4).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/GestureConfig", fileName = "GestureConfig")]
    public sealed class GestureConfigAsset : ScriptableObject
    {
        [SerializeField] private GestureConfig _values = new GestureConfig();

        /// <summary>Authoring values. Copy (Clone) before handing them to a run.</summary>
        public GestureConfig Values => _values;
    }
}
