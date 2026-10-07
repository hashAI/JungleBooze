using JungleBooze.Core;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// The T1 route: a dead-straight, flat, unbanked trail along +z. World z = path s, x = lateral, y = height, so
    /// <see cref="PathFrame.ToWorld"/> is the identity and the game looks exactly as it did before the PathFrame.
    /// </summary>
    public sealed class StraightRouteSource : IRouteSource
    {
        /// <summary>Playable half width in m (7.2 m playable, spec 003 section 5.1).</summary>
        public const float HalfWidthM = 3.6f;

        public void Begin(IRandom routeRandom, double startS)
        {
        }

        public void NextSample(double s, out RouteSample sample)
        {
            sample = default(RouteSample);
            sample.Z = s;
            sample.HalfWidthM = HalfWidthM;
            sample.Layer = PathLayer.Floor;
            sample.Surface = PathSurface.Trail;
        }
    }
}
