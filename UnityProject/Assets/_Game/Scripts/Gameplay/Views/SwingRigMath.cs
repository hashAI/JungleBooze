using System;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// One anchor tree beside the path (spec 003 section 8.2). Plain value from <see cref="SwingRigMath.PlaceTree"/>.
    /// </summary>
    public readonly struct SwingTree
    {
        public SwingTree(float side, float lateralM, float trunkRadiusM, float heightM)
        {
            Side = side;
            LateralM = lateralM;
            TrunkRadiusM = trunkRadiusM;
            HeightM = heightM;
        }

        /// <summary>+1 = right of the path, -1 = left.</summary>
        public float Side { get; }

        /// <summary>Distance of the trunk axis from the centerline (6 to 9 m).</summary>
        public float LateralM { get; }

        public float TrunkRadiusM { get; }

        public float HeightM { get; }

        /// <summary>Signed lateral position of the trunk axis (route x).</summary>
        public float TrunkX => Side * LateralM;
    }

    /// <summary>
    /// Pure math of the natural swing rig (spec 003 section 8): where the hidden knot rests and how long the overhead
    /// span must be, sway, and stateless anchor tree placement. No Unity types, no allocation, no simulation state:
    /// everything is presentation and derived from <c>VineConfig</c> numbers passed in (AC-318 to AC-320).
    /// </summary>
    public static class SwingRigMath
    {
        /// <summary>Extra span beyond the highest pivot travel (spec 8.1).</summary>
        public const float SpanMarginM = 4f;

        /// <summary>Consecutive spans of a chain overlap by this much (spec 8.1, AC-319).</summary>
        public const float ChainOverlapM = 6f;

        /// <summary>The span starts this far before the grab point, where the anchor limb meets it.</summary>
        public const float SpanLeadM = 2f;

        /// <summary>[ASSUMED] highest speed a vine section can spawn at (spec 8.1 example, 21 m/s).</summary>
        public const float DefaultMaxSpeedMps = 21f;

        public const float SwayAmplitudeDeg = 5f;
        public const float SwayNearAmplitudeDeg = 0.5f;
        public const float SwayPeriodS = 3.2f;
        public const float SwayFadeM = 30f;

        /// <summary>After the grab zone the sway grows back to full amplitude over this distance.</summary>
        public const float SwayRegrowM = 6f;

        public const float TreeLateralMinM = 6f;
        public const float TreeLateralMaxM = 9f;
        public const float TreeRadiusMinM = 1.25f;
        public const float TreeRadiusMaxM = 1.75f;
        public const float TreeHeightMinM = 28f;
        public const float TreeHeightMaxM = 40f;

        /// <summary>The landing pad starts this far after the last grab point (the chunk's 18 m gap, spec 8.2). [ASSUMED]</summary>
        public const float GladeStartAfterGrabM = 15f;

        public const float GladeLengthM = 20f;

        /// <summary>Rest height of the rope's top above the path: grab height + radius * cos(start angle) = 8.64 m (AC-318).</summary>
        public static float RestPivotHeightM(float grabPointHeightM, float swingRadiusM, double swingStartAngleRad)
        {
            return grabPointHeightM + swingRadiusM * (float)Math.Cos(swingStartAngleRad);
        }

        /// <summary>How far ahead of the grab point the rope's top rests: radius * sin(20 deg) = 2.05 m (the start angle leans back).</summary>
        public static float RestPivotAheadM(float swingRadiusM, double swingStartAngleRad)
        {
            return -swingRadiusM * (float)Math.Sin(swingStartAngleRad);
        }

        /// <summary>Pivot offset from the hand at <paramref name="angleRad"/>: up = R cos(a), ahead = -R sin(a) (the rope end is the hand).</summary>
        public static void PivotOffsetFromHand(float swingRadiusM, double angleRad, out float up, out float ahead)
        {
            up = swingRadiusM * (float)Math.Cos(angleRad);
            ahead = -swingRadiusM * (float)Math.Sin(angleRad);
        }

        /// <summary>Distance the pivot travels with the runner during one swing at <paramref name="speedMps"/>.</summary>
        public static float SwingTravelM(float speedMps, float swingSeconds)
        {
            return speedMps * swingSeconds;
        }

        /// <summary>Span length from the grab point: rest offset + travel at the highest speed + 4 m (spec 8.1).</summary>
        public static float SpanLengthM(float pivotAheadM, float maxSpeedMps, float swingSeconds)
        {
            return pivotAheadM + SwingTravelM(maxSpeedMps, swingSeconds) + SpanMarginM;
        }

        /// <summary>True when a span of <paramref name="spanLengthM"/> starting <paramref name="spanStartBeforeGrabM"/> before the grab covers the whole pivot travel plus the margin.</summary>
        public static bool SpanCoversTravel(float spanStartBeforeGrabM, float spanLengthM, float pivotAheadM, float travelM)
        {
            float spanEndFromGrab = -spanStartBeforeGrabM + spanLengthM;
            return spanEndFromGrab >= pivotAheadM + travelM + SpanMarginM - 1e-4f;
        }

        /// <summary>Sway amplitude in degrees for a vine <paramref name="distanceToGrabM"/> ahead of the runner (negative = passed).</summary>
        public static float SwayAmplitudeForDistance(float distanceToGrabM)
        {
            if (distanceToGrabM >= SwayFadeM)
            {
                return SwayAmplitudeDeg;
            }

            if (distanceToGrabM >= 0f)
            {
                float t = distanceToGrabM / SwayFadeM;
                return SwayNearAmplitudeDeg + (SwayAmplitudeDeg - SwayNearAmplitudeDeg) * t;
            }

            float regrow = Math.Min(1f, -distanceToGrabM / SwayRegrowM);
            return SwayNearAmplitudeDeg + (SwayAmplitudeDeg - SwayNearAmplitudeDeg) * regrow;
        }

        /// <summary>Sway angle in radians about the rest angle.</summary>
        public static float SwayAngleRad(float timeS, float phaseRad, float amplitudeDeg)
        {
            return amplitudeDeg * (float)(Math.PI / 180.0) * (float)Math.Sin(2.0 * Math.PI * timeS / SwayPeriodS + phaseRad);
        }

        /// <summary>Horizontal displacement of the rope end (glow) if the whole rope tilted by the amplitude about its top: R sin(amp).</summary>
        public static float GlowSwayBoundM(float swingRadiusM, float amplitudeDeg)
        {
            return swingRadiusM * (float)Math.Sin(amplitudeDeg * Math.PI / 180.0);
        }

        /// <summary>Seconds-based visibility: the rig must be drawn at least this far before the grab zone at the given speed.</summary>
        public static float RequiredVisibleAheadM(float speedMps, float seconds)
        {
            return speedMps * seconds;
        }

        /// <summary>
        /// Stateless anchor tree for one vine of a section: a pure function of the run seed, the Scenery stream id, the
        /// chunk serial and the row, so a rebuilt tree is identical and nothing the player does can change it.
        /// Rows alternate sides (zigzag, spec 8.2).
        /// </summary>
        public static SwingTree PlaceTree(ulong runSeed, ulong sceneryStreamId, int chunkSerial, int row)
        {
            ulong baseHash = Mix(runSeed ^ (sceneryStreamId * 0x9E3779B97F4A7C15UL), (ulong)chunkSerial, 0x51UL);
            float side = (baseHash & 1UL) == 0UL ? 1f : -1f;
            if ((row & 1) == 1)
            {
                side = -side;
            }

            ulong h = Mix(baseHash, (ulong)row, 0xA7UL);
            float lateral = TreeLateralMinM + Unit(h, 0) * (TreeLateralMaxM - TreeLateralMinM);
            float radius = TreeRadiusMinM + Unit(h, 1) * (TreeRadiusMaxM - TreeRadiusMinM);
            float height = TreeHeightMinM + Unit(h, 2) * (TreeHeightMaxM - TreeHeightMinM);
            return new SwingTree(side, lateral, radius, height);
        }

        /// <summary>Sway phase (0 to 2 pi) of one vine, stateless like the tree.</summary>
        public static float SwayPhaseRad(ulong runSeed, ulong sceneryStreamId, int chunkSerial, int row)
        {
            ulong h = Mix(runSeed ^ (sceneryStreamId * 0x9E3779B97F4A7C15UL), (ulong)chunkSerial, 0xC3UL + (ulong)row);
            return Unit(h, 0) * (float)(2.0 * Math.PI);
        }

        private static float Unit(ulong hash, int slot)
        {
            ulong x = hash >> (slot * 16);
            return (x & 0xFFFFUL) / 65536f;
        }

        private static ulong Mix(ulong a, ulong b, ulong salt)
        {
            unchecked
            {
                ulong x = a + 0x9E3779B97F4A7C15UL * (b + 1UL) + salt * 0xBF58476D1CE4E5B9UL;
                x ^= x >> 30;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 27;
                x *= 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return x;
            }
        }
    }
}
