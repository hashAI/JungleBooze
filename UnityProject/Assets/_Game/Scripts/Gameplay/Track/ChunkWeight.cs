using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>One (chunk, weight) entry of a difficulty tier's pool (spec 002 sections 3.3 and 7.3).</summary>
    [Serializable]
    public struct ChunkWeight
    {
        public string ChunkId;
        public int Weight;

        public ChunkWeight(string chunkId, int weight)
        {
            ChunkId = chunkId;
            Weight = weight;
        }
    }
}
