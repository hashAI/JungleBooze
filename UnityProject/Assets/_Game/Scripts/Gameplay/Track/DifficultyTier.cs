namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Runtime form of one difficulty tier (spec 002 section 3.3), with the derived <see cref="VMaxMps"/> and
    /// <see cref="MinRowSpacingM"/> and the pool weights resolved to library indices.
    /// </summary>
    public sealed class DifficultyTier
    {
        private readonly int[] _weightByChunk;

        internal DifficultyTier(int number, double fromM, float targetRowsPer100M, float minActionS, double vMaxMps, int[] weightByChunk)
        {
            Number = number;
            FromM = fromM;
            TargetRowsPer100M = targetRowsPer100M;
            MinActionS = minActionS;
            VMaxMps = vMaxMps;
            MinRowSpacingM = minActionS * vMaxMps;
            _weightByChunk = weightByChunk;
            int total = 0;
            for (int i = 0; i < weightByChunk.Length; i++)
            {
                total += weightByChunk[i];
            }

            TotalWeight = total;
        }

        /// <summary>1-based tier number.</summary>
        public int Number { get; }

        public double FromM { get; }

        public float TargetRowsPer100M { get; }

        public float MinActionS { get; }

        /// <summary>Derived: speed curve at the next tier's <c>fromM</c>; the curve cap for the last tier.</summary>
        public double VMaxMps { get; }

        /// <summary>Derived: <see cref="MinActionS"/> × <see cref="VMaxMps"/>.</summary>
        public double MinRowSpacingM { get; }

        /// <summary>Number of library chunks the weights cover.</summary>
        public int ChunkCount => _weightByChunk.Length;

        /// <summary>Sum of all pool weights.</summary>
        public int TotalWeight { get; }

        /// <summary>Pool weight of the chunk at library index <paramref name="chunkIndex"/> (0 = not in the pool).</summary>
        public int GetWeight(int chunkIndex)
        {
            return _weightByChunk[chunkIndex];
        }
    }
}
