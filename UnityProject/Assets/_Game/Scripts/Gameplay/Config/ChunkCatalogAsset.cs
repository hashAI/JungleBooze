using System.Collections.Generic;
using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>The chunk set the director may use (spec 102 §9; 15 chunks for the slice).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/World/ChunkCatalog", fileName = "ChunkCatalog")]
    public sealed class ChunkCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<ChunkDefinitionAsset> _chunks = new List<ChunkDefinitionAsset>();

        public IReadOnlyList<ChunkDefinitionAsset> Chunks => _chunks;

        public void SetChunks(List<ChunkDefinitionAsset> chunks)
        {
            _chunks = chunks;
        }

        /// <summary>The definitions (setup time; allocates).</summary>
        public List<ChunkDefinition> Definitions()
        {
            var list = new List<ChunkDefinition>(_chunks.Count);
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i] != null)
                {
                    list.Add(_chunks[i].Definition);
                }
            }

            return list;
        }
    }
}
