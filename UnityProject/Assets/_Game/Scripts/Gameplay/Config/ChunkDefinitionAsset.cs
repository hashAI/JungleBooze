using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>One chunk (spec 102 §2) under <c>Assets/_Game/Config/Chunks</c>. The simulation reads only the definition.</summary>
    [CreateAssetMenu(menuName = "JungleBooze/World/ChunkDefinition", fileName = "Chunk")]
    public sealed class ChunkDefinitionAsset : ScriptableObject
    {
        [SerializeField] private ChunkDefinition _definition = new ChunkDefinition();

        public ChunkDefinition Definition => _definition;

        /// <summary>Editor authoring: replaces the layout.</summary>
        public void SetDefinition(ChunkDefinition definition)
        {
            _definition = definition;
        }
    }
}
