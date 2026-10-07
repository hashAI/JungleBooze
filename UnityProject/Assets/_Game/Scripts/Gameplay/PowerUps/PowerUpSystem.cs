using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>
    /// Active power-ups of one run (GDD 10). Plain C#, deterministic, driven by the track world's step hooks:
    /// <list type="bullet">
    /// <item>Step 11a (<see cref="OnCoinPickups"/>, before coin pickups): collects pickups HERO touches; Magnet pulls
    /// coins of all 3 lanes within <c>MagnetRadiusM</c> ahead (chunk coins and vine bonus coins, GDD 7.4) and the
    /// Speed Boost dash pulls coins of HERO's lane, so the normal coin pickup collects them.</item>
    /// <item>Step 11b (<see cref="OnScore"/>): timers, then the effects for the next tick (speed multiplier,
    /// invulnerability, gap auto-jump).</item>
    /// <item>Step 11 (<see cref="IRunnerContactHooks"/>): the Shield absorbs a contact that would end the run (the
    /// obstacle shatters, then <c>ShieldInvulnerabilityS</c> of invulnerability); a plain stumble does not use it
    /// [ASSUMED]. Obstacles touched while boosting (or in the shield's invulnerability) are smashed.</item>
    /// </list>
    /// Rules: picking up an active power-up resets its timer (no stacking); different ones stack. Magnet and Shield
    /// end normally in the air, but a Shield that runs out on a vine (or in a vine launch) lasts until
    /// <c>ShieldVineGraceS</c> after landing (GDD 7.4). A Speed Boost never ends in the air: the dash runs on until
    /// HERO lands, then the slowdown (invulnerable) starts and clears the track ahead for the slowdown plus
    /// <c>SpeedBoostClearStretchS</c>. During the boost HERO auto-jumps gaps. A shield never saves from falling.
    /// While a boost is active the generator will not start a vine section (GDD 7.2). No allocation per tick.
    /// </summary>
    public sealed class PowerUpSystem : IRunnerContactHooks
    {
        private const int TypeSlots = 4;

        /// <summary>Invulnerability re-armed every tick while boosting (it counts down once per tick in the runner).</summary>
        private const int BoostInvulnerableTicks = 2;

        /// <summary>Shortest take-off distance before a gap's near edge for the boost auto-jump (m).</summary>
        private const double MinAutoJumpLeadM = 0.3;

        private readonly PowerUpConfig _config;
        private readonly RunnerConfig _runner;
        private readonly int[] _levels = new int[TypeSlots];
        private readonly int[] _ticksLeft = new int[TypeSlots];
        private readonly int[] _fullTicks = new int[TypeSlots];

        private TrackSimulation _track;
        private PowerUpPlacer _placer;
        private bool _magnet;
        private bool _shield;
        private bool _shieldHeld;
        private int _shieldGraceLeft;
        private int _shieldInvulnerableLeft;
        private SpeedBoostPhase _boost;
        private int _slowdownElapsed;
        private double _multiplier;
        private bool _multiplierApplied;

        public PowerUpSystem(PowerUpConfig config, RunnerConfig runner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            for (int i = 0; i < TypeSlots; i++)
            {
                _levels[i] = 1;
            }

            Reset();
        }

        public PowerUpConfig Config => _config;

        /// <summary>The pickups on the track (null until bound to a track).</summary>
        public PowerUpPlacer Placer => _placer;

        /// <summary>Speed Boost phase (None when not boosting).</summary>
        public SpeedBoostPhase BoostPhase => _boost;

        /// <summary>Speed multiplier the runner uses from the next tick (1 without a boost).</summary>
        public double SpeedMultiplier => _multiplier;

        /// <summary>The shield ran out on a vine and is held until shortly after landing (GDD 7.4).</summary>
        public bool ShieldHeld => _shield && _shieldHeld;

        /// <summary>Ticks left of the invulnerability after the shield absorbed a hit.</summary>
        public int ShieldInvulnerableTicksLeft => _shieldInvulnerableLeft;

        /// <summary>Pickups collected this run.</summary>
        public int CollectedCount { get; private set; }

        /// <summary>Hits the shield absorbed this run.</summary>
        public int ShieldHitsAbsorbed { get; private set; }

        /// <summary>Connects the system to the run's track and its pickups (called by the track world at setup).</summary>
        public void Bind(TrackSimulation track)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _placer = track.PowerUpPickups;
            if (_placer != null)
            {
                _placer.BoostDurationS = _config.SpeedBoostSeconds(_levels[(int)PowerUpType.SpeedBoost]);
            }
        }

        /// <summary>Upgrade level 1–5 of <paramref name="type"/> (shop, GDD 13.4). Takes effect on the next pickup.</summary>
        public void SetLevel(PowerUpType type, int level)
        {
            if (type == PowerUpType.None)
            {
                return;
            }

            _levels[(int)type] = PowerUpConfig.ClampLevel(level);
            if (type == PowerUpType.SpeedBoost && _placer != null)
            {
                _placer.BoostDurationS = _config.SpeedBoostSeconds(_levels[(int)type]);
            }
        }

        public int GetLevel(PowerUpType type)
        {
            return type == PowerUpType.None ? 0 : _levels[(int)type];
        }

        /// <summary>All power-ups off (new run).</summary>
        public void Reset()
        {
            for (int i = 0; i < TypeSlots; i++)
            {
                _ticksLeft[i] = 0;
                _fullTicks[i] = 0;
            }

            _magnet = false;
            _shield = false;
            _shieldHeld = false;
            _shieldGraceLeft = 0;
            _shieldInvulnerableLeft = 0;
            _boost = SpeedBoostPhase.None;
            _slowdownElapsed = 0;
            _multiplier = 1.0;
            _multiplierApplied = false;
            CollectedCount = 0;
            ShieldHitsAbsorbed = 0;
        }

        /// <summary>
        /// Shop Shield start (GDD 13.4): the run begins with a Shield active for its normal time at the current level.
        /// Call before the first tick. [ASSUMED] It lasts as long as a picked-up Shield.
        /// </summary>
        public void GrantStartShield()
        {
            int s = (int)PowerUpType.Shield;
            int full = _config.DurationTicks(PowerUpType.Shield, _levels[s]);
            _ticksLeft[s] = full;
            _fullTicks[s] = full;
            _shield = true;
            _shieldHeld = false;
            _shieldGraceLeft = 0;
        }

        /// <summary>
        /// Shop Head Start (GDD 13.4): the run begins with a Speed Boost dash lasting <paramref name="ticks"/> (the
        /// caller works out how long covers the wanted distance). Call before the first tick.
        /// </summary>
        public void GrantStartBoost(int ticks)
        {
            if (ticks <= 0)
            {
                return;
            }

            int b = (int)PowerUpType.SpeedBoost;
            _ticksLeft[b] = ticks;
            _fullTicks[b] = ticks;
            _boost = SpeedBoostPhase.Dash;
            _slowdownElapsed = 0;
            _multiplier = _config.SpeedBoostMultiplier;
            HoldVineSections();
        }

        public bool IsActive(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Magnet:
                    return _magnet;
                case PowerUpType.Shield:
                    return _shield;
                case PowerUpType.SpeedBoost:
                    return _boost != SpeedBoostPhase.None;
                default:
                    return false;
            }
        }

        /// <summary>Ticks left on the timer of <paramref name="type"/> (0 while held after its time ran out).</summary>
        public int TicksLeft(PowerUpType type)
        {
            return type == PowerUpType.None ? 0 : _ticksLeft[(int)type];
        }

        /// <summary>Remaining fraction 0–1 of <paramref name="type"/> for the HUD (Speed Boost: the dash, then the slowdown).</summary>
        public float RemainingFraction(PowerUpType type)
        {
            if (!IsActive(type))
            {
                return 0f;
            }

            if (type == PowerUpType.SpeedBoost && _boost == SpeedBoostPhase.Slowdown)
            {
                int total = _config.SpeedBoostSlowdownTicks;
                return total > 0 ? 1f - Math.Min(1f, _slowdownElapsed / (float)total) : 0f;
            }

            int full = _fullTicks[(int)type];
            return full > 0 ? Math.Min(1f, _ticksLeft[(int)type] / (float)full) : 0f;
        }

        /// <summary>Remaining seconds for the HUD (0 while held).</summary>
        public float RemainingSeconds(PowerUpType type)
        {
            if (!IsActive(type))
            {
                return 0f;
            }

            if (type == PowerUpType.SpeedBoost && _boost == SpeedBoostPhase.Slowdown)
            {
                return (float)((_config.SpeedBoostSlowdownTicks - _slowdownElapsed) * RunnerConfig.TickSeconds);
            }

            return (float)(_ticksLeft[(int)type] * RunnerConfig.TickSeconds);
        }

        /// <summary>True in the warning time before <paramref name="type"/> ends (flash / flicker).</summary>
        public bool IsEnding(PowerUpType type)
        {
            if (!IsActive(type))
            {
                return false;
            }

            if (type == PowerUpType.SpeedBoost)
            {
                return _boost == SpeedBoostPhase.Slowdown;
            }

            if (type == PowerUpType.Shield && _shieldHeld)
            {
                return true;
            }

            return _ticksLeft[(int)type] <= _config.EndWarningTicks;
        }

        // ---- Step hooks ----

        /// <summary>Step 11a, before the coin pickups: pickups, then the coin pull. Not called on the death tick.</summary>
        public void OnCoinPickups(in RunnerTickInfo info, RunnerSimulation runner)
        {
            if (_placer != null)
            {
                CollectPickups(info, runner);
            }

            if (_track == null)
            {
                return;
            }

            if (_magnet)
            {
                PullCoins(info, _config.MagnetRadiusM, false);
            }
            else if (_boost == SpeedBoostPhase.Dash || _boost == SpeedBoostPhase.DashUntilLanding)
            {
                PullCoins(info, _config.SpeedBoostCollectAheadM, true);
            }
        }

        /// <summary>Step 11b: timers, then speed, invulnerability and auto-jump for the next tick.</summary>
        public void OnScore(in RunnerTickInfo info, RunnerSimulation runner)
        {
            if (info.IsDead || runner == null)
            {
                return;
            }

            if (_magnet)
            {
                _ticksLeft[(int)PowerUpType.Magnet]--;
                if (_ticksLeft[(int)PowerUpType.Magnet] <= 0)
                {
                    EndMagnet(info.Tick, runner);
                }
            }

            if (_shield)
            {
                UpdateShield(info, runner);
            }

            if (_shieldInvulnerableLeft > 0)
            {
                _shieldInvulnerableLeft--;
            }

            bool grounded = info.Locomotion == Locomotion.Running || info.Locomotion == Locomotion.Sliding;
            switch (_boost)
            {
                case SpeedBoostPhase.Dash:
                    _ticksLeft[(int)PowerUpType.SpeedBoost]--;
                    if (_ticksLeft[(int)PowerUpType.SpeedBoost] <= 0)
                    {
                        _ticksLeft[(int)PowerUpType.SpeedBoost] = 0;
                        if (grounded)
                        {
                            StartSlowdown(info, runner);
                        }
                        else
                        {
                            _boost = SpeedBoostPhase.DashUntilLanding;
                        }
                    }

                    break;

                case SpeedBoostPhase.DashUntilLanding:
                    if (grounded)
                    {
                        StartSlowdown(info, runner);
                    }

                    break;

                case SpeedBoostPhase.Slowdown:
                    int total = _config.SpeedBoostSlowdownTicks;
                    // GDD 10: a Speed Boost never ends in the air. The slowdown keeps counting, but the last
                    // tick waits until HERO is running or sliding. [ASSUMED] coyote time counts as still in the air.
                    if (_slowdownElapsed + 1 >= total && !grounded)
                    {
                        break;
                    }

                    _slowdownElapsed++;
                    if (_slowdownElapsed >= total)
                    {
                        EndBoost(info.Tick, runner);
                    }
                    else
                    {
                        double t = total > 0 ? _slowdownElapsed / (double)total : 1.0;
                        _multiplier = _config.SpeedBoostMultiplier + (1.0 - _config.SpeedBoostMultiplier) * t;
                    }

                    break;
            }

            HoldVineSections();
            ApplyEffects(info, runner);
        }

        // ---- Contact hooks (step 11) ----

        public bool TryAbsorbLethalContact(RunnerSimulation runner, long tick, in ObstacleBox box)
        {
            if (!_shield)
            {
                return false;
            }

            _shield = false;
            _shieldHeld = false;
            _shieldGraceLeft = 0;
            _ticksLeft[(int)PowerUpType.Shield] = 0;
            _shieldInvulnerableLeft = _config.ShieldInvulnerableTicks;
            ShieldHitsAbsorbed++;
            if (runner.InvulnerableTicks < _config.ShieldInvulnerableTicks)
            {
                runner.SetInvulnerableTicks(_config.ShieldInvulnerableTicks);
            }

            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.ShieldAbsorbed,
                Tick = tick,
                EntityId = box.Id,
                Lane = box.Lane,
                Archetype = (byte)box.Archetype,
            });

            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.PowerUpEnded,
                Tick = tick,
                Value = (short)PowerUpType.Shield,
                Flags = RunnerEventFlags.PowerUpUsedUp,
            });

            Smash(runner, tick, box);
            return true;
        }

        public void OnInvulnerableContact(RunnerSimulation runner, long tick, in ObstacleBox box)
        {
            if (_boost != SpeedBoostPhase.None || _shieldInvulnerableLeft > 0)
            {
                Smash(runner, tick, box);
            }
        }

        public ulong ComputeStateHash(ulong h)
        {
            for (int i = 0; i < TypeSlots; i++)
            {
                h = StableHash.Mix(h, _levels[i]);
                h = StableHash.Mix(h, _ticksLeft[i]);
                h = StableHash.Mix(h, _fullTicks[i]);
            }

            h = StableHash.Mix(h, _magnet);
            h = StableHash.Mix(h, _shield);
            h = StableHash.Mix(h, _shieldHeld);
            h = StableHash.Mix(h, _shieldGraceLeft);
            h = StableHash.Mix(h, _shieldInvulnerableLeft);
            h = StableHash.Mix(h, (int)_boost);
            h = StableHash.Mix(h, _slowdownElapsed);
            h = StableHash.Mix(h, _multiplier);
            h = StableHash.Mix(h, CollectedCount);
            h = StableHash.Mix(h, ShieldHitsAbsorbed);
            return _placer != null ? _placer.ComputeStateHash(h) : h;
        }

        // ---- Internals ----

        private void CollectPickups(in RunnerTickInfo info, RunnerSimulation runner)
        {
            float r = _config.PickupRadiusM;
            float reachX = info.HalfWidth + r;
            double zLo = info.ZPrev - info.HalfDepth - r;
            double zHi = info.Z + info.HalfDepth + r;
            float yLo = info.Y - r;
            float yHi = info.Y + info.HitboxHeight + r;
            double back = info.Z - info.HalfDepth;
            int count = _placer.Count;
            for (int i = 0; i < count; i++)
            {
                ref PowerUpPickup p = ref _placer.PickupAt(i);
                if (p.Z > zHi)
                {
                    break; // sorted by z
                }

                if (p.Resolved)
                {
                    continue;
                }

                if (p.Z >= zLo && Math.Abs(p.X - info.X) <= reachX && p.Y >= yLo && p.Y <= yHi)
                {
                    p.Collected = true;
                    p.Resolved = true;
                    Activate(p.Type, info, runner, p.Id, p.Lane);
                    continue;
                }

                if (back > p.Z + r)
                {
                    p.Resolved = true;
                }
            }
        }

        private void Activate(PowerUpType type, in RunnerTickInfo info, RunnerSimulation runner, int pickupId, byte lane)
        {
            int full = _config.DurationTicks(type, _levels[(int)type]);
            _ticksLeft[(int)type] = full;
            _fullTicks[(int)type] = full;
            CollectedCount++;
            switch (type)
            {
                case PowerUpType.Magnet:
                    _magnet = true;
                    break;
                case PowerUpType.Shield:
                    _shield = true;
                    _shieldHeld = false;
                    _shieldGraceLeft = 0;
                    break;
                case PowerUpType.SpeedBoost:
                    _boost = SpeedBoostPhase.Dash;
                    _slowdownElapsed = 0;
                    _multiplier = _config.SpeedBoostMultiplier;
                    break;
            }

            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.PowerUpCollected,
                Tick = info.Tick,
                EntityId = pickupId,
                Lane = lane,
                Value = (short)type,
            });
        }

        private void UpdateShield(in RunnerTickInfo info, RunnerSimulation runner)
        {
            int s = (int)PowerUpType.Shield;
            if (_ticksLeft[s] > 0)
            {
                _ticksLeft[s]--;
            }

            if (_ticksLeft[s] > 0)
            {
                return;
            }

            bool onVine = info.Locomotion == Locomotion.Carried || info.InVineFlight;
            if (onVine)
            {
                // GDD 7.4: never lose the shield in the air of a swing; it lasts until shortly after landing.
                _shieldHeld = true;
                _shieldGraceLeft = _config.ShieldVineGraceTicks;
                return;
            }

            if (_shieldHeld && _shieldGraceLeft > 0)
            {
                _shieldGraceLeft--;
                if (_shieldGraceLeft > 0)
                {
                    return;
                }
            }

            _shield = false;
            _shieldHeld = false;
            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.PowerUpEnded,
                Tick = info.Tick,
                Value = (short)PowerUpType.Shield,
            });
        }

        private void EndMagnet(long tick, RunnerSimulation runner)
        {
            _magnet = false;
            _ticksLeft[(int)PowerUpType.Magnet] = 0;
            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.PowerUpEnded,
                Tick = tick,
                Value = (short)PowerUpType.Magnet,
            });
        }

        private void StartSlowdown(in RunnerTickInfo info, RunnerSimulation runner)
        {
            _boost = SpeedBoostPhase.Slowdown;
            _slowdownElapsed = 0;
            double m = _config.SpeedBoostMultiplier;
            double baseSpeed = info.Speed / (_multiplier > 0.0 ? _multiplier : 1.0);
            double length = baseSpeed * (_config.SpeedBoostSlowdownS * (1.0 + m) * 0.5 + _config.SpeedBoostClearStretchS);
            if (_track != null && length > 0.0)
            {
                // GDD 10: a guaranteed clear stretch after the slowdown (cleared now, before it comes into view).
                _track.ClearObstacles(info.FrontZ, info.Z + length);
            }

            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.SpeedBoostSlowdown,
                Tick = info.Tick,
                Lane = (byte)info.OccupiedLane,
                Value = (short)PowerUpType.SpeedBoost,
            });
        }

        private void EndBoost(long tick, RunnerSimulation runner)
        {
            _boost = SpeedBoostPhase.None;
            _slowdownElapsed = 0;
            _multiplier = 1.0;
            _ticksLeft[(int)PowerUpType.SpeedBoost] = 0;
            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.PowerUpEnded,
                Tick = tick,
                Value = (short)PowerUpType.SpeedBoost,
            });
        }

        /// <summary>
        /// GDD 7.2 / 10: no vine section starts while a boost is active. The placer blocks the planned reach when
        /// the pickup is placed; this keeps the block past the already-generated track if the boost runs longer
        /// (a jump, or a vine swing already on the track). Called before chunk generation and again after timers.
        /// </summary>
        internal void HoldVineSections()
        {
            if (_boost == SpeedBoostPhase.None || _track == null)
            {
                return;
            }

            double until = _track.GeneratedEndZ + _config.SpeedBoostVineBlockMarginM;
            TrackGenerator generator = _track.Generator;
            if (until > generator.VineBlockedUntilZ)
            {
                generator.VineBlockedUntilZ = until;
            }
        }

        private void ApplyEffects(in RunnerTickInfo info, RunnerSimulation runner)
        {
            if (_boost != SpeedBoostPhase.None)
            {
                runner.SpeedMultiplier = _multiplier;
                _multiplierApplied = true;
                if (runner.InvulnerableTicks < BoostInvulnerableTicks)
                {
                    runner.SetInvulnerableTicks(BoostInvulnerableTicks);
                }

                RequestAutoJumpOverGaps(info, runner);
            }
            else if (_multiplierApplied)
            {
                runner.SpeedMultiplier = 1.0;
                _multiplierApplied = false;
            }
        }

        /// <summary>GDD 10: Pista auto-jumps gaps during a boost (take-off centred so the jump spans the gap).</summary>
        private void RequestAutoJumpOverGaps(in RunnerTickInfo info, RunnerSimulation runner)
        {
            if (info.Locomotion == Locomotion.Coyote)
            {
                runner.RequestAutoJump();
                return;
            }

            if ((info.Locomotion != Locomotion.Running && info.Locomotion != Locomotion.Sliding) || _track == null)
            {
                return;
            }

            if (!_track.TryGetNextGapEdge(info.OccupiedLane, info.Z - info.HalfDepth, out double edge, out float length))
            {
                return;
            }

            double v = info.Speed > 0.0 ? info.Speed : 1.0;
            double air = _runner.JumpAirtimeTicks * RunnerConfig.TickSeconds * v;
            double lead = (air - length) * 0.5 - info.HalfDepth;
            if (lead < MinAutoJumpLeadM)
            {
                lead = MinAutoJumpLeadM;
            }

            double distance = edge - info.FrontZ;
            if (distance <= lead + v * RunnerConfig.TickSeconds)
            {
                runner.RequestAutoJump();
            }
        }

        /// <summary>
        /// Moves uncollected coins up to <paramref name="aheadM"/> ahead of HERO toward his lane and chest height so
        /// they arrive as he reaches them (the coin pickup then collects them). Keeps every coin's z, so the coin
        /// ring stays sorted. <paramref name="laneOnly"/>: only coins already in HERO's lane.
        /// </summary>
        private void PullCoins(in RunnerTickInfo info, float aheadM, bool laneOnly)
        {
            double v = info.Speed > 1.0 ? info.Speed : 1.0;
            double behind = info.HalfDepth + _track.CoinConfig.PickupRadiusM;
            float targetY = info.Y + _config.CoinPullHeightM;
            float laneHalf = _runner.LaneWidthM * 0.5f;
            double zHi = info.Z + aheadM;

            int count = _track.CoinCount;
            for (int i = 0; i < count; i++)
            {
                ref CoinInstance c = ref _track.CoinAt(i);
                if (c.Z > zHi)
                {
                    break;
                }

                PullCoin(ref c, info, v, behind, targetY, laneOnly, laneHalf);
            }

            int bonus = _track.BonusCoinCount;
            for (int i = 0; i < bonus; i++)
            {
                ref CoinInstance c = ref _track.BonusCoinAt(i);
                if (c.Z <= zHi)
                {
                    PullCoin(ref c, info, v, behind, targetY, laneOnly, laneHalf);
                }
            }
        }

        private static void PullCoin(ref CoinInstance c, in RunnerTickInfo info, double speed, double behind, float targetY, bool laneOnly, float laneHalf)
        {
            if (c.Resolved)
            {
                return;
            }

            double dz = c.Z - info.Z;
            if (dz < -behind)
            {
                return;
            }

            if (laneOnly && Math.Abs(c.X - info.X) > laneHalf)
            {
                return;
            }

            double timeToReach = dz / speed;
            float f = timeToReach <= RunnerConfig.TickSeconds ? 1f : (float)(RunnerConfig.TickSeconds / timeToReach);
            c.X += (info.X - c.X) * f;
            c.Y += (targetY - c.Y) * f;
        }

        private void Smash(RunnerSimulation runner, long tick, in ObstacleBox box)
        {
            if (_track == null || !_track.RemoveObstacle(box.Id))
            {
                return;
            }

            runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.ObstacleSmashed,
                Tick = tick,
                EntityId = box.Id,
                Lane = box.Lane,
                Archetype = (byte)box.Archetype,
            });
        }
    }
}
