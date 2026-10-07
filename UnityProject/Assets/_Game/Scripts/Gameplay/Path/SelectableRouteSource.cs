using JungleBooze.Core;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Chooses the route at the start of each run: the generated winding route, the curved <see cref="DebugRouteSource"/>
    /// or the straight route. Changing <see cref="Mode"/> takes effect at the next <see cref="Begin"/>, never in the
    /// middle of a run.
    /// </summary>
    public sealed class SelectableRouteSource : IRouteSource
    {
        private readonly StraightRouteSource _straight = new StraightRouteSource();
        private readonly DebugRouteSource _curved = new DebugRouteSource();
        private readonly IRouteSource _generated;
        private IRouteSource _active;
        private RouteMode _activeMode;

        /// <summary>Straight route, no generated route available (<see cref="RouteMode.Generated"/> falls back to straight).</summary>
        public SelectableRouteSource()
            : this(null, RouteMode.Straight)
        {
        }

        /// <param name="generated">The generated route source (null: none, Generated falls back to straight).</param>
        /// <param name="initialMode">The mode of the first run.</param>
        public SelectableRouteSource(IRouteSource generated, RouteMode initialMode)
        {
            _generated = generated;
            Mode = initialMode;
            _active = Resolve(initialMode, out _activeMode);
        }

        /// <summary>The mode the next run will use.</summary>
        public RouteMode Mode { get; set; }

        /// <summary>The mode of the run in progress (or the last run).</summary>
        public RouteMode ActiveMode => _activeMode;

        /// <summary>True when <see cref="Mode"/> differs from the run in progress, so it applies at the next run.</summary>
        public bool Pending => Mode != _activeMode;

        /// <summary>True: the next run uses the curved debug route.</summary>
        public bool Curved
        {
            get => Mode == RouteMode.DebugCurve;
            set => Mode = value ? RouteMode.DebugCurve : RouteMode.Straight;
        }

        /// <summary>True while the run in progress was started on the curved debug route.</summary>
        public bool ActiveIsCurved => _activeMode == RouteMode.DebugCurve;

        public void Begin(IRandom routeRandom, double startS)
        {
            _active = Resolve(Mode, out _activeMode);
            _active.Begin(routeRandom, startS);
        }

        public void NextSample(double s, out RouteSample sample)
        {
            _active.NextSample(s, out sample);
        }

        private IRouteSource Resolve(RouteMode mode, out RouteMode actual)
        {
            if (mode == RouteMode.Generated && _generated != null)
            {
                actual = RouteMode.Generated;
                return _generated;
            }

            if (mode == RouteMode.DebugCurve)
            {
                actual = RouteMode.DebugCurve;
                return _curved;
            }

            actual = RouteMode.Straight;
            return _straight;
        }
    }
}
