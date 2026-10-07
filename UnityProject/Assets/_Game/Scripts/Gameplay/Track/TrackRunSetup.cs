using System;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// The immutable configs a track run is built from (spec 002 section 3): track, obstacle kit, coins, score,
    /// chunk library, difficulty tiers (in designer form, because the derived values need the run's speed curve)
    /// and the world skin. Built once per session by the composition root (from the config assets, or
    /// <see cref="CreateDefault"/>) and shared by every run. The tiers built for a speed curve are cached.
    /// </summary>
    public sealed class TrackRunSetup
    {
        private readonly DifficultyTiersDesignValues _tierValues;
        private SpeedCurve _cachedCurve;
        private DifficultyTiersConfig _cachedTiers;

        public TrackRunSetup(
            TrackConfig track,
            ObstacleKitConfig kit,
            CoinConfig coins,
            ScoreConfig score,
            ChunkLibrary library,
            DifficultyTiersDesignValues tiers,
            WorldSkinConfig skin = null)
        {
            Track = track ?? throw new ArgumentNullException(nameof(track));
            Kit = kit ?? throw new ArgumentNullException(nameof(kit));
            Coins = coins ?? throw new ArgumentNullException(nameof(coins));
            Score = score ?? throw new ArgumentNullException(nameof(score));
            Library = library ?? throw new ArgumentNullException(nameof(library));
            _tierValues = (tiers ?? throw new ArgumentNullException(nameof(tiers))).Clone();
            Skin = skin ?? WorldSkinConfig.CreateJungle();
            if (library.IndexOf(track.StartChunkId) < 0)
            {
                throw new ArgumentException("The chunk library has no start chunk '" + track.StartChunkId + "'.", nameof(library));
            }
        }

        public TrackConfig Track { get; }

        public ObstacleKitConfig Kit { get; }

        public CoinConfig Coins { get; }

        public ScoreConfig Score { get; }

        public ChunkLibrary Library { get; }

        public WorldSkinConfig Skin { get; }

        /// <summary>The FP1 defaults: spec 002 start values and the 16-chunk Jungle library.</summary>
        public static TrackRunSetup CreateDefault()
        {
            return new TrackRunSetup(
                TrackConfig.CreateDefault(),
                ObstacleKitConfig.CreateDefault(),
                CoinConfig.CreateDefault(),
                ScoreConfig.CreateDefault(),
                JungleChunkLibraryDefaults.CreateLibrary(),
                DifficultyTiersDesignValues.CreateDefault(),
                WorldSkinConfig.CreateJungle());
        }

        /// <summary>
        /// The difficulty tiers derived for <paramref name="curve"/> (AC-202). Cached per curve instance; throws
        /// <see cref="ArgumentException"/> if the tier values do not fit the library (AC-201). Setup time only.
        /// </summary>
        public DifficultyTiersConfig GetTiers(SpeedCurve curve)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }

            if (!ReferenceEquals(curve, _cachedCurve))
            {
                _cachedTiers = DifficultyTiersConfig.FromDesignValues(_tierValues, curve, Library);
                _cachedCurve = curve;
            }

            return _cachedTiers;
        }

        /// <summary>Hash of every simulation config in the setup (replays and sim reports).</summary>
        public ulong ComputeConfigHash(SpeedCurve curve)
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, Track.ComputeHash());
            h = StableHash.Mix(h, Kit.ComputeHash());
            h = StableHash.Mix(h, Coins.ComputeHash());
            h = StableHash.Mix(h, Score.ComputeHash());
            h = StableHash.Mix(h, Library.DataHash);
            h = StableHash.Mix(h, GetTiers(curve).ComputeHash());
            return h;
        }
    }
}
