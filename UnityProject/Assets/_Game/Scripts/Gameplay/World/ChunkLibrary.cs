using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// All chunk variants of a catalog, prebuilt as <see cref="ChunkRuntime"/>s (setup time). Entries are indexed by
    /// <see cref="ChunkRuntime.LibraryIndex"/>; the director picks entries, the path places them.
    /// </summary>
    public sealed class ChunkLibrary
    {
        private readonly List<ChunkDefinition> _definitions;
        private readonly ChunkRuntime[] _entries;
        private readonly int[] _firstEntry;

        public ChunkLibrary(IList<ChunkDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitions = new List<ChunkDefinition>(definitions);
            var entries = new List<ChunkRuntime>();
            _firstEntry = new int[_definitions.Count];
            for (int d = 0; d < _definitions.Count; d++)
            {
                ChunkDefinition def = _definitions[d] ?? throw new ArgumentException("Null chunk definition at " + d);
                _firstEntry[d] = entries.Count;
                for (int v = 0; v < def.Variants.Count; v++)
                {
                    entries.Add(new ChunkRuntime(def, d, v, entries.Count));
                }
            }

            _entries = entries.ToArray();
        }

        public int DefinitionCount => _definitions.Count;

        public int EntryCount => _entries.Length;

        public ChunkDefinition GetDefinition(int index) => _definitions[index];

        public ChunkRuntime GetEntry(int index) => _entries[index];

        /// <summary>Library index of a chunk variant, or −1.</summary>
        public int Find(string chunkId, string variant)
        {
            for (int d = 0; d < _definitions.Count; d++)
            {
                if (_definitions[d].Id != chunkId)
                {
                    continue;
                }

                int v = string.IsNullOrEmpty(variant) ? 0 : _definitions[d].FindVariant(variant);
                return v < 0 ? -1 : _firstEntry[d] + v;
            }

            return -1;
        }

        public int FindDefinition(string chunkId)
        {
            for (int d = 0; d < _definitions.Count; d++)
            {
                if (_definitions[d].Id == chunkId)
                {
                    return d;
                }
            }

            return -1;
        }

        public int FirstEntryOf(int definition) => _firstEntry[definition];

        /// <summary>True when a variant may be used by the director (not script-only and validated, if required).</summary>
        public bool IsPoolVariant(ChunkRuntime entry, bool requireValidated)
        {
            if (entry.Variant.ScriptOnly)
            {
                return false;
            }

            return !requireValidated || (entry.Definition.ValidatedMask & (1 << entry.VariantIndex)) != 0;
        }
    }
}
