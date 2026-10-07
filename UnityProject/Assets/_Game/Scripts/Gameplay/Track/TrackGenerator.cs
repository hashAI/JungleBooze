using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;

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

        public TrackGenerator(TrackConfig config, ChunkLibrary library, DifficultyTiersConfig tiers, SpeedCurve curve)
        {
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

        /// <summary>
        /// Starts a new run on <paramref name="trackStream"/> (the forked <see cref="RandomStreamIds.TrackGeneration"/>
        /// stream). Clears all generator state and draws the first breather interval.
        /// </summary>
        public void Reset(IRandom trackStream)
        {
            _rng = trackStream ?? throw new ArgumentNullException(nameof(trackStream));
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
                return new ChunkPick(_startIndex, false, tier, false);
            }

            if (_breatherPending || _inBreatherRun)
            {
                return PickBreather(startZ, tier);
            }

            return PickNormal(startZ, tierIndex, tier);
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
            return h;
        }

        private ChunkPick PickNormal(double startZ, int tierIndex, int tier)
        {
            DifficultyTier pool = _tiers.GetTier(tierIndex);
            int total = BuildPool(pool, true);
            if (total <= 0)
            {
                // Spec 8.3: if the no-repeat rule empties the pool, ignore it.
                total = BuildPool(pool, false);
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
        private int BuildPool(DifficultyTier pool, bool applyNoRepeat)
        {
            int total = 0;
            for (int i = 0; i < _excluded.Length; i++)
            {
                bool excluded = pool.GetWeight(i) <= 0 || (applyNoRepeat && InHistory(i));
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
