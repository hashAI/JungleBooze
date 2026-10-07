namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// A stretch of track the route must keep straight and level (spec 003 sections 4.1 and 4.3): a committed vine
    /// chunk (<see cref="RouteBeatKind.SwingZone"/>) or a world gateway chunk (<see cref="RouteBeatKind.Gateway"/>).
    /// </summary>
    public struct RouteZone
    {
        public RouteBeatKind Kind;

        /// <summary>Arc length where the chunk starts.</summary>
        public double StartS;

        /// <summary>Arc length where the chunk ends.</summary>
        public double EndS;

        /// <summary>Generation serial of the chunk (<c>TrackSimulation.PeekChunk</c>).</summary>
        public int Serial;
    }
}
