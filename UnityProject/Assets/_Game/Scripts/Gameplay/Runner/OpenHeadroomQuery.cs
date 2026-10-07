namespace JungleBooze.Gameplay.Runner
{
    /// <summary>No obstacles overhead anywhere. Default until the collision stage exists.</summary>
    public sealed class OpenHeadroomQuery : IHeadroomQuery
    {
        public static readonly OpenHeadroomQuery Instance = new OpenHeadroomQuery();

        public bool CanStand(float xMin, float xMax, double zMin, double zMax, float standingHeightM)
        {
            return true;
        }
    }
}
