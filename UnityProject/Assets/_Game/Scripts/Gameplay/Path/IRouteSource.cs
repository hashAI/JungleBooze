using JungleBooze.Core;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Produces route samples one after the other, 1 m apart in arc length. The straight route (T1) and the route
    /// generator (T2) implement it. Implementations must not allocate in <see cref="NextSample"/>.
    /// </summary>
    public interface IRouteSource
    {
        /// <summary>Starts a new route at arc length <paramref name="startS"/>, drawing from the run's Route stream.</summary>
        void Begin(IRandom routeRandom, double startS);

        /// <summary>Writes the sample at arc length <paramref name="s"/>. Called with strictly increasing s, one spacing apart.</summary>
        void NextSample(double s, out RouteSample sample);
    }
}
