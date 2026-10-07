namespace JungleBooze.Gameplay.Path
{
    /// <summary>Which route the next run is built on. The order is the F4 cycle order.</summary>
    public enum RouteMode
    {
        /// <summary>The seeded winding route from <see cref="RouteGenerator"/> (default).</summary>
        Generated = 0,

        /// <summary>The fixed sinusoidal <see cref="DebugRouteSource"/> curve.</summary>
        DebugCurve = 1,

        /// <summary>The straight route, kept for comparison.</summary>
        Straight = 2,
    }
}
