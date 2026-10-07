using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Builds a route the way the game builds it, without a scene (spec 003 section 14.2): a scripted hero advances
    /// along the real generated track in <c>heroStepM</c> steps, and after every step the route is extended to
    /// <c>hero + AheadM</c> through <see cref="SessionRouteChunkSource"/>. Records the smallest committed distance
    /// ahead of the hero (AC-313) and how close to the end of the committed track the route was ever built.
    /// A tool for tests and the editor menu: it allocates. The scripted hero never boosts, dies or swings, so the
    /// vine hold of a Speed Boost (spec 003 risk R9) is not covered.
    /// </summary>
    public sealed class RouteTrackSimulator
    {
        public RouteSample[] Samples;
        public WorldKind[] Worlds;
        public int Count;
        public double StartS;

        /// <summary>Smallest <c>GeneratedEndZ - heroZ</c> seen after an update (AC-313: at least 175 m).</summary>
        public double MinCommittedAheadM = double.MaxValue;

        /// <summary>Smallest <c>GeneratedEndZ - ReadAheadM - s</c> over every built sample; negative means the route was built on unknown chunks.</summary>
        public double WorstReadMarginM = double.MaxValue;

        public TrackSimulation TrackSim;

        /// <summary>A track for the default game setup, reset with the three streams in the order the game uses.</summary>
        public static TrackSimulation CreateTrack(ulong seed, TrackConfig config)
        {
            TrackRunSetup setup = TrackRunSetup.CreateDefault();
            SpeedCurve curve = SpeedCurve.CreateDefault();
            var track = new TrackSimulation(
                config ?? setup.Track,
                setup.Kit,
                setup.Coins,
                setup.Library,
                setup.GetTiers(curve),
                curve,
                RunnerConfig.CreateDefault(),
                setup.Vines,
                setup.PowerUps,
                setup.Hazards,
                setup.Worlds);
            var root = new Pcg32Random(seed);
            IRandom trackStream = root.Fork(RandomStreamIds.TrackGeneration);
            IRandom vineStream = root.Fork(RandomStreamIds.VineSchedule);
            IRandom pickupStream = root.Fork(RandomStreamIds.Pickups);
            track.Reset(trackStream, vineStream, pickupStream);
            return track;
        }

        /// <summary>Builds the route of run seed <paramref name="seed"/> for a hero walking <paramref name="lengthM"/> m.</summary>
        public static RouteTrackSimulator Build(ulong seed, double lengthM, double heroStepM, RouteTuning tuning)
        {
            TrackSimulation track = CreateTrack(seed, null);
            var chunks = new SessionRouteChunkSource(track);
            var generator = new RouteGenerator(tuning, chunks);
            double startS = -tuning.BehindM;
            double ds = tuning.SampleSpacingM;
            generator.Begin(new Pcg32Random(seed).Fork(RandomStreamIds.Route), startS);

            int capacity = (int)((lengthM + tuning.AheadM - startS) / ds) + 8;
            var result = new RouteTrackSimulator
            {
                Samples = new RouteSample[capacity],
                Worlds = new WorldKind[capacity],
                StartS = startS,
                TrackSim = track,
            };

            double previous = -heroStepM;
            for (double z = 0.0; z <= lengthM + 1e-9; z += heroStepM)
            {
                var info = new RunnerTickInfo
                {
                    Z = z,
                    ZPrev = previous,
                    Speed = (z - previous) * 60.0,
                    HitboxHeight = 1.8f,
                    HalfWidth = 0.35f,
                    HalfDepth = 0.25f,
                    OccupiedLane = 1,
                };
                track.Update(info, null);
                previous = z;

                double ahead = track.CommittedAheadM(z);
                if (ahead < result.MinCommittedAheadM)
                {
                    result.MinCommittedAheadM = ahead;
                }

                double limit = z + tuning.AheadM;
                while (result.Count < capacity && startS + (result.Count * ds) <= limit)
                {
                    double s = startS + (result.Count * ds);
                    double margin = track.GeneratedEndZ - tuning.ReadAheadM - s;
                    if (margin < result.WorstReadMarginM)
                    {
                        result.WorstReadMarginM = margin;
                    }

                    generator.NextSample(s, out result.Samples[result.Count]);
                    result.Worlds[result.Count] = track.WorldKindAt(s);
                    result.Count++;
                }
            }

            return result;
        }
    }
}
