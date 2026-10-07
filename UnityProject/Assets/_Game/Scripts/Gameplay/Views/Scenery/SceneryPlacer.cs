using JungleBooze.Gameplay.Path;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Stateless scenery placement (spec 003 section 11.2). A cell of <see cref="ScenerySettings.CellLengthM"/> is a pure
    /// function of (run seed, stream id, cell index, route context): <see cref="PlaceCell"/> can be called again at any
    /// time, in any order, and returns the same pieces (AC-309). Every solid piece stays outside the sight corridor
    /// (11.1, AC-308): its inner edge is pushed out to the corridor edge plus the margin when the hash puts it closer.
    /// <para>Dense corridor (owner direction): from the corridor edge outward the rings are the wall ring (trunks, big
    /// opaque leaf masses, branch stubs with a leaf tuft and a hanging vine at the tip), the near ring (understory),
    /// ground cover (ferns, small rocks, roots, leaf litter), the mid ring (big trees) and the far ring (silhouettes in
    /// the fog). Overhead: canopy leaf masses 12 m or more above the path and the vines hanging from their undersides.
    /// Every hanging vine hangs from a stub tuft or a canopy mass, never from nothing.</para>
    /// <para>Only three kinds of piece may enter the corridor and only above 7 m: stub tips and their tufts (at most
    /// 1 m inside the edge, 0 m in a vine section or on the inner side of a bend) and canopy masses (12 m and up) with
    /// their vines (tips 7 m and up). Leaf litter lies flat outside the lane band. Pure math plus <see cref="Mathf"/>; no
    /// allocation.</para>
    /// </summary>
    public static class SceneryPlacer
    {
        /// <summary>Near-ring slots per side per cell.</summary>
        public const int NearSlots = 4;

        /// <summary>Mid-ring slots per side per cell.</summary>
        public const int MidSlots = 2;

        public const int WallTrunkSlots = 3;
        public const int WallFillerSlots = 2;
        public const int CoverSlots = 3;
        public const int LitterSlots = 3;
        public const int FarSlots = 2;
        public const int CanopySlots = 2;

        /// <summary>Stubs rise this many degrees above horizontal toward their tip.</summary>
        public const float StubTiltDeg = 12f;

        /// <summary>Scale of the leaf tuft at a stub tip.</summary>
        public const float TuftScale = 0.7f;

        /// <summary>A stub shorter than this (horizontal reach) is not made.</summary>
        public const float MinStubReachM = 1.5f;

        /// <summary>Length of the Vine_Creeper art at scale 1 (the placer, the tests and the nominal bounds agree on it).</summary>
        public const float VineNativeLengthM = 6f;

        private const uint SaltPresence = 0;
        private const uint SaltPick = 1;
        private const uint SaltAlong = 2;
        private const uint SaltLateral = 3;
        private const uint SaltScaleA = 4;
        private const uint SaltScaleB = 5;
        private const uint SaltYaw = 6;
        private const uint SaltExtra = 7;
        private const uint SaltAux = 8;
        private const uint SaltHeight = 9;
        private const uint SaltCrownPick = 10;
        private const uint SaltCrownA = 11;
        private const uint SaltCrownB = 12;
        private const uint SaltCrownHeight = 13;
        private const uint SaltCrownYaw = 14;
        private const uint SaltStubYaw = 21;
        private const uint SaltStubLength = 22;
        private const uint SaltStubHeight = 23;
        private const uint SaltStubThick = 24;
        private const uint SaltTuftPick = 25;
        private const uint SaltTuftB = 26;
        private const uint SaltVinePresence = 27;
        private const uint SaltVineLength = 28;
        private const uint SaltVineThick = 29;

        // Slot ids per side (multiplied by the side index): near 0..3, mid 4..5, wall trunks 8..10, wall fillers 12..13,
        // cover 16..18, litter 20..22, far 24..25. Slots without a side: canopy 28..29, canopy vines 32..35, shaft 36.
        private const uint SlotNear = 0;
        private const uint SlotMid = 4;
        private const uint SlotWallTrunk = 8;
        private const uint SlotWallFiller = 12;
        private const uint SlotCover = 16;
        private const uint SlotLitter = 20;
        private const uint SlotFar = 24;
        private const uint SlotCanopy = 28;
        private const uint SlotCanopyVine = 32;
        private const uint SlotShaft = 36;
        private const uint SlotsPerSide = 40;
        private const uint SlotStretch = 90;

        /// <summary>Lateral distance inside which canopy masses keep clear in a clearing or a vine section (sky and span stay open).</summary>
        private const float CanopyOpenM = 9f;

        private readonly struct CellArgs
        {
            public CellArgs(
                ulong seed, ulong stream, long cell, double cellStart, bool swing, float curvature, RouteBeatKind beat, int world, ScenerySettings settings)
            {
                Seed = seed;
                Stream = stream;
                Cell = cell;
                CellStart = cellStart;
                Swing = swing;
                Curvature = curvature;
                Beat = beat;
                World = world;
                S = settings;
            }

            public readonly ulong Seed;
            public readonly ulong Stream;
            public readonly long Cell;
            public readonly double CellStart;

            /// <summary>Vine section framing: the anchor tree must stay visible.</summary>
            public readonly bool Swing;

            public readonly float Curvature;
            public readonly RouteBeatKind Beat;
            public readonly int World;
            public readonly ScenerySettings S;

            public double CellEnd => CellStart + S.CellLengthM;

            public float Roll(SceneryBand band, uint slot, uint salt)
            {
                return SceneryHash.Roll(Seed, Stream, Cell, band, slot, salt);
            }
        }

        /// <summary>
        /// Lateral distance (always positive) inside which nothing may stand on <paramref name="side"/> (+1 right, -1 left):
        /// 7 m on the inner side of a bend, 9 m on both sides in a swing zone, 5 m elsewhere (spec 11.1, 11.3).
        /// Curvature is positive when the route turns right, so the inner side of that bend is the right side.
        /// </summary>
        public static float CorridorEdgeM(int side, float curvature, RouteBeatKind beat, ScenerySettings s)
        {
            if (beat == RouteBeatKind.SwingZone)
            {
                return s.CorridorSwingM;
            }

            float edge = s.CorridorEdgeM;
            if (Mathf.Abs(curvature) >= s.BendCurvature)
            {
                int inner = curvature > 0f ? 1 : -1;
                if (side == inner)
                {
                    edge = s.CorridorBendInnerM;
                }
            }

            return edge;
        }

        /// <summary>
        /// Writes the pieces of <paramref name="cell"/> (covering s from cell * length to the next cell) into
        /// <paramref name="buffer"/> and returns how many. Stops when the buffer is full (shafts and canopy are placed
        /// first so a full buffer never loses them).
        /// </summary>
        public static int PlaceCell(
            ulong runSeed, ulong streamId, long cell, in SceneryCellContext context, ScenerySettings s, SceneryPiece[] buffer)
        {
            SceneryCellProfile profile = SceneryCellProfile.For(context.Beat, context.Layer, s.DensityOfWorld(context.WorldIndex), s);
            double cellStart = cell * (double)s.CellLengthM;
            bool swing = context.SwingFrame || context.Beat == RouteBeatKind.SwingZone;
            var args = new CellArgs(runSeed, streamId, cell, cellStart, swing, context.Curvature, context.Beat, context.WorldIndex, s);

            // Seeded variation per world: every 8 cells (80 m) is a bit thinner or thicker than the base density.
            uint worldSlot = SlotStretch + (uint)Mathf.Max(0, context.WorldIndex);
            float stretch = Mathf.Lerp(
                s.StretchDensityMin, s.StretchDensityMax,
                SceneryHash.Roll(runSeed, streamId, cell >> 3, SceneryBand.Wall, worldSlot, SaltExtra));
            profile.ScaleDensities(stretch);

            int n = 0;
            PlaceShaft(in args, in profile, buffer, ref n);
            PlaceCanopy(in args, in profile, buffer, ref n);
            for (int si = 0; si < 2; si++)
            {
                int side = si == 0 ? -1 : 1;
                float edge = args.Swing ? s.CorridorSwingM : CorridorEdgeM(side, context.Curvature, context.Beat, s);
                float placeEdge = args.Swing ? Mathf.Max(edge, s.SwingFrameInnerM) : edge;
                PlaceWall(in args, si, side, edge, placeEdge, in profile, buffer, ref n);
                PlaceMid(in args, si, side, placeEdge, in profile, buffer, ref n);
                PlaceNear(in args, si, side, placeEdge, in profile, buffer, ref n);
                PlaceCover(in args, si, side, placeEdge, in profile, buffer, ref n);
                PlaceFar(in args, si, side, in profile, buffer, ref n);
            }

            return n;
        }

        private static void Add(SceneryPiece[] buffer, ref int n, in SceneryPiece piece)
        {
            if (n < buffer.Length)
            {
                buffer[n++] = piece;
            }
        }

        /// <summary>Leaf mass variant by a roll, shifted per world so each world's stretches favour a different tint.</summary>
        private static SceneryModel LeafModel(float roll, int world)
        {
            float r = roll + (0.31f * Mathf.Max(0, world));
            r -= Mathf.Floor(r);
            if (r < 0.4f)
            {
                return SceneryModel.LeafMassA;
            }

            return r < 0.75f ? SceneryModel.LeafMassB : SceneryModel.LeafMassC;
        }

        private static bool IsInnerBend(in CellArgs a, int side)
        {
            if (Mathf.Abs(a.Curvature) < a.S.BendCurvature)
            {
                return false;
            }

            return side == (a.Curvature > 0f ? 1 : -1);
        }

        // ---- Near ring (understory) ----

        private static void PlaceNear(
            in CellArgs a, int si, int side, float placeEdge, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            float slotLength = s.CellLengthM / NearSlots;
            for (int i = 0; i < NearSlots; i++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotNear + (uint)i;
                if (a.Roll(SceneryBand.Near, slot, SaltPresence) >= s.NearChance * profile.NearDensity)
                {
                    continue;
                }

                float pick = a.Roll(SceneryBand.Near, slot, SaltPick);
                SceneryModel model = PickNear(pick, i, profile.NearTuftsOnly);
                float sa = a.Roll(SceneryBand.Near, slot, SaltScaleA);
                float sb = a.Roll(SceneryBand.Near, slot, SaltScaleB);

                float sxz;
                float sy;
                switch (model)
                {
                    case SceneryModel.GiantTrunk:
                        // Thin trunk: radius 0.18 to 0.30 m on the 1.5 m native trunk (diameter 0.6 m or less), 12 to 18 m tall on the 34 m native.
                        sxz = Mathf.Lerp(0.12f, 0.2f, sa);
                        sy = Mathf.Lerp(0.35f, 0.5f, sb);
                        break;
                    case SceneryModel.Bush:
                        sxz = Mathf.Lerp(0.9f, 1.5f, sa);
                        sy = sxz * Mathf.Lerp(0.8f, 1.2f, sb);
                        break;
                    case SceneryModel.Rock:
                        sxz = Mathf.Lerp(0.6f, 1.6f, sa);
                        sy = sxz * Mathf.Lerp(0.5f, 1f, sb);
                        break;
                    case SceneryModel.Root:
                        sxz = Mathf.Lerp(0.8f, 1.4f, sa);
                        sy = sxz * Mathf.Lerp(0.6f, 1f, sb);
                        break;
                    default:
                        sxz = Mathf.Lerp(0.8f, 1.5f, sa);
                        sy = sxz * Mathf.Lerp(0.8f, 1.2f, sb);
                        break;
                }

                float footprint = s.FootprintOf(model, sxz);
                float lo = placeEdge + footprint + s.ClearanceMarginM;
                float hi = Mathf.Max(lo + 1f, s.NearMaxM);
                float lateral = Mathf.Lerp(lo, hi, a.Roll(SceneryBand.Near, slot, SaltLateral));
                double along = a.CellStart + ((i + 0.2f + (0.6f * a.Roll(SceneryBand.Near, slot, SaltAlong))) * slotLength);
                float yaw = 360f * a.Roll(SceneryBand.Near, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(model, SceneryBand.Near, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));
            }
        }

        private static SceneryModel PickNear(float pick, int slotIndex, bool tuftsOnly)
        {
            if (tuftsOnly)
            {
                return SceneryModel.Fern;
            }

            if (slotIndex == 1 && pick >= 0.7f)
            {
                // Only the second slot may hold a thin trunk, so two trunks are at least 8 m apart (spec: 4 m or more).
                return SceneryModel.GiantTrunk;
            }

            if (pick < 0.3f)
            {
                return SceneryModel.Fern;
            }

            if (pick < 0.58f)
            {
                return SceneryModel.Bush;
            }

            if (pick < 0.8f)
            {
                return SceneryModel.Rock;
            }

            return pick < 0.95f ? SceneryModel.Root : SceneryModel.Fern;
        }

        // ---- Mid ring (big trees) ----

        private static void PlaceMid(
            in CellArgs a, int si, int side, float placeEdge, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            for (int k = 0; k < MidSlots; k++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotMid + (uint)k;
                if (a.Roll(SceneryBand.Mid, slot, SaltPresence) >= s.MidChance * profile.MidDensity)
                {
                    continue;
                }

                float pick = a.Roll(SceneryBand.Mid, slot, SaltPick);
                SceneryModel model;
                if (profile.MidColumnsOnly || (pick >= 0.68f && pick < 0.8f))
                {
                    model = SceneryModel.GiantTrunk;
                }
                else if (pick < 0.34f)
                {
                    model = SceneryModel.TreeA;
                }
                else if (pick < 0.68f)
                {
                    model = SceneryModel.TreeB;
                }
                else
                {
                    model = pick < 0.9f ? SceneryModel.Bush : SceneryModel.Rock;
                }

                float sa = a.Roll(SceneryBand.Mid, slot, SaltScaleA);
                float sb = a.Roll(SceneryBand.Mid, slot, SaltScaleB);
                float sxz;
                float sy;
                switch (model)
                {
                    case SceneryModel.GiantTrunk:
                        sxz = Mathf.Lerp(0.55f, 0.95f, sa);
                        sy = sxz * Mathf.Lerp(0.9f, 1.1f, sb);
                        break;
                    case SceneryModel.Bush:
                        sxz = Mathf.Lerp(2f, 3f, sa);
                        sy = sxz * Mathf.Lerp(0.8f, 1.1f, sb);
                        break;
                    case SceneryModel.Rock:
                        sxz = Mathf.Lerp(2.5f, 4f, sa);
                        sy = sxz * Mathf.Lerp(0.5f, 0.9f, sb);
                        break;
                    default:
                        sxz = Mathf.Lerp(1.4f, 2.2f, sa);
                        sy = sxz * Mathf.Lerp(0.95f, 1.15f, sb);
                        break;
                }

                float footprint = s.FootprintOf(model, sxz);
                float lo = Mathf.Max(placeEdge + footprint + s.ClearanceMarginM, profile.MidMinM + footprint);
                float hi = Mathf.Max(lo, s.MidMaxM);
                float u = a.Roll(SceneryBand.Mid, slot, SaltLateral);
                float lateral = Mathf.Lerp(lo, hi, u * u);
                double along = a.CellStart + (((k * 0.5f) + 0.05f + (0.4f * a.Roll(SceneryBand.Mid, slot, SaltAlong))) * s.CellLengthM);
                float yaw = 360f * a.Roll(SceneryBand.Mid, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(model, SceneryBand.Mid, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));
            }
        }

        // ---- Wall ring ----

        private static void PlaceWall(
            in CellArgs a, int si, int side, float edge, float placeEdge, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            float segment = s.CellLengthM / WallTrunkSlots;
            for (int i = 0; i < WallTrunkSlots; i++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotWallTrunk + (uint)i;
                if (a.Roll(SceneryBand.Wall, slot, SaltPresence) >= s.WallChance * profile.WallDensity)
                {
                    continue;
                }

                float sxz = Mathf.Lerp(0.7f, 1.7f, a.Roll(SceneryBand.Wall, slot, SaltScaleA));
                float sy = Mathf.Lerp(0.8f, 1.25f, a.Roll(SceneryBand.Wall, slot, SaltScaleB));
                float footprint = s.FootprintOf(SceneryModel.WallTrunk, sxz);
                float lo = placeEdge + footprint + s.ClearanceMarginM;
                float u = a.Roll(SceneryBand.Wall, slot, SaltLateral);
                float lateral = lo + (s.WallDepthM * u * u);
                double along = a.CellStart + ((i + 0.15f + (0.7f * a.Roll(SceneryBand.Wall, slot, SaltAlong))) * segment);
                float yaw = 360f * a.Roll(SceneryBand.Wall, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(SceneryModel.WallTrunk, SceneryBand.Wall, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));

                if (a.Roll(SceneryBand.Wall, slot, SaltExtra) < s.WallCrownChance)
                {
                    // A big opaque leaf mass wrapped round the trunk, 3.5 to 8.5 m up.
                    SceneryModel crown = LeafModel(a.Roll(SceneryBand.Wall, slot, SaltCrownPick), a.World);
                    float cs = Mathf.Lerp(1.3f, 2.1f, a.Roll(SceneryBand.Wall, slot, SaltCrownA));
                    float cy = cs * Mathf.Lerp(0.8f, 1.2f, a.Roll(SceneryBand.Wall, slot, SaltCrownB));
                    float cf = s.FootprintOf(crown, cs);
                    float cl = Mathf.Max(lateral, placeEdge + cf + s.ClearanceMarginM);
                    float ch = Mathf.Lerp(3.5f, 8.5f, a.Roll(SceneryBand.Wall, slot, SaltCrownHeight));
                    float cyaw = 360f * a.Roll(SceneryBand.Wall, slot, SaltCrownYaw);
                    Add(buffer, ref n, new SceneryPiece(crown, SceneryBand.Wall, side, cl, along, cs, cy, cyaw, 0f, cf, ch));
                }

                if (a.Roll(SceneryBand.Wall, slot, SaltAux) < s.StubChance)
                {
                    PlaceStub(in a, side, a.Swing ? placeEdge : edge, slot, lateral, along, buffer, ref n);
                }
            }

            for (int i = 0; i < WallFillerSlots; i++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotWallFiller + (uint)i;
                if (a.Roll(SceneryBand.Wall, slot, SaltPresence) >= s.WallFillerChance * profile.WallDensity)
                {
                    continue;
                }

                SceneryModel model = LeafModel(a.Roll(SceneryBand.Wall, slot, SaltPick), a.World);
                float sxz = Mathf.Lerp(1.4f, 2.4f, a.Roll(SceneryBand.Wall, slot, SaltScaleA));
                float sy = sxz * Mathf.Lerp(0.8f, 1.3f, a.Roll(SceneryBand.Wall, slot, SaltScaleB));
                float footprint = s.FootprintOf(model, sxz);
                float lo = placeEdge + footprint + s.ClearanceMarginM;
                float lateral = lo + (3.5f * a.Roll(SceneryBand.Wall, slot, SaltLateral));
                double along = a.CellStart + (((i * 0.5f) + 0.05f + (0.4f * a.Roll(SceneryBand.Wall, slot, SaltAlong))) * s.CellLengthM);
                float yaw = 360f * a.Roll(SceneryBand.Wall, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(model, SceneryBand.Wall, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));
            }
        }

        /// <summary>
        /// A branch stub from a wall trunk reaching toward the path at 10.8 m or higher, a leaf tuft at its tip and (usually) a
        /// vine hanging from the tuft. The tip (with the tuft's reach) stops at most <see cref="ScenerySettings.StubOverhangM"/>
        /// inside the corridor edge, and at the edge itself in a vine section or on the inner side of a bend. Skipped when the
        /// trunk is too far out to reach (less than 1.5 m of reach).
        /// </summary>
        private static void PlaceStub(
            in CellArgs a, int side, float edge, uint slot, float trunkLateral, double along, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            float overhang = a.Swing || IsInnerBend(in a, side) ? 0f : s.StubOverhangM;
            float tiltRad = StubTiltDeg * Mathf.Deg2Rad;
            float cosTilt = Mathf.Cos(tiltRad);
            float tuftFoot = s.FootprintOf(SceneryModel.LeafMassA, TuftScale);
            float allowed = trunkLateral - (edge - overhang) - tuftFoot;
            float wantedLength = Mathf.Lerp(3.6f, 6f, a.Roll(SceneryBand.Wall, slot, SaltStubLength));
            float yawDeg = Mathf.Lerp(-25f, 25f, a.Roll(SceneryBand.Wall, slot, SaltStubYaw));

            // Try the yaw, its mirror, then straight: the tip must stay in this cell so a rebuilt cell never leaks pieces.
            float reach = 0f;
            float length = 0f;
            double tipS = along;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt == 1)
                {
                    yawDeg = -yawDeg;
                }
                else if (attempt == 2)
                {
                    yawDeg = 0f;
                }

                float yawRad = yawDeg * Mathf.Deg2Rad;
                float cosYaw = Mathf.Cos(yawRad);
                reach = Mathf.Min(wantedLength * cosTilt * cosYaw, allowed);
                if (reach < MinStubReachM)
                {
                    return;
                }

                length = reach / (cosTilt * cosYaw);
                tipS = along + (side * length * cosTilt * Mathf.Sin(yawRad));
                if (tipS >= a.CellStart && tipS <= a.CellEnd)
                {
                    break;
                }
            }

            float baseHeight = Mathf.Lerp(s.StubMinHeightM, s.StubMinHeightM + 2.2f, a.Roll(SceneryBand.Wall, slot, SaltStubHeight));
            float tipHeight = baseHeight + (length * Mathf.Sin(tiltRad));
            float thick = Mathf.Lerp(1.6f, 2.4f, a.Roll(SceneryBand.Wall, slot, SaltStubThick));

            // A positive roll tips the branch's +Y toward -x, so the right side (+1) rolls positive (toward the path).
            float rollDeg = side * (90f - StubTiltDeg);
            Add(buffer, ref n, new SceneryPiece(
                SceneryModel.BranchStub, SceneryBand.Overhead, side, trunkLateral, along, thick, length, yawDeg, rollDeg, reach, baseHeight));

            float tipLateral = trunkLateral - reach;
            SceneryModel tuft = LeafModel(a.Roll(SceneryBand.Wall, slot, SaltTuftPick), a.World);
            float tuftY = TuftScale * Mathf.Lerp(0.9f, 1.2f, a.Roll(SceneryBand.Wall, slot, SaltTuftB));
            float tuftYaw = 360f * a.Roll(SceneryBand.Wall, slot, SaltCrownYaw);
            Add(buffer, ref n, new SceneryPiece(
                tuft, SceneryBand.Overhead, side, tipLateral, tipS, TuftScale, tuftY, tuftYaw, 0f, tuftFoot, tipHeight - 0.9f));

            if (a.Roll(SceneryBand.Wall, slot, SaltVinePresence) < s.StubVineChance)
            {
                // The vine's top (0.3 m below the tip) sits inside the tuft; its lowest point stays 7 m or more above the path.
                float vineLength = Mathf.Lerp(2.4f, 3.6f, a.Roll(SceneryBand.Wall, slot, SaltVineLength));
                float vineXz = Mathf.Lerp(0.45f, 0.7f, a.Roll(SceneryBand.Wall, slot, SaltVineThick));
                Add(buffer, ref n, new SceneryPiece(
                    SceneryModel.HangingVine, SceneryBand.Overhead, side, tipLateral, tipS, vineXz, vineLength / VineNativeLengthM,
                    360f * a.Roll(SceneryBand.Wall, slot, SaltYaw), 0f, s.FootprintOf(SceneryModel.HangingVine, vineXz), tipHeight - 0.3f));
            }
        }

        // ---- Ground cover ----

        private static void PlaceCover(
            in CellArgs a, int si, int side, float placeEdge, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            if (profile.NoGroundCover)
            {
                return;
            }

            ScenerySettings s = a.S;
            float slotLength = s.CellLengthM / CoverSlots;
            for (int i = 0; i < CoverSlots; i++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotCover + (uint)i;
                if (a.Roll(SceneryBand.Cover, slot, SaltPresence) >= s.CoverChance * profile.CoverDensity)
                {
                    continue;
                }

                float pick = a.Roll(SceneryBand.Cover, slot, SaltPick);
                float sa = a.Roll(SceneryBand.Cover, slot, SaltScaleA);
                float sb = a.Roll(SceneryBand.Cover, slot, SaltScaleB);
                SceneryModel model;
                float sxz;
                float sy;
                if (pick < 0.62f)
                {
                    model = SceneryModel.Fern;
                    sxz = Mathf.Lerp(0.7f, 1.2f, sa);
                    sy = sxz * Mathf.Lerp(0.8f, 1.2f, sb);
                }
                else if (pick < 0.82f)
                {
                    model = SceneryModel.Rock;
                    sxz = Mathf.Lerp(0.5f, 1f, sa);
                    sy = sxz * Mathf.Lerp(0.5f, 0.9f, sb);
                }
                else
                {
                    model = SceneryModel.Root;
                    sxz = Mathf.Lerp(0.7f, 1.1f, sa);
                    sy = sxz * Mathf.Lerp(0.6f, 1f, sb);
                }

                float footprint = s.FootprintOf(model, sxz);
                float lo = placeEdge + footprint + s.ClearanceMarginM;
                float lateral = lo + (2.5f * a.Roll(SceneryBand.Cover, slot, SaltLateral));
                double along = a.CellStart + ((i + 0.1f + (0.8f * a.Roll(SceneryBand.Cover, slot, SaltAlong))) * slotLength);
                float yaw = 360f * a.Roll(SceneryBand.Cover, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(model, SceneryBand.Cover, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));
            }

            for (int i = 0; i < LitterSlots; i++)
            {
                // Flat decals under 0.1 m: allowed from the lane band outward (spec 11.1 ground-hugging cards).
                uint slot = (uint)(si * SlotsPerSide) + SlotLitter + (uint)i;
                if (a.Roll(SceneryBand.Cover, slot, SaltPresence) >= s.LitterChance * profile.CoverDensity)
                {
                    continue;
                }

                float sxz = Mathf.Lerp(1.4f, 3.4f, a.Roll(SceneryBand.Cover, slot, SaltScaleA));
                float footprint = s.FootprintOf(SceneryModel.LeafLitter, sxz);
                float lo = s.LitterInnerM + footprint;
                float hi = Mathf.Max(lo, s.LitterOuterM + footprint);
                float lateral = Mathf.Lerp(lo, hi, a.Roll(SceneryBand.Cover, slot, SaltLateral));
                double along = a.CellStart + ((i + 0.1f + (0.8f * a.Roll(SceneryBand.Cover, slot, SaltAlong))) * slotLength);
                float yaw = 360f * a.Roll(SceneryBand.Cover, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(
                    SceneryModel.LeafLitter, SceneryBand.Cover, side, lateral, along, sxz, 1f, yaw, 0f, footprint, s.LitterHeightM));
            }
        }

        // ---- Far ring ----

        private static void PlaceFar(
            in CellArgs a, int si, int side, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            for (int k = 0; k < FarSlots; k++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotFar + (uint)k;
                if (a.Roll(SceneryBand.Far, slot, SaltPresence) >= s.FarChance * profile.FarDensity)
                {
                    continue;
                }

                float sxz = Mathf.Lerp(1.2f, 2.6f, a.Roll(SceneryBand.Far, slot, SaltScaleA));
                float sy = sxz * Mathf.Lerp(0.9f, 1.5f, a.Roll(SceneryBand.Far, slot, SaltScaleB));
                float footprint = s.FootprintOf(SceneryModel.FarTree, sxz);
                float lateral = s.FarMinM + footprint + ((s.FarMaxM - s.FarMinM) * a.Roll(SceneryBand.Far, slot, SaltLateral));
                double along = a.CellStart + (((k * 0.5f) + 0.05f + (0.4f * a.Roll(SceneryBand.Far, slot, SaltAlong))) * s.CellLengthM);
                float yaw = 360f * a.Roll(SceneryBand.Far, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(SceneryModel.FarTree, SceneryBand.Far, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));
            }
        }

        // ---- Canopy ----

        /// <summary>
        /// Overhead leaf masses 12 m or more above the path (above the 8.64 m swing span, out of the camera's reach) and the
        /// vines hanging from their undersides (tips 7 m or higher). In a clearing and in a vine section they stay off the
        /// lanes: inner edge 9 m (11 m in a vine section) or farther, and no vines hang in a clearing. Nothing here can
        /// hide an obstacle, pickup or vine: a camera below 12 m never looks through it at anything lower than 12 m.
        /// </summary>
        private static void PlaceCanopy(
            in CellArgs a, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            bool open = a.Swing || profile.CanopyAtSidesOnly;
            for (int k = 0; k < CanopySlots; k++)
            {
                uint slot = SlotCanopy + (uint)k;
                if (a.Roll(SceneryBand.Canopy, slot, SaltPresence) >= s.CanopyChance * profile.CanopyDensity)
                {
                    continue;
                }

                SceneryModel model = LeafModel(a.Roll(SceneryBand.Canopy, slot, SaltPick), a.World);
                float sxz = Mathf.Lerp(2.6f, 4.5f, a.Roll(SceneryBand.Canopy, slot, SaltScaleA));
                float sy = Mathf.Lerp(0.55f, 0.9f, a.Roll(SceneryBand.Canopy, slot, SaltScaleB));
                float footprint = s.FootprintOf(model, sxz);
                float u = a.Roll(SceneryBand.Canopy, slot, SaltLateral);
                int side = a.Roll(SceneryBand.Canopy, slot, SaltExtra) < 0.5f ? -1 : 1;
                float lateral;
                if (open)
                {
                    float edge = a.Swing ? s.SwingFrameInnerM : Mathf.Max(CorridorEdgeM(side, a.Curvature, a.Beat, s), CanopyOpenM);
                    lateral = Mathf.Max(edge, CanopyOpenM) + footprint + (6f * u);
                }
                else
                {
                    float x = Mathf.Lerp(-15f, 15f, u);
                    side = x < 0f ? -1 : 1;
                    lateral = Mathf.Max(0.05f, Mathf.Abs(x));
                }

                float height = Mathf.Lerp(s.CanopyMinHeightM, s.CanopyMaxHeightM, a.Roll(SceneryBand.Canopy, slot, SaltHeight));
                double along = a.CellStart + (((k * 0.5f) + 0.05f + (0.4f * a.Roll(SceneryBand.Canopy, slot, SaltAlong))) * s.CellLengthM);
                float yaw = 360f * a.Roll(SceneryBand.Canopy, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(model, SceneryBand.Canopy, side, lateral, along, sxz, sy, yaw, 0f, footprint, height));

                if (profile.CanopyAtSidesOnly)
                {
                    continue;
                }

                for (int v = 0; v < 2; v++)
                {
                    uint vslot = SlotCanopyVine + (uint)(k * 2) + (uint)v;
                    if (a.Roll(SceneryBand.Canopy, vslot, SaltPresence) >= s.CanopyVineChance)
                    {
                        continue;
                    }

                    float dx = (a.Roll(SceneryBand.Canopy, vslot, SaltLateral) - 0.5f) * 2f * 0.55f * footprint;
                    float vx = (side * lateral) + dx;
                    int vSide = vx < 0f ? -1 : 1;
                    float vLateral = Mathf.Max(0.05f, Mathf.Abs(vx));
                    if (a.Swing && vLateral < s.CorridorSwingM)
                    {
                        continue;
                    }

                    // Top 0.8 m above the mass's underside (inside it); length 2.2 to 4.4 m, so the tip is 8.4 m or higher.
                    float vineLength = Mathf.Lerp(2.2f, 4.4f, a.Roll(SceneryBand.Canopy, vslot, SaltVineLength));
                    float vineXz = Mathf.Lerp(0.45f, 0.7f, a.Roll(SceneryBand.Canopy, vslot, SaltVineThick));
                    Add(buffer, ref n, new SceneryPiece(
                        SceneryModel.HangingVine, SceneryBand.Canopy, vSide, vLateral, along, vineXz, vineLength / VineNativeLengthM,
                        360f * a.Roll(SceneryBand.Canopy, vslot, SaltYaw), 0f, s.FootprintOf(SceneryModel.HangingVine, vineXz), height + 0.8f));
                }
            }
        }

        // ---- Light shafts ----

        private static void PlaceShaft(in CellArgs a, in SceneryCellProfile profile, SceneryPiece[] buffer, ref int n)
        {
            ScenerySettings s = a.S;
            int every = Mathf.Max(1, s.ShaftEveryCells);
            long phase = ((a.Cell % every) + every) % every;
            if (!profile.ShaftEveryCell && phase != 0L)
            {
                return;
            }

            if (a.Roll(SceneryBand.Overhead, SlotShaft, SaltPresence) >= profile.ShaftChance)
            {
                return;
            }

            int side = a.Roll(SceneryBand.Overhead, SlotShaft, SaltExtra) < 0.5f ? -1 : 1;
            float edge = a.Swing ? Mathf.Max(s.CorridorSwingM, s.SwingFrameInnerM) : CorridorEdgeM(side, a.Curvature, a.Beat, s);
            float sxz = Mathf.Lerp(1.5f, 3f, a.Roll(SceneryBand.Overhead, SlotShaft, SaltScaleA));
            float sy = Mathf.Lerp(10f, 15f, a.Roll(SceneryBand.Overhead, SlotShaft, SaltScaleB));

            // The card stands on the ground and leans outward (its top is farther from the path than its foot), so the
            // footprint is just half its width: no part of it is nearer than the foot.
            float footprint = s.FootprintOf(SceneryModel.LightShaft, sxz);
            float lateral = edge + footprint + s.ClearanceMarginM + 1f + (8f * a.Roll(SceneryBand.Overhead, SlotShaft, SaltLateral));
            double along = a.CellStart + (a.Roll(SceneryBand.Overhead, SlotShaft, SaltAlong) * s.CellLengthM);
            float lean = Mathf.Lerp(12f, 18f, a.Roll(SceneryBand.Overhead, SlotShaft, SaltYaw));

            // Turn the card toward the camera behind HERO (its normal along -forward) with a little jitter.
            float jitter = Mathf.Lerp(10f, 35f, a.Roll(SceneryBand.Overhead, SlotShaft, SaltPick));
            float yaw = a.Roll(SceneryBand.Overhead, SlotShaft, SaltAux) < 0.5f ? jitter : -jitter;

            // A positive roll about forward tips the top toward -x, so the right side (+1) rolls negative.
            float roll = -side * lean;
            Add(buffer, ref n, new SceneryPiece(
                SceneryModel.LightShaft, SceneryBand.Overhead, side, lateral, along, sxz, sy, yaw, roll, footprint, 0f));
        }
    }
}
