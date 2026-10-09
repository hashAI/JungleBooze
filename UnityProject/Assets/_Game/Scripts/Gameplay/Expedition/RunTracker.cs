using System;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Everything per tick that is not movement (spec 102–103), run after each simulation step: crystals and the
    /// Shield (pads of spec 103 §9.1), discovery triggers (first time → reward, else a sighting), route choice at
    /// every divider front, the Clean Line bonus at the merge, chunk entry, hits for the director's mercy rule, and
    /// the death report. Part B: Perfect release and Perfect Span coins, traversal counters (DDA), creature
    /// observations from <see cref="SailbackSystem"/> (discovery or sighting), Deep-Breath-only triggers, the vista
    /// beat and water-curtain passes. Appends world events to the run's event buffer. Allocation-free per tick.
    /// </summary>
    public sealed class RunTracker : ICreatureDiscoverySink
    {
        private readonly WorldPath _path;
        private readonly RunnerSimulation _sim;
        private readonly WorldDirector _director;
        private readonly ExpeditionContent _content;
        private readonly RunEventBuffer _events;
        private readonly int[] _crystalTaken = new int[WorldPath.CrystalCapacity];
        private readonly int[] _powerUpTaken = new int[WorldPath.PowerUpCapacity];
        private readonly bool[] _knownBefore;
        private readonly bool[] _foundThisRun;
        private readonly int _shieldTicks;

        private AbilityFlags _owned;
        private int _currentChunk;
        private bool _cleanLineActive;
        private float _cleanLineMergeS;
        private int _cleanLineCoins;
        private int _cleanLineHits;
        private int _cleanLineChunk;
        private int _spanChunk;
        private int _spanPerfects;

        public RunTracker(WorldPath path, RunnerSimulation sim, WorldDirector director, ExpeditionContent content, RunEventBuffer events)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            _director = director ?? throw new ArgumentNullException(nameof(director));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _knownBefore = new bool[Math.Max(1, content.Discoveries.Count)];
            _foundThisRun = new bool[Math.Max(1, content.Discoveries.Count)];
            _shieldTicks = Math.Max(1, (int)Math.Round(content.Pickups.ShieldDuration / sim.StepSeconds));
        }

        public RunStats Stats { get; } = new RunStats();

        /// <summary>When true, no events are written (bot probes never use the tracker, but tools may).</summary>
        public bool MuteEvents { get; set; }

        /// <summary>
        /// Starts a run. <paramref name="discovered"/> tells which entries the profile already has (index → bool), so
        /// a second encounter is a sighting, not a reward.
        /// </summary>
        public void BeginRun(AbilityFlags owned, Func<string, bool> discovered)
        {
            _owned = owned;
            Stats.Reset();
            Array.Clear(_crystalTaken, 0, _crystalTaken.Length);
            Array.Clear(_powerUpTaken, 0, _powerUpTaken.Length);
            for (int i = 0; i < _knownBefore.Length; i++)
            {
                _knownBefore[i] = i < _content.Discoveries.Count && discovered != null && discovered(_content.Discoveries[i].Id);
                _foundThisRun[i] = false;
            }

            _currentChunk = -1;
            _cleanLineActive = false;
            _spanChunk = -1;
            _spanPerfects = 0;
        }

        public bool IsCrystalCollected(int id)
        {
            return id >= 0 && _crystalTaken[id % _crystalTaken.Length] == id + 1;
        }

        public bool IsPowerUpCollected(int id)
        {
            return id >= 0 && _powerUpTaken[id % _powerUpTaken.Length] == id + 1;
        }

        /// <summary>True if the entry was found in this run or before it.</summary>
        public bool IsDiscovered(int entryIndex)
        {
            return entryIndex >= 0 && entryIndex < _knownBefore.Length && (_knownBefore[entryIndex] || _foundThisRun[entryIndex]);
        }

        /// <summary>Call once after every simulation step with the state before and after it.</summary>
        public void AfterStep(in RunnerState before, in RunnerState now)
        {
            if (now.Tick == before.Tick)
            {
                return;
            }

            Stats.Ticks = now.Tick;
            Stats.Distance = now.Distance;
            Stats.Coins = now.Coins;

            if (now.Hits > before.Hits)
            {
                for (int h = before.Hits; h < now.Hits; h++)
                {
                    _director.OnHit(now.Tick);
                }

                Stats.Hits = now.Hits;
            }

            Stats.TraversalAttempts = now.TraversalAttempts;
            Stats.TraversalSuccesses = now.TraversalSuccesses;
            bool aliveBefore = !before.Dead;
            if (aliveBefore)
            {
                StepChunkEntry(now);
                StepRoutes(before, now);
                StepPickups(now);
                StepDiscoveries(before, now);
                StepCurtains(before, now);
                StepCleanLine(now);
                StepReleases(before, now);
            }

            if (now.Dead && !before.Dead)
            {
                Stats.Dead = true;
                Stats.Cause = now.Cause;
                Stats.DeathLabel = now.DeathObstacle >= 0 ? _path.GetObstacleLabel(now.DeathObstacle) : now.Cause.ToString();
                Stats.DeathChunk = Stats.CurrentChunk;
            }
        }

        private void StepChunkEntry(in RunnerState now)
        {
            int serial = _path.ChunkAt(now.S);
            if (serial == _currentChunk || serial < 0)
            {
                return;
            }

            _currentChunk = serial;
            Stats.ChunksEntered++;
            ChunkRuntime chunk = _path.Chunk(serial).Chunk;
            Stats.CurrentChunk = chunk.Id;
            Emit(RunEventType.ChunkEntered, serial, 0, 0f, now.Tick);
        }

        private void StepRoutes(in RunnerState before, in RunnerState now)
        {
            for (int i = 0; i < _path.ForkCount; i++)
            {
                ForkPoint fork = _path.GetFork(i);
                if (!(before.S < fork.SFront && now.S >= fork.SFront))
                {
                    continue;
                }

                if (!_path.TryGetForkChunk(fork.Id, out int serial, out _))
                {
                    continue;
                }

                ref readonly PlacedChunk placed = ref _path.Chunk(serial);
                float local = fork.SFront - placed.StartS + 0.01f;
                int routeIndex = placed.Chunk.RouteIndexAt(local, now.X);
                RouteType route = routeIndex >= 0 ? placed.Chunk.GetRoute(routeIndex).Type : RouteType.Main;
                Stats.CountRoute(route);
                Emit(RunEventType.RouteChosen, serial, (byte)route, 0f, now.Tick);
                if (route == RouteType.Risky && !_cleanLineActive && placed.Chunk.TryGetRouteSpan(routeIndex, out _, out float merge))
                {
                    _cleanLineActive = true;
                    _cleanLineMergeS = placed.StartS + merge;
                    _cleanLineCoins = now.Coins;
                    _cleanLineHits = now.Hits;
                    _cleanLineChunk = serial;
                }
            }
        }

        private void StepCleanLine(in RunnerState now)
        {
            if (!_cleanLineActive || now.S < _cleanLineMergeS)
            {
                return;
            }

            _cleanLineActive = false;
            if (now.Hits != _cleanLineHits)
            {
                return;
            }

            int bonus = (int)Math.Floor((now.Coins - _cleanLineCoins) * _content.Pickups.CleanLineBonus);
            Stats.CleanLines++;
            Stats.CleanLineCoins += bonus;
            Emit(RunEventType.CleanLine, _cleanLineChunk, 0, bonus, now.Tick);
        }

        /// <summary>Perfect release coins and the Perfect Span bonus (spec 103 §5.4, §9.1).</summary>
        private void StepReleases(in RunnerState before, in RunnerState now)
        {
            if (now.Releases == before.Releases)
            {
                return;
            }

            int chunk = now.VineId >= 0 ? now.VineId / ChunkRuntime.LocalIdStride : -1;
            if (chunk != _spanChunk)
            {
                _spanChunk = chunk;
                _spanPerfects = 0;
            }

            Stats.VineReleases++;
            if (now.Perfects == before.Perfects)
            {
                return;
            }

            Stats.PerfectReleases++;
            int coins = _content.Pickups.PerfectReleaseCoins;
            Stats.PerfectCoins += coins;
            Emit(RunEventType.PerfectRelease, now.VineId, 0, coins, now.Tick);
            _spanPerfects++;
            if (_spanPerfects == 2)
            {
                int bonus = _content.Pickups.PerfectSpanBonusCoins;
                Stats.PerfectCoins += bonus;
                Stats.PerfectSpans++;
                Emit(RunEventType.PerfectSpan, chunk, 0, bonus, now.Tick);
            }
        }

        private void StepCurtains(in RunnerState before, in RunnerState now)
        {
            int serial = _path.ChunkAt(now.S);
            if (serial < 0)
            {
                return;
            }

            ref readonly PlacedChunk p = ref _path.Chunk(serial);
            ChunkRuntime c = p.Chunk;
            for (int i = 0; i < c.TraversalCount; i++)
            {
                TraversalZone z = c.GetTraversal(i);
                float s = z.SMin + p.StartS;
                if (z.Mode == TraversalMode.Curtain && before.S < s && now.S >= s && now.X >= z.XMin && now.X <= z.XMax)
                {
                    Emit(RunEventType.CurtainPass, serial, 0, 0f, now.Tick);
                }
            }
        }

        /// <summary>From <see cref="SailbackSystem"/>: a spawn group was observed long enough (spec 103 §7.3).</summary>
        public void OnCreatureObserved(string entryId, int chunkSerial, long tick)
        {
            int entry = _content.FindDiscovery(entryId);
            if (entry >= 0)
            {
                Discover(entry, tick);
            }
        }

        private void Discover(int entry, long tick)
        {
            if (entry < 32)
            {
                Stats.MissedDiscoveries &= ~(1 << entry);
            }

            if (IsDiscovered(entry))
            {
                Stats.Sightings++;
                Emit(RunEventType.Discovery, entry, 0, 0f, tick);
                return;
            }

            _foundThisRun[entry] = true;
            DiscoveryEntry e = _content.Discoveries[entry];
            Stats.AddNewDiscovery(entry);
            Stats.DiscoveryCoins += e.RewardCoins;
            Stats.DiscoveryCrystals += e.RewardCrystals;
            Emit(RunEventType.Discovery, entry, 1, 0f, tick);
        }

        private void StepPickups(in RunnerState now)
        {
            _sim.GetHitbox(out float hs0, out float hs1, out float hx0, out float hx1, out float hy0, out float hy1);
            float halfDepth = (hs1 - hs0) * 0.5f;
            float halfWidth = (hx1 - hx0) * 0.5f;
            float height = hy1 - hy0;
            float centreS = (hs0 + hs1) * 0.5f;
            float centreX = (hx0 + hx1) * 0.5f;

            float pad = _content.Pickups.CrystalPad;
            for (int id = _path.FirstCrystalId; id < _path.NextCrystalId; id++)
            {
                if (IsCrystalCollected(id))
                {
                    continue;
                }

                CoinPoint c = _path.GetCrystal(id);
                if (Overlaps(centreS, centreX, hy0, c.S, c.X, c.Y, halfDepth + pad, halfWidth + pad, height, pad))
                {
                    _crystalTaken[id % _crystalTaken.Length] = id + 1;
                    Stats.Crystals++;
                    Emit(RunEventType.Crystal, id, 0, 0f, now.Tick);
                }
            }

            pad = _content.Pickups.PowerUpPad;
            for (int id = _path.FirstPowerUpId; id < _path.NextPowerUpId; id++)
            {
                if (IsPowerUpCollected(id))
                {
                    continue;
                }

                PowerUpPoint p = _path.GetPowerUp(id);
                if (!Overlaps(centreS, centreX, hy0, p.S, p.X, p.Y, halfDepth + pad, halfWidth + pad, height, pad))
                {
                    continue;
                }

                _powerUpTaken[id % _powerUpTaken.Length] = id + 1;
                Stats.PowerUps++;
                if (p.Kind == PowerUpKind.Shield)
                {
                    _sim.GrantShield(_shieldTicks);
                }

                Emit(RunEventType.PowerUp, id, (byte)p.Kind, 0f, now.Tick);
            }
        }

        private void StepDiscoveries(in RunnerState before, in RunnerState now)
        {
            for (int id = _path.FirstDiscoveryId; id < _path.NextDiscoveryId; id++)
            {
                DiscoveryPoint d = _path.GetDiscovery(id);
                if (!(before.S < d.S && now.S >= d.S))
                {
                    continue;
                }

                if (!_path.IsLive(d.ChunkSerial))
                {
                    continue;
                }

                DiscoveryTrigger trigger = _path.Chunk(d.ChunkSerial).Chunk.GetDiscovery(d.LocalIndex);
                if ((trigger.RequiredAbility & ~_owned) != 0)
                {
                    continue;
                }

                int entry = _content.FindDiscovery(trigger.EntryId);
                if (entry < 0)
                {
                    continue;
                }

                bool deepOnly = trigger.RequireDeepDive && now.Mode != MoveMode.DeepDive && before.Mode != MoveMode.DeepDive;
                if (now.X < d.XMin || now.X > d.XMax || deepOnly)
                {
                    if (!IsDiscovered(entry) && entry < 32)
                    {
                        Stats.MissedDiscoveries |= 1 << entry;
                    }

                    continue;
                }

                if (trigger.Vista)
                {
                    Emit(RunEventType.Vista, entry, 0, 0f, now.Tick);
                }

                Discover(entry, now.Tick);
            }
        }

        private static bool Overlaps(float rs, float rx, float ry0, float s, float x, float y, float halfDepth, float halfWidth, float height, float padY)
        {
            return Math.Abs(s - rs) <= halfDepth && Math.Abs(x - rx) <= halfWidth && y >= ry0 - padY && y <= ry0 + height + padY;
        }

        private void Emit(RunEventType type, int id, byte reason, float value, long tick)
        {
            if (!MuteEvents)
            {
                _events.Add(new RunEvent(type, tick, id, reason, value));
            }
        }
    }
}
