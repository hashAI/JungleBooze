using System.Collections.Generic;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Runs the <see cref="ChunkValidator"/> over a whole catalog (spec 102 §4.1, spec 103 §10.1): every
    /// non-script variant over its definition's phase range, and every Expedition 1 entry at the speed the script
    /// meets it. Produces one report per check and the per-variant <see cref="ChunkDefinition.ValidatedMask"/>.
    /// Tools and tests only (slow: the Perfect bot drives every route).
    /// </summary>
    public static class CatalogValidation
    {
        public sealed class Result
        {
            public List<ValidationReport> Reports { get; } = new List<ValidationReport>();

            /// <summary>Validated mask per definition (library definition order).</summary>
            public int[] Masks { get; set; }

            public int Failures
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < Reports.Count; i++)
                    {
                        n += Reports[i].Passed ? 0 : 1;
                    }

                    return n;
                }
            }

            public int BotRuns
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < Reports.Count; i++)
                    {
                        n += Reports[i].BotRuns;
                    }

                    return n;
                }
            }
        }

        public static Result Run(ChunkLibrary library, ExpeditionScript script, MovementConfig movement, WorldDirectorConfig director)
        {
            var validator = new ChunkValidator(movement, director);
            var result = new Result { Masks = new int[library.DefinitionCount] };
            var scriptPassed = new Dictionary<int, bool>();

            // Expedition 1 entries at their met speed.
            float distance = 0f;
            if (script != null)
            {
                for (int i = 0; i < script.Entries.Count; i++)
                {
                    ExpeditionScriptEntry entry = script.Entries[i];
                    int index = library.Find(entry.ChunkId, entry.Variant);
                    if (index < 0)
                    {
                        var missing = new ValidationReport(entry.ChunkId, entry.Variant);
                        missing.Add("SCRIPT", 0f, "script entry not in the catalog");
                        result.Reports.Add(missing);
                        continue;
                    }

                    ChunkRuntime chunk = library.GetEntry(index);
                    ValidationReport report = validator.ValidateScripted(chunk, entry.RulesPhase, distance);
                    result.Reports.Add(report);
                    scriptPassed[index] = (scriptPassed.TryGetValue(index, out bool before) ? before : true) && report.Passed;
                    distance += chunk.Length;
                }
            }

            // Pool variants over the definition's phase range.
            for (int e = 0; e < library.EntryCount; e++)
            {
                ChunkRuntime chunk = library.GetEntry(e);
                bool passed;
                if (chunk.Variant.ScriptOnly)
                {
                    passed = scriptPassed.TryGetValue(e, out bool ok) && ok;
                }
                else
                {
                    ValidationReport report = validator.ValidatePool(chunk);
                    result.Reports.Add(report);
                    passed = report.Passed;
                }

                if (passed)
                {
                    result.Masks[chunk.DefinitionIndex] |= 1 << chunk.VariantIndex;
                }
            }

            return result;
        }

        /// <summary>Writes the masks into the definitions (the editor then saves the assets).</summary>
        public static void ApplyMasks(ChunkLibrary library, Result result)
        {
            for (int d = 0; d < library.DefinitionCount; d++)
            {
                library.GetDefinition(d).ValidatedMask = result.Masks[d];
            }
        }
    }
}
