using System;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// One obstacle in a chunk (spec 002 section 4.1). Positions are chunk-relative fronts (for a gap: the near
    /// edge). Serializable so chunk assets can hold it directly.
    /// </summary>
    [Serializable]
    public struct ObstaclePlacement
    {
        public ObstacleArchetype Archetype;

        /// <summary>Contiguous occupied lanes (see <see cref="LaneMasks"/>). Mover: exactly its start lane.</summary>
        public byte LaneMask;

        /// <summary>Chunk-relative front in m (multiple of 0.5 m).</summary>
        public float Zc;

        /// <summary>Gap only: one of <see cref="ObstacleKitConfig.GapLengthsM"/>.</summary>
        public float GapLengthM;

        /// <summary>Mover only: the end lane (one lane from the start lane in FP1).</summary>
        public int MoverToLane;

        public static ObstaclePlacement Low(byte laneMask, float zc)
        {
            return Make(ObstacleArchetype.LowBarrier, laneMask, zc);
        }

        public static ObstaclePlacement High(byte laneMask, float zc)
        {
            return Make(ObstacleArchetype.HighBarrier, laneMask, zc);
        }

        public static ObstaclePlacement Full(byte laneMask, float zc)
        {
            return Make(ObstacleArchetype.FullBlock, laneMask, zc);
        }

        public static ObstaclePlacement Mover(int fromLane, int toLane, float zc)
        {
            ObstaclePlacement p = Make(ObstacleArchetype.Mover, LaneMasks.Of(fromLane), zc);
            p.MoverToLane = toLane;
            return p;
        }

        /// <summary>Signature lane denial (GDD 8.3, thorn patch): a contiguous 2-lane mask.</summary>
        public static ObstaclePlacement LaneDenial(byte laneMask, float zc)
        {
            return Make(ObstacleArchetype.LaneDenial, laneMask, zc);
        }

        /// <summary>Signature telegraphed lane strike (GDD 8.3) in one lane.</summary>
        public static ObstaclePlacement LaneStrike(int lane, float zc)
        {
            return Make(ObstacleArchetype.LaneStrike, LaneMasks.Of(lane), zc);
        }

        public static ObstaclePlacement Gap(byte laneMask, float zc, float lengthM)
        {
            ObstaclePlacement p = Make(ObstacleArchetype.Gap, laneMask, zc);
            p.GapLengthM = lengthM;
            return p;
        }

        /// <summary>Start lane of a mover (the lowest lane of the mask for other archetypes).</summary>
        public int FromLane => LaneMasks.Lowest(LaneMask);

        /// <summary>Spec 002 section 4.2: lanes l → 2 − l (mask and mover end lane); nothing else changes.</summary>
        public ObstaclePlacement Mirrored()
        {
            ObstaclePlacement p = this;
            p.LaneMask = LaneMasks.Mirror(LaneMask);
            if (Archetype == ObstacleArchetype.Mover)
            {
                p.MoverToLane = LaneMasks.MirrorLane(MoverToLane);
            }

            return p;
        }

        public override string ToString()
        {
            return Archetype + "[" + LaneMask + "]@" + Zc;
        }

        private static ObstaclePlacement Make(ObstacleArchetype archetype, byte laneMask, float zc)
        {
            return new ObstaclePlacement
            {
                Archetype = archetype,
                LaneMask = laneMask,
                Zc = zc,
                GapLengthM = 0f,
                MoverToLane = -1,
            };
        }
    }
}
