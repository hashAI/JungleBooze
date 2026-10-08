using JungleBooze.Gameplay.CameraRig;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>One camera profile (landscape or portrait), spec 101 §5.</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/CameraProfile", fileName = "CameraProfile")]
    public sealed class CameraProfileAsset : ScriptableObject
    {
        [SerializeField] private CameraProfile _values = new CameraProfile();

        public CameraProfile Values => _values;

        /// <summary>Editor setup: replaces the values (used to create the portrait profile).</summary>
        public void SetValues(CameraProfile values)
        {
            _values = values;
        }
    }
}
