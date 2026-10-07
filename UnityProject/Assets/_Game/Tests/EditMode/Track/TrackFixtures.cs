using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Tests.EditMode.Track
{
    /// <summary>
    /// Fixture chunks and setups. A fixture run starts with a long custom start chunk (kind Start, never mirrored)
    /// that holds the obstacles and coins under test at known world z (start chunk = world z 0), followed by flat
    /// normal chunks and one flat breather.
    /// </summary>
    internal static class TrackFixtures
    {
        public const string StartId = "FX-START";
        public const string FlatNormalId = "FX-N1";
        public const string BreatherId = "FX-B1";

        public static ChunkData Start(float lengthM, ObstaclePlacement[] obstacles, CoinPattern[] coins = null)
        {
            return new ChunkData(StartId, ChunkKind.Start, lengthM, 1, 6, obstacles, coins ?? new CoinPattern[0], allowMirror: false);
        }

        public static ChunkData Normal(string id, ObstaclePlacement[] obstacles, CoinPattern[] coins = null, float lengthM = 40f, bool allowMirror = true)
        {
            return new ChunkData(id, ChunkKind.Normal, lengthM, 1, 6, obstacles, coins ?? new CoinPattern[0], allowMirror);
        }

        public static ChunkData Breather(string id, float lengthM = 30f)
        {
            return new ChunkData(id, ChunkKind.Breather, lengthM, 1, 6, new ObstaclePlacement[0], new CoinPattern[0]);
        }

        /// <summary>
        /// A setup whose library is <paramref name="start"/>, the given normal chunks (a flat one if none) and a
        /// flat breather; one tier (from 0 m) with weight 1 per normal chunk.
        /// </summary>
        public static TrackRunSetup Setup(
            ChunkData start,
            ObstacleKitConfig kit = null,
            CoinConfig coins = null,
            TrackDesignValues track = null,
            params ChunkData[] normals)
        {
            var chunks = new List<ChunkData> { start };
            if (normals == null || normals.Length == 0)
            {
                normals = new[] { Normal(FlatNormalId, new ObstaclePlacement[0]) };
            }

            chunks.AddRange(normals);
            chunks.Add(Breather(BreatherId));

            var weights = new ChunkWeight[normals.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                weights[i] = new ChunkWeight(normals[i].Id, 1);
            }

            var tiers = new DifficultyTiersDesignValues
            {
                Tiers = new[] { new DifficultyTierDesign(0f, 4f, 0.9f, weights) },
            };

            TrackDesignValues trackValues = track ?? TrackDesignValues.CreateDefault();
            trackValues.StartChunkId = start.Id;
            return new TrackRunSetup(
                TrackConfig.FromDesignValues(trackValues),
                kit ?? ObstacleKitConfig.CreateDefault(),
                coins ?? CoinConfig.CreateDefault(),
                ScoreConfig.CreateDefault(),
                new ChunkLibrary(chunks),
                tiers,
                WorldSkinConfig.CreateJungle());
        }

        /// <summary>A track built from <paramref name="setup"/> and reset with <paramref name="seed"/> (no runner).</summary>
        public static TrackSimulation BuildTrack(TrackRunSetup setup, ulong seed = 1UL, RunnerConfig runner = null, SpeedCurve curve = null)
        {
            RunnerConfig config = runner ?? RunnerConfig.CreateDefault();
            SpeedCurve c = curve ?? SpeedCurve.CreateConstant(10.0);
            var track = new TrackSimulation(setup.Track, setup.Kit, setup.Coins, setup.Library, setup.GetTiers(c), c, config);
            track.Reset(new Pcg32Random(seed).Fork(RandomStreamIds.TrackGeneration));
            return track;
        }

        /// <summary>A tick snapshot as the runner would pass it (standing HERO, spec 001 hitbox).</summary>
        public static RunnerTickInfo Info(float x, float y, double z, double zPrev, float height = 1.8f, long tick = 0)
        {
            return new RunnerTickInfo
            {
                Tick = tick,
                X = x,
                XPrev = x,
                Y = y,
                YPrev = y,
                Z = z,
                ZPrev = zPrev,
                Speed = (z - zPrev) * 60.0,
                HitboxHeight = height,
                HalfWidth = 0.35f,
                HalfDepth = 0.25f,
                OccupiedLane = 1,
            };
        }

        /// <summary>Ring index of the first coin whose z is within 1 mm of <paramref name="z"/>, or -1.</summary>
        public static int FindCoin(TrackSimulation track, double z)
        {
            for (int i = 0; i < track.CoinCount; i++)
            {
                if (System.Math.Abs(track.GetCoin(i).Z - z) < 1e-3)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Ring index of the obstacle with <paramref name="archetype"/> nearest after <paramref name="fromZ"/>, or -1.</summary>
        public static int FindObstacle(TrackSimulation track, ObstacleArchetype archetype, double fromZ = 0.0)
        {
            for (int i = 0; i < track.ObstacleCount; i++)
            {
                ObstacleInstance o = track.GetObstacle(i);
                if (o.Archetype == archetype && o.Z >= fromZ)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
