using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Immutable runtime form of one chunk (spec 002 section 4.1). Built from a <c>ChunkAsset</c> or, for the FP1
    /// library, by <see cref="JungleChunkLibraryDefaults"/>. Lists are copied on construction and read by index,
    /// so reading never allocates. Format rules (G1 and the other static rules) are checked by the chunk validator,
    /// not here; the constructor only rejects data the runtime cannot hold.
    /// </summary>
    public sealed class ChunkData
    {
        /// <summary>Spec 002 section 4.1 <c>formatVersion</c>.</summary>
        public const int CurrentFormatVersion = 1;

        private readonly ObstaclePlacement[] _obstacles;
        private readonly CoinPattern[] _coins;

        public ChunkData(
            string id,
            ChunkKind kind,
            float lengthM,
            int minTier,
            int maxTier,
            ObstaclePlacement[] obstacles,
            CoinPattern[] coins,
            bool allowMirror = true,
            WorldMask worldMask = WorldMask.All,
            string displayName = null,
            string designNote = null,
            int formatVersion = CurrentFormatVersion)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("A chunk needs an id.", nameof(id));
            }

            if (!(lengthM > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(lengthM), "Chunk " + id + ": length must be positive.");
            }

            Id = id;
            Kind = kind;
            LengthM = lengthM;
            MinTier = minTier;
            MaxTier = maxTier;
            AllowMirror = allowMirror;
            WorldMask = worldMask;
            DisplayName = displayName ?? id;
            DesignNote = designNote ?? string.Empty;
            FormatVersion = formatVersion;
            _obstacles = obstacles == null ? Array.Empty<ObstaclePlacement>() : (ObstaclePlacement[])obstacles.Clone();
            _coins = coins == null ? Array.Empty<CoinPattern>() : (CoinPattern[])coins.Clone();
        }

        /// <summary>Stable unique id (<c>T1-01</c>), stored in replays and sim reports.</summary>
        public string Id { get; }

        public ChunkKind Kind { get; }

        public float LengthM { get; }

        public int MinTier { get; }

        public int MaxTier { get; }

        public bool AllowMirror { get; }

        public WorldMask WorldMask { get; }

        /// <summary>Designer label (debug overlay).</summary>
        public string DisplayName { get; }

        /// <summary>One-line design intent (spec 002 section 7.2).</summary>
        public string DesignNote { get; }

        public int FormatVersion { get; }

        public int ObstacleCount => _obstacles.Length;

        public int CoinPatternCount => _coins.Length;

        public ObstaclePlacement GetObstacle(int index)
        {
            return _obstacles[index];
        }

        public CoinPattern GetCoinPattern(int index)
        {
            return _coins[index];
        }

        /// <summary>
        /// Number of rows: obstacles whose fronts lie within <paramref name="rowGroupingM"/> of the previous front
        /// belong to the same row (spec 002 section 2). Obstacles must be sorted by front.
        /// </summary>
        public int CountRows(float rowGroupingM)
        {
            int rows = 0;
            float rowStart = float.NegativeInfinity;
            for (int i = 0; i < _obstacles.Length; i++)
            {
                float z = _obstacles[i].Zc;
                if (z - rowStart > rowGroupingM)
                {
                    rows++;
                    rowStart = z;
                }
            }

            return rows;
        }

        /// <summary>A copy with every lane mirrored (spec 002 section 4.2). Same id. Setup and tests only.</summary>
        public ChunkData Mirrored()
        {
            var obstacles = new ObstaclePlacement[_obstacles.Length];
            for (int i = 0; i < obstacles.Length; i++)
            {
                obstacles[i] = _obstacles[i].Mirrored();
            }

            var coins = new CoinPattern[_coins.Length];
            for (int i = 0; i < coins.Length; i++)
            {
                coins[i] = _coins[i].Mirrored();
            }

            return new ChunkData(
                Id, Kind, LengthM, MinTier, MaxTier, obstacles, coins, AllowMirror, WorldMask, DisplayName, DesignNote, FormatVersion);
        }

        /// <summary>Stable hash of every field that affects the simulation (part of the config hash).</summary>
        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, Id);
            h = StableHash.Mix(h, FormatVersion);
            h = StableHash.Mix(h, (int)Kind);
            h = StableHash.Mix(h, LengthM);
            h = StableHash.Mix(h, MinTier);
            h = StableHash.Mix(h, MaxTier);
            h = StableHash.Mix(h, (int)WorldMask);
            h = StableHash.Mix(h, AllowMirror);
            h = StableHash.Mix(h, _obstacles.Length);
            for (int i = 0; i < _obstacles.Length; i++)
            {
                ObstaclePlacement o = _obstacles[i];
                h = StableHash.Mix(h, (int)o.Archetype);
                h = StableHash.Mix(h, (int)o.LaneMask);
                h = StableHash.Mix(h, o.Zc);
                h = StableHash.Mix(h, o.GapLengthM);
                h = StableHash.Mix(h, o.MoverToLane);
            }

            h = StableHash.Mix(h, _coins.Length);
            for (int i = 0; i < _coins.Length; i++)
            {
                CoinPattern c = _coins[i];
                h = StableHash.Mix(h, (int)c.Type);
                h = StableHash.Mix(h, c.Lane);
                h = StableHash.Mix(h, c.ToLane);
                h = StableHash.Mix(h, c.ZStart);
                h = StableHash.Mix(h, c.ZEnd);
                h = StableHash.Mix(h, c.SpacingM);
                h = StableHash.Mix(h, c.X);
                h = StableHash.Mix(h, c.Y);
            }

            return h;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
