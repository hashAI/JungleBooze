using System;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// The streamed, endless path (spec 102): chunk instances placed end to end along s, implementing
    /// <see cref="IPathQuery"/> for the simulation and the bots. Obstacles, coins, splits, crystals, power-ups,
    /// discovery triggers and help markers of the placed chunks live in fixed-size rings with increasing ids
    /// (slot = id % capacity), so <see cref="Append"/> and <see cref="Retire"/> never allocate and the simulation's
    /// per-item state survives slot reuse (<see cref="IPathQuery"/> id rules). Queries are allocation-free.
    /// Traversal (<see cref="ITraversalQuery"/>, spec 103) is answered from the chunk at s; vines and deep-dive zones
    /// get ids serial × <see cref="ChunkRuntime.LocalIdStride"/> + index. The view-side centreline is the chain of
    /// chunk curves (<see cref="GetFrame"/>), tangent-continuous across seams.
    /// </summary>
    public sealed class WorldPath : IPathQuery, ITraversalQuery
    {
        public const int ChunkCapacity = 16;
        public const int ObstacleCapacity = 512;
        public const int CoinCapacity = 4096;
        public const int ForkCapacity = 64;
        public const int CrystalCapacity = 128;
        public const int PowerUpCapacity = 32;
        public const int DiscoveryCapacity = 64;
        public const int HelpCapacity = 64;

        private readonly PlacedChunk[] _chunks = new PlacedChunk[ChunkCapacity];
        private readonly ObstacleBox[] _obstacles = new ObstacleBox[ObstacleCapacity];
        private readonly CoinPoint[] _coins = new CoinPoint[CoinCapacity];
        private readonly ForkPoint[] _forks = new ForkPoint[ForkCapacity];
        private readonly int[] _forkChunk = new int[ForkCapacity];
        private readonly int[] _forkLocal = new int[ForkCapacity];
        private readonly CoinPoint[] _crystals = new CoinPoint[CrystalCapacity];
        private readonly PowerUpPoint[] _powerUps = new PowerUpPoint[PowerUpCapacity];
        private readonly DiscoveryPoint[] _discoveries = new DiscoveryPoint[DiscoveryCapacity];
        private readonly HelpPoint[] _help = new HelpPoint[HelpCapacity];

        private int _firstChunk;
        private int _nextChunk;
        private int _cachedChunk;
        private int _obstacleFirst;
        private int _obstacleNext;
        private int _coinFirst;
        private int _coinNext;
        private int _forkFirst;
        private int _forkNext;
        private int _crystalFirst;
        private int _crystalNext;
        private int _powerUpFirst;
        private int _powerUpNext;
        private int _discoveryFirst;
        private int _discoveryNext;
        private int _helpFirst;
        private int _helpNext;
        private float _maxObstacleLength;

        public WorldPath()
        {
            Reset();
        }

        public float FinishS { get; set; } = float.PositiveInfinity;

        /// <summary>Incremented on every append/retire (views poll it).</summary>
        public int Revision { get; private set; }

        /// <summary>Serial of the oldest placed chunk.</summary>
        public int FirstChunkSerial => _firstChunk;

        /// <summary>Serial the next appended chunk gets.</summary>
        public int NextChunkSerial => _nextChunk;

        public int ChunkCount => _nextChunk - _firstChunk;

        public float StartS => ChunkCount > 0 ? Chunk(_firstChunk).StartS : 0f;

        public float EndS => ChunkCount > 0 ? Chunk(_nextChunk - 1).EndS : 0f;

        public int ObstacleSlots => ObstacleCapacity;

        public int CoinSlots => CoinCapacity;

        public int ForkCount => _forkNext - _forkFirst;

        public int FirstObstacleId => _obstacleFirst;

        public int NextObstacleId => _obstacleNext;

        public int FirstCoinId => _coinFirst;

        public int NextCoinId => _coinNext;

        public int FirstCrystalId => _crystalFirst;

        public int NextCrystalId => _crystalNext;

        public int FirstPowerUpId => _powerUpFirst;

        public int NextPowerUpId => _powerUpNext;

        public int FirstDiscoveryId => _discoveryFirst;

        public int NextDiscoveryId => _discoveryNext;

        public int FirstHelpId => _helpFirst;

        public int NextHelpId => _helpNext;

        public void Reset()
        {
            _firstChunk = 0;
            _nextChunk = 0;
            _cachedChunk = 0;
            _obstacleFirst = _obstacleNext = 0;
            _coinFirst = _coinNext = 0;
            _forkFirst = _forkNext = 0;
            _crystalFirst = _crystalNext = 0;
            _powerUpFirst = _powerUpNext = 0;
            _discoveryFirst = _discoveryNext = 0;
            _helpFirst = _helpNext = 0;
            _maxObstacleLength = 0f;
            Revision++;
        }

        /// <summary>The placed chunk with this serial (must be live).</summary>
        public ref readonly PlacedChunk Chunk(int serial)
        {
            return ref _chunks[serial % ChunkCapacity];
        }

        public bool IsLive(int serial)
        {
            return serial >= _firstChunk && serial < _nextChunk;
        }

        /// <summary>Places a chunk after the current end. Allocation-free; throws if a ring would overflow (content bug).</summary>
        public void Append(ChunkRuntime chunk, in ChunkPick pick)
        {
            if (chunk == null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            if (ChunkCount >= ChunkCapacity || _obstacleNext - _obstacleFirst + chunk.ObstacleCount > ObstacleCapacity ||
                _coinNext - _coinFirst + chunk.CoinCount > CoinCapacity || _forkNext - _forkFirst + chunk.DividerCount > ForkCapacity ||
                _crystalNext - _crystalFirst + chunk.CrystalCount + 1 > CrystalCapacity ||
                _powerUpNext - _powerUpFirst + 1 > PowerUpCapacity || _discoveryNext - _discoveryFirst + chunk.DiscoveryCount > DiscoveryCapacity ||
                _helpNext - _helpFirst + chunk.HelpCount > HelpCapacity)
            {
                throw new InvalidOperationException("WorldPath capacity exceeded: retire chunks behind the runner before appending.");
            }

            float start = EndS;
            int serial = _nextChunk;
            PathFrame frame = default;
            if (ChunkCount > 0)
            {
                ref readonly PlacedChunk last = ref Chunk(_nextChunk - 1);
                PathCurve c = last.Chunk.Curve;
                frame = PathFrame.Compose(last.Start, c.EndX, c.EndZ, c.EndHeading);
            }

            ref PlacedChunk placed = ref _chunks[serial % ChunkCapacity];
            placed.Chunk = chunk;
            placed.Pick = pick;
            placed.Serial = serial;
            placed.StartS = start;
            placed.Start = frame;

            placed.ObstacleBase = _obstacleNext;
            for (int i = 0; i < chunk.ObstacleCount; i++)
            {
                ObstacleBox o = chunk.GetObstacle(i);
                int id = _obstacleNext++;
                _obstacles[id % ObstacleCapacity] = new ObstacleBox(id, o.Class, o.SMin + start, o.SMax + start, o.XMin, o.XMax, o.YMin, o.YMax, o.WalkableTop);
                if (o.SMax - o.SMin > _maxObstacleLength)
                {
                    _maxObstacleLength = o.SMax - o.SMin;
                }
            }

            placed.CoinBase = _coinNext;
            placed.CrystalBase = _crystalNext;
            float density = pick.CoinDensity > 0f && pick.CoinDensity < 1f ? pick.CoinDensity : 1f;
            for (int i = 0; i < chunk.CoinCount; i++)
            {
                CoinPoint c = chunk.GetCoin(i);
                if (i == pick.FlowCrystalCoin)
                {
                    int crystal = _crystalNext++;
                    _crystals[crystal % CrystalCapacity] = new CoinPoint(crystal, c.S + start, c.X, c.Y);
                    continue;
                }

                if (density < 1f && (int)((i + 1) * density) == (int)(i * density))
                {
                    continue;
                }

                int id = _coinNext++;
                _coins[id % CoinCapacity] = new CoinPoint(id, c.S + start, c.X, c.Y);
            }

            placed.CoinCount = _coinNext - placed.CoinBase;

            for (int i = 0; i < chunk.CrystalCount; i++)
            {
                CrystalAnchor a = chunk.GetCrystal(i);
                if (a.Always || (i < 31 && (pick.CrystalMask & (1 << i)) != 0))
                {
                    int id = _crystalNext++;
                    _crystals[id % CrystalCapacity] = new CoinPoint(id, a.S + start, a.X, a.Y);
                }
            }

            placed.CrystalCount = _crystalNext - placed.CrystalBase;

            placed.ForkBase = _forkNext;
            for (int i = 0; i < chunk.DividerCount; i++)
            {
                ChunkDivider d = chunk.GetDivider(i);
                int id = _forkNext++;
                int slot = id % ForkCapacity;
                _forks[slot] = new ForkPoint(id, d.SFront + start, d.SMerge + start, d.CenterX, d.HalfWidth, d.SafeSide);
                _forkChunk[slot] = serial;
                _forkLocal[slot] = i;
            }

            placed.PowerUpBase = _powerUpNext;
            if (pick.PowerUp != PowerUpKind.None && pick.PowerUpSlot >= 0 && pick.PowerUpSlot < chunk.PowerUpSlotCount)
            {
                PowerUpSlot p = chunk.GetPowerUpSlot(pick.PowerUpSlot);
                int id = _powerUpNext++;
                _powerUps[id % PowerUpCapacity] = new PowerUpPoint(id, pick.PowerUp, p.S + start, p.X, p.Y);
            }

            placed.PowerUpCount = _powerUpNext - placed.PowerUpBase;

            placed.DiscoveryBase = _discoveryNext;
            for (int i = 0; i < chunk.DiscoveryCount; i++)
            {
                DiscoveryTrigger t = chunk.GetDiscovery(i);
                int id = _discoveryNext++;
                _discoveries[id % DiscoveryCapacity] = new DiscoveryPoint(id, serial, i, t.S + start, t.XMin, t.XMax);
            }

            placed.HelpBase = _helpNext;
            for (int i = 0; i < chunk.HelpCount; i++)
            {
                HelpMarker h = chunk.GetHelp(i);
                int id = _helpNext++;
                _help[id % HelpCapacity] = new HelpPoint(id, h.Move, h.S + start, h.XMin, h.XMax);
            }

            _nextChunk++;
            Revision++;
        }

        /// <summary>Drops placed chunks that end before <paramref name="s"/> (keeps at least one).</summary>
        public void Retire(float s)
        {
            bool changed = false;
            while (ChunkCount > 1 && Chunk(_firstChunk).EndS < s)
            {
                _firstChunk++;
                ref readonly PlacedChunk next = ref Chunk(_firstChunk);
                _obstacleFirst = next.ObstacleBase;
                _coinFirst = next.CoinBase;
                _forkFirst = next.ForkBase;
                _crystalFirst = next.CrystalBase;
                _powerUpFirst = next.PowerUpBase;
                _discoveryFirst = next.DiscoveryBase;
                _helpFirst = next.HelpBase;
                changed = true;
            }

            if (changed)
            {
                if (_cachedChunk < _firstChunk)
                {
                    _cachedChunk = _firstChunk;
                }

                Revision++;
            }
        }

        /// <summary>Serial of the placed chunk containing s (clamped to the first/last).</summary>
        public int ChunkAt(float s)
        {
            if (ChunkCount == 0)
            {
                return -1;
            }

            int c = _cachedChunk;
            if (c < _firstChunk || c >= _nextChunk)
            {
                c = _firstChunk;
            }

            ref readonly PlacedChunk cached = ref Chunk(c);
            if (s >= cached.StartS && s < cached.EndS)
            {
                return c;
            }

            if (s < Chunk(_firstChunk).StartS)
            {
                return _firstChunk;
            }

            for (int i = _firstChunk; i < _nextChunk; i++)
            {
                if (s < Chunk(i).EndS)
                {
                    _cachedChunk = i;
                    return i;
                }
            }

            return _nextChunk - 1;
        }

        public void GetLateralBounds(float s, float x, out float xMin, out float xMax)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                xMin = -3.5f;
                xMax = 3.5f;
                return;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            p.Chunk.GetLateralBounds(s - p.StartS, x, out xMin, out xMax);
        }

        /// <summary>Outer edges ignoring dividers (views).</summary>
        public void GetOuterBounds(float s, out float xMin, out float xMax)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                xMin = -3.5f;
                xMax = 3.5f;
                return;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            p.Chunk.GetOuterBounds(s - p.StartS, out xMin, out xMax);
        }

        public bool TryGetFloor(float s, float x, out float floorY)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                floorY = 0f;
                return true;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            return p.Chunk.TryGetFloor(s - p.StartS, x, out floorY);
        }

        public bool TryFindFloorAhead(float s, float x, float reach, out float lipS, out float lipY)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                lipS = 0f;
                lipY = 0f;
                return false;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            if (p.Chunk.TryFindFloorAhead(s - p.StartS, x, reach, out lipS, out lipY))
            {
                lipS += p.StartS;
                return true;
            }

            return false;
        }

        /// <summary>Route of the lane at (s, x) and the chunk it belongs to.</summary>
        public RouteType RouteAt(float s, float x)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                return RouteType.Main;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            return p.Chunk.RouteAt(s - p.StartS, x);
        }

        public ObstacleBox GetObstacle(int id)
        {
            return _obstacles[id % ObstacleCapacity];
        }

        public int FindObstacles(float sMin, float sMax, int[] results)
        {
            int count = 0;
            int lo = _obstacleFirst;
            int hi = _obstacleNext;
            float from = sMin - _maxObstacleLength;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_obstacles[mid % ObstacleCapacity].SMin < from)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            for (int id = lo; id < _obstacleNext; id++)
            {
                ObstacleBox box = _obstacles[id % ObstacleCapacity];
                if (box.SMin > sMax)
                {
                    break;
                }

                if (box.SMax >= sMin)
                {
                    if (count == results.Length)
                    {
                        break;
                    }

                    results[count++] = id;
                }
            }

            return count;
        }

        public CoinPoint GetCoin(int id)
        {
            return _coins[id % CoinCapacity];
        }

        public int FindCoins(float sMin, float sMax, int[] results)
        {
            int lo = _coinFirst;
            int hi = _coinNext;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_coins[mid % CoinCapacity].S < sMin)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            int count = 0;
            for (int id = lo; id < _coinNext && count < results.Length; id++)
            {
                if (_coins[id % CoinCapacity].S > sMax)
                {
                    break;
                }

                results[count++] = id;
            }

            return count;
        }

        public ForkPoint GetFork(int index)
        {
            return _forks[(_forkFirst + index) % ForkCapacity];
        }

        /// <summary>The chunk serial and local divider index of a live fork id.</summary>
        public bool TryGetForkChunk(int forkId, out int chunkSerial, out int localDivider)
        {
            if (forkId < _forkFirst || forkId >= _forkNext)
            {
                chunkSerial = -1;
                localDivider = -1;
                return false;
            }

            int slot = forkId % ForkCapacity;
            chunkSerial = _forkChunk[slot];
            localDivider = _forkLocal[slot];
            return true;
        }

        public CoinPoint GetCrystal(int id)
        {
            return _crystals[id % CrystalCapacity];
        }

        public PowerUpPoint GetPowerUp(int id)
        {
            return _powerUps[id % PowerUpCapacity];
        }

        public DiscoveryPoint GetDiscovery(int id)
        {
            return _discoveries[id % DiscoveryCapacity];
        }

        public HelpPoint GetHelp(int id)
        {
            return _help[id % HelpCapacity];
        }

        // ---- View-side centreline (spec 102 §2.1) ----

        /// <summary>World centreline point and heading at s (straight extrapolation before the first / after the last chunk).</summary>
        public PathFrame GetFrame(float s)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                return new PathFrame { X = 0f, Z = s, Heading = 0f };
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            p.Chunk.Curve.Evaluate(s - p.StartS, out float lx, out float lz, out float lh);
            return PathFrame.Compose(p.Start, lx, lz, lh);
        }

        // ---- ITraversalQuery (spec 103) ----

        public bool TryGetWater(float s, float x, out float surfaceY)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                surfaceY = 0f;
                return false;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            return p.Chunk.TryGetWater(s - p.StartS, x, out surfaceY);
        }

        public void GetCurrent(float s, float x, out float lateral, out float forward)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                lateral = 0f;
                forward = 0f;
                return;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            if (!p.Chunk.TryGetWater(s - p.StartS, x, out _))
            {
                lateral = 0f;
                forward = 0f;
                return;
            }

            p.Chunk.GetCurrentAt(s - p.StartS, out lateral, out forward);
        }

        public bool TryFindVine(float s, float before, float after, out VinePoint vine)
        {
            int c = ChunkAt(s);
            if (c >= 0)
            {
                ref readonly PlacedChunk p = ref Chunk(c);
                int i = p.Chunk.FindVine(s - p.StartS, before, after);
                if (i >= 0)
                {
                    vine = MakeVine(p, i);
                    return true;
                }
            }

            vine = default;
            return false;
        }

        public bool TryGetVine(int id, out VinePoint vine)
        {
            int serial = id >= 0 ? id / ChunkRuntime.LocalIdStride : -1;
            int i = id >= 0 ? id % ChunkRuntime.LocalIdStride : -1;
            if (serial < 0 || !IsLive(serial) || i >= Chunk(serial).Chunk.VineCount)
            {
                vine = default;
                return false;
            }

            vine = MakeVine(Chunk(serial), i);
            return true;
        }

        public bool TryFindDeepDive(float s, float x, out DeepDivePoint zone)
        {
            int c = ChunkAt(s);
            if (c >= 0)
            {
                ref readonly PlacedChunk p = ref Chunk(c);
                float local = s - p.StartS;
                int i = p.Chunk.FindDeepDive(local, x);
                if (i >= 0)
                {
                    DeepDiveZone z = p.Chunk.GetDeepDive(i);
                    p.Chunk.TryGetWater(local, x, out float surface);
                    zone = new DeepDivePoint((p.Serial * ChunkRuntime.LocalIdStride) + i, z.SMin + p.StartS, z.SMax + p.StartS, z.XMin, z.XMax, z.ExitS + p.StartS, z.ExitX, surface);
                    return true;
                }
            }

            zone = default;
            return false;
        }

        public bool IsCanopy(float s)
        {
            int c = ChunkAt(s);
            if (c < 0)
            {
                return false;
            }

            ref readonly PlacedChunk p = ref Chunk(c);
            return p.Chunk.IsCanopy(s - p.StartS);
        }

        private static VinePoint MakeVine(in PlacedChunk p, int i)
        {
            VineAnchor a = p.Chunk.GetVine(i);
            float takeoff = p.Chunk.GetVineTakeoffY(i);
            return new VinePoint((p.Serial * ChunkRuntime.LocalIdStride) + i, a.AnchorS + p.StartS, a.X, takeoff + a.AnchorHeight, a.Length, a.LipS + p.StartS, a.LandingS + p.StartS, takeoff, p.Serial);
        }

        /// <summary>Label of a live obstacle id ("C12 Low @90"), for death reports.</summary>
        public string GetObstacleLabel(int id)
        {
            for (int c = _firstChunk; c < _nextChunk; c++)
            {
                ref readonly PlacedChunk p = ref Chunk(c);
                if (id >= p.ObstacleBase && id < p.ObstacleBase + p.Chunk.ObstacleCount)
                {
                    return p.Chunk.GetLabel(id - p.ObstacleBase);
                }
            }

            return "-";
        }

        /// <summary>Serial of the chunk an obstacle id belongs to, or −1.</summary>
        public int ChunkOfObstacle(int id)
        {
            for (int c = _firstChunk; c < _nextChunk; c++)
            {
                ref readonly PlacedChunk p = ref Chunk(c);
                if (id >= p.ObstacleBase && id < p.ObstacleBase + p.Chunk.ObstacleCount)
                {
                    return c;
                }
            }

            return -1;
        }
    }
}
