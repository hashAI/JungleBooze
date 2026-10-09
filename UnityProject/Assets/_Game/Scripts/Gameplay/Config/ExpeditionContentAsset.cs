using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// The content bundle of the Expedition scene (spec 103 §14): chunk catalog, script, director, pickups, results,
    /// journal entries and abilities. <see cref="Build"/> copies it into the plain <see cref="ExpeditionContent"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/World/ExpeditionContent", fileName = "ExpeditionContent")]
    public sealed class ExpeditionContentAsset : ScriptableObject
    {
        public ChunkCatalogAsset Catalog;
        public ExpeditionScriptAsset Script;
        public WorldDirectorConfigAsset Director;
        public PickupConfigAsset Pickups;
        public ResultsConfigAsset Results;
        public List<DiscoveryEntryAsset> Discoveries = new List<DiscoveryEntryAsset>();
        public List<AbilityDefinitionAsset> Abilities = new List<AbilityDefinitionAsset>();
        public SailbackConfigAsset Sailback;

        public bool IsComplete => Catalog != null && Script != null && Director != null && Pickups != null && Results != null;

        public ExpeditionContent Build()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Expedition content assets are missing (run JungleBooze > Expedition > Build Scene).");
            }

            var discoveries = new List<DiscoveryEntry>();
            for (int i = 0; i < Discoveries.Count; i++)
            {
                if (Discoveries[i] != null)
                {
                    discoveries.Add(Discoveries[i].Values);
                }
            }

            var abilities = new List<AbilityDefinition>();
            for (int i = 0; i < Abilities.Count; i++)
            {
                if (Abilities[i] != null)
                {
                    abilities.Add(Abilities[i].Values);
                }
            }

            return new ExpeditionContent(Catalog.Definitions(), Script.Values, Director.Values, Pickups.Values, Results.Values, discoveries, abilities, Sailback != null ? Sailback.Values : null);
        }

        /// <summary>Stable hash of the content (replay header), FNV-1a over the JSON.</summary>
        public ulong ComputeHash()
        {
            ulong hash = 14695981039346656037UL;
            hash = MovementConfigAssets.Mix(hash, JsonUtility.ToJson(Script.Values));
            hash = MovementConfigAssets.Mix(hash, JsonUtility.ToJson(Director.Values));
            IReadOnlyList<ChunkDefinitionAsset> chunks = Catalog.Chunks;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] != null)
                {
                    hash = MovementConfigAssets.Mix(hash, JsonUtility.ToJson(chunks[i].Definition));
                }
            }

            return hash;
        }

        /// <summary>The chunk definitions (for tools).</summary>
        public List<ChunkDefinition> Chunks()
        {
            return Catalog.Definitions();
        }
    }
}
