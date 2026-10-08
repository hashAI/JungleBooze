using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// <c>TrackTuning</c> in designer units (spec 002 section 3.1). <c>TrackConfigAsset</c> copies its serialized
    /// fields into one of these. Field defaults are the spec start values. The chunk library is passed separately
    /// (it is its own asset).
    /// </summary>
    [Serializable]
    public sealed class TrackDesignValues
    {
        public float GenerateAheadM = 150f;
        public float DespawnBehindM = 15f;
        public float LeadInM = 6f;
        public float LeadOutM = 6f;
        public float RowGroupingM = 1f;
        public float BreatherIntervalMinS = 25f;
        public float BreatherIntervalMaxS = 35f;
        public float BreatherMinDurationS = 2f;
        public int NoRepeatWindow = 3;
        public float MirrorChance = 0.5f;
        public int MaxPickAttempts = 8;
        public int MaxActiveChunks = 8;
        public int MaxActiveObstacles = 64;
        public int MaxActiveCoins = 256;

        /// <summary>Id of the first chunk of every run.</summary>
        public string StartChunkId = "S-01";

        /// <summary>Capacity of the track event ring per run [ASSUMED; not in spec 002].</summary>
        public int EventBufferCapacity = 512;

        public static TrackDesignValues CreateDefault()
        {
            return new TrackDesignValues();
        }

        public TrackDesignValues Clone()
        {
            return (TrackDesignValues)MemberwiseClone();
        }

        /// <summary>Range checks of spec 002 section 3.1. Appends one message per problem.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "generateAheadM", GenerateAheadM, 100, 250);
            ConfigChecks.Range(errors, "despawnBehindM", DespawnBehindM, 10, 40);
            ConfigChecks.Range(errors, "leadInM", LeadInM, 4, 10);
            ConfigChecks.Range(errors, "leadOutM", LeadOutM, 4, 10);
            ConfigChecks.Range(errors, "rowGroupingM", RowGroupingM, 0.5, 2);
            ConfigChecks.Range(errors, "breatherIntervalMinS", BreatherIntervalMinS, 10, 60);
            if (!(BreatherIntervalMaxS >= BreatherIntervalMinS))
            {
                errors.Add("breatherIntervalMaxS must be ≥ breatherIntervalMinS.");
            }

            ConfigChecks.Range(errors, "breatherMinDurationS", BreatherMinDurationS, 1, 4);
            ConfigChecks.Range(errors, "noRepeatWindow", NoRepeatWindow, 0, 6);
            ConfigChecks.Range(errors, "mirrorChance", MirrorChance, 0, 1);
            ConfigChecks.Range(errors, "maxPickAttempts", MaxPickAttempts, 1, 32);
            ConfigChecks.Range(errors, "maxActiveChunks", MaxActiveChunks, 4, 16);
            ConfigChecks.Range(errors, "maxActiveObstacles", MaxActiveObstacles, 32, 256);
            ConfigChecks.Range(errors, "maxActiveCoins", MaxActiveCoins, 64, 1024);
            ConfigChecks.Range(errors, "eventBufferCapacity", EventBufferCapacity, 64, 4096);
            if (string.IsNullOrEmpty(StartChunkId))
            {
                errors.Add("startChunk must be set.");
            }

            return errors.Count == before;
        }
    }
}
