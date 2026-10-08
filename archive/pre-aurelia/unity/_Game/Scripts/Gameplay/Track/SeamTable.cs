using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Precomputed seam compatibility (spec 002 section 8.5): <c>IsCompatible(prev, prevMirror, next, nextMirror)</c>
    /// for every ordered pair of library chunks and both orientations. Built by the chunk validator (stage B3) and
    /// stored in the library asset together with <see cref="SourceHash"/>, the <see cref="ChunkLibrary.DataHash"/>
    /// it was computed from. A new table allows every pair. Lookups do not allocate.
    /// </summary>
    public sealed class SeamTable
    {
        private readonly bool[] _compatible;

        public SeamTable(int chunkCount, ulong sourceHash)
        {
            if (chunkCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkCount), "Must be positive.");
            }

            ChunkCount = chunkCount;
            SourceHash = sourceHash;
            _compatible = new bool[chunkCount * 2 * chunkCount * 2];
            for (int i = 0; i < _compatible.Length; i++)
            {
                _compatible[i] = true;
            }
        }

        public int ChunkCount { get; }

        /// <summary>Hash of the chunk data the table was computed from (stale table check, AC-201).</summary>
        public ulong SourceHash { get; }

        /// <summary>Number of stored entries (chunkCount² × 4).</summary>
        public int EntryCount => _compatible.Length;

        public bool IsCompatible(int prev, bool prevMirrored, int next, bool nextMirrored)
        {
            return _compatible[Index(prev, prevMirrored, next, nextMirrored)];
        }

        public void SetCompatible(int prev, bool prevMirrored, int next, bool nextMirrored, bool compatible)
        {
            _compatible[Index(prev, prevMirrored, next, nextMirrored)] = compatible;
        }

        /// <summary>Raw entry by flat index (serialization).</summary>
        public bool GetEntry(int index)
        {
            return _compatible[index];
        }

        /// <summary>Raw entry by flat index (serialization).</summary>
        public void SetEntry(int index, bool compatible)
        {
            _compatible[index] = compatible;
        }

        /// <summary>Flat index: ((prev × 2 + prevMirror) × n + next) × 2 + nextMirror.</summary>
        public int Index(int prev, bool prevMirrored, int next, bool nextMirrored)
        {
            if ((uint)prev >= (uint)ChunkCount || (uint)next >= (uint)ChunkCount)
            {
                throw new ArgumentOutOfRangeException(nameof(prev), "Chunk index outside the seam table.");
            }

            return (((prev * 2) + (prevMirrored ? 1 : 0)) * ChunkCount + next) * 2 + (nextMirrored ? 1 : 0);
        }
    }
}
