using JungleBooze.Core;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Chooses between the straight route (default) and the curved <see cref="DebugRouteSource"/> at the start of each
    /// run. Changing <see cref="Curved"/> takes effect at the next <see cref="Begin"/>, never in the middle of a run.
    /// </summary>
    public sealed class SelectableRouteSource : IRouteSource
    {
        private readonly StraightRouteSource _straight = new StraightRouteSource();
        private readonly DebugRouteSource _curved = new DebugRouteSource();
        private IRouteSource _active;

        public SelectableRouteSource()
        {
            _active = _straight;
        }

        /// <summary>True: the next run uses the curved debug route. False (default): the straight route.</summary>
        public bool Curved { get; set; }

        /// <summary>True while the run in progress was started on the curved route.</summary>
        public bool ActiveIsCurved => _active == _curved;

        public void Begin(IRandom routeRandom, double startS)
        {
            _active = Curved ? (IRouteSource)_curved : _straight;
            _active.Begin(routeRandom, startS);
        }

        public void NextSample(double s, out RouteSample sample)
        {
            _active.NextSample(s, out sample);
        }
    }
}
