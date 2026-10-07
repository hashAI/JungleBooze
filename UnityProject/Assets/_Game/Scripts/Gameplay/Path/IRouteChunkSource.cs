using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// What the route generator may know about the track (spec 003 section 4.1): the committed chunks, read only.
    /// The route is a pure function of the seed and of what this reports for the chunks within
    /// <see cref="RouteTuning.ReadAheadM"/> of the sample being built, so the owner of the route must not build
    /// beyond <c>CommittedEndS - ReadAheadM</c>. Implementations must not allocate.
    /// </summary>
    public interface IRouteChunkSource
    {
        /// <summary>End of the committed track (arc length). Nothing beyond it is known.</summary>
        double CommittedEndS { get; }

        /// <summary>
        /// The first committed vine or gateway chunk that ends after <paramref name="s"/> and starts no later than
        /// <paramref name="s"/> + <paramref name="lookaheadM"/>. False if there is none.
        /// </summary>
        bool TryFindZone(double s, double lookaheadM, out RouteZone zone);

        /// <summary>World at <paramref name="s"/> (switches at the centre of each committed gateway chunk).</summary>
        WorldKind WorldKindAt(double s);
    }
}
