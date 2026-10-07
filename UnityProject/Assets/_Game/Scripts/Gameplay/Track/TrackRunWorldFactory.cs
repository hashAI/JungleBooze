using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Creates a <see cref="TrackRunWorld"/> per run. Swap it for <see cref="FlatRunWorldFactory"/> in the
    /// composition root to play on the generated track. <see cref="ForcedSeed"/> (spec 3.7 <c>devFixedSeed</c>,
    /// development builds) replaces the session's seed when non-zero; the world's <see cref="IRunWorld.Seed"/> then
    /// reports the forced seed.
    /// </summary>
    public sealed class TrackRunWorldFactory : IRunWorldFactory
    {
        public TrackRunWorldFactory()
            : this(TrackRunSetup.CreateDefault())
        {
        }

        public TrackRunWorldFactory(TrackRunSetup setup, ulong forcedSeed = 0UL)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            ForcedSeed = forcedSeed;
        }

        public TrackRunSetup Setup { get; }

        /// <summary>0 = off.</summary>
        public ulong ForcedSeed { get; set; }

        /// <summary>The world created last (views and HUD can also read <c>GameSession.World as TrackRunWorld</c>).</summary>
        public TrackRunWorld LastWorld { get; private set; }

        public IRunWorld Create(ulong seed, RunnerConfig config)
        {
            LastWorld = new TrackRunWorld(Setup, ForcedSeed != 0UL ? ForcedSeed : seed);
            return LastWorld;
        }
    }
}
