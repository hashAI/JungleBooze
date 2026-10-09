using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>One scripted chunk of Expedition 1 (spec 103 §10.1).</summary>
    [Serializable]
    public struct ExpeditionScriptEntry
    {
        public string ChunkId;
        public string Variant;

        /// <summary>Power-up slot to fill (−1 none) and with what (the C11 Shield).</summary>
        public int PowerUpSlot;

        public PowerUpKind PowerUp;

        /// <summary>Difficulty rules the validator applies to this entry (spec 103 §3: e.g. C12–C13 use Challenge rules).</summary>
        public DifficultyPhase RulesPhase;

        /// <summary>Beat label for logs ("C3 First route choice").</summary>
        public string Beat;
    }
}
