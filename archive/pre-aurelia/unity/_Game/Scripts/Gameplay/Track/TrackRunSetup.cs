using System;
using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;

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
            WorldSkinConfig skin = null,
            VineConfig vines = null,
            PowerUpConfig powerUps = null,
            HazardConfig hazards = null)
        {
            Vines = vines ?? VineConfig.CreateDefault();
            PowerUps = powerUps ?? PowerUpConfig.CreateDefault();
            Hazards = hazards ?? HazardConfig.CreateDefault();
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

        /// <summary>Vine tuning (GDD 7.5): vine sections, swing, release, launch, vine score and coins.</summary>
        public VineConfig Vines { get; }

        /// <summary>Power-up tuning (GDD 10): pickup schedule and placement, durations, effects.</summary>
        public PowerUpConfig PowerUps { get; }

        /// <summary>Signature hazard tuning (GDD 8.3): lane-strike warning, active time and rhythm.</summary>
        public HazardConfig Hazards { get; }

        /// <summary>
        /// The defaults: spec 002 start values, the 16-chunk Jungle library plus the vine sections and signature hazard chunks, and the GDD 7.5
        /// vine values.
        /// </summary>
        public static TrackRunSetup CreateDefault()
        {
            return CreateDefault(VineConfig.CreateDefault());
        }

        /// <summary>Like <see cref="CreateDefault()"/> with the given vine tuning (for example from the VineTuning asset).</summary>
        public static TrackRunSetup CreateDefault(VineConfig vines)
        {
            return CreateDefault(vines, null, null);
        }

        /// <summary>
        /// The game setup: defaults plus the given vine, power-up and hazard tuning (null = built-in defaults), on the
        /// game library (FP1 chunks, vine sections and signature hazard chunks, GDD 8.3).
        /// </summary>
        public static TrackRunSetup CreateDefault(VineConfig vines, PowerUpConfig powerUps, HazardConfig hazards)
        {
            return new TrackRunSetup(
                TrackConfig.CreateDefault(),
                ObstacleKitConfig.CreateDefault(),
                CoinConfig.CreateDefault(),
                ScoreConfig.CreateDefault(),
                JungleChunkLibraryDefaults.CreateGameLibrary(),
                DifficultyTiersDesignValues.CreateGameDefault(),
                WorldSkinConfig.CreateJungle(),
                vines,
                powerUps,
                hazards);
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
            h = StableHash.Mix(h, Vines.ComputeHash());
            h = StableHash.Mix(h, PowerUps.ComputeHash());
            h = StableHash.Mix(h, Hazards.ComputeHash());
            return h;
        }
    }
}
