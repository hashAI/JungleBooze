using System;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Everything per tick that is not movement (spec 102–103), run after each simulation step: crystals and the
    /// Shield (pads of spec 103 §9.1), discovery triggers (first time → reward, else a sighting), route choice at
    /// every divider front, the Clean Line bonus at the merge, chunk entry, hits for the director's mercy rule, and
    /// the death report. Appends world events to the run's event buffer. Allocation-free per tick.
    /// </summary>
    public sealed class RunTracker
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

            bool aliveBefore = !before.Dead;
            if (aliveBefore)
            {
                StepChunkEntry(now);
                StepRoutes(before, now);
                StepPickups(now);
                StepDiscoveries(before, now);
                StepCleanLine(now);
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

        private void StepPickups(in RunnerState now)
        {
            HitboxConfig hb = _sim.Config.Hitbox;
            float halfDepth = _sim.HitboxDepth * 0.5f;
            float height = _sim.HitboxHeight;
            float halfWidth = hb.Width * 0.5f;

            float pad = _content.Pickups.CrystalPad;
            for (int id = _path.FirstCrystalId; id < _path.NextCrystalId; id++)
            {
                if (IsCrystalCollected(id))
                {
                    continue;
                }

                CoinPoint c = _path.GetCrystal(id);
                if (Overlaps(now, c.S, c.X, c.Y, halfDepth + pad, halfWidth + pad, height, pad))
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
                if (!Overlaps(now, p.S, p.X, p.Y, halfDepth + pad, halfWidth + pad, height, pad))
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

                if (now.X < d.XMin || now.X > d.XMax)
                {
                    if (!IsDiscovered(entry) && entry < 32)
                    {
                        Stats.MissedDiscoveries |= 1 << entry;
                    }

                    continue;
                }

                if (entry < 32)
                {
                    Stats.MissedDiscoveries &= ~(1 << entry);
                }

                if (IsDiscovered(entry))
                {
                    Stats.Sightings++;
                    Emit(RunEventType.Discovery, entry, 0, 0f, now.Tick);
                    continue;
                }

                _foundThisRun[entry] = true;
                DiscoveryEntry e = _content.Discoveries[entry];
                Stats.AddNewDiscovery(entry);
                Stats.DiscoveryCoins += e.RewardCoins;
                Stats.DiscoveryCrystals += e.RewardCrystals;
                Emit(RunEventType.Discovery, entry, 1, 0f, now.Tick);
            }
        }

        private static bool Overlaps(in RunnerState r, float s, float x, float y, float halfDepth, float halfWidth, float height, float padY)
        {
            return Math.Abs(s - r.S) <= halfDepth && Math.Abs(x - r.X) <= halfWidth && y >= r.Y - padY && y <= r.Y + height + padY;
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
