using JungleBooze.Gameplay.CameraRig;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Beat camera modifiers (spec 103 §11, §14 <c>Camera/CameraModifiers</c>).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Camera/CameraModifiers", fileName = "CameraModifiers")]
    public sealed class CameraModifiersAsset : ScriptableObject
    {
        [SerializeField] private CameraModifiers _values = new CameraModifiers();

        public CameraModifiers Values => _values;

        public void SetValues(CameraModifiers values)
        {
            _values = values;
        }
    }
}
