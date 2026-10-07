namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Endless flat ground everywhere. Default track for week 1.</summary>
    public sealed class FlatTrackQuery : ITrackQuery
    {
        public static readonly FlatTrackQuery Instance = new FlatTrackQuery();

        public bool HasGround(float xMin, float xMax, double zMin, double zMax)
        {
            return true;
        }
    }
}
