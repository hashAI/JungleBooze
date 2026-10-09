namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Coin pitch along a streak (spec 103 §11: "pitch rises along a streak"): each coin within
    /// <see cref="Gap"/> seconds of the previous one climbs one step of a major scale, capped a fifth up (the coin is a bright chime: higher gets shrill);
    /// a pause resets it. <see cref="Update"/> reports the end of a streak of at least <see cref="MinForHaptic"/> coins
    /// (light haptic at streak end).
    /// </summary>
    public sealed class CoinStreak
    {
        public const float Gap = 0.45f;
        public const int MinForHaptic = 5;

        private static readonly float[] Steps = { 0f, 2f, 4f, 5f, 7f };

        private double _last = double.NegativeInfinity;

        public int Count { get; private set; }

        /// <summary>A coin was collected: returns the pitch offset in semitones.</summary>
        public float OnCoin(double now)
        {
            Count = now - _last <= Gap ? Count + 1 : 1;
            _last = now;
            int i = Count - 1;
            return Steps[i < Steps.Length ? i : Steps.Length - 1];
        }

        /// <summary>True once when a streak of ≥ <see cref="MinForHaptic"/> coins has ended.</summary>
        public bool Update(double now)
        {
            if (Count > 0 && now - _last > Gap)
            {
                bool ended = Count >= MinForHaptic;
                Count = 0;
                return ended;
            }

            return false;
        }

        public void Reset()
        {
            Count = 0;
            _last = double.NegativeInfinity;
        }
    }
}
