using System;
using System.Collections.Generic;

namespace JungleBooze.Core.Save
{
    /// <summary>
    /// The player's persistent progress (ARCHITECTURE §9): wallet, best distance, abilities, journal, skill estimate.
    /// Public lower-case fields are the JSON format: never rename or reuse a field; add fields and bump
    /// <see cref="CurrentVersion"/> with an <see cref="ISaveMigration"/> when the meaning of data changes.
    /// No personal data. Format history: 1 = FP1 lane game (retired); 2 = AURELIA vertical slice.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        /// <summary>Format version (0 = not a save).</summary>
        public int version;

        /// <summary>Per-install seed for directed runs (run seeds derive from it and the run index).</summary>
        public long worldSeed;

        public int coins;

        public int crystals;

        /// <summary>Longest run, m.</summary>
        public float bestDistance;

        /// <summary>All runs, m (whole metres).</summary>
        public long totalDistance;

        /// <summary>Finished runs (deaths count). 0 = the next run is Expedition 1.</summary>
        public int runsCompleted;

        /// <summary>Owned abilities (bit flags, JungleBooze.Gameplay.World.AbilityFlags values).</summary>
        public int abilities;

        /// <summary>Abilities learned whose Showcase chunk has not appeared yet (spec 103 §10.2).</summary>
        public int pendingShowcase;

        /// <summary>Dynamic difficulty skill estimate S, −1…1 (spec 102 §6.4).</summary>
        public float skill;

        public List<JournalRecord> journal = new List<JournalRecord>();

        public static SaveData CreateDefault(long worldSeed, float startSkill)
        {
            return new SaveData { version = CurrentVersion, worldSeed = worldSeed, skill = startSkill };
        }

        public JournalRecord FindJournal(string id)
        {
            for (int i = 0; i < journal.Count; i++)
            {
                if (journal[i] != null && journal[i].id == id)
                {
                    return journal[i];
                }
            }

            return null;
        }

        public bool IsDiscovered(string id)
        {
            return FindJournal(id) != null;
        }

        public SaveData Clone()
        {
            var copy = (SaveData)MemberwiseClone();
            copy.journal = new List<JournalRecord>(journal.Count);
            for (int i = 0; i < journal.Count; i++)
            {
                if (journal[i] != null)
                {
                    copy.journal.Add(journal[i].Clone());
                }
            }

            return copy;
        }
    }
}
