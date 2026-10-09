using System;
using JungleBooze.Core.Save;
using UnityEngine;

namespace JungleBooze.Services.Save
{
    /// <summary>
    /// <see cref="SaveData"/> ⇄ versioned JSON. Decoding never throws: anything that is not a save (empty, cut off,
    /// not JSON, version 0) fails so the caller can fall back to the backup or defaults. Older versions are upgraded
    /// through <see cref="SaveMigrations"/>; out-of-range values are repaired. Allocates; load/save time only.
    /// </summary>
    public static class SaveCodec
    {
        public static string Encode(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.version = SaveData.CurrentVersion;
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// Parses, upgrades and repairs. <paramref name="futureVersion"/> is true when the file comes from a newer
        /// build: it is read as far as this build understands it and must not be overwritten (ARCHITECTURE §9).
        /// </summary>
        public static bool TryDecode(string json, out SaveData data, out bool futureVersion, out string error)
        {
            data = null;
            futureVersion = false;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "the file is empty";
                return false;
            }

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                error = "not valid JSON (" + e.Message + ")";
                return false;
            }

            if (parsed == null || parsed.version < 1)
            {
                error = "missing or invalid version";
                return false;
            }

            if (parsed.journal == null)
            {
                parsed.journal = new System.Collections.Generic.List<JournalRecord>();
            }

            futureVersion = parsed.version > SaveData.CurrentVersion;
            if (!futureVersion)
            {
                SaveMigrations.Upgrade(parsed);
            }

            Repair(parsed);
            data = parsed;
            error = null;
            return true;
        }

        private static void Repair(SaveData data)
        {
            data.coins = Math.Max(0, data.coins);
            data.crystals = Math.Max(0, data.crystals);
            data.runsCompleted = Math.Max(0, data.runsCompleted);
            data.totalDistance = Math.Max(0L, data.totalDistance);
            if (float.IsNaN(data.bestDistance) || float.IsInfinity(data.bestDistance) || data.bestDistance < 0f)
            {
                data.bestDistance = 0f;
            }

            if (float.IsNaN(data.skill) || float.IsInfinity(data.skill))
            {
                data.skill = 0f;
            }

            data.skill = Mathf.Clamp(data.skill, -1f, 1f);
            for (int i = data.journal.Count - 1; i >= 0; i--)
            {
                JournalRecord r = data.journal[i];
                if (r == null || string.IsNullOrEmpty(r.id) || data.journal.FindIndex(o => o != null && o.id == r.id) != i)
                {
                    data.journal.RemoveAt(i);
                }
                else
                {
                    r.sightings = Math.Max(1, r.sightings);
                }
            }
        }
    }
}
