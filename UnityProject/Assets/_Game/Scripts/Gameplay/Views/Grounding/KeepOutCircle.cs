namespace JungleBooze.Gameplay.Views
{
    /// <summary>A ground footprint the scenery placer must keep clear (route coordinates: distance s along the route, lateral x).</summary>
    public struct KeepOutCircle
    {
        public double S;
        public float X;
        public float RadiusM;
        public float HeightM;
    }
}
