using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// The generated track of one run (spec 002 sections 4.4, 5.5, 6, 8.2 and 8.6): fixed-capacity rings of
    /// chunks, obstacles and coins, filled ahead of HERO by the <see cref="TrackGenerator"/> and emptied behind him.
    /// Implements <see cref="ITrackQuery"/> (the spec's <c>ChunkTrackQuery</c>): ground and gaps, obstacle boxes
    /// (movers at their current position, with the previous position for the relative-motion sweep) and the next
    /// gap edge. Advanced once per tick by <see cref="Update"/> (step 7a); queries never change state.
    /// Also holds the vines of vine sections (<see cref="IVineTrackQuery"/>, GDD 7) and the bonus coins a vine
    /// release throws along the launch arc (coin shower and Perfect ring, GDD 7.3 step 4).
    /// No allocation after construction.
    /// </summary>
    public sealed class TrackSimulation : ITrackQuery, IVineTrackQuery
    {
        /// <summary>Height of a bonus coin above HERO's feet on the launch arc (about the chest, so it is picked up).</summary>
        private const float BonusCoinBodyOffsetM = 0.9f;

        /// <summary>Bonus coins start this long into the flight, after HERO has left the vine.</summary>
        private const double BonusCoinStartS = 0.12;

        /// <summary>Bonus coins cover the flight up to this fraction of its length.</summary>
        private const double BonusCoinEndFraction = 0.8;

        /// <summary>Coins one chunk may hold (scratch buffer size).</summary>
        public const int MaxCoinsPerChunk = 256;

        /// <summary>Tolerance for the mover trigger distance (float tuning against double distances).</summary>
        private const double MoverTriggerToleranceM = 1e-4;

        private readonly TrackConfig _config;
        private readonly ObstacleKitConfig _kit;
        private readonly CoinConfig _coinConfig;
        private readonly ChunkLibrary _library;
        private readonly VineConfig _vines;
        private readonly FixedRing<VineInstance> _vineRing;
        private readonly CoinInstance[] _bonusCoins;
        private int _bonusCoinCount;
        private int _nextVineId;
        private readonly SpeedCurve _curve;
        private readonly RunnerConfig _runner;
        private readonly TrackGenerator _generator;

        private readonly FixedRing<ChunkInstance> _chunks;
        private readonly FixedRing<ObstacleInstance> _obstacles;
        private readonly FixedRing<CoinInstance> _coins;

        private readonly float[] _scratchX = new float[MaxCoinsPerChunk];
        private readonly float[] _scratchY = new float[MaxCoinsPerChunk];
        private readonly double[] _scratchZ = new double[MaxCoinsPerChunk];
        private readonly int[] _order = new int[MaxCoinsPerChunk];

        private readonly float _halfWidth;
        private readonly float _laneHalf;
        private readonly float _moverStepM;
        private readonly int _moverTicks;

        private int _nextObstacleId;
        private int _nextCoinId;
        private int _nextChunkSerial;
        private double _generatedEndZ;
        private int _enteredSerial;
        private int _currentTier;
        private int _currentChunkIndex;
        private bool _hasReset;

        private readonly HazardConfig _hazards;
        private readonly PowerUpPlacer _pickups;

        public TrackSimulation(
            TrackConfig config,
            ObstacleKitConfig kit,
            CoinConfig coins,
            ChunkLibrary library,
            DifficultyTiersConfig tiers,
            SpeedCurve curve,
            RunnerConfig runner,
            VineConfig vines = null,
            PowerUpConfig powerUps = null,
            HazardConfig hazards = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _kit = kit ?? throw new ArgumentNullException(nameof(kit));
            _coinConfig = coins ?? throw new ArgumentNullException(nameof(coins));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (runner.LaneCount != LaneMasks.LaneCount)
            {
                throw new ArgumentException("The track supports exactly " + LaneMasks.LaneCount + " lanes.", nameof(runner));
            }

            _vines = vines ?? VineConfig.CreateDefault();
            _generator = new TrackGenerator(config, library, tiers, curve, _vines);
            _vineRing = new FixedRing<VineInstance>(_vines.MaxActiveVines);
            _bonusCoins = new CoinInstance[_vines.MaxBonusCoins];
            _chunks = new FixedRing<ChunkInstance>(config.MaxActiveChunks);
            _obstacles = new FixedRing<ObstacleInstance>(config.MaxActiveObstacles);
            _coins = new FixedRing<CoinInstance>(config.MaxActiveCoins);

            _halfWidth = runner.PlayerHitboxWidthM * 0.5f;
            _laneHalf = runner.LaneWidthM * 0.5f;
            _moverStepM = (float)(kit.MoverLateralSpeedMps * RunnerConfig.TickSeconds);
            _moverTicks = Math.Max(1, (int)Math.Ceiling(runner.LaneWidthM / (kit.MoverLateralSpeedMps * RunnerConfig.TickSeconds) - 1e-6));
            _currentChunkIndex = -1;
            _hazards = hazards ?? HazardConfig.CreateDefault();
            _pickups = new PowerUpPlacer(powerUps ?? PowerUpConfig.CreateDefault(), runner, curve);
        }

        /// <summary>Lane-strike timing (GDD 8.3).</summary>
        public HazardConfig Hazards => _hazards;

        /// <summary>Power-up pickups on the track (GDD 10).</summary>
        public PowerUpPlacer PowerUpPickups => _pickups;

        public TrackConfig Config => _config;

        public ObstacleKitConfig Kit => _kit;

        public CoinConfig CoinConfig => _coinConfig;

        public ChunkLibrary Library => _library;

        public RunnerConfig RunnerConfig => _runner;

        public VineConfig VineConfig => _vines;

        /// <summary>Live vines (oldest first, sorted by z).</summary>
        public int VineCount => _vineRing.Count;

        /// <summary>Items dropped because the vine ring or the bonus coin buffer was full.</summary>
        public int VineOverflowCount { get; private set; }

        public int BonusCoinOverflowCount { get; private set; }

        /// <summary>Live bonus coins from vine releases (separate from the chunk coin ring).</summary>
        public int BonusCoinCount => _bonusCoinCount;

        public TrackGenerator Generator => _generator;

        /// <summary>End z of the last generated chunk.</summary>
        public double GeneratedEndZ => _generatedEndZ;

        /// <summary>Tier of the chunk HERO's centre is in (1 before the first tick).</summary>
        public int CurrentTier => _currentTier;

        /// <summary>Library index of the chunk HERO's centre is in (-1 before the first tick).</summary>
        public int CurrentChunkIndex => _currentChunkIndex;

        /// <summary>Ticks a mover needs for one lane (30 with the start values).</summary>
        public int MoverMoveTicks => _moverTicks;

        public int ChunkCount => _chunks.Count;

        public int ObstacleCount => _obstacles.Count;

        public int CoinCount => _coins.Count;

        public int ChunkCapacity => _chunks.Capacity;

        public int ObstacleCapacity => _obstacles.Capacity;

        public int CoinCapacity => _coins.Capacity;

        /// <summary>Highest ring fill levels this run (pool sizing, AC-220).</summary>
        public int ChunkHighWaterMark => _chunks.HighWaterMark;

        public int ObstacleHighWaterMark => _obstacles.HighWaterMark;

        public int CoinHighWaterMark => _coins.HighWaterMark;

        /// <summary>Items dropped because a ring was full. Non-zero is an error in development builds.</summary>
        public int ChunkOverflowCount { get; private set; }

        public int ObstacleOverflowCount { get; private set; }

        public int CoinOverflowCount { get; private set; }

        /// <summary>Next obstacle id to be assigned (ids start at 1 each run).</summary>
        public int NextObstacleId => _nextObstacleId;

        /// <summary>Next coin id to be assigned (ids start at 1 each run).</summary>
        public int NextCoinId => _nextCoinId;

        /// <summary>Chunks generated this run (serials start at 1).</summary>
        public int GeneratedChunkCount => _nextChunkSerial - 1;

        /// <summary>Active chunk <paramref name="index"/>, 0 = oldest. Read-only view of the ring.</summary>
        public ref readonly ChunkInstance GetChunk(int index)
        {
            return ref _chunks[index];
        }

        /// <summary>Active obstacle <paramref name="index"/>, 0 = oldest (lowest id, smallest z).</summary>
        public ref readonly ObstacleInstance GetObstacle(int index)
        {
            return ref _obstacles[index];
        }

        /// <summary>Active coin <paramref name="index"/>, 0 = oldest (lowest id, smallest z).</summary>
        public ref readonly CoinInstance GetCoin(int index)
        {
            return ref _coins[index];
        }

        /// <summary>Live vine <paramref name="index"/>, 0 = oldest.</summary>
        public ref readonly VineInstance GetVine(int index)
        {
            return ref _vineRing[index];
        }

        /// <summary>Bonus coin <paramref name="index"/> (vine coin shower / ring).</summary>
        public ref readonly CoinInstance GetBonusCoin(int index)
        {
            return ref _bonusCoins[index];
        }

        /// <summary>Writable bonus coin access for the coin pickup step (same assembly only).</summary>
        internal ref CoinInstance BonusCoinAt(int index)
        {
            return ref _bonusCoins[index];
        }

        /// <summary>Writable coin access for the coin pickup step (same assembly only).</summary>
        internal ref CoinInstance CoinAt(int index)
        {
            return ref _coins[index];
        }

        /// <summary>
        /// Starts a new run: clears every ring, id counter and generator state, then generates the start chunk and
        /// the chunks after it until <c>generateAheadM</c> is covered (spec 8.2: before tick 0).
        /// <paramref name="trackStream"/> is the run's forked <see cref="RandomStreamIds.TrackGeneration"/> stream.
        /// </summary>
        public void Reset(IRandom trackStream)
        {
            Reset(trackStream, null);
        }

        /// <summary>
        /// Like <see cref="Reset(IRandom)"/>, with the run's forked <see cref="RandomStreamIds.VineSchedule"/> stream
        /// (null = no vine sections).
        /// </summary>
        public void Reset(IRandom trackStream, IRandom vineStream)
        {
            Reset(trackStream, vineStream, null);
        }

        /// <summary>
        /// Like <see cref="Reset(IRandom, IRandom)"/>, with the run's forked <see cref="RandomStreamIds.Pickups"/>
        /// stream (null = no power-up pickups).
        /// </summary>
        public void Reset(IRandom trackStream, IRandom vineStream, IRandom pickupStream)
        {
            _pickups.Reset(pickupStream);
            _vineRing.Clear();
            _bonusCoinCount = 0;
            _nextVineId = 1;
            VineOverflowCount = 0;
            BonusCoinOverflowCount = 0;
            _chunks.Clear();
            _obstacles.Clear();
            _coins.Clear();
            _nextObstacleId = 1;
            _nextCoinId = 1;
            _nextChunkSerial = 1;
            _generatedEndZ = 0.0;
            _enteredSerial = 0;
            _currentTier = 1;
            _currentChunkIndex = -1;
            ChunkOverflowCount = 0;
            ObstacleOverflowCount = 0;
            CoinOverflowCount = 0;
            _generator.Reset(trackStream, vineStream);
            _hasReset = true;
            GenerateAhead(0.0);
        }

        /// <summary>
        /// Step 7a (spec 002 section 13.3): generate ahead, despawn behind, mover triggers and motion,
        /// <c>ChunkEntered</c>/<c>TierChanged</c>. Events go to <paramref name="runner"/>'s event buffer
        /// (null = no events, for tests). No allocation.
        /// </summary>
        public void Update(in RunnerTickInfo info, RunnerSimulation runner)
        {
            if (!_hasReset)
            {
                throw new InvalidOperationException("Call Reset before the first update.");
            }

            GenerateAhead(info.Z);
            DespawnBehind(info.Z);
            UpdateMovers(info, runner);
            UpdateLaneStrikes(info, runner);
            UpdateEnteredChunks(info, runner);
        }

        /// <summary>Generates chunks until the generated track reaches <paramref name="heroZ"/> + generateAheadM.</summary>
        public void GenerateAhead(double heroZ)
        {
            double target = heroZ + _config.GenerateAheadM;
            while (_generatedEndZ < target)
            {
                SpawnNextChunk();
            }
        }

        /// <summary>Ring position of the active chunk that contains <paramref name="z"/>, or -1.</summary>
        public int FindChunkAt(double z)
        {
            for (int i = _chunks.Count - 1; i >= 0; i--)
            {
                ref ChunkInstance c = ref _chunks[i];
                if (z >= c.StartZ && z < c.EndZ)
                {
                    return i;
                }
            }

            return -1;
        }

        // ---- ITrackQuery ----

        public bool HasGround(float x, double zMin, double zMax)
        {
            float xMin = x - _halfWidth;
            float xMax = x + _halfWidth;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                if (o.Z > zMax)
                {
                    break;
                }

                if (o.Archetype != ObstacleArchetype.Gap)
                {
                    continue;
                }

                // Ground is missing for z in [near, near + length): the footprint is unsupported only when it lies
                // entirely inside that range and entirely inside the gap's lanes (partial support counts as ground).
                if (zMin >= o.Z && zMax < o.Z + o.GapLengthM)
                {
                    int lo = LaneMasks.Lowest(o.LaneMask);
                    int hi = LaneMasks.Highest(o.LaneMask);
                    float gx0 = lo == 0 ? float.NegativeInfinity : _runner.LaneCenterX(lo) - _laneHalf;
                    float gx1 = hi == LaneMasks.LaneCount - 1 ? float.PositiveInfinity : _runner.LaneCenterX(hi) + _laneHalf;
                    if (xMin > gx0 && xMax < gx1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public int GetBoxes(double zMin, double zMax, Span<ObstacleBox> buffer)
        {
            int count = 0;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                if (o.Z > zMax)
                {
                    break;
                }

                if (o.Archetype == ObstacleArchetype.Gap || o.BackZ < zMin)
                {
                    continue;
                }

                if (o.Archetype == ObstacleArchetype.LaneStrike && o.StrikePhase != LaneStrikePhase.Active)
                {
                    // GDD 8.3: the lane is only dangerous while the strike is active.
                    continue;
                }

                ObstacleShape s = _kit.GetShape(o.Archetype);
                float half = s.WidthM * 0.5f;
                if (o.Archetype == ObstacleArchetype.Mover)
                {
                    if (count >= buffer.Length)
                    {
                        return count;
                    }

                    buffer[count++] = new ObstacleBox
                    {
                        Id = o.Id,
                        Archetype = o.Archetype,
                        Lane = o.FromLane,
                        XMin = o.MoverX - half,
                        XMax = o.MoverX + half,
                        XMinPrev = o.MoverXPrev - half,
                        XMaxPrev = o.MoverXPrev + half,
                        YMin = s.BottomM,
                        YMax = s.TopM,
                        ZMin = o.Z,
                        ZMax = o.BackZ,
                    };
                    continue;
                }

                for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
                {
                    if (!LaneMasks.Contains(o.LaneMask, lane))
                    {
                        continue;
                    }

                    if (count >= buffer.Length)
                    {
                        return count;
                    }

                    float cx = _runner.LaneCenterX(lane);
                    buffer[count++] = ObstacleBox.Static(o.Id, o.Archetype, (byte)lane, cx - half, cx + half, s.BottomM, s.TopM, o.Z, o.BackZ);
                }
            }

            return count;
        }

        public int GetVines(double zMin, double zMax, Span<VineAnchor> buffer)
        {
            int count = 0;
            for (int i = 0; i < _vineRing.Count; i++)
            {
                ref VineInstance v = ref _vineRing[i];
                if (v.Z > zMax)
                {
                    break;
                }

                if (v.Z < zMin)
                {
                    continue;
                }

                if (count >= buffer.Length)
                {
                    return count;
                }

                buffer[count++] = new VineAnchor
                {
                    Id = v.Id,
                    Group = v.ChunkSerial,
                    Row = v.Row,
                    Lane = v.Lane,
                    Z = v.Z,
                    OverChasm = v.OverChasm,
                };
            }

            return count;
        }

        /// <summary>
        /// Throws the bonus coins of a vine release along HERO's launch arc (GDD 7.3 step 4, 7.5): Good = a trail of
        /// <c>GoodCoins</c>; Perfect = a trail plus a ring of <c>PerfectRingCoins</c> around the arc's apex. Auto
        /// throws none. Positions follow the runner's launch (start, vertical speed, gravity, forward speed), so HERO
        /// flies through them. Called at step 11a of the release tick, before pickups. No allocation.
        /// </summary>
        public void SpawnVineBonusCoins(VineReleaseGrade grade, RunnerSimulation runner)
        {
            int total = _vines.CoinsFor(grade);
            if (total <= 0 || runner == null)
            {
                return;
            }

            double y0 = runner.LaunchStartY;
            double vy = runner.LaunchVelocityYMps;
            double g = runner.LaunchGravityMps2 > 0.0 ? runner.LaunchGravityMps2 : 1.0;
            double v = runner.LaunchForwardSpeedMps;
            double z0 = runner.LaunchStartZ;
            double flight = runner.LaunchFlightSeconds;
            float x = _runner.LaneCenterX(runner.LaunchLane);

            int ring = grade == VineReleaseGrade.Perfect ? Math.Min(_vines.PerfectRingCoins, total) : 0;
            int trail = total - ring;
            double t0 = BonusCoinStartS;
            double t1 = flight * BonusCoinEndFraction;
            if (t1 < t0)
            {
                t1 = t0;
            }

            for (int k = 0; k < trail; k++)
            {
                double t = trail > 1 ? t0 + (t1 - t0) * k / (trail - 1) : t0;
                AddBonusCoin(x, (float)(y0 + vy * t - 0.5 * g * t * t) + BonusCoinBodyOffsetM, z0 + v * t);
            }

            if (ring > 0)
            {
                double ta = vy / g;
                if (ta < t0)
                {
                    ta = t0;
                }

                if (ta > t1)
                {
                    ta = t1;
                }

                float cy = (float)(y0 + vy * ta - 0.5 * g * ta * ta) + BonusCoinBodyOffsetM;
                double cz = z0 + v * ta;
                float r = _vines.RingRadiusM;
                for (int k = 0; k < ring; k++)
                {
                    double a = 2.0 * Math.PI * k / ring;
                    AddBonusCoin(x + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a)), cz);
                }
            }
        }

        /// <summary>
        /// Removes every obstacle and gap that overlaps [<paramref name="fromZ"/>, <paramref name="toZ"/>] (Continue
        /// and Lift touchdown clear stretches, GDD 14.4 and 15.1). Views poll the ring, so removed pieces disappear on
        /// the next frame. Returns how many were removed. No allocation; for rare events only.
        /// </summary>
        public int ClearObstacles(double fromZ, double toZ)
        {
            int removed = 0;
            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                double back = o.Archetype == ObstacleArchetype.Gap ? o.Z + o.GapLengthM : o.BackZ;
                if (o.Z <= toZ && back >= fromZ)
                {
                    _obstacles.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// Where the next stretch of solid ground in <paramref name="lane"/> starts at or after <paramref name="fromZ"/>
        /// for a footprint of <paramref name="depthM"/>, searched in <paramref name="stepM"/> steps up to
        /// <paramref name="maxSearchM"/> ahead (generating track as needed). Returns <paramref name="fromZ"/> if there
        /// is ground there already.
        /// </summary>
        public double FindGroundAhead(int lane, double fromZ, double depthM, double stepM, double maxSearchM)
        {
            GenerateAhead(fromZ + maxSearchM);
            float x = _runner.LaneCenterX(lane);
            double half = depthM * 0.5;
            double step = stepM > 0.01 ? stepM : 0.01;
            for (double z = fromZ; z <= fromZ + maxSearchM; z += step)
            {
                if (HasGround(x, z - half, z + half))
                {
                    return z;
                }
            }

            return fromZ + maxSearchM;
        }

        public bool TryGetNextGapEdge(int lane, double fromZ, out double nearEdge, out float length)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                if (o.Archetype != ObstacleArchetype.Gap || o.Z < fromZ || !LaneMasks.Contains(o.LaneMask, lane))
                {
                    continue;
                }

                nearEdge = o.Z;
                length = o.GapLengthM;
                return true;
            }

            nearEdge = 0.0;
            length = 0f;
            return false;
        }

        /// <summary>
        /// Stable hash of the whole track state: rings, id counters, generator (AC-242, AC-247). Allocation-free.
        /// </summary>
        public ulong ComputeStateHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, _nextObstacleId);
            h = StableHash.Mix(h, _nextCoinId);
            h = StableHash.Mix(h, _nextChunkSerial);
            h = StableHash.Mix(h, _generatedEndZ);
            h = StableHash.Mix(h, _enteredSerial);
            h = StableHash.Mix(h, _currentTier);
            h = StableHash.Mix(h, _currentChunkIndex);
            h = _generator.ComputeStateHash(h);
            h = StableHash.Mix(h, _chunks.Count);
            for (int i = 0; i < _chunks.Count; i++)
            {
                ref ChunkInstance c = ref _chunks[i];
                h = StableHash.Mix(h, c.Serial);
                h = StableHash.Mix(h, c.ChunkIndex);
                h = StableHash.Mix(h, c.Mirrored);
                h = StableHash.Mix(h, c.StartZ);
                h = StableHash.Mix(h, (int)c.Tier);
                h = StableHash.Mix(h, c.IsSeamFallback);
            }

            h = StableHash.Mix(h, _obstacles.Count);
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                h = StableHash.Mix(h, o.Id);
                h = StableHash.Mix(h, (int)o.Archetype);
                h = StableHash.Mix(h, (int)o.LaneMask);
                h = StableHash.Mix(h, o.Z);
                h = StableHash.Mix(h, (int)o.Phase);
                h = StableHash.Mix(h, o.MoverX);
                h = StableHash.Mix(h, o.MoverXPrev);
                h = StableHash.Mix(h, o.MoverTicks);
                h = StableHash.Mix(h, (int)o.StrikePhase);
                h = StableHash.Mix(h, o.StrikeTicks);
            }

            h = StableHash.Mix(h, _coins.Count);
            for (int i = 0; i < _coins.Count; i++)
            {
                h = MixCoin(h, _coins[i]);
            }

            h = StableHash.Mix(h, _nextVineId);
            h = StableHash.Mix(h, _vineRing.Count);
            for (int i = 0; i < _vineRing.Count; i++)
            {
                ref VineInstance v = ref _vineRing[i];
                h = StableHash.Mix(h, v.Id);
                h = StableHash.Mix(h, (int)v.Lane);
                h = StableHash.Mix(h, (int)v.Row);
                h = StableHash.Mix(h, v.Z);
                h = StableHash.Mix(h, v.OverChasm);
            }

            h = StableHash.Mix(h, _bonusCoinCount);
            for (int i = 0; i < _bonusCoinCount; i++)
            {
                h = MixCoin(h, _bonusCoins[i]);
            }

            return _pickups.ComputeStateHash(h);
        }

        /// <summary>Hash of the coin layout only (positions and ids of every active coin), for determinism tests.</summary>
        public ulong ComputeCoinLayoutHash(ulong h)
        {
            for (int i = 0; i < _coins.Count; i++)
            {
                ref CoinInstance c = ref _coins[i];
                h = StableHash.Mix(h, c.Id);
                h = StableHash.Mix(h, c.X);
                h = StableHash.Mix(h, c.Y);
                h = StableHash.Mix(h, c.Z);
            }

            return h;
        }

        private static ulong MixCoin(ulong h, in CoinInstance c)
        {
            h = StableHash.Mix(h, c.Id);
            h = StableHash.Mix(h, c.X);
            h = StableHash.Mix(h, c.Y);
            h = StableHash.Mix(h, c.Z);
            h = StableHash.Mix(h, c.Collected);
            return StableHash.Mix(h, c.Resolved);
        }

        // ---- Generation ----

        private void SpawnNextChunk()
        {
            double startZ = _generatedEndZ;
            ChunkPick pick = _generator.NextChunk(startZ);
            ChunkData data = _library[pick.ChunkIndex];
            int serial = _nextChunkSerial++;

            var chunk = new ChunkInstance
            {
                Serial = serial,
                ChunkIndex = pick.ChunkIndex,
                Mirrored = pick.Mirrored,
                Kind = data.Kind,
                StartZ = startZ,
                LengthM = data.LengthM,
                Tier = (byte)pick.Tier,
                IsSeamFallback = pick.IsSeamFallback,
            };

            chunk.FirstObstacleId = data.ObstacleCount > 0 ? _nextObstacleId : 0;
            chunk.ObstacleCount = data.ObstacleCount;
            for (int i = 0; i < data.ObstacleCount; i++)
            {
                ObstaclePlacement p = data.GetObstacle(i);
                if (pick.Mirrored)
                {
                    p = p.Mirrored();
                }

                AddObstacle(p, startZ, serial);
            }

            int n = 0;
            for (int i = 0; i < data.CoinPatternCount; i++)
            {
                CoinPattern pattern = data.GetCoinPattern(i);
                if (pick.Mirrored)
                {
                    pattern = pattern.Mirrored();
                }

                double arcSpeed = _curve.Evaluate(startZ + pattern.ZCenter);
                n += CoinLayout.Generate(pattern, startZ, arcSpeed, _coinConfig, _runner, _scratchX, _scratchY, _scratchZ, n);
            }

            for (int i = 0; i < data.VineCount; i++)
            {
                VinePlacement vp = data.GetVine(i);
                if (pick.Mirrored)
                {
                    vp = vp.Mirrored();
                }

                var vine = new VineInstance
                {
                    Id = _nextVineId++,
                    Lane = (byte)vp.Lane,
                    Row = (byte)vp.Row,
                    Z = startZ + vp.Zc,
                    OverChasm = vp.OverChasm,
                    ChunkSerial = serial,
                };

                if (!_vineRing.TryAdd(vine))
                {
                    VineOverflowCount++;
                }
            }

            SortScratchByZ(n);
            chunk.FirstCoinId = n > 0 ? _nextCoinId : 0;
            chunk.CoinCount = n;
            for (int k = 0; k < n; k++)
            {
                int s = _order[k];
                AddCoin(_scratchX[s], _scratchY[s], _scratchZ[s], serial);
            }

            if (!_chunks.TryAdd(chunk))
            {
                ChunkOverflowCount++;
            }

            // GDD 10: maybe a power-up pickup in this chunk; a Speed Boost holds back vine sections.
            double vineBlock = _pickups.OnChunkSpawned(this, data.Kind, startZ, data.LengthM, serial);
            if (vineBlock > _generator.VineBlockedUntilZ)
            {
                _generator.VineBlockedUntilZ = vineBlock;
            }

            _generatedEndZ = startZ + data.LengthM;
        }

        private void AddObstacle(in ObstaclePlacement p, double startZ, int serial)
        {
            var o = new ObstacleInstance
            {
                Id = _nextObstacleId++,
                Archetype = p.Archetype,
                LaneMask = p.LaneMask,
                Z = startZ + p.Zc,
                ChunkSerial = serial,
                Phase = MoverPhase.Idle,
            };

            if (p.Archetype == ObstacleArchetype.Gap)
            {
                o.GapLengthM = p.GapLengthM;
                o.DepthM = p.GapLengthM;
                o.FromLane = (byte)LaneMasks.Lowest(p.LaneMask);
                o.ToLane = (byte)LaneMasks.Highest(p.LaneMask);
            }
            else if (p.Archetype == ObstacleArchetype.Mover)
            {
                int from = LaneMasks.Lowest(p.LaneMask);
                o.DepthM = _kit.Mover.DepthM;
                o.FromLane = (byte)from;
                o.ToLane = (byte)p.MoverToLane;
                o.MoverX = _runner.LaneCenterX(from);
                o.MoverXPrev = o.MoverX;
            }
            else
            {
                o.DepthM = _kit.GetShape(p.Archetype).DepthM;
                o.FromLane = (byte)LaneMasks.Lowest(p.LaneMask);
                o.ToLane = (byte)LaneMasks.Highest(p.LaneMask);
            }

            if (!_obstacles.TryAdd(o))
            {
                ObstacleOverflowCount++;
            }
        }

        private void AddCoin(float x, float y, double z, int serial)
        {
            var c = new CoinInstance
            {
                Id = _nextCoinId++,
                X = x,
                Y = y,
                Z = z,
                Lane = (byte)CoinLayout.NearestLane(x, _runner),
                ChunkSerial = serial,
            };

            if (!_coins.TryAdd(c))
            {
                CoinOverflowCount++;
            }
        }

        private void AddBonusCoin(float x, float y, double z)
        {
            if (_bonusCoinCount >= _bonusCoins.Length)
            {
                BonusCoinOverflowCount++;
                return;
            }

            _bonusCoins[_bonusCoinCount++] = new CoinInstance
            {
                Id = _nextCoinId++,
                X = x,
                Y = y,
                Z = z,
                Lane = (byte)CoinLayout.NearestLane(x, _runner),
                ChunkSerial = 0,
            };
        }

        /// <summary>Stable insertion sort of the scratch coins by z into <see cref="_order"/>.</summary>
        private void SortScratchByZ(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int item = i;
                int j = i - 1;
                while (j >= 0 && _scratchZ[_order[j]] > _scratchZ[item])
                {
                    _order[j + 1] = _order[j];
                    j--;
                }

                _order[j + 1] = item;
            }
        }

        // ---- Per-tick update ----

        private void DespawnBehind(double heroZ)
        {
            double limit = heroZ - _config.DespawnBehindM;
            while (_obstacles.Count > 0 && _obstacles[0].BackZ < limit)
            {
                _obstacles.RemoveFirst();
            }

            float coinBack = _coinConfig.CoinVisualRadiusM;
            while (_coins.Count > 0 && _coins[0].Z + coinBack < limit)
            {
                _coins.RemoveFirst();
            }

            while (_chunks.Count > 0 && _chunks[0].EndZ < limit)
            {
                _chunks.RemoveFirst();
            }

            while (_vineRing.Count > 0 && _vineRing[0].Z < limit)
            {
                _vineRing.RemoveFirst();
            }

            _pickups.Despawn(limit);

            int write = 0;
            for (int i = 0; i < _bonusCoinCount; i++)
            {
                if (_bonusCoins[i].Z + coinBack < limit)
                {
                    continue;
                }

                if (write != i)
                {
                    _bonusCoins[write] = _bonusCoins[i];
                }

                write++;
            }

            _bonusCoinCount = write;
        }

        private void UpdateMovers(in RunnerTickInfo info, RunnerSimulation runner)
        {
            double heroFront = info.FrontZ;
            double lead = _kit.MoverTriggerLeadS * info.Speed + MoverTriggerToleranceM;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                if (o.Archetype != ObstacleArchetype.Mover)
                {
                    continue;
                }

                o.MoverXPrev = o.MoverX;
                switch (o.Phase)
                {
                    case MoverPhase.Idle:
                        if (o.Z - heroFront <= lead)
                        {
                            o.Phase = MoverPhase.Moving;
                            o.MoverTicks = 0;
                            Emit(runner, new RunnerEvent
                            {
                                Type = RunnerEventType.MoverStarted,
                                Tick = info.Tick,
                                EntityId = o.Id,
                                Lane = o.FromLane,
                                Value = o.ToLane,
                                Dir = (sbyte)(o.ToLane > o.FromLane ? 1 : -1),
                                Archetype = (byte)ObstacleArchetype.Mover,
                            });
                        }

                        break;

                    case MoverPhase.Moving:
                        o.MoverTicks++;
                        float fromX = _runner.LaneCenterX(o.FromLane);
                        float toX = _runner.LaneCenterX(o.ToLane);
                        if (o.MoverTicks >= _moverTicks)
                        {
                            o.MoverX = toX;
                            o.Phase = MoverPhase.Settled;
                            Emit(runner, new RunnerEvent
                            {
                                Type = RunnerEventType.MoverSettled,
                                Tick = info.Tick,
                                EntityId = o.Id,
                                Lane = o.ToLane,
                                Archetype = (byte)ObstacleArchetype.Mover,
                            });
                        }
                        else
                        {
                            float dir = toX > fromX ? 1f : -1f;
                            o.MoverX = fromX + dir * _moverStepM * o.MoverTicks;
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Removes the obstacle with <paramref name="id"/> from the ring (smashed by a power-up, GDD 10). Returns false
        /// if it is not live. O(n); for single events, never per tick.
        /// </summary>
        public bool RemoveObstacle(int id)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                if (_obstacles[i].Id == id)
                {
                    _obstacles.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// GDD 8.3 telegraphed lane strike: dormant until HERO is <c>TriggerLeadS</c> away at his current speed,
        /// then Warning → Active → Rest → Warning … The box exists only while Active (see <see cref="GetBoxes"/>).
        /// </summary>
        private void UpdateLaneStrikes(in RunnerTickInfo info, RunnerSimulation runner)
        {
            double speed = info.Speed > 0.1 ? info.Speed : 0.1;
            double lead = _hazards.TriggerLeadS * speed;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ref ObstacleInstance o = ref _obstacles[i];
                if (o.Archetype != ObstacleArchetype.LaneStrike)
                {
                    continue;
                }

                switch (o.StrikePhase)
                {
                    case LaneStrikePhase.Dormant:
                        if (o.Z - info.FrontZ <= lead)
                        {
                            EnterStrikePhase(ref o, LaneStrikePhase.Warning, info.Tick, runner);
                        }

                        break;

                    case LaneStrikePhase.Warning:
                        if (++o.StrikeTicks >= _hazards.WarningTicks)
                        {
                            EnterStrikePhase(ref o, LaneStrikePhase.Active, info.Tick, runner);
                        }

                        break;

                    case LaneStrikePhase.Active:
                        if (++o.StrikeTicks >= _hazards.ActiveTicks)
                        {
                            EnterStrikePhase(ref o, LaneStrikePhase.Rest, info.Tick, runner);
                        }

                        break;

                    case LaneStrikePhase.Rest:
                        if (++o.StrikeTicks >= _hazards.RestTicks)
                        {
                            EnterStrikePhase(ref o, LaneStrikePhase.Warning, info.Tick, runner);
                        }

                        break;
                }
            }
        }

        private static void EnterStrikePhase(ref ObstacleInstance o, LaneStrikePhase phase, long tick, RunnerSimulation runner)
        {
            o.StrikePhase = phase;
            o.StrikeTicks = 0;
            if (phase != LaneStrikePhase.Warning && phase != LaneStrikePhase.Active)
            {
                return;
            }

            Emit(runner, new RunnerEvent
            {
                Type = phase == LaneStrikePhase.Warning ? RunnerEventType.HazardWarning : RunnerEventType.HazardStrike,
                Tick = tick,
                EntityId = o.Id,
                Lane = o.FromLane,
                Archetype = (byte)ObstacleArchetype.LaneStrike,
            });
        }

        private void UpdateEnteredChunks(in RunnerTickInfo info, RunnerSimulation runner)
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                ref ChunkInstance c = ref _chunks[i];
                if (c.Serial <= _enteredSerial)
                {
                    continue;
                }

                if (info.Z < c.StartZ)
                {
                    break;
                }

                _enteredSerial = c.Serial;
                _currentChunkIndex = c.ChunkIndex;
                Emit(runner, new RunnerEvent
                {
                    Type = RunnerEventType.ChunkEntered,
                    Tick = info.Tick,
                    EntityId = c.Serial,
                    Lane = c.Tier,
                    Value = (short)c.ChunkIndex,
                    Flags = TrackEventCodes.PackChunkFlags(c.Mirrored, c.Kind),
                });

                if (c.Tier != _currentTier)
                {
                    _currentTier = c.Tier;
                    Emit(runner, new RunnerEvent
                    {
                        Type = RunnerEventType.TierChanged,
                        Tick = info.Tick,
                        Value = c.Tier,
                    });
                }
            }
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
