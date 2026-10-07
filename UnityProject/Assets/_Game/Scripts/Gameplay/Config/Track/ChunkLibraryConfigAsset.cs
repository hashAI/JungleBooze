using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// <c>ChunkLibrary_Main.asset</c> (spec 002 section 4.3): the ordered chunk list and the stored seam table.
    /// <b>List order is part of determinism</b>: append only. While the list is empty (no chunk assets authored
    /// yet), <see cref="ToLibrary"/> returns the code defaults of <see cref="JungleChunkLibraryDefaults"/>
    /// [ASSUMED]. The seam table entries and their source hash are written by the chunk validator (stage B3); an
    /// empty entry list means "every pair compatible".
    /// </summary>
    [CreateAssetMenu(fileName = "ChunkLibrary_Main", menuName = "JungleBooze/Config/Chunk Library")]
    public sealed class ChunkLibraryConfigAsset : ScriptableObject
    {
        [SerializeField] private ChunkAsset[] _chunks = new ChunkAsset[0];
        [SerializeField] private bool[] _seamEntries = new bool[0];
        [SerializeField] private ulong _seamSourceHash;

        public int ChunkAssetCount => _chunks == null ? 0 : _chunks.Length;

        /// <summary>Builds the runtime library (allocates; setup time only).</summary>
        public ChunkLibrary ToLibrary()
        {
            ChunkData[] data;
            if (_chunks == null || _chunks.Length == 0)
            {
                data = JungleChunkLibraryDefaults.CreateChunks();
            }
            else
            {
                data = new ChunkData[_chunks.Length];
                for (int i = 0; i < _chunks.Length; i++)
                {
                    data[i] = _chunks[i] == null ? null : _chunks[i].ToChunkData();
                }
            }

            SeamTable seams = null;
            if (_seamEntries != null && _seamEntries.Length > 0)
            {
                seams = new SeamTable(data.Length, _seamSourceHash);
                if (_seamEntries.Length == seams.EntryCount)
                {
                    for (int i = 0; i < _seamEntries.Length; i++)
                    {
                        seams.SetEntry(i, _seamEntries[i]);
                    }
                }
                else
                {
                    // Wrong size: a stale table. Keep it all-compatible but with a hash that can never match, so
                    // ChunkLibrary.SeamTableIsCurrent reports it (AC-201).
                    seams = new SeamTable(data.Length, ~_seamSourceHash);
                }
            }

            return new ChunkLibrary(data, seams);
        }

        /// <summary>Stores a seam table (chunk validator output). For editor tooling.</summary>
        internal void SetSeamTable(SeamTable table)
        {
            _seamSourceHash = table.SourceHash;
            _seamEntries = new bool[table.EntryCount];
            for (int i = 0; i < _seamEntries.Length; i++)
            {
                _seamEntries[i] = table.GetEntry(i);
            }
        }
    }
}
