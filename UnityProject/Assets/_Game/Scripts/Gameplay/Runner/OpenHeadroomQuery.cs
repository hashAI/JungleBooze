namespace JungleBooze.Gameplay.Runner
{
    /// <summary>No obstacles overhead anywhere: standing up is always allowed. For worlds without obstacle boxes and for tests.</summary>
    public sealed class OpenHeadroomQuery : IHeadroomQuery
    {
        public static readonly OpenHeadroomQuery Instance = new OpenHeadroomQuery();

        public bool CanStand(float xMin, float xMax, double zMin, double zMax, float standingHeightM)
        {
            return true;
        }
    }
}
