using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Coins, coin streak and score of one run (spec 002 sections 10.4 to 10.6), run at steps 11a and 11b.
    /// <list type="bullet">
    /// <item>Pickup (11a, after collisions, never on the death tick): a coin is collected on the first tick its
    /// centre lies inside HERO's box grown by <c>pickupRadiusM</c> on every axis; along z the box is swept from
    /// the start of the tick (back face) to the end (front face), so no coin is skipped at any speed.</item>
    /// <item>Streak: consecutive collected coins. A <c>Stumbled</c> or a missed coin (HERO's back face passed
    /// <c>coin.z + pickupRadiusM</c> while the coin was uncollected and within half a lane width of HERO's x)
    /// resets it; reaching <c>streakLength</c> pays <c>coinStreakBonus</c> and resets it.</item>
    /// <item>Score (11b, also on the death tick): distance from HERO's z plus near-miss, streak and vine release
    /// bonuses (GDD 7.5: Good 150 / Perfect 400 / Auto 50 × the chain multiplier).</item>
    /// <item>Vine bonus coins (coin shower and Perfect ring) are picked up like other coins and count in the
    /// streak, but a bonus coin that is not collected never breaks the streak [ASSUMED].</item>
    /// </list>
    /// No allocation per tick.
    /// </summary>
    public sealed class RunScoring
    {
        /// <summary>Tolerance for the pickup bounds (float positions against exact thresholds, AC-223).</summary>
        private const float PickupToleranceM = 1e-4f;

        private readonly CoinConfig _coins;
        private readonly ScoreConfig _score;
        private readonly VineConfig _vines;
        private readonly float _missHalfWidth;

        private RunTotals _totals;
        private DeathInfo _death;
        private int _pendingStreakBonuses;
        private int _multiplierOverride;

        public RunScoring(CoinConfig coins, ScoreConfig score, RunnerConfig runner, VineConfig vines = null)
        {
            _vines = vines ?? VineConfig.CreateDefault();
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _score = score ?? throw new ArgumentNullException(nameof(score));
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            // Spec 10.5: "in HERO's lane" = within half the lane width (1.2 m), derived.
            _missHalfWidth = runner.LaneWidthM * 0.5f;
            Reset();
        }

        public CoinConfig CoinConfig => _coins;

        public ScoreConfig ScoreConfig => _score;

        /// <summary>
        /// The score multiplier in use: the player's (GDD 13.1, set by <c>TrackRunWorld.ApplyLoadout</c>), or
        /// the config's when none was set. Stays across <see cref="Reset"/>.
        /// </summary>
        public int ScoreMultiplier
        {
            get => _multiplierOverride > 0 ? _multiplierOverride : _score.ScoreMultiplier;
            set => _multiplierOverride = value > 0 ? value : 0;
        }

        /// <summary>Distance, coins, score, bonus, streak, tier, current chunk (spec 13.2 <c>RunTotals</c>).</summary>
        public RunTotals Totals => _totals;

        /// <summary>How the run ended; <see cref="DeathInfo.HasDied"/> is false while HERO is alive.</summary>
        public DeathInfo Death => _death;

        /// <summary>Back to a fresh run: all totals 0, no death.</summary>
        public void Reset()
        {
            _totals = new RunTotals { Tier = 1, CurrentChunkIndex = -1 };
            _death = new DeathInfo { ChunkIndex = -1 };
            _pendingStreakBonuses = 0;
        }

        /// <summary>Step 11a: coin pickups, missed coins and the streak. The caller skips it on the death tick.</summary>
        public void OnCoinPickups(in RunnerTickInfo info, TrackSimulation track, RunnerSimulation runner)
        {
            if (info.StumbledThisTick)
            {
                _totals.Streak = 0;
            }

            float r = _coins.PickupRadiusM;
            float reachX = info.HalfWidth + r + PickupToleranceM;
            double zLo = info.ZPrev - info.HalfDepth - r;
            double zHi = info.Z + info.HalfDepth + r;
            float yLo = info.Y - r - PickupToleranceM;
            float yHi = info.Y + info.HitboxHeight + r + PickupToleranceM;
            double back = info.Z - info.HalfDepth;

            int count = track.CoinCount;
            for (int i = 0; i < count; i++)
            {
                ref CoinInstance c = ref track.CoinAt(i);
                if (c.Z > zHi)
                {
                    break; // the ring is sorted by z
                }

                if (c.Resolved)
                {
                    continue;
                }

                float dx = Math.Abs(c.X - info.X);
                if (c.Z >= zLo && dx <= reachX && c.Y >= yLo && c.Y <= yHi)
                {
                    Collect(ref c, info.Tick, runner);
                    continue;
                }

                if (back > c.Z + r)
                {
                    c.Resolved = true;
                    if (dx <= _missHalfWidth)
                    {
                        _totals.Streak = 0;
                        _totals.CoinsMissed++;
                    }
                }
            }

            int bonus = track.BonusCoinCount;
            for (int i = 0; i < bonus; i++)
            {
                ref CoinInstance c = ref track.BonusCoinAt(i);
                if (c.Resolved)
                {
                    continue;
                }

                if (c.Z >= zLo && c.Z <= zHi && Math.Abs(c.X - info.X) <= reachX && c.Y >= yLo && c.Y <= yHi)
                {
                    Collect(ref c, info.Tick, runner);
                    continue;
                }

                if (back > c.Z + r)
                {
                    c.Resolved = true;
                }
            }
        }

        /// <summary>Step 11b: distance, bonuses of this tick (near-misses, completed streaks), score, death info.</summary>
        public void OnScore(in RunnerTickInfo info, TrackSimulation track, RunnerSimulation runner)
        {
            for (int n = 0; n < info.NearMissesThisTick; n++)
            {
                _totals.NearMisses++;
                _totals.BonusScore += _score.NearMissBonus;
                Emit(runner, new RunnerEvent
                {
                    Type = RunnerEventType.ScoreBonus,
                    Tick = info.Tick,
                    Value = ClampShort(_score.NearMissBonus),
                    Flags = RunnerEventFlags.BonusNearMiss,
                });
            }

            for (int n = 0; n < _pendingStreakBonuses; n++)
            {
                _totals.BonusScore += _score.CoinStreakBonus;
                Emit(runner, new RunnerEvent
                {
                    Type = RunnerEventType.ScoreBonus,
                    Tick = info.Tick,
                    Value = ClampShort(_score.CoinStreakBonus),
                    Flags = RunnerEventFlags.BonusStreak,
                });
            }

            _pendingStreakBonuses = 0;

            if (info.VineGrabbedThisTick)
            {
                _totals.VinesGrabbed++;
            }

            if (info.VineRelease != VineReleaseGrade.None)
            {
                _totals.VineReleases++;
                if (info.VineRelease == VineReleaseGrade.Perfect)
                {
                    _totals.PerfectReleases++;
                }

                int points = (int)Math.Round(_vines.ScoreFor(info.VineRelease) * (double)info.VineBonusMultiplier);
                _totals.BonusScore += points;
                Emit(runner, new RunnerEvent
                {
                    Type = RunnerEventType.ScoreBonus,
                    Tick = info.Tick,
                    Value = ClampShort(points),
                    Flags = RunnerEventFlags.BonusVine,
                    Archetype = (byte)info.VineRelease,
                });
            }

            _totals.DistanceM = info.Z > 0.0 ? info.Z : 0.0;
            _totals.Score = _score.ComputeScore(_totals.DistanceM, _totals.BonusScore, ScoreMultiplier);
            if (track != null)
            {
                _totals.Tier = track.CurrentTier;
                _totals.CurrentChunkIndex = track.CurrentChunkIndex;
            }

            if (info.IsDead && !_death.HasDied && runner != null)
            {
                RecordDeath(info, track, runner);
            }
        }

        /// <summary>Continue (GDD 14.4): HERO is alive again, so the next death is recorded afresh.</summary>
        public void ClearDeath()
        {
            _death = new DeathInfo { ChunkIndex = -1 };
        }

        /// <summary>
        /// Lift coin pull (GDD 15.1): collects every uncollected coin (chunk and vine bonus coins) whose z lies from
        /// HERO's back face to <paramref name="aheadM"/> ahead of his centre, in every lane and at any height.
        /// Called at step 11a while lifted, after the normal pickups. No allocation.
        /// </summary>
        public void CollectAhead(in RunnerTickInfo info, TrackSimulation track, RunnerSimulation runner, double aheadM)
        {
            double zLo = info.Z - info.HalfDepth;
            double zHi = info.Z + aheadM;
            int count = track.CoinCount;
            for (int i = 0; i < count; i++)
            {
                ref CoinInstance c = ref track.CoinAt(i);
                if (c.Z > zHi)
                {
                    break; // the ring is sorted by z
                }

                if (!c.Resolved && c.Z >= zLo)
                {
                    Collect(ref c, info.Tick, runner);
                }
            }

            int bonus = track.BonusCoinCount;
            for (int i = 0; i < bonus; i++)
            {
                ref CoinInstance c = ref track.BonusCoinAt(i);
                if (!c.Resolved && c.Z >= zLo && c.Z <= zHi)
                {
                    Collect(ref c, info.Tick, runner);
                }
            }
        }

        /// <summary>Stable hash of the scoring state (AC-242, AC-247).</summary>
        public ulong ComputeStateHash(ulong h)
        {
            h = StableHash.Mix(h, _totals.DistanceM);
            h = StableHash.Mix(h, _totals.Coins);
            h = StableHash.Mix(h, _totals.Score);
            h = StableHash.Mix(h, _totals.BonusScore);
            h = StableHash.Mix(h, _totals.Streak);
            h = StableHash.Mix(h, _totals.StreaksCompleted);
            h = StableHash.Mix(h, _totals.NearMisses);
            h = StableHash.Mix(h, _totals.CoinsMissed);
            h = StableHash.Mix(h, _totals.Tier);
            h = StableHash.Mix(h, _totals.CurrentChunkIndex);
            h = StableHash.Mix(h, _totals.VinesGrabbed);
            h = StableHash.Mix(h, _totals.VineReleases);
            h = StableHash.Mix(h, _totals.PerfectReleases);
            h = StableHash.Mix(h, _pendingStreakBonuses);
            h = StableHash.Mix(h, _death.HasDied);
            h = StableHash.Mix(h, (int)_death.Cause);
            h = StableHash.Mix(h, _death.ObstacleId);
            return h;
        }

        /// <summary>Test hook: sets the totals as if the run had reached them (score formula checks).</summary>
        internal void DebugSetTotals(RunTotals totals)
        {
            _totals = totals;
        }

        private void Collect(ref CoinInstance c, long tick, RunnerSimulation runner)
        {
            c.Collected = true;
            c.Resolved = true;
            _totals.Coins += _coins.CoinValue;
            _totals.Streak++;
            Emit(runner, new RunnerEvent
            {
                Type = RunnerEventType.CoinCollected,
                Tick = tick,
                EntityId = c.Id,
                Lane = c.Lane,
                Value = (short)_coins.CoinValue,
            });

            if (_totals.Streak >= _coins.StreakLength)
            {
                Emit(runner, new RunnerEvent
                {
                    Type = RunnerEventType.CoinStreak,
                    Tick = tick,
                    Value = ClampShort(_totals.Streak),
                });

                _totals.Streak = 0;
                _totals.StreaksCompleted++;
                _pendingStreakBonuses++;
            }
        }

        private void RecordDeath(in RunnerTickInfo info, TrackSimulation track, RunnerSimulation runner)
        {
            _death.HasDied = true;
            _death.Cause = runner.DeathCause;
            _death.Archetype = runner.DeathArchetype;
            _death.ObstacleId = runner.DeathEntityId;
            _death.AfterStumble = runner.DeathAfterStumble;
            _death.DistanceM = _totals.DistanceM;
            _death.Tick = info.Tick;
            _death.ChunkIndex = -1;
            _death.ChunkId = null;
            if (track != null)
            {
                int ring = track.FindChunkAt(info.Z);
                if (ring >= 0)
                {
                    int index = track.GetChunk(ring).ChunkIndex;
                    _death.ChunkIndex = index;
                    _death.ChunkId = track.Library[index].Id;
                }
            }
        }

        private static short ClampShort(int value)
        {
            return value > short.MaxValue ? short.MaxValue : (short)value;
        }

        private static void Emit(RunnerSimulation runner, in RunnerEvent e)
        {
            if (runner != null)
            {
                runner.EmitExternal(e);
            }
        }
    }
}
