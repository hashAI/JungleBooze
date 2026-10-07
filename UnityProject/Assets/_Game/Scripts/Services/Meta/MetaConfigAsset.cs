using System.Collections.Generic;
using UnityEngine;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// Missions, daily reward and shop tuning as a config asset. Saved as
    /// <c>Assets/_Game/Config/Resources/MetaConfig.asset</c>, where the run bootstrap loads it; without the asset the
    /// defaults built into <see cref="MetaConfig"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaConfig", menuName = "JungleBooze/Config/Meta Config")]
    public sealed class MetaConfigAsset : ScriptableObject
    {
        [SerializeField] private MetaConfig _values = MetaConfig.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public MetaConfig ToDesignValues()
        {
            return (_values ?? MetaConfig.CreateDefault()).Clone();
        }

        /// <summary>Validates and returns a copy. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public MetaConfig ToConfig()
        {
            MetaConfig config = ToDesignValues();
            var errors = new List<string>();
            if (!config.Validate(errors))
            {
                throw new System.ArgumentException("Invalid meta config: " + string.Join(" ", errors));
            }

            return config;
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!ToDesignValues().Validate(errors))
            {
                Debug.LogWarning(name + " (MetaConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
