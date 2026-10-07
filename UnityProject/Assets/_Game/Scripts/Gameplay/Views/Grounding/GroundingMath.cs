using JungleBooze.Core;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Pure math of the grounded obstacle rigs (spec 005): stateless variant selection, embed depth, sway, the
    /// no-slip roll of the barrel boulder, the settle and chock-pop poses and the dust puff curve. No Unity objects,
    /// no random source, no time source: the same inputs always give the same outputs, so EditMode tests cover it.
    /// </summary>
    public static class GroundingMath
    {
        /// <summary>The random stream id the cosmetic variants are keyed on (spec 005 G10).</summary>
        public const ulong VariantStream = RandomStreamIds.Cosmetic;

        private const float TwoPi = 6.2831853f;

        /// <summary>
        /// Mixes (run seed, stream id, obstacle id, salt) into 64 bits. A pure function: the call order and the frame
        /// rate cannot change the result (spec 005 G10).
        /// </summary>
        public static ulong Mix(ulong runSeed, ulong streamId, int obstacleId, uint salt)
        {
            unchecked
            {
                ulong h = runSeed ^ (streamId * 0x9E3779B97F4A7C15UL);
                h = Finalize(h + ((ulong)(uint)obstacleId * 0xBF58476D1CE4E5B9UL) + 0x632BE59BD9B4E019UL);
                return Finalize(h ^ (((ulong)salt + 1UL) * 0x94D049BB133111EBUL));
            }
        }

        /// <summary>Uniform value in [0, 1) from the top 24 bits of a hash.</summary>
        public static float Unit(ulong hash)
        {
            return (float)(hash >> 40) * (1f / 16777216f);
        }

        /// <summary>Shorthand for <c>Unit(Mix(...))</c> on the cosmetic variant stream.</summary>
        public static float Roll(ulong runSeed, int obstacleId, uint salt)
        {
            return Unit(Mix(runSeed, VariantStream, obstacleId, salt));
        }

        private static ulong Finalize(ulong x)
        {
            unchecked
            {
                x ^= x >> 33;
                x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33;
                x *= 0xC4CEB9FE1A85EC53UL;
                x ^= x >> 33;
                return x;
            }
        }

        /// <summary>
        /// Skin, mirror and context side of one obstacle (spec 005 3.6, G10): a pure function of (run seed, obstacle id)
        /// and, for the context side only, of the route curvature there. <paramref name="preferredSide"/> (-1, 0 or +1) is
        /// used on a straight when the row has a natural side (for example the edge lane it touches).
        /// </summary>
        public static ObstacleVariant ResolveVariant(
            ulong runSeed, int obstacleId, int skinCount, float curvature, int preferredSide, float bendCurvature)
        {
            var variant = new ObstacleVariant();
            int count = skinCount < 1 ? 1 : skinCount;
            int skin = (int)(Roll(runSeed, obstacleId, 1) * count);
            variant.Skin = skin >= count ? count - 1 : skin;
            variant.Mirror = Roll(runSeed, obstacleId, 2) < 0.5f;
            variant.Detail0 = Roll(runSeed, obstacleId, 3);
            variant.Detail1 = Roll(runSeed, obstacleId, 4);
            variant.ContextSide = ContextSide(curvature, Roll(runSeed, obstacleId, 5), preferredSide, bendCurvature);
            return variant;
        }

        /// <summary>
        /// Side of the context pieces (spec 005 3.3): the outer side of a bend (curvature is positive for a right turn,
        /// so the outer side is left); on a straight the preferred side, else a hash pick.
        /// </summary>
        public static int ContextSide(float curvature, float hashUnit, int preferredSide, float bendCurvature)
        {
            if (curvature >= bendCurvature)
            {
                return -1;
            }

            if (curvature <= -bendCurvature)
            {
                return 1;
            }

            if (preferredSide != 0)
            {
                return preferredSide < 0 ? -1 : 1;
            }

            return hashUnit < 0.5f ? -1 : 1;
        }

        /// <summary>
        /// How far the lowest point of a grounded model sinks below the ground (spec 005 3.4): by surface, then plus
        /// <c>tan(slope) * footprintDepth / 2</c> so a rigid model never floats on its downhill side.
        /// <paramref name="unit"/> in [0, 1) picks a depth inside the surface range.
        /// </summary>
        public static float EmbedDepth(PathSurface surface, float unit, float gradePct, float footprintDepthM)
        {
            float lo;
            float hi;
            switch (surface)
            {
                case PathSurface.Mud:
                    lo = 0.10f;
                    hi = 0.12f;
                    break;
                case PathSurface.Bough:
                    lo = 0.04f;
                    hi = 0.08f;
                    break;
                case PathSurface.Ledge:
                case PathSurface.Planks:
                    lo = 0.03f;
                    hi = 0.06f;
                    break;
                default:
                    lo = 0.06f;
                    hi = 0.12f;
                    break;
            }

            float u = unit < 0f ? 0f : (unit > 1f ? 1f : unit);
            float flat = lo + ((hi - lo) * u);
            float slope = Mathf.Abs(gradePct) * 0.01f * footprintDepthM * 0.5f;
            return flat + slope;
        }

        /// <summary>Sway amplitude of hanging parts: <paramref name="nearDeg"/> inside the fade distance, full from twice that (spec 005 6.1).</summary>
        public static float SwayAmplitudeDeg(float distanceAheadM, float fullDeg, float nearDeg, float fadeM)
        {
            if (!(fadeM > 0f) || distanceAheadM <= fadeM)
            {
                return nearDeg;
            }

            if (distanceAheadM >= 2f * fadeM)
            {
                return fullDeg;
            }

            float t = (distanceAheadM - fadeM) / fadeM;
            return nearDeg + ((fullDeg - nearDeg) * t);
        }

        /// <summary>Sway angle in degrees at cosmetic time <paramref name="clockS"/>.</summary>
        public static float SwayDeg(float clockS, float phase, float hz, float amplitudeDeg)
        {
            return amplitudeDeg * Mathf.Sin((TwoPi * hz * clockS) + phase);
        }

        /// <summary>
        /// No-slip roll angle in degrees about the barrel axis (+z, Unity convention: a positive angle turns x toward y)
        /// for a center that has moved <paramref name="xFromStartM"/> sideways: <c>-x / R</c>, so the top of the barrel
        /// moves with the travel. A function of the simulated position, so the spin can never lead or trail the box.
        /// </summary>
        public static float RollAngleDeg(float xFromStartM, float radiusM)
        {
            return -(xFromStartM / radiusM) * Mathf.Rad2Deg;
        }

        /// <summary>Angular speed of a no-slip roll: <c>v / R</c> (5.05 rad/s at 4.8 m/s on the 0.95 m barrel).</summary>
        public static float RollOmega(float speedMps, float radiusM)
        {
            return speedMps / radiusM;
        }

        /// <summary>Speed of the surface point touching the ground relative to the ground: <c>|v - omega * R|</c> (0 for a no-slip roll).</summary>
        public static float ContactPointSpeed(float speedMps, float omega, float radiusM)
        {
            return Mathf.Abs(speedMps - (omega * radiusM));
        }

        /// <summary>
        /// Rocking angle in degrees about the barrel axis while it waits (spec 005 8.4 rules 1 and 2): +-idle degrees at
        /// <paramref name="hz"/>, growing to +-anticipation degrees and leaning toward the travel direction
        /// (<paramref name="travelSign"/>) as <paramref name="anticipation01"/> goes from 0 to 1.
        /// </summary>
        public static float IdleRockDeg(
            float clockS, float phase, float hz, float idleDeg, float anticipationDeg, float anticipation01, float travelSign)
        {
            float a = anticipation01 < 0f ? 0f : (anticipation01 > 1f ? 1f : anticipation01);
            float amplitude = idleDeg + ((anticipationDeg - idleDeg) * a);
            return (amplitude * Mathf.Sin((TwoPi * hz * clockS) + phase)) + (travelSign * 0.5f * anticipationDeg * a);
        }

        /// <summary>
        /// Settle pose (spec 005 8.4 rule 5) <paramref name="tS"/> seconds after the barrel stops: a sink that reaches
        /// <paramref name="sinkTotalM"/> over <paramref name="sinkTicks"/> ticks (60 Hz) and then relaxes by 30 percent
        /// over 0.1 s (it rests 0.07 m low), a 3 percent squash for the first half of the sink, then a damped rock of
        /// 3 degrees that has died out 0.4 s after the stop.
        /// </summary>
        public static void SettlePose(float tS, float sinkTotalM, int sinkTicks, out float sinkM, out float squash, out float rockDeg)
        {
            float sinkS = sinkTicks / 60f;
            float t = tS < 0f ? 0f : tS;
            float s = sinkS > 0f ? Mathf.Clamp01(t / sinkS) : 1f;
            float smooth = s * s * (3f - (2f * s));
            float relax = Mathf.Clamp01((t - sinkS) / 0.1f);
            sinkM = sinkTotalM * (smooth - (0.3f * relax * relax * (3f - (2f * relax))));
            float squashT = sinkS > 0f ? Mathf.Clamp01(t / (sinkS * 0.5f)) : 1f;
            squash = 0.03f * Mathf.Sin(Mathf.PI * squashT);
            if (t <= sinkS || t >= 0.4f)
            {
                rockDeg = 0f;
                return;
            }

            float u = t - sinkS;
            float envelope = Mathf.Clamp01(1f - (u / (0.4f - sinkS)));
            rockDeg = 3f * envelope * envelope * Mathf.Sin(TwoPi * 3f * u);
        }

        /// <summary>
        /// Chock stone flung away at the trigger (spec 005 8.4 rule 3): offset from its rest place after
        /// <paramref name="tS"/> seconds. False once <paramref name="durationS"/> is over (hide it).
        /// </summary>
        public static bool ChockPopPose(float tS, float durationS, float travelSign, out float dx, out float dy, out float spinDeg)
        {
            dx = 0f;
            dy = 0f;
            spinDeg = 0f;
            if (tS < 0f || tS >= durationS)
            {
                return false;
            }

            dx = travelSign * 3f * tS;
            dy = (2.6f * tS) - (0.5f * 9.81f * tS * tS);
            spinDeg = travelSign * 540f * tS;
            return true;
        }

        /// <summary>Scale of a dust puff at <paramref name="age01"/> (0 = just emitted, 1 = gone): grows, then shrinks to nothing.</summary>
        public static float PuffScale(float age01, float size)
        {
            float a = age01 < 0f ? 0f : (age01 > 1f ? 1f : age01);
            return size * (0.5f + (0.5f * a)) * (1f - (a * a));
        }

        /// <summary>Puffs to emit this frame: carries the fraction in <paramref name="accumulator"/> (no allocation).</summary>
        public static int PuffsDue(ref float accumulator, float perSecond, float dtS)
        {
            accumulator += perSecond * (dtS < 0f ? 0f : dtS);
            int n = (int)accumulator;
            accumulator -= n;
            return n;
        }

        /// <summary>True when the pose is early enough that nothing may change state here (inside the fog start, spec 005 11).</summary>
        public static bool IsInsidePopInLimit(float distanceAheadM, float popInMinDistM)
        {
            return distanceAheadM < popInMinDistM;
        }

        /// <summary>The route surface and grade at the rig center (for the embed), read once when the rig is built.</summary>
        public static float EmbedAt(PathFrame frame, double s, float unit, float footprintDepthM)
        {
            frame.Sample(s, out PathPose pose);
            return EmbedDepth(pose.Surface, unit, pose.GradePct, footprintDepthM);
        }

        /// <summary>Skin count per archetype in wave 1 (Jungle floor): low 3, high 2, full 3, mover 1, thorn 2.</summary>
        public static int SkinCount(ObstacleArchetype kind)
        {
            switch (kind)
            {
                case ObstacleArchetype.LowBarrier:
                    return 3;
                case ObstacleArchetype.HighBarrier:
                    return 2;
                case ObstacleArchetype.FullBlock:
                    return 3;
                case ObstacleArchetype.LaneDenial:
                    return 2;
                default:
                    return 1;
            }
        }
    }
}
