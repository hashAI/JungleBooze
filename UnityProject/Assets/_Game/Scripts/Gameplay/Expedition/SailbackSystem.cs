using System;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// The slice creature (spec 103 §7): sailbacks spawned deterministically from placed chunks' spawn data, ticked
    /// after each simulation step from Pista's state. Perched → Alert (Δs ≤ alert distance, or a startle) → Launch
    /// (Δs ≤ launch distance, or after a startle) → Glide along the authored line at Pista's speed + bonus → Gone at
    /// the line's end. Never collides, never below <see cref="SailbackConfig.MinHeightOverPath"/> over the path.
    /// Observation (Δs ∈ [2, 35], |Δx| ≤ 10) accumulates per spawn group; at <c>observeTime</c> the group reports to
    /// the discovery sink once. Fixed slots, no allocation per tick.
    /// </summary>
    public sealed class SailbackSystem
    {
        public const int Capacity = 24;
        private const int GroupCapacity = 16;

        private readonly WorldPath _path;
        private readonly SailbackConfig _config;
        private readonly float _dt;
        private readonly RunEventBuffer _events;
        private readonly Sailback[] _animals = new Sailback[Capacity];
        private readonly int[] _groupId = new int[GroupCapacity];
        private readonly int[] _groupObserved = new int[GroupCapacity];
        private readonly bool[] _groupFired = new bool[GroupCapacity];
        private readonly string[] _groupEntry = new string[GroupCapacity];
        private readonly int[] _groupSerial = new int[GroupCapacity];
        private readonly int _alertTicks;
        private readonly int _launchTicks;
        private readonly int _flockTicks;
        private readonly int _observeTicks;
        private int _scanned;

        public SailbackSystem(WorldPath path, SailbackConfig config, float stepSeconds, RunEventBuffer events)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _config = (config ?? new SailbackConfig()).Clone();
            _dt = stepSeconds;
            _events = events;
            _alertTicks = Math.Max(1, (int)Math.Round(_config.AlertTime / stepSeconds));
            _launchTicks = Math.Max(1, (int)Math.Round(_config.LaunchTime / stepSeconds));
            _flockTicks = Math.Max(0, (int)Math.Round(_config.FlockOffsetTime / stepSeconds));
            _observeTicks = Math.Max(1, (int)Math.Round(_config.ObserveTime / stepSeconds));
            BeginRun();
        }

        public SailbackConfig Config => _config;

        public ICreatureDiscoverySink Sink { get; set; }

        public bool MuteEvents { get; set; }

        /// <summary>Ticks of observation needed (48 at 60 Hz).</summary>
        public int ObserveTicks => _observeTicks;

        public int Count => Capacity;

        public ref readonly Sailback Get(int slot) => ref _animals[slot];

        /// <summary>Observed ticks of a spawn group so far (tests).</summary>
        public int ObservedTicks(int group)
        {
            for (int g = 0; g < GroupCapacity; g++)
            {
                if (_groupId[g] == group)
                {
                    return _groupObserved[g];
                }
            }

            return 0;
        }

        public void BeginRun()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _animals[i] = default;
            }

            for (int g = 0; g < GroupCapacity; g++)
            {
                _groupId[g] = -1;
                _groupObserved[g] = 0;
                _groupFired[g] = false;
                _groupEntry[g] = null;
            }

            _scanned = 0;
        }

        /// <summary>After each simulation step (alive or not: dead runners observe nothing).</summary>
        public void Step(in RunnerState runner)
        {
            Spawn();
            long t = runner.Tick;
            for (int i = 0; i < Capacity; i++)
            {
                if (_animals[i].State == CreatureState.Inactive)
                {
                    continue;
                }

                StepAnimal(i, runner, t);
            }

            if (!runner.Dead)
            {
                Observe(runner, t);
            }
        }

        private void Spawn()
        {
            if (_scanned < _path.FirstChunkSerial)
            {
                _scanned = _path.FirstChunkSerial;
            }

            for (; _scanned < _path.NextChunkSerial; _scanned++)
            {
                ref readonly PlacedChunk p = ref _path.Chunk(_scanned);
                for (int k = 0; k < p.Chunk.CreatureCount; k++)
                {
                    CreatureSpawn sp = p.Chunk.GetCreature(k);
                    int group = (p.Serial * ChunkRuntime.LocalIdStride) + k;
                    int g = group % GroupCapacity;
                    _groupId[g] = group;
                    _groupObserved[g] = 0;
                    _groupFired[g] = false;
                    _groupEntry[g] = sp.Ambient ? null : sp.EntryId;
                    _groupSerial[g] = p.Serial;
                    float perchFloor = p.Chunk.BaseHeight(sp.S, 0f);
                    for (int m = 0; m < Math.Max(1, sp.Count); m++)
                    {
                        int slot = FreeSlot();
                        if (slot < 0)
                        {
                            break;
                        }

                        float s = p.StartS + sp.S + (m * sp.Spacing);
                        float x = sp.X + (m % 2 == 0 ? 0f : 0.25f);
                        _animals[slot] = new Sailback
                        {
                            State = CreatureState.Perched,
                            StateTick = 0,
                            Group = group,
                            Member = m,
                            ChunkSerial = p.Serial,
                            Ambient = sp.Ambient,
                            Roost = sp.LaunchDistance < 0f,
                            S = s,
                            X = x,
                            Y = perchFloor + sp.Y,
                            PerchS = s,
                            PerchX = x,
                            PerchY = perchFloor + sp.Y,
                            EndS = s + sp.EndDS,
                            EndX = sp.EndX + (m * 0.6f),
                            EndY = perchFloor + sp.EndY + (m * 0.3f),
                            LaunchDistance = sp.LaunchDistance > 0f ? sp.LaunchDistance : _config.LaunchDistance,
                            Sail = 0.5f,
                        };
                    }
                }
            }
        }

        private int FreeSlot()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_animals[i].State == CreatureState.Inactive)
                {
                    return i;
                }
            }

            // Reuse the oldest finished one.
            for (int i = 0; i < Capacity; i++)
            {
                if (_animals[i].State == CreatureState.Gone && !_path.IsLive(_animals[i].ChunkSerial))
                {
                    return i;
                }
            }

            return -1;
        }

        private void StepAnimal(int i, in RunnerState runner, long t)
        {
            ref Sailback a = ref _animals[i];
            float ds = a.S - runner.S;
            switch (a.State)
            {
                case CreatureState.Perched:
                    a.HeadTracking = ds < _config.HeadTrackDistance && ds > -2f;
                    if (runner.Dead)
                    {
                        return;
                    }

                    bool startle = a.HeadTracking && Math.Abs(a.X - runner.X) <= _config.StartleLateral;
                    float alertAt = a.Roost ? _config.AlertDistance : Math.Max(_config.AlertDistance, a.LaunchDistance);
                    if ((ds <= alertAt && ds > -2f) || startle)
                    {
                        a.Startled = startle;
                        SetState(i, CreatureState.Alert, t);
                        a.Sail = 1f;
                    }

                    break;

                case CreatureState.Alert:
                    a.HeadTracking = true;
                    if (a.Roost)
                    {
                        if (ds < -5f)
                        {
                            SetState(i, CreatureState.Gone, t);
                        }

                        return;
                    }

                    long delay = _flockTicks * a.Member;
                    if (t - a.StateTick >= _alertTicks + delay && (a.Startled || ds <= a.LaunchDistance))
                    {
                        SetState(i, CreatureState.Launch, t);
                    }

                    break;

                case CreatureState.Launch:
                {
                    float u = Math.Min(1f, (float)(t - a.StateTick) / _launchTicks);
                    a.S += Math.Max(1f, runner.Speed) * _dt;
                    a.Y = a.PerchY - (_config.LaunchDrop * u);
                    a.Sail = 1f;
                    ClampOverPath(ref a);
                    if (t - a.StateTick >= _launchTicks)
                    {
                        a.GlideS0 = a.S;
                        a.GlideX0 = a.X;
                        a.GlideY0 = a.Y;
                        SetState(i, CreatureState.Glide, t);
                    }

                    break;
                }

                case CreatureState.Glide:
                {
                    float speed = Math.Max(1f, runner.Speed) + _config.GlideSpeedBonus;
                    a.S += speed * _dt;
                    float span = Math.Max(0.01f, a.EndS - a.GlideS0);
                    float u = Clamp01((a.S - a.GlideS0) / span);
                    float w = u * u * (3f - (2f * u));
                    a.X = a.GlideX0 + ((a.EndX - a.GlideX0) * w);
                    a.Y = a.GlideY0 + ((a.EndY - a.GlideY0) * u);
                    ClampOverPath(ref a);
                    if (u >= 1f)
                    {
                        a.S = a.EndS;
                        SetState(i, CreatureState.Gone, t);
                    }

                    break;
                }

                case CreatureState.Gone:
                    if (!_path.IsLive(a.ChunkSerial) && a.S < _path.StartS)
                    {
                        a.State = CreatureState.Inactive;
                    }

                    break;
            }
        }

        private void ClampOverPath(ref Sailback a)
        {
            // AC-103-29: over the path the animal stays ≥ MinHeightOverPath above the floor.
            _path.GetOuterBounds(a.S, out float xMin, out float xMax);
            if (a.X < xMin || a.X > xMax)
            {
                return;
            }

            if (_path.TryGetFloor(a.S, a.X, out float floor) && a.Y < floor + _config.MinHeightOverPath)
            {
                a.Y = floor + _config.MinHeightOverPath;
            }
        }

        private void Observe(in RunnerState runner, long t)
        {
            for (int g = 0; g < GroupCapacity; g++)
            {
                if (_groupId[g] < 0 || _groupFired[g] || _groupEntry[g] == null)
                {
                    continue;
                }

                bool seen = false;
                for (int i = 0; i < Capacity && !seen; i++)
                {
                    ref readonly Sailback a = ref _animals[i];
                    if (a.Group != _groupId[g] || a.State == CreatureState.Inactive || a.State == CreatureState.Gone || a.Ambient)
                    {
                        continue;
                    }

                    float ds = a.S - runner.S;
                    seen = ds >= _config.ObserveMinDs && ds <= _config.ObserveMaxDs && Math.Abs(a.X - runner.X) <= _config.ObserveMaxDx;
                }

                if (!seen)
                {
                    continue;
                }

                _groupObserved[g]++;
                if (_groupObserved[g] >= _observeTicks)
                {
                    _groupFired[g] = true;
                    Sink?.OnCreatureObserved(_groupEntry[g], _groupSerial[g], t);
                }
            }
        }

        private void SetState(int i, CreatureState state, long t)
        {
            _animals[i].State = state;
            _animals[i].StateTick = t;
            if (!MuteEvents && _events != null)
            {
                _events.Add(new RunEvent(RunEventType.CreatureState, t, i, (byte)state, 0f));
            }
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
