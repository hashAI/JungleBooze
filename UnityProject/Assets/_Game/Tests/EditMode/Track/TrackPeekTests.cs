using System.Collections.Generic;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Path;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Track
{
    /// <summary>Spec 003 T2: AC-313 (committed ahead), <c>PeekChunk</c>, and the guarantee that a longer look-ahead does not change which chunks are generated.</summary>
    public sealed class TrackPeekTests
    {
        [Test]
        public void AC313_DefaultLookAheadCoversTheRouteRequirement()
        {
            Assert.GreaterOrEqual(TrackConfig.CreateDefault().GenerateAheadM, 175f);
        }

        [Test]
        public void AC313_TrackKeepsAtLeast175MetresCommittedAheadOnEveryTick()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                TrackSimulation track = RouteTrackSimulator.CreateTrack(seed, null);
                Assert.GreaterOrEqual(track.CommittedAheadM(0.0), 175.0, "after Reset, seed " + seed);
                double previous = 0.0;
                double minAhead = double.MaxValue;
                for (double z = 0.0; z <= 6000.0; z += 0.4)
                {
                    track.Update(TrackFixtures.Info(0f, 0f, z, previous), null);
                    previous = z;
                    double ahead = track.CommittedAheadM(z);
                    if (ahead < minAhead)
                    {
                        minAhead = ahead;
                    }
                }

                Assert.GreaterOrEqual(minAhead, 175.0, "seed " + seed);
                Assert.AreEqual(0, track.ChunkOverflowCount, "chunk ring overflow, seed " + seed);
                Assert.AreEqual(0, track.CoinOverflowCount, "coin ring overflow, seed " + seed);
                Assert.AreEqual(0, track.ObstacleOverflowCount, "obstacle ring overflow, seed " + seed);
            }
        }

        [Test]
        public void PeekChunkIsReadOnlyAndMatchesTheRing()
        {
            TrackSimulation track = RouteTrackSimulator.CreateTrack(3UL, null);
            int generatedBefore = track.GeneratedChunkCount;

            ChunkPeek first = track.PeekChunk(1);
            Assert.IsTrue(first.IsValid);
            Assert.AreEqual(1, first.Serial);
            Assert.AreEqual(ChunkKind.Start, first.Kind);
            Assert.AreEqual(0.0, first.StartZ);
            Assert.Greater(first.LengthM, 0f);

            Assert.IsFalse(track.PeekChunk(0).IsValid);
            Assert.IsFalse(track.PeekChunk(generatedBefore + 1).IsValid, "never generates");
            Assert.AreEqual(generatedBefore, track.GeneratedChunkCount, "peeking must not generate");

            for (int i = 0; i < track.ChunkCount; i++)
            {
                ChunkInstance chunk = track.GetChunk(i);
                ChunkPeek peek = track.PeekChunk(chunk.Serial);
                Assert.IsTrue(peek.IsValid);
                Assert.AreEqual(chunk.Kind, peek.Kind);
                Assert.AreEqual(chunk.StartZ, peek.StartZ);
                Assert.AreEqual(chunk.LengthM, peek.LengthM);
                Assert.AreEqual(chunk.EndZ, peek.EndZ);
            }

            Assert.AreEqual(1, track.OldestChunkSerial);
            double previous = 0.0;
            for (double z = 0.0; z <= 800.0; z += 5.0)
            {
                track.Update(TrackFixtures.Info(0f, 0f, z, previous), null);
                previous = z;
            }

            Assert.IsFalse(track.PeekChunk(1).IsValid, "the start chunk has been despawned");
            Assert.Greater(track.OldestChunkSerial, 1);
        }

        [Test]
        public void ALongerLookAheadDoesNotChangeTheGeneratedChunks()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                List<string> shortAhead = RecordChunks(seed, 150f);
                List<string> longAhead = RecordChunks(seed, 190f);
                int common = System.Math.Min(shortAhead.Count, longAhead.Count);
                Assert.Greater(common, 60, "seed " + seed);
                for (int i = 0; i < common; i++)
                {
                    Assert.AreEqual(shortAhead[i], longAhead[i], "seed " + seed + " chunk " + i);
                }
            }
        }

        [Test]
        public void TheSameSeedGeneratesTheSameChunksTwice()
        {
            List<string> a = RecordChunks(5UL, 190f);
            List<string> b = RecordChunks(5UL, 190f);
            CollectionAssert.AreEqual(a, b);
        }

        /// <summary>Walks a hero along the track and records every chunk (library index, mirror, start, length, ids) when it first appears.</summary>
        private static List<string> RecordChunks(ulong seed, float generateAheadM)
        {
            TrackDesignValues values = TrackDesignValues.CreateDefault();
            values.GenerateAheadM = generateAheadM;
            TrackSimulation track = RouteTrackSimulator.CreateTrack(seed, TrackConfig.FromDesignValues(values));
            var recorded = new List<string>();
            int lastSerial = 0;
            double previous = 0.0;
            for (double z = 0.0; z <= 4000.0; z += 5.0)
            {
                track.Update(TrackFixtures.Info(0f, 0f, z, previous), null);
                previous = z;
                for (int i = 0; i < track.ChunkCount; i++)
                {
                    ChunkInstance chunk = track.GetChunk(i);
                    if (chunk.Serial > lastSerial)
                    {
                        lastSerial = chunk.Serial;
                        recorded.Add(chunk.Serial + ":" + chunk.ChunkIndex + ":" + chunk.Mirrored + ":" + chunk.StartZ + ":" + chunk.LengthM
                            + ":" + chunk.FirstObstacleId + ":" + chunk.ObstacleCount + ":" + chunk.CoinCount);
                    }
                }
            }

            return recorded;
        }
    }
}
