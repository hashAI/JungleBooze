using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Ordered, immutable chunk list (spec 002 section 4.3). <b>List order is part of determinism</b>: weighted
    /// selection walks it in order, so appending is safe and reordering changes every seed. Holds the seam table
    /// (null = every pair compatible). Index lookups do not allocate; <see cref="IndexOf"/> is for setup.
    /// </summary>
    public sealed class ChunkLibrary
    {
        private readonly ChunkData[] _chunks;
        private readonly int[] _breathers;

        public ChunkLibrary(IList<ChunkData> chunks, SeamTable seams = null)
        {
            if (chunks == null || chunks.Count == 0)
            {
                throw new ArgumentException("The chunk library needs at least one chunk.", nameof(chunks));
            }

            _chunks = new ChunkData[chunks.Count];
            var breathers = new List<int>();
            for (int i = 0; i < chunks.Count; i++)
            {
                ChunkData chunk = chunks[i] ?? throw new ArgumentException("Chunk " + i + " is null.", nameof(chunks));
                for (int j = 0; j < i; j++)
                {
                    if (_chunks[j].Id == chunk.Id)
                    {
                        throw new ArgumentException("Duplicate chunk id " + chunk.Id + ".", nameof(chunks));
                    }
                }

                _chunks[i] = chunk;
                if (chunk.Kind == ChunkKind.Breather)
                {
                    breathers.Add(i);
                }
            }

            _breathers = breathers.ToArray();
            DataHash = ComputeDataHash(_chunks);

            if (seams != null && seams.ChunkCount != _chunks.Length)
            {
                throw new ArgumentException("The seam table covers " + seams.ChunkCount + " chunks, the library has " + _chunks.Length + ".", nameof(seams));
            }

            Seams = seams;
        }

        public int Count => _chunks.Length;

        public ChunkData this[int index] => _chunks[index];

        /// <summary>Null when no table was built: every pair is compatible.</summary>
        public SeamTable Seams { get; }

        /// <summary>Hash of every chunk in order (config hash, stale seam table check).</summary>
        public ulong DataHash { get; }

        /// <summary>True when there is no table or its source hash matches the chunk data (AC-201).</summary>
        public bool SeamTableIsCurrent => Seams == null || Seams.SourceHash == DataHash;

        public int BreatherCount => _breathers.Length;

        /// <summary>Library index of the k-th breather in library order.</summary>
        public int GetBreatherIndex(int k)
        {
            return _breathers[k];
        }

        /// <summary>Library index of the chunk with <paramref name="id"/>, or -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < _chunks.Length; i++)
            {
                if (_chunks[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>A copy of this library with another seam table (validator output).</summary>
        public ChunkLibrary WithSeams(SeamTable seams)
        {
            return new ChunkLibrary(_chunks, seams);
        }

        /// <summary>
        /// Seam rule of spec 002 section 8.3/8.5: breathers and the start chunk are compatible with everything;
        /// otherwise the table decides (no table = compatible). <paramref name="prev"/> = -1 means "no previous chunk".
        /// </summary>
        public bool IsSeamCompatible(int prev, bool prevMirrored, int next, bool nextMirrored)
        {
            if (prev < 0 || Seams == null)
            {
                return true;
            }

            if (_chunks[prev].Kind != ChunkKind.Normal || _chunks[next].Kind != ChunkKind.Normal)
            {
                return true;
            }

            return Seams.IsCompatible(prev, prevMirrored, next, nextMirrored);
        }

        private static ulong ComputeDataHash(ChunkData[] chunks)
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, chunks.Length);
            for (int i = 0; i < chunks.Length; i++)
            {
                h = StableHash.Mix(h, chunks[i].ComputeHash());
            }

            return h;
        }
    }
}
