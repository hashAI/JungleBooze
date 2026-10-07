namespace JungleBooze.Gameplay.Track
{
    /// <summary>Coin pattern shapes (spec 002 section 4.1). Stored in chunk assets: append only.</summary>
    public enum CoinPatternType : byte
    {
        /// <summary>Straight line in one lane.</summary>
        Line = 0,

        /// <summary>Jump parabola centred on a z (section 10.2).</summary>
        Arc = 1,

        /// <summary>Smoothstep lane change between two lanes (section 10.3).</summary>
        Trail = 2,

        /// <summary>One hand-placed coin.</summary>
        Single = 3,
    }
}
