using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>Immutable runtime <c>TrackTuning</c> (spec 002 section 3.1). The simulation only reads this.</summary>
    public sealed class TrackConfig
    {
        private TrackConfig(TrackDesignValues v)
        {
            GenerateAheadM = v.GenerateAheadM;
            DespawnBehindM = v.DespawnBehindM;
            LeadInM = v.LeadInM;
            LeadOutM = v.LeadOutM;
            RowGroupingM = v.RowGroupingM;
            BreatherIntervalMinS = v.BreatherIntervalMinS;
            BreatherIntervalMaxS = v.BreatherIntervalMaxS;
            BreatherMinDurationS = v.BreatherMinDurationS;
            NoRepeatWindow = v.NoRepeatWindow;
            MirrorChance = v.MirrorChance;
            MaxPickAttempts = v.MaxPickAttempts;
            MaxActiveChunks = v.MaxActiveChunks;
            MaxActiveObstacles = v.MaxActiveObstacles;
            MaxActiveCoins = v.MaxActiveCoins;
            StartChunkId = v.StartChunkId;
            EventBufferCapacity = v.EventBufferCapacity;
        }

        public float GenerateAheadM { get; }

        public float DespawnBehindM { get; }

        public float LeadInM { get; }

        public float LeadOutM { get; }

        public float RowGroupingM { get; }

        public float BreatherIntervalMinS { get; }

        public float BreatherIntervalMaxS { get; }

        public float BreatherMinDurationS { get; }

        public int NoRepeatWindow { get; }

        public float MirrorChance { get; }

        public int MaxPickAttempts { get; }

        public int MaxActiveChunks { get; }

        public int MaxActiveObstacles { get; }

        public int MaxActiveCoins { get; }

        public string StartChunkId { get; }

        public int EventBufferCapacity { get; }

        public static TrackConfig FromDesignValues(TrackDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid track tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new TrackConfig(values);
        }

        public static TrackConfig CreateDefault()
        {
            return FromDesignValues(TrackDesignValues.CreateDefault());
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, GenerateAheadM);
            h = StableHash.Mix(h, DespawnBehindM);
            h = StableHash.Mix(h, LeadInM);
            h = StableHash.Mix(h, LeadOutM);
            h = StableHash.Mix(h, RowGroupingM);
            h = StableHash.Mix(h, BreatherIntervalMinS);
            h = StableHash.Mix(h, BreatherIntervalMaxS);
            h = StableHash.Mix(h, BreatherMinDurationS);
            h = StableHash.Mix(h, NoRepeatWindow);
            h = StableHash.Mix(h, MirrorChance);
            h = StableHash.Mix(h, MaxPickAttempts);
            h = StableHash.Mix(h, MaxActiveChunks);
            h = StableHash.Mix(h, MaxActiveObstacles);
            h = StableHash.Mix(h, MaxActiveCoins);
            h = StableHash.Mix(h, StartChunkId);
            return h;
        }
    }
}
