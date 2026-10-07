using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>
    /// Places power-up pickups on the generated track (GDD 10) and holds them in a fixed ring. Uses its own random
    /// stream (<see cref="RandomStreamIds.Pickups"/>), so the track and vine streams never shift.
    /// <para>Schedule: like vine sections, a clock of planned run time (chunk length / speed-curve speed). The first
    /// pickup is due after <c>FirstPickupMinS..MaxS</c>, then every <c>IntervalMinS..MaxS</c> (average 25 s), so
    /// at most one is ever on screen. When due, the next Normal or Breather chunk gets one; never Start, Vine
    /// (no pickups in vine approaches), Signature or Gateway chunks.</para>
    /// <para>Fair placement: the pickup's lane is free of obstacles, gaps and hazards from <c>ClearBeforeM</c> before
    /// to <c>ClearAfterM</c> after it, and it stays <c>ChunkEdgeMarginM</c> from the chunk ends. Spots without
    /// coins in the same lane are preferred. If no spot fits, the next eligible chunk is tried.</para>
    /// <para>Draws: exactly 1 interval draw at <see cref="Reset"/> and after every placed pickup, and exactly 3 per
    /// placement attempt (type, first lane, z fraction).</para>
    /// <para>Speed Boost and vines (GDD 7.2, 10): a placed Speed Boost returns a z before which no vine section may
    /// start, covering the longest possible boost (dash, landing extension, slowdown and clear stretch).</para>
    /// No allocation after construction.
    /// </summary>
    public sealed class PowerUpPlacer
    {
        /// <summary>z candidates per lane and attempt.</summary>
        private const int ZCandidates = 4;

        /// <summary>Extra air time a boost may run on until landing (s), used for the vine block [ASSUMED bound].</summary>
        private const double BoostAirExtensionS = 1.0;

        /// <summary>Speed look-ahead for the vine block (the curve rises with distance; this bounds it).</summary>
        private const double SpeedLookAheadM = 400.0;

        private readonly PowerUpConfig _config;
        private readonly RunnerConfig _runner;
        private readonly SpeedCurve _curve;
        private readonly FixedRing<PowerUpPickup> _ring;

        private IRandom _rng;
        private double _elapsedS;
        private double _dueS;
        private int _nextId;

        public PowerUpPlacer(PowerUpConfig config, RunnerConfig runner, SpeedCurve curve)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));
            _ring = new FixedRing<PowerUpPickup>(config.MaxActivePickups);
            BoostDurationS = config.SpeedBoostSeconds(1);
            _dueS = double.PositiveInfinity;
        }

        public PowerUpConfig Config => _config;

        /// <summary>Speed Boost dash length of this run (set from the player's upgrade level), for the vine block.</summary>
        public float BoostDurationS { get; set; }

        /// <summary>Live pickups (oldest first, sorted by z).</summary>
        public int Count => _ring.Count;

        /// <summary>Pickups placed this run.</summary>
        public int PlacedCount { get; private set; }

        /// <summary>Pickups dropped because the ring was full.</summary>
        public int OverflowCount { get; private set; }

        /// <summary>Planned run time since the last pickup (s).</summary>
        public double ElapsedSeconds => _elapsedS;

        /// <summary>Planned run time after which the next pickup is placed (s).</summary>
        public double DueSeconds => _dueS;

        public ref readonly PowerUpPickup GetPickup(int index)
        {
            return ref _ring[index];
        }

        /// <summary>Writable access for the pickup step (same assembly only).</summary>
        internal ref PowerUpPickup PickupAt(int index)
        {
            return ref _ring[index];
        }

        /// <summary>Starts a run on the forked <see cref="RandomStreamIds.Pickups"/> stream (null = no pickups).</summary>
        public void Reset(IRandom stream)
        {
            _ring.Clear();
            _rng = _config.Enabled ? stream : null;
            _elapsedS = 0.0;
            _nextId = 1;
            PlacedCount = 0;
            OverflowCount = 0;
            _dueS = _rng != null ? _rng.NextFloat(_config.FirstPickupMinS, _config.FirstPickupMaxS) : double.PositiveInfinity;
        }

        /// <summary>
        /// Called by the track right after a chunk's obstacles and coins were added. May place one pickup in it.
        /// Returns the z before which no vine section may start (a placed Speed Boost), or negative infinity.
        /// </summary>
        public double OnChunkSpawned(TrackSimulation track, ChunkKind kind, double startZ, float lengthM, int serial)
        {
            double vineBlockZ = double.NegativeInfinity;
            if (_rng == null)
            {
                return vineBlockZ;
            }

            bool eligible = kind == ChunkKind.Normal || kind == ChunkKind.Breather;
            if (eligible && _elapsedS >= _dueS && lengthM > 2f * _config.ChunkEdgeMarginM)
            {
                PowerUpType type = _config.TypeForDraw(_rng.NextInt(0, _config.TotalWeight)); // draw 1
                int firstLane = _rng.NextInt(0, LaneMasks.LaneCount); // draw 2
                float zFraction = _rng.NextFloat(); // draw 3
                if (TryFindSpot(track, startZ, lengthM, firstLane, zFraction, true, out int lane, out double z)
                    || TryFindSpot(track, startZ, lengthM, firstLane, zFraction, false, out lane, out z))
                {
                    Add(type, lane, z, serial);
                    _elapsedS = 0.0;
                    _dueS = _rng.NextFloat(_config.IntervalMinS, _config.IntervalMaxS);
                    if (type == PowerUpType.SpeedBoost)
                    {
                        vineBlockZ = z + BoostReachM(z);
                    }
                }
            }

            double v = _curve.Evaluate(startZ);
            _elapsedS += lengthM / (v > 0.0 ? v : 1.0);
            return vineBlockZ;
        }

        /// <summary>Removes pickups whose z is behind <paramref name="limitZ"/>.</summary>
        public void Despawn(double limitZ)
        {
            while (_ring.Count > 0 && _ring[0].Z < limitZ)
            {
                _ring.RemoveFirst();
            }
        }

        public ulong ComputeStateHash(ulong h)
        {
            h = StableHash.Mix(h, _elapsedS);
            h = StableHash.Mix(h, _dueS);
            h = StableHash.Mix(h, _nextId);
            h = StableHash.Mix(h, _ring.Count);
            for (int i = 0; i < _ring.Count; i++)
            {
                ref PowerUpPickup p = ref _ring[i];
                h = StableHash.Mix(h, p.Id);
                h = StableHash.Mix(h, (int)p.Type);
                h = StableHash.Mix(h, (int)p.Lane);
                h = StableHash.Mix(h, p.Z);
                h = StableHash.Mix(h, p.Collected);
                h = StableHash.Mix(h, p.Resolved);
            }

            return h;
        }

        /// <summary>Upper bound of the distance a Speed Boost picked up at <paramref name="z"/> covers until its clear stretch ends.</summary>
        private double BoostReachM(double z)
        {
            double v = _curve.Evaluate(z + SpeedLookAheadM);
            double m = _config.SpeedBoostMultiplier;
            double dash = v * m * (BoostDurationS + BoostAirExtensionS);
            double slow = v * (1.0 + m) * 0.5 * _config.SpeedBoostSlowdownS;
            double clear = v * _config.SpeedBoostClearStretchS;
            return dash + slow + clear + _config.SpeedBoostVineBlockMarginM;
        }

        private bool TryFindSpot(
            TrackSimulation track, double startZ, float lengthM, int firstLane, float zFraction, bool avoidCoins, out int lane, out double z)
        {
            double margin = _config.ChunkEdgeMarginM;
            double span = lengthM - 2.0 * margin;
            for (int k = 0; k < ZCandidates; k++)
            {
                double f = zFraction + (double)k / ZCandidates;
                if (f >= 1.0)
                {
                    f -= 1.0;
                }

                double candidateZ = startZ + margin + f * span;
                for (int l = 0; l < LaneMasks.LaneCount; l++)
                {
                    int candidateLane = (firstLane + l) % LaneMasks.LaneCount;
                    if (IsLaneFree(track, candidateLane, candidateZ) && (!avoidCoins || !HasCoinNear(track, candidateLane, candidateZ)))
                    {
                        lane = candidateLane;
                        z = candidateZ;
                        return true;
                    }
                }
            }

            lane = -1;
            z = 0.0;
            return false;
        }

        private bool IsLaneFree(TrackSimulation track, int lane, double z)
        {
            double lo = z - _config.ClearBeforeM;
            double hi = z + _config.ClearAfterM;
            int count = track.ObstacleCount;
            for (int i = 0; i < count; i++)
            {
                ref readonly ObstacleInstance o = ref track.GetObstacle(i);
                double back = o.Archetype == ObstacleArchetype.Gap ? o.Z + o.GapLengthM : o.BackZ;
                if (o.Z > hi || back < lo)
                {
                    continue;
                }

                if (o.Archetype == ObstacleArchetype.Mover)
                {
                    int a = o.FromLane < o.ToLane ? o.FromLane : o.ToLane;
                    int b = o.FromLane < o.ToLane ? o.ToLane : o.FromLane;
                    if (lane >= a && lane <= b)
                    {
                        return false;
                    }

                    continue;
                }

                if (LaneMasks.Contains(o.LaneMask, lane))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasCoinNear(TrackSimulation track, int lane, double z)
        {
            double r = _config.CoinClearanceM;
            int count = track.CoinCount;
            for (int i = 0; i < count; i++)
            {
                ref readonly CoinInstance c = ref track.GetCoin(i);
                if (c.Z > z + r)
                {
                    break;
                }

                if (c.Z >= z - r && c.Lane == lane)
                {
                    return true;
                }
            }

            return false;
        }

        private void Add(PowerUpType type, int lane, double z, int serial)
        {
            var p = new PowerUpPickup
            {
                Id = _nextId++,
                Type = type,
                Lane = (byte)lane,
                X = _runner.LaneCenterX(lane),
                Y = _config.PickupHeightM,
                Z = z,
                ChunkSerial = serial,
            };

            if (_ring.TryAdd(p))
            {
                PlacedCount++;
            }
            else
            {
                OverflowCount++;
            }
        }
    }
}
