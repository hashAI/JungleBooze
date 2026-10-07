using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// World order and lengths (GDD 9, config asset <c>WorldSchedule</c>). Saved as
    /// <c>Assets/_Game/Config/Resources/WorldSchedule.asset</c>; without the asset the defaults of
    /// <see cref="WorldScheduleConfig.CreateDefault"/> are used.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldSchedule", menuName = "JungleBooze/Config/World Schedule")]
    public sealed class WorldScheduleConfigAsset : ScriptableObject
    {
        [SerializeField] private List<WorldDesign> _worlds = new List<WorldDesign>
        {
            new WorldDesign(WorldKind.Jungle, 1100f),
            new WorldDesign(WorldKind.River, 1200f),
            new WorldDesign(WorldKind.Mountains, 1300f),
            new WorldDesign(WorldKind.Ruins, 1400f),
        };

        [SerializeField] private float _signatureQuietM = 150f;
        [SerializeField] private int _duskFromLap = 1;

        /// <summary>Validates and converts. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public WorldScheduleConfig ToConfig()
        {
            return new WorldScheduleConfig(_worlds, _signatureQuietM, _duskFromLap);
        }

        public bool Validate(List<string> errors)
        {
            return WorldScheduleConfig.Validate(_worlds, _signatureQuietM, _duskFromLap, errors);
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (WorldScheduleConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
