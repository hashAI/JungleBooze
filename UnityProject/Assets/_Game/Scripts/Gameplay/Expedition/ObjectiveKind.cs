namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Next-objective priorities (GDD §17; first match wins).</summary>
    public enum ObjectiveKind : byte
    {
        None = 0,

        /// <summary>1. An ability is affordable now.</summary>
        AbilityReady,

        /// <summary>2. The run ended within 10% of the best distance.</summary>
        NearBest,

        /// <summary>3. The next ability is ≥ 60% funded (≥ 50% after run 1).</summary>
        AbilityProgress,

        /// <summary>4. An undiscovered entry exists in reached territory.</summary>
        UnseenEntry,

        /// <summary>5. Secrets of the biome.</summary>
        Secrets,

        /// <summary>Nothing else matched: beat the best distance [ASSUMED].</summary>
        BeatBest,
    }
}
