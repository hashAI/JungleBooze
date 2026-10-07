using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Pure layout of the context pieces of one obstacle row (spec 005 3.3 and 5 to 9): skin, side and the anchors of
    /// root plates, stumps, support trunks, verge trunks, scree banks and root crowns. A pure function of (run seed,
    /// obstacle id, row shape, route curvature), so the rig builder and the scenery keep-out query always agree.
    /// Pieces stand in the context zone (4.2 to 7.0 m) with their footprints, at most 2.5 m high inside 4.2 to 5.0 m.
    /// </summary>
    public static class ContextLayout
    {
        /// <summary>Largest number of anchors one row can have.</summary>
        public const int MaxAnchors = 3;

        private const float PlateX = 4.5f;
        private const float StumpX = 6.1f;
        private const float CrownX = 5.7f;
        private const float SupportTrunkX = 5.9f;
        private const float VergeTrunkX = 5.6f;
        private const float ScreeBankX = 5.0f;
        private const float RootCrownX = 4.9f;

        /// <summary>
        /// Computes variant, effective skin and anchors for one obstacle row. <paramref name="buffer"/> must hold
        /// <see cref="MaxAnchors"/> entries; the return value is the number written.
        /// </summary>
        public static int ForObstacle(
            ulong runSeed,
            int obstacleId,
            ObstacleArchetype kind,
            byte laneMask,
            int fromLane,
            int toLane,
            float curvature,
            float laneWidthM,
            float bendCurvature,
            out ObstacleVariant variant,
            out int skin,
            ContextAnchor[] buffer)
        {
            int preferred = PreferredSide(kind, laneMask, fromLane, toLane);
            variant = GroundingMath.ResolveVariant(
                runSeed, obstacleId, GroundingMath.SkinCount(kind), curvature, preferred, bendCurvature);
            skin = EffectiveSkin(kind, variant.Skin, laneMask);

            float zSign = variant.Mirror ? -1f : 1f;
            int n = 0;
            switch (kind)
            {
                case ObstacleArchetype.LowBarrier:
                {
                    int side = PlateSide(laneMask, variant.ContextSide);
                    variant.ContextSide = side;
                    if (skin < 2 && side != 0)
                    {
                        buffer[n++] = new ContextAnchor { Kind = ContextKind.RootPlate, X = side * PlateX, RadiusM = 1.3f, HeightM = 2.0f };
                        if (skin == 0)
                        {
                            buffer[n++] = new ContextAnchor
                            {
                                Kind = ContextKind.Stump, X = side * StumpX, ZOffsetM = zSign * (variant.Detail0 - 0.5f) * 3f, RadiusM = 0.55f, HeightM = 0.9f,
                            };
                        }
                        else
                        {
                            buffer[n++] = new ContextAnchor
                            {
                                Kind = ContextKind.Crown, X = side * CrownX, ZOffsetM = zSign * (variant.Detail0 - 0.5f) * 2.4f, RadiusM = 0.95f, HeightM = 1.5f,
                            };
                        }
                    }

                    break;
                }

                case ObstacleArchetype.HighBarrier:
                    buffer[n++] = new ContextAnchor { Kind = ContextKind.SupportTrunk, X = variant.ContextSide * SupportTrunkX, RadiusM = 0.8f, HeightM = 8f };
                    break;
                case ObstacleArchetype.FullBlock:
                    if (skin == 2)
                    {
                        int side = LaneSide(laneMask);
                        variant.ContextSide = side;
                        buffer[n++] = new ContextAnchor { Kind = ContextKind.VergeTrunk, X = side * VergeTrunkX, RadiusM = 0.6f, HeightM = 12f };
                    }

                    break;
                case ObstacleArchetype.Mover:
                {
                    // The bank is on the start side: opposite to the travel direction.
                    int dir = toLane >= fromLane ? 1 : -1;
                    variant.ContextSide = -dir;
                    buffer[n++] = new ContextAnchor
                    {
                        Kind = ContextKind.ScreeBank, X = -dir * ScreeBankX, ZOffsetM = zSign * (variant.Detail0 - 0.5f) * 1.2f, RadiusM = 1.4f, HeightM = 2.2f,
                    };
                    break;
                }

                case ObstacleArchetype.LaneDenial:
                    buffer[n++] = new ContextAnchor { Kind = ContextKind.RootCrown, X = variant.ContextSide * RootCrownX, RadiusM = 0.7f, HeightM = 2.3f };
                    break;
            }

            return n;
        }

        /// <summary>
        /// The skin actually built: a log with a root plate needs a lane at the plate side, so a row that touches
        /// no edge lane gets the single-log skin 2 (spec 005 5.2); the wedged slab (full block skin 2) needs a single outer lane.
        /// </summary>
        public static int EffectiveSkin(ObstacleArchetype kind, int skin, byte laneMask)
        {
            if (kind == ObstacleArchetype.LowBarrier)
            {
                bool left = LaneMasks.Contains(laneMask, 0);
                bool right = LaneMasks.Contains(laneMask, LaneMasks.LaneCount - 1);
                return skin < 2 && !left && !right ? 2 : skin;
            }

            if (kind == ObstacleArchetype.FullBlock && skin == 2)
            {
                if (LaneSide(laneMask) != 0)
                {
                    return 2;
                }

                return LaneMasks.Contains(laneMask, 1) ? 1 : 0;
            }

            return skin;
        }

        /// <summary>Side of the plate end: the context side if the row reaches that edge lane, else the edge it does reach, else 0.</summary>
        public static int PlateSide(byte laneMask, int contextSide)
        {
            bool left = LaneMasks.Contains(laneMask, 0);
            bool right = LaneMasks.Contains(laneMask, LaneMasks.LaneCount - 1);
            if (contextSide < 0 && left)
            {
                return -1;
            }

            if (contextSide > 0 && right)
            {
                return 1;
            }

            if (left)
            {
                return -1;
            }

            return right ? 1 : 0;
        }

        /// <summary>-1 when the row is a single left-edge lane, +1 for a single right-edge lane, else 0.</summary>
        public static int LaneSide(byte laneMask)
        {
            if (LaneMasks.Count(laneMask) != 1)
            {
                return 0;
            }

            if (LaneMasks.Contains(laneMask, 0))
            {
                return -1;
            }

            return LaneMasks.Contains(laneMask, LaneMasks.LaneCount - 1) ? 1 : 0;
        }

        private static int PreferredSide(ObstacleArchetype kind, byte laneMask, int fromLane, int toLane)
        {
            switch (kind)
            {
                case ObstacleArchetype.LowBarrier:
                {
                    bool left = LaneMasks.Contains(laneMask, 0);
                    bool right = LaneMasks.Contains(laneMask, LaneMasks.LaneCount - 1);
                    if (left && !right)
                    {
                        return -1;
                    }

                    return right && !left ? 1 : 0;
                }

                case ObstacleArchetype.HighBarrier:
                case ObstacleArchetype.LaneDenial:
                {
                    // The side of the lanes' center: the trunk is on the near verge, so the limb stays short.
                    int sum = 0;
                    int count = 0;
                    for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
                    {
                        if (LaneMasks.Contains(laneMask, lane))
                        {
                            sum += lane - ((LaneMasks.LaneCount - 1) / 2);
                            count++;
                        }
                    }

                    if (count == 0 || sum == 0)
                    {
                        return 0;
                    }

                    return sum < 0 ? -1 : 1;
                }

                case ObstacleArchetype.Mover:
                    return toLane >= fromLane ? -1 : 1;
                default:
                    return 0;
            }
        }
    }
}
