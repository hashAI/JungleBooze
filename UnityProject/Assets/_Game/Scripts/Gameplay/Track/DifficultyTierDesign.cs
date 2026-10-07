namespace JungleBooze.Gameplay.Track
{
    /// <summary>One authored row of <c>DifficultyTiers</c> (spec 002 section 3.3). Tier numbers are 1-based by position.</summary>
    public sealed class DifficultyTierDesign
    {
        public float FromM;
        public float TargetRowsPer100M;
        public float MinActionS;
        public ChunkWeight[] Weights = new ChunkWeight[0];

        public DifficultyTierDesign()
        {
        }

        public DifficultyTierDesign(float fromM, float targetRowsPer100M, float minActionS, ChunkWeight[] weights)
        {
            FromM = fromM;
            TargetRowsPer100M = targetRowsPer100M;
            MinActionS = minActionS;
            Weights = weights ?? new ChunkWeight[0];
        }

        public DifficultyTierDesign Clone()
        {
            return new DifficultyTierDesign(FromM, TargetRowsPer100M, MinActionS, (ChunkWeight[])Weights.Clone());
        }
    }
}
