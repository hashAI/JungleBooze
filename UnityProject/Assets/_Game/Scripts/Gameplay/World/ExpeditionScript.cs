using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// The first-ever run, Expedition 1 (spec 103 §2, §10.1): a fixed seed and an ordered chunk list. The script
    /// overrides the director's forced picks, weights and repetition rules but never the validator; crystals are the
    /// variants' fixed anchors, cue intensity 1.00, and after the last entry the director forces a Recovery.
    /// </summary>
    [Serializable]
    public sealed class ExpeditionScript
    {
        public ulong Seed = 1UL;
        public float CueIntensity = 1f;

        /// <summary>First-run health floor (s) and "no gaps" window (spec 103 §10.1, AC-103-41).</summary>
        public float NoGapSeconds = 60f;

        public List<ExpeditionScriptEntry> Entries = new List<ExpeditionScriptEntry>();
    }
}
