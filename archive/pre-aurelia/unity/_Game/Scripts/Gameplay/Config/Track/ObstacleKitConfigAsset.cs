using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Shared obstacle kit (spec 002 sections 3.2 and 5). Saved as <c>Assets/_Game/Config/ObstacleKit.asset</c>. Holds designer values
    /// (<see cref="ObstacleKitDesignValues"/>); <see cref="ToDesignValues"/> returns a copy for the plain C# config.
    /// </summary>
    [CreateAssetMenu(fileName = "ObstacleKit", menuName = "JungleBooze/Config/Obstacle Kit")]
    public sealed class ObstacleKitConfigAsset : ScriptableObject
    {
        [SerializeField] private ObstacleKitDesignValues _values = ObstacleKitDesignValues.CreateDefault();

        /// <summary>A copy of the serialized values.</summary>
        public ObstacleKitDesignValues ToDesignValues()
        {
            return (_values ?? ObstacleKitDesignValues.CreateDefault()).Clone();
        }

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public ObstacleKitConfig ToConfig()
        {
            return ObstacleKitConfig.FromDesignValues(ToDesignValues());
        }

        /// <summary>Range checks of spec 002 section 3.2.</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites the serialized values. For tests and editor tooling.</summary>
        internal void SetDesignValues(ObstacleKitDesignValues values)
        {
            _values = values.Clone();
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (ObstacleKitConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
