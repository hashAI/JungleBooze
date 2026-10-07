using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Seeded chunk selection (spec 002 section 8). Plain C#; the only user of the run's
    /// <see cref="RandomStreamIds.TrackGeneration"/> stream. It only decides <i>which</i> chunk comes next; the
    /// <see cref="TrackSimulation"/> places it. No allocation after construction.
    /// <para>Draws (AC-221): 1 per breather interval (<c>NextFloat(min, max)</c>, at <see cref="Reset"/> and after
    /// every breather run), exactly 2 per normal pick attempt (<c>NextInt</c> over the pool weight, then
    /// <c>NextFloat</c> for the mirror, always drawn) and exactly 2 per breather pick (<c>NextInt</c> over the
    /// breather count, then the mirror draw). The start chunk and the seam fallback draw nothing.</para>
    /// <para>Vine sections (GDD 7.2, 7.5) use their own stream (<see cref="RandomStreamIds.VineSchedule"/>), so they
    /// never shift the track stream: 1 draw per section interval (at <see cref="Reset"/> and after every vine
    /// section), 2 per vine pick (<c>NextInt</c> over the eligible vine chunks, then the mirror draw). Without a
    /// vine stream no vine section is ever placed and generation is exactly the FP1 generation.</para>
    /// </summary>
    public sealed class TrackGenerator
    {
        private readonly TrackConfig _config;
        private readonly ChunkLibrary _library;
        private readonly DifficultyTiersConfig _tiers;
        private readonly SpeedCurve _curve;
        private readonly int _startIndex;
        private readonly int _fallbackIndex;
        private readonly int[] _history;
        private readonly bool[] _excluded;

        private IRandom _rng;
        private int _historyCount;
        private int _historyNext;
        private int _prevIndex;
        private bool _prevMirrored;
        private bool _startEmitted;
        private double _plannedNormalS;
        private double _breatherDueS;
        private bool _breatherPending;
        private bool _inBreatherRun;
        private double _breatherRunLengthM;
        private double _breatherRunRequiredM;

        private readonly VineConfig _vines;
        private IRandom _vineRng;
        private double _vineElapsedS;
        private double _vineDueS;

        // Worlds (GDD 9): null = a single world, exactly the generation without worlds.
        private readonly WorldScheduleConfig _worlds;
        private readonly int _gatewayIndex = -1;
        private readonly double _gatewayHalfM;
        private readonly double _maxVineLengthM;
        private double _worldStartZ;

        public TrackGenerator(
            TrackConfig config,
            ChunkLibrary library,
            DifficultyTiersConfig tiers,
            SpeedCurve curve,
            VineConfig vines = null,
            WorldScheduleConfig worlds = null)
        {
            _vines = vines ?? VineConfig.CreateDefault();
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));

            _startIndex = library.IndexOf(config.StartChunkId);
            if (_startIndex < 0)
            {
                throw new ArgumentException("The library has no start chunk '" + config.StartChunkId + "'.", nameof(library));
            }

            if (library.BreatherCount == 0)
            {
                throw new ArgumentException("The library needs at least one breather (breathers and the seam fallback).", nameof(library));
            }

            int fallback = library.IndexOf(JungleChunkLibraryDefaults.FallbackBreatherId);
            _fallbackIndex = fallback >= 0 && library[fallback].Kind == ChunkKind.Breather ? fallback : library.GetBreatherIndex(0);

            for (int t = 0; t < tiers.TierCount; t++)
            {
                if (tiers.GetTier(t).ChunkCount != library.Count)
                {
                    throw new ArgumentException("The difficulty tiers were built for another library.", nameof(tiers));
                }
            }

            _worlds = worlds;
            if (worlds != null)
            {
                _gatewayIndex = library.IndexOf(JungleChunkLibraryDefaults.GatewayId);
                if (_gatewayIndex < 0 || library[_gatewayIndex].Kind != ChunkKind.Gateway)
                {
                    throw new ArgumentException("Worlds need a gateway chunk '" + JungleChunkLibraryDefaults.GatewayId + "' in the library.", nameof(library));
                }

                _gatewayHalfM = library[_gatewayIndex].LengthM * 0.5;
                for (int i = 0; i < library.Count; i++)
                {
                    if (library[i].Kind == ChunkKind.Vine)
                    {
                        _maxVineLengthM = Math.Max(_maxVineLengthM, library[i].LengthM);
                    }
                }
            }

            _history = new int[Math.Max(1, config.NoRepeatWindow)];
            _excluded = new bool[library.Count];
            _prevIndex = -1;
        }

        public ChunkLibrary Library => _library;

        public DifficultyTiersConfig Tiers => _tiers;

        /// <summary>Times no attempt passed the seam table and the fallback breather was used (sim counter, S-213).</summary>
        public int SeamFallbackCount { get; private set; }

        /// <summary>Normal pick attempts so far (every attempt costs 2 draws).</summary>
        public int PickAttemptCount { get; private set; }

        /// <summary>Normal chunks picked so far.</summary>
        public int NormalPickCount { get; private set; }

        /// <summary>Breather chunks picked by the schedule (not counting seam fallbacks).</summary>
        public int BreatherPickCount { get; private set; }

        /// <summary>Breather intervals drawn so far (1 draw each).</summary>
        public int BreatherIntervalDrawCount { get; private set; }

        /// <summary>Planned run time of the normal chunks generated since the last breather (s).</summary>
        public double PlannedNormalSeconds => _plannedNormalS;

        /// <summary>The current breather interval (s).</summary>
        public double BreatherDueSeconds => _breatherDueS;

        /// <summary>World gateways placed so far (also the index of the current world segment).</summary>
        public int GatewaysEmitted { get; private set; }

        /// <summary>Vine sections picked so far.</summary>
        public int VinePickCount { get; private set; }

        /// <summary>Planned run time since the last vine section (or the run start) (s).</summary>
        public double VineElapsedSeconds => _vineElapsedS;

        /// <summary>Planned run time after which the next vine section is placed (s).</summary>
        public double VineDueSeconds => _vineDueS;

        /// <summary>
        /// No vine section starts before this z (GDD 7.2: never while a Speed Boost is active; the pickup placer
        /// raises it for every placed Speed Boost). Other systems may raise it too (for example Lift, GDD 15.1).
        /// Reset to negative infinity by <see cref="Reset(IRandom, IRandom)"/>.
        /// </summary>
        public double VineBlockedUntilZ { get; set; }

        /// <summary>
        /// Starts a new run on <paramref name="trackStream"/> (the forked <see cref="RandomStreamIds.TrackGeneration"/>
        /// stream). Clears all generator state and draws the first breather interval.
        /// </summary>
        public void Reset(IRandom trackStream)
        {
            Reset(trackStream, null);
        }

        /// <summary>
        /// Like <see cref="Reset(IRandom)"/>, with the run's forked <see cref="RandomStreamIds.VineSchedule"/> stream.
        /// Null (or vines disabled in the config) = no vine sections.
        /// </summary>
        public void Reset(IRandom trackStream, IRandom vineStream)
        {
            _rng = trackStream ?? throw new ArgumentNullException(nameof(trackStream));
            _vineRng = _vines.Enabled ? vineStream : null;
            _vineElapsedS = 0.0;
            VinePickCount = 0;
            VineBlockedUntilZ = double.NegativeInfinity;
            _vineDueS = _vineRng != null
                ? _vineRng.NextFloat(_vines.FirstSectionMinS, _vines.FirstSectionMaxS)
                : double.PositiveInfinity;
            GatewaysEmitted = 0;
            _worldStartZ = 0.0;
            _historyCount = 0;
            _historyNext = 0;
            for (int i = 0; i < _history.Length; i++)
            {
                _history[i] = -1;
            }

            _prevIndex = -1;
            _prevMirrored = false;
            _startEmitted = false;
            _plannedNormalS = 0.0;
            _breatherPending = false;
            _inBreatherRun = false;
            _breatherRunLengthM = 0.0;
            _breatherRunRequiredM = 0.0;
            SeamFallbackCount = 0;
            PickAttemptCount = 0;
            NormalPickCount = 0;
            BreatherPickCount = 0;
            BreatherIntervalDrawCount = 0;
            DrawBreatherInterval();
        }

        /// <summary>
        /// Picks the chunk that starts at <paramref name="startZ"/> (the end of the previous chunk; 0 for the first).
        /// The first call of a run always returns the start chunk. Deterministic; no allocation.
        /// </summary>
        public ChunkPick NextChunk(double startZ)
        {
            if (_rng == null)
            {
                throw new InvalidOperationException("Call Reset before generating.");
            }

            int tierIndex = _tiers.TierIndexAt(startZ);
            int tier = tierIndex + 1;

            if (!_startEmitted)
            {
                _startEmitted = true;
                Remember(_startIndex, false);
                return CountVineTime(new ChunkPick(_startIndex, false, tier, false), startZ);
            }

            // GDD 9: a gateway (no obstacles, coin arc) where the next world boundary falls. It uses no random draws,
            // so it never shifts the track or vine streams; the world switches at its centre.
            if (_worlds != null && startZ + _gatewayHalfM >= _worlds.SegmentStartZ(GatewaysEmitted + 1))
            {
                GatewaysEmitted++;
                _worldStartZ = startZ + _gatewayHalfM;
                Remember(_gatewayIndex, false);
                return CountVineTime(new ChunkPick(_gatewayIndex, false, tier, false), startZ);
            }

            if (_breatherPending || _inBreatherRun)
            {
                return CountVineTime(PickBreather(startZ, tier), startZ);
            }

            if (_vineRng != null && _vineElapsedS >= _vineDueS && startZ >= VineBlockedUntilZ && !VineWouldCrossBoundary(startZ))
            {
                int vine = PickVine(startZ, tier, out bool vineMirrored);
                if (vine >= 0)
                {
                    // Spec 002 seam rule: vine chunks are compatible with everything (not Normal).
                    Remember(vine, vineMirrored);
                    VinePickCount++;
                    _vineElapsedS = 0.0;
                    _vineDueS = _vineRng.NextFloat(_vines.SectionIntervalMinS, _vines.SectionIntervalMaxS);
                    return new ChunkPick(vine, vineMirrored, tier, false);
                }
            }

            return CountVineTime(PickNormal(startZ, tierIndex, tier), startZ);
        }

        /// <summary>Stable hash of the generator state (AC-242, AC-247).</summary>
        public ulong ComputeStateHash(ulong h)
        {
            h = StableHash.Mix(h, _historyCount);
            h = StableHash.Mix(h, _historyNext);
            for (int i = 0; i < _history.Length; i++)
            {
                h = StableHash.Mix(h, _history[i]);
            }

            h = StableHash.Mix(h, _prevIndex);
            h = StableHash.Mix(h, _prevMirrored);
            h = StableHash.Mix(h, _startEmitted);
            h = StableHash.Mix(h, _plannedNormalS);
            h = StableHash.Mix(h, _breatherDueS);
            h = StableHash.Mix(h, _breatherPending);
            h = StableHash.Mix(h, _inBreatherRun);
            h = StableHash.Mix(h, _breatherRunLengthM);
            h = StableHash.Mix(h, _breatherRunRequiredM);
            h = StableHash.Mix(h, SeamFallbackCount);
            h = StableHash.Mix(h, PickAttemptCount);
            h = StableHash.Mix(h, _vineElapsedS);
            h = StableHash.Mix(h, _vineDueS);
            h = StableHash.Mix(h, VinePickCount);
            h = StableHash.Mix(h, VineBlockedUntilZ);
            if (_worlds != null)
            {
                h = StableHash.Mix(h, GatewaysEmitted);
                h = StableHash.Mix(h, _worldStartZ);
            }

            return h;
        }

        /// <summary>Adds the planned run time of a picked chunk to the vine clock.</summary>
        private ChunkPick CountVineTime(ChunkPick pick, double startZ)
        {
            _vineElapsedS += _library[pick.ChunkIndex].LengthM / PlannedSpeed(startZ);
            return pick;
        }

        /// <summary>
        /// Picks a vine chunk eligible for the tier (and for chasms, the distance) uniformly with the vine stream.
        /// Returns -1 (no draws) when none is eligible; the section then waits for the next chunk.
        /// </summary>
        private int PickVine(double startZ, int tier, out bool mirrored)
        {
            mirrored = false;
            int eligible = 0;
            for (int i = 0; i < _library.Count; i++)
            {
                if (IsVineEligible(_library[i], startZ, tier))
                {
                    eligible++;
                }
            }

            if (eligible == 0)
            {
                return -1;
            }

            int r = _vineRng.NextInt(0, eligible); // draw 1
            bool mirror = _vineRng.NextFloat() < _config.MirrorChance; // draw 2, always drawn
            for (int i = 0; i < _library.Count; i++)
            {
                if (!IsVineEligible(_library[i], startZ, tier))
                {
                    continue;
                }

                if (r == 0)
                {
                    mirrored = mirror && _library[i].AllowMirror;
                    return i;
                }

                r--;
            }

            return -1;
        }

        private bool IsVineEligible(ChunkData chunk, double startZ, int tier)
        {
            if (chunk.Kind != ChunkKind.Vine || chunk.VineCount == 0 || tier < chunk.MinTier || tier > chunk.MaxTier)
            {
                return false;
            }

            return !chunk.HasChasmVine || startZ >= _vines.ChasmVinesFromM;
        }

        /// <summary>True when a vine section started here could still be running when the next gateway is due.</summary>
        private bool VineWouldCrossBoundary(double startZ)
        {
            return _worlds != null && startZ + _maxVineLengthM + _gatewayHalfM >= _worlds.SegmentStartZ(GatewaysEmitted + 1);
        }

        private ChunkPick PickNormal(double startZ, int tierIndex, int tier)
        {
            DifficultyTier pool = _tiers.GetTier(tierIndex);
            int total = BuildPool(pool, true, startZ);
            if (total <= 0)
            {
                // Spec 8.3: if the no-repeat rule empties the pool, ignore it.
                total = BuildPool(pool, false, startZ);
            }

            if (total <= 0)
            {
                // World rules (chunk world masks, signature quiet distance) left nothing: breather, no draws.
                SeamFallbackCount++;
                Remember(_fallbackIndex, false);
                return new ChunkPick(_fallbackIndex, false, tier, true);
            }

            int attempts = _config.MaxPickAttempts;
            for (int a = 0; a < attempts; a++)
            {
                PickAttemptCount++;
                int r = _rng.NextInt(0, total); // draw 1
                int chunk = WalkPool(pool, r);
                bool mirror = _rng.NextFloat() < _config.MirrorChance; // draw 2, always drawn
                mirror = mirror && _library[chunk].AllowMirror;
                if (_library.IsSeamCompatible(_prevIndex, _prevMirrored, chunk, mirror))
                {
                    NormalPickCount++;
                    PushHistory(chunk);
                    Remember(chunk, mirror);
                    _plannedNormalS += _library[chunk].LengthM / PlannedSpeed(startZ);
                    if (_plannedNormalS >= _breatherDueS)
                    {
                        _breatherPending = true;
                    }

                    return new ChunkPick(chunk, mirror, tier, false);
                }
            }

            SeamFallbackCount++;
            Remember(_fallbackIndex, false);
            return new ChunkPick(_fallbackIndex, false, tier, true);
        }

        private ChunkPick PickBreather(double startZ, int tier)
        {
            if (!_inBreatherRun)
            {
                _inBreatherRun = true;
                _breatherPending = false;
                _breatherRunLengthM = 0.0;
                _breatherRunRequiredM = _config.BreatherMinDurationS * PlannedSpeed(startZ);
            }

            int k = _rng.NextInt(0, _library.BreatherCount); // draw 1
            int chunk = _library.GetBreatherIndex(k);
            bool mirror = _rng.NextFloat() < _config.MirrorChance; // draw 2, always drawn
            mirror = mirror && _library[chunk].AllowMirror;
            BreatherPickCount++;
            Remember(chunk, mirror);

            _breatherRunLengthM += _library[chunk].LengthM;
            if (_breatherRunLengthM >= _breatherRunRequiredM)
            {
                _inBreatherRun = false;
                _plannedNormalS = 0.0;
                DrawBreatherInterval();
            }

            return new ChunkPick(chunk, mirror, tier, false);
        }

        /// <summary>Marks excluded chunks and returns the pool's total weight.</summary>
        private int BuildPool(DifficultyTier pool, bool applyNoRepeat, double startZ)
        {
            int total = 0;
            WorldMask worldMask = WorldMask.All;
            bool signatureQuiet = false;
            if (_worlds != null)
            {
                // GDD 8.3: only the worlds that own a signature hazard get it, and never in the first 150 m of a world.
                worldMask = WorldScheduleConfig.MaskOf(_worlds.KindOfSegment(GatewaysEmitted));
                signatureQuiet = startZ < _worldStartZ + _worlds.SignatureQuietM;
            }

            for (int i = 0; i < _excluded.Length; i++)
            {
                ChunkData candidate = _library[i];
                bool excluded = pool.GetWeight(i) <= 0
                    || (applyNoRepeat && InHistory(i))
                    || (candidate.WorldMask & worldMask) == WorldMask.None
                    || (signatureQuiet && candidate.Kind == ChunkKind.Signature);
                _excluded[i] = excluded;
                if (!excluded)
                {
                    total += pool.GetWeight(i);
                }
            }

            return total;
        }

        /// <summary>Walks the pool in library order, subtracting weights, until r &lt; weight (spec 8.3).</summary>
        private int WalkPool(DifficultyTier pool, int r)
        {
            int last = -1;
            for (int i = 0; i < _excluded.Length; i++)
            {
                if (_excluded[i])
                {
                    continue;
                }

                int w = pool.GetWeight(i);
                if (r < w)
                {
                    return i;
                }

                r -= w;
                last = i;
            }

            return last;
        }

        private bool InHistory(int chunk)
        {
            int window = _config.NoRepeatWindow;
            for (int i = 0; i < _historyCount && i < window; i++)
            {
                if (_history[i] == chunk)
                {
                    return true;
                }
            }

            return false;
        }

        private void PushHistory(int chunk)
        {
            if (_config.NoRepeatWindow <= 0)
            {
                return;
            }

            _history[_historyNext] = chunk;
            _historyNext = (_historyNext + 1) % _history.Length;
            if (_historyCount < _history.Length)
            {
                _historyCount++;
            }
        }

        private void Remember(int chunk, bool mirrored)
        {
            _prevIndex = chunk;
            _prevMirrored = mirrored;
        }

        private void DrawBreatherInterval()
        {
            _breatherDueS = _rng.NextFloat(_config.BreatherIntervalMinS, _config.BreatherIntervalMaxS);
            BreatherIntervalDrawCount++;
        }

        private double PlannedSpeed(double startZ)
        {
            double v = _curve.Evaluate(startZ);
            return v > 0.0 ? v : 1.0;
        }
    }
}
