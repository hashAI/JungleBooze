using System;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// One anchor tree beside the path (spec 004 section 8, spec 003 section 8.2). Plain value from <see cref="SwingRigMath.PlaceTree"/>.
    /// </summary>
    public readonly struct SwingTree
    {
        public SwingTree(float side, float lateralM, float trunkRadiusM, float heightM, float branchRootHeightM)
        {
            Side = side;
            LateralM = lateralM;
            TrunkRadiusM = trunkRadiusM;
            HeightM = heightM;
            BranchRootHeightM = branchRootHeightM;
        }

        /// <summary>+1 = right of the path, -1 = left.</summary>
        public float Side { get; }

        /// <summary>Distance of the trunk axis from the centerline (6 to 9 m).</summary>
        public float LateralM { get; }

        public float TrunkRadiusM { get; }

        /// <summary>Trunk top above the path surface (28 to 40 m).</summary>
        public float HeightM { get; }

        /// <summary>Height above the path surface where the branch leaves the trunk axis (15 to 19 m).</summary>
        public float BranchRootHeightM { get; }

        /// <summary>Signed lateral position of the trunk axis (route x).</summary>
        public float TrunkX => Side * LateralM;

        /// <summary>The open side of the canyon, opposite the trunk (+1 right, -1 left): where the swing camera moves to.</summary>
        public int OpenSide => Side > 0f ? -1 : 1;
    }

    /// <summary>
    /// Pure math of the fixed-pivot swing rig (spec 004 sections 3 and 8): rope end from the real pendulum angle,
    /// rest sway with the true pendulum period, the damped swing of the rope after a release, the branch curve from
    /// the anchor trunk to the pivot, and stateless anchor tree placement. No Unity types, no allocation, no
    /// simulation state: everything is presentation and derived from numbers passed in (AC-429 to AC-433).
    /// </summary>
    public static class SwingRigMath
    {
        public const float SwayAmplitudeDeg = 5f;
        public const float SwayNearAmplitudeDeg = 0.5f;
        public const float SwayFadeM = 30f;

        /// <summary>After the grab zone the sway grows back to full amplitude over this distance.</summary>
        public const float SwayRegrowM = 6f;

        public const float TreeLateralMinM = 6f;
        public const float TreeLateralMaxM = 9f;
        public const float TreeRadiusMinM = 1.25f;
        public const float TreeRadiusMaxM = 1.75f;
        public const float TreeHeightMinM = 28f;
        public const float TreeHeightMaxM = 40f;
        public const float BranchRootMinM = 15f;
        public const float BranchRootMaxM = 19f;

        /// <summary>The limb from the trunk surface to the pivot is at least this long (lifts the trunk away when the lane is on the tree's side).</summary>
        public const float BranchMinReachM = 4f;

        /// <summary>The branch arches this far above the higher of its two ends before it droops to the pivot.</summary>
        public const float BranchArchM = 1.2f;

        /// <summary>Branch thickness at the trunk and at the tip (spec 004 section 8).</summary>
        public const float BranchBaseDiameterM = 0.8f;

        public const float BranchTipDiameterM = 0.35f;

        /// <summary>The landing glade starts this far after the last pivot (the chasm's far edge, spec 004 section 8). [ASSUMED]</summary>
        public const float GladeStartAfterGrabM = 12f;

        public const float GladeLengthM = 20f;

        /// <summary>Damping of the rope after a release, per second (amplitude falls with a time constant of 2 / this = 2 s). [ASSUMED]</summary>
        public const float PostReleaseDampingPerS = 1f;

        /// <summary>The after-release swing is dropped (only the rest sway remains) below this angle and speed.</summary>
        public const float PostReleaseRestRad = 0.0087f;

        public const float PostReleaseRestOmegaRadS = 0.05f;

        /// <summary>Rope end (hand) distance along the route from the pivot: <c>L sin(theta)</c>.</summary>
        public static float RopeEndOffsetS(float ropeLengthM, float thetaRad)
        {
            return ropeLengthM * (float)Math.Sin(thetaRad);
        }

        /// <summary>Rope end height above the path: <c>pivotHeight - L cos(theta)</c>.</summary>
        public static float RopeEndHeightM(float pivotHeightM, float ropeLengthM, float thetaRad)
        {
            return pivotHeightM - (ropeLengthM * (float)Math.Cos(thetaRad));
        }

        /// <summary>Period of the free pendulum: <c>2 pi sqrt(L / g)</c> (5.0 s for L 14 m and g 22).</summary>
        public static float SwayPeriodSeconds(float ropeLengthM, float gravityMps2)
        {
            return (float)(2.0 * Math.PI * Math.Sqrt(ropeLengthM / gravityMps2));
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
                return SwayNearAmplitudeDeg + ((SwayAmplitudeDeg - SwayNearAmplitudeDeg) * t);
            }

            float regrow = Math.Min(1f, -distanceToGrabM / SwayRegrowM);
            return SwayNearAmplitudeDeg + ((SwayAmplitudeDeg - SwayNearAmplitudeDeg) * regrow);
        }

        /// <summary>Sway angle in radians about the rest angle 0 (rope straight down), period <paramref name="periodS"/>.</summary>
        public static float SwayAngleRad(float timeS, float phaseRad, float amplitudeDeg, float periodS)
        {
            return amplitudeDeg * (float)(Math.PI / 180.0) * (float)Math.Sin((2.0 * Math.PI * timeS / periodS) + phaseRad);
        }

        /// <summary>Horizontal displacement of the rope end (glow) for a swing of <paramref name="amplitudeDeg"/>: <c>L sin(amp)</c>.</summary>
        public static float GlowSwayBoundM(float ropeLengthM, float amplitudeDeg)
        {
            return ropeLengthM * (float)Math.Sin(amplitudeDeg * Math.PI / 180.0);
        }

        /// <summary>
        /// One step of the damped pendulum the rope follows after a release:
        /// <c>omega' = -(g / L) sin(theta) - damping omega</c>, semi-implicit Euler. Presentation only.
        /// </summary>
        public static void StepPostRelease(ref float thetaRad, ref float omegaRadS, float gravityMps2, float ropeLengthM, float dampingPerS, float dt)
        {
            omegaRadS += ((-(gravityMps2 / ropeLengthM) * (float)Math.Sin(thetaRad)) - (dampingPerS * omegaRadS)) * dt;
            thetaRad += omegaRadS * dt;
        }

        /// <summary>True when the after-release swing is small enough to hand over to the rest sway.</summary>
        public static bool PostReleaseSettled(float thetaRad, float omegaRadS)
        {
            return Math.Abs(thetaRad) < PostReleaseRestRad && Math.Abs(omegaRadS) < PostReleaseRestOmegaRadS;
        }

        /// <summary>
        /// Point of the branch centerline in the cross-section plane (route x, height y) at <paramref name="t"/> in 0..1:
        /// a quadratic curve from the trunk axis at the branch root height, arching up, ending exactly at the pivot
        /// (<paramref name="pivotX"/>, <paramref name="pivotHeightM"/>) at t = 1.
        /// </summary>
        public static void BranchPoint(in SwingTree tree, float pivotX, float pivotHeightM, float t, out float x, out float y)
        {
            float x0 = tree.TrunkX;
            float y0 = tree.BranchRootHeightM;
            float x1 = 0.5f * (x0 + pivotX);
            float y1 = Math.Max(y0, pivotHeightM) + BranchArchM;
            float u = 1f - t;
            x = (u * u * x0) + (2f * u * t * x1) + (t * t * pivotX);
            y = (u * u * y0) + (2f * u * t * y1) + (t * t * pivotHeightM);
        }

        /// <summary>Branch diameter at <paramref name="t"/> (0 at the trunk, 1 at the tip): 0.8 m tapering to 0.35 m.</summary>
        public static float BranchDiameterM(float t)
        {
            return BranchBaseDiameterM + ((BranchTipDiameterM - BranchBaseDiameterM) * t);
        }

        /// <summary>Distance from the trunk surface to the pivot along the branch cross-section (straight line, a lower bound of the limb length).</summary>
        public static float BranchReachM(in SwingTree tree, float pivotX)
        {
            return Math.Abs(pivotX - tree.TrunkX) - tree.TrunkRadiusM;
        }

        /// <summary>How far ahead of the hero the rig is drawn: the view distance plus the signpost lead (spec 004 section 8: at least 2.0 s ahead).</summary>
        public static float RigDrawAheadM(float viewDistanceM, float signpostLeadM)
        {
            return viewDistanceM + signpostLeadM;
        }

        /// <summary>Seconds-based visibility: the rig must be drawn at least this far before the grab zone at the given speed.</summary>
        public static float RequiredVisibleAheadM(float speedMps, float seconds)
        {
            return speedMps * seconds;
        }

        /// <summary>
        /// Stateless anchor tree for one vine of a section: a pure function of the run seed, the Scenery stream id, the
        /// chunk serial and the row, so a rebuilt tree is identical and nothing the player does can change it.
        /// Rows alternate sides (zigzag, spec 003 8.2); each vine has its own tree and its own limb. When the vine's lane
        /// (<paramref name="laneX"/>, route x of the lane centre) is on the trunk's side, the trunk is moved out so the limb
        /// stays at least <see cref="BranchMinReachM"/> long (at most 8.15 m from the centerline for the outer lane).
        /// </summary>
        public static SwingTree PlaceTree(ulong runSeed, ulong sceneryStreamId, int chunkSerial, int row, float laneX)
        {
            ulong baseHash = Mix(runSeed ^ (sceneryStreamId * 0x9E3779B97F4A7C15UL), (ulong)chunkSerial, 0x51UL);
            float side = (baseHash & 1UL) == 0UL ? 1f : -1f;
            if ((row & 1) == 1)
            {
                side = -side;
            }

            ulong h = Mix(baseHash, (ulong)row, 0xA7UL);
            float lateral = TreeLateralMinM + (Unit(h, 0) * (TreeLateralMaxM - TreeLateralMinM));
            float radius = TreeRadiusMinM + (Unit(h, 1) * (TreeRadiusMaxM - TreeRadiusMinM));
            float height = TreeHeightMinM + (Unit(h, 2) * (TreeHeightMaxM - TreeHeightMinM));
            float root = BranchRootMinM + (Unit(h, 3) * (BranchRootMaxM - BranchRootMinM));
            if (laneX * side > 0f)
            {
                float needed = Math.Abs(laneX) + radius + BranchMinReachM;
                if (lateral < needed)
                {
                    lateral = needed;
                }
            }

            return new SwingTree(side, lateral, radius, height, root);
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
                ulong x = a + (0x9E3779B97F4A7C15UL * (b + 1UL)) + (salt * 0xBF58476D1CE4E5B9UL);
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
