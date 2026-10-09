namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// One World Director decision (spec 102 §6.2): which chunk variant, and how it is dressed (rewards, power-up,
    /// crystals, cue intensity). Plain struct; the path stores it per placed chunk.
    /// </summary>
    public struct ChunkPick
    {
        /// <summary>Library entry (chunk + variant).</summary>
        public int Entry;

        /// <summary>Pick number in the run (0 = the opening chunk).</summary>
        public int Serial;

        public PickReason Reason;

        public DifficultyPhase Phase;

        /// <summary>Bit per route of the variant that is open.</summary>
        public int EnabledRoutes;

        public RewardProfile Reward;

        /// <summary>Fraction of authored coins kept, (0, 1].</summary>
        public float CoinDensity;

        /// <summary>Bit per crystal anchor that is filled (Always anchors are always filled).</summary>
        public int CrystalMask;

        /// <summary>Local coin index turned into a flow crystal, or −1.</summary>
        public int FlowCrystalCoin;

        /// <summary>Filled power-up slot, or −1.</summary>
        public int PowerUpSlot;

        public PowerUpKind PowerUp;

        /// <summary>Secret/risky cue strength from dynamic difficulty (0.85–1.30).</summary>
        public float CueIntensity;

        /// <summary>Relaxation level the filter needed (0 = none; diagnostics).</summary>
        public int RelaxLevel;

        public bool Scripted => Reason == PickReason.Script;
    }
}
