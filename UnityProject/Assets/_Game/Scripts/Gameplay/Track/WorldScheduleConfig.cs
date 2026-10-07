using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// World order and lengths by distance (GDD 9). Immutable and plain C#: the run walks the worlds in order and then
    /// loops with the same lengths; the Jungle of every later lap uses the dusk lighting preset (GDD 9, style guide
    /// section 5). <b>Segment</b> k is the k-th stretch of the run (0 = the first Jungle); the world of segment k is
    /// world <c>k mod count</c>. Nominal boundaries come from the lengths; the actual switch happens at the centre of
    /// the gateway chunk the generator places at each boundary (<see cref="TrackSimulation.WorldSegmentAt"/>).
    /// Lookups do not allocate.
    /// </summary>
    public sealed class WorldScheduleConfig
    {
        public const int MaxWorlds = 8;

        private readonly WorldKind[] _kinds;
        private readonly double[] _starts;
        private readonly double _periodM;

        /// <param name="worlds">The worlds in run order with their lengths in metres.</param>
        /// <param name="signatureQuietM">No signature hazard chunk this close after a world switch or the run start (GDD 8.3: 150 m).</param>
        /// <param name="duskFromLap">First lap (0-based) whose Jungle uses the dusk lighting (GDD 9: lap 1).</param>
        public WorldScheduleConfig(IList<WorldDesign> worlds, float signatureQuietM, int duskFromLap)
        {
            var errors = new List<string>();
            if (!Validate(worlds, signatureQuietM, duskFromLap, errors))
            {
                throw new ArgumentException("Invalid world schedule: " + string.Join(" ", errors), nameof(worlds));
            }

            int n = worlds.Count;
            _kinds = new WorldKind[n];
            _starts = new double[n];
            double at = 0.0;
            for (int i = 0; i < n; i++)
            {
                _kinds[i] = worlds[i].Kind;
                _starts[i] = at;
                at += worlds[i].LengthM;
            }

            _periodM = at;
            SignatureQuietM = signatureQuietM;
            DuskFromLap = duskFromLap;
        }

        /// <summary>Number of worlds per lap.</summary>
        public int Count => _kinds.Length;

        /// <summary>Total length of one lap in metres.</summary>
        public double PeriodM => _periodM;

        public float SignatureQuietM { get; }

        public int DuskFromLap { get; }

        /// <summary>GDD 9 [ASSUMED lengths for the loop]: Jungle 1100, River 1200, Mountains 1300, Ruins 1400 m.</summary>
        public static WorldScheduleConfig CreateDefault()
        {
            return new WorldScheduleConfig(
                new[]
                {
                    new WorldDesign(WorldKind.Jungle, 1100f),
                    new WorldDesign(WorldKind.River, 1200f),
                    new WorldDesign(WorldKind.Mountains, 1300f),
                    new WorldDesign(WorldKind.Ruins, 1400f),
                },
                150f,
                1);
        }

        /// <summary>The bit of <paramref name="kind"/> in a <see cref="WorldMask"/>.</summary>
        public static WorldMask MaskOf(WorldKind kind)
        {
            return (WorldMask)(1 << (int)kind);
        }

        /// <summary>World of segment <paramref name="segment"/> (0-based, run-global).</summary>
        public WorldKind KindOfSegment(int segment)
        {
            return _kinds[segment % _kinds.Length];
        }

        /// <summary>True when the segment is a Jungle of a later lap (the dusk lighting variant).</summary>
        public bool IsDuskSegment(int segment)
        {
            int n = _kinds.Length;
            return _kinds[segment % n] == WorldKind.Jungle && segment / n >= DuskFromLap;
        }

        /// <summary>Nominal start distance of a segment (the boundary the gateway is placed at). Segment 0 starts at 0.</summary>
        public double SegmentStartZ(int segment)
        {
            int n = _kinds.Length;
            return (segment / n) * _periodM + _starts[segment % n];
        }

        /// <summary>Segment that contains the nominal distance <paramref name="z"/> (negative z = segment 0).</summary>
        public int SegmentAt(double z)
        {
            if (!(z > 0.0))
            {
                return 0;
            }

            long lap = (long)Math.Floor(z / _periodM);
            double rest = z - lap * _periodM;
            int index = 0;
            for (int i = 1; i < _starts.Length; i++)
            {
                if (_starts[i] <= rest)
                {
                    index = i;
                }
            }

            return (int)(lap * _kinds.Length + index);
        }

        public WorldKind KindAt(double z)
        {
            return KindOfSegment(SegmentAt(z));
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, _kinds.Length);
            for (int i = 0; i < _kinds.Length; i++)
            {
                h = StableHash.Mix(h, (int)_kinds[i]);
                h = StableHash.Mix(h, _starts[i]);
            }

            h = StableHash.Mix(h, _periodM);
            h = StableHash.Mix(h, SignatureQuietM);
            return StableHash.Mix(h, DuskFromLap);
        }

        /// <summary>Range checks: 1 to 8 worlds, each at least 400 m (a gateway must fit), no negative quiet distance.</summary>
        public static bool Validate(IList<WorldDesign> worlds, float signatureQuietM, int duskFromLap, List<string> errors)
        {
            int before = errors.Count;
            if (worlds == null || worlds.Count == 0 || worlds.Count > MaxWorlds)
            {
                errors.Add("The world list needs 1 to " + MaxWorlds + " worlds.");
                return false;
            }

            for (int i = 0; i < worlds.Count; i++)
            {
                if (!(worlds[i].LengthM >= MinWorldLengthM))
                {
                    errors.Add("world " + (i + 1) + ": length must be at least " + MinWorldLengthM + " m.");
                }
            }

            if (!(signatureQuietM >= 0f))
            {
                errors.Add("signatureQuietM must not be negative.");
            }

            if (duskFromLap < 1)
            {
                errors.Add("duskFromLap must be at least 1 (the first Jungle is never dusk).");
            }

            return errors.Count == before;
        }

        private const float MinWorldLengthM = 200f;
    }
}
