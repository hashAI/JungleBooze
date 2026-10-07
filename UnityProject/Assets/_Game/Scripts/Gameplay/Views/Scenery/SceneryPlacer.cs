using JungleBooze.Gameplay.Path;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Stateless scenery placement (spec 003 section 11.2). A cell of <see cref="ScenerySettings.CellLengthM"/> is a pure
    /// function of (run seed, stream id, cell index, route context): <see cref="PlaceCell"/> can be called again at any
    /// time, in any order, and returns the same pieces (AC-309). Every piece stays outside the sight corridor
    /// (11.1, AC-308): its inner edge is pushed out to the corridor edge plus the margin when the hash puts it closer.
    /// Pure math plus <see cref="Mathf"/>; no allocation.
    /// </summary>
    public static class SceneryPlacer
    {
        /// <summary>Near-ring slots per side per cell.</summary>
        public const int NearSlots = 3;

        private const uint SaltPresence = 0;
        private const uint SaltPick = 1;
        private const uint SaltAlong = 2;
        private const uint SaltLateral = 3;
        private const uint SaltScaleA = 4;
        private const uint SaltScaleB = 5;
        private const uint SaltYaw = 6;
        private const uint SaltExtra = 7;
        private const uint SaltAux = 8;

        // Slot ids per side: near 0..2, mid 3, vines 4..5, shaft 6.
        private const uint SlotMid = 3;
        private const uint SlotVine = 4;
        private const uint SlotShaft = 6;
        private const uint SlotsPerSide = 8;

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
        /// <paramref name="buffer"/> and returns how many. Stops when the buffer is full.
        /// </summary>
        public static int PlaceCell(
            ulong runSeed, ulong streamId, long cell, in SceneryCellContext context, ScenerySettings s, SceneryPiece[] buffer)
        {
            SceneryCellProfile profile = SceneryCellProfile.For(context.Beat, context.Layer, s.DensityOfWorld(context.WorldIndex), s);
            double cellStart = cell * (double)s.CellLengthM;
            int n = 0;
            for (int si = 0; si < 2; si++)
            {
                int side = si == 0 ? -1 : 1;
                float edge = CorridorEdgeM(side, context.Curvature, context.Beat, s);
                PlaceNear(runSeed, streamId, cell, cellStart, si, side, edge, in profile, s, buffer, ref n);
                PlaceMid(runSeed, streamId, cell, cellStart, si, side, edge, in profile, s, buffer, ref n);
                PlaceVines(runSeed, streamId, cell, cellStart, si, side, edge, in profile, s, buffer, ref n);
            }

            PlaceShaft(runSeed, streamId, cell, cellStart, in context, in profile, s, buffer, ref n);
            return n;
        }

        private static float Roll(ulong seed, ulong stream, long cell, SceneryBand band, uint slot, uint salt)
        {
            return SceneryHash.Roll(seed, stream, cell, band, slot, salt);
        }

        private static void Add(SceneryPiece[] buffer, ref int n, in SceneryPiece piece)
        {
            if (n < buffer.Length)
            {
                buffer[n++] = piece;
            }
        }

        private static void PlaceNear(
            ulong seed, ulong stream, long cell, double cellStart, int si, int side, float edge,
            in SceneryCellProfile profile, ScenerySettings s, SceneryPiece[] buffer, ref int n)
        {
            float slotLength = s.CellLengthM / NearSlots;
            for (int i = 0; i < NearSlots; i++)
            {
                uint slot = (uint)(si * SlotsPerSide) + (uint)i;
                if (Roll(seed, stream, cell, SceneryBand.Near, slot, SaltPresence) >= s.NearChance * profile.NearDensity)
                {
                    continue;
                }

                float pick = Roll(seed, stream, cell, SceneryBand.Near, slot, SaltPick);
                SceneryModel model = PickNear(pick, i, profile.NearTuftsOnly);
                float a = Roll(seed, stream, cell, SceneryBand.Near, slot, SaltScaleA);
                float b = Roll(seed, stream, cell, SceneryBand.Near, slot, SaltScaleB);

                float sxz;
                float sy;
                switch (model)
                {
                    case SceneryModel.GiantTrunk:
                        // Thin trunk: radius 0.18 to 0.30 m on the 1.5 m native trunk (diameter 0.6 m or less), 12 to 18 m tall on the 34 m native.
                        sxz = Mathf.Lerp(0.12f, 0.2f, a);
                        sy = Mathf.Lerp(0.35f, 0.5f, b);
                        break;
                    case SceneryModel.Bush:
                        sxz = Mathf.Lerp(0.9f, 1.5f, a);
                        sy = sxz * Mathf.Lerp(0.8f, 1.2f, b);
                        break;
                    case SceneryModel.Rock:
                        sxz = Mathf.Lerp(0.6f, 1.6f, a);
                        sy = sxz * Mathf.Lerp(0.5f, 1f, b);
                        break;
                    case SceneryModel.Root:
                        sxz = Mathf.Lerp(0.8f, 1.4f, a);
                        sy = sxz * Mathf.Lerp(0.6f, 1f, b);
                        break;
                    default:
                        sxz = Mathf.Lerp(0.8f, 1.5f, a);
                        sy = sxz * Mathf.Lerp(0.8f, 1.2f, b);
                        break;
                }

                float footprint = s.FootprintOf(model, sxz);
                float lo = edge + footprint + s.ClearanceMarginM;
                float hi = Mathf.Max(lo, s.NearMaxM);
                float lateral = Mathf.Lerp(lo, hi, Roll(seed, stream, cell, SceneryBand.Near, slot, SaltLateral));
                double along = cellStart + ((i + 0.2f + (0.6f * Roll(seed, stream, cell, SceneryBand.Near, slot, SaltAlong))) * slotLength);
                float yaw = 360f * Roll(seed, stream, cell, SceneryBand.Near, slot, SaltYaw);
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
                // Only the middle slot may hold a thin trunk, so two trunks are at least 7 m apart (spec: 4 m or more).
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

        private static void PlaceMid(
            ulong seed, ulong stream, long cell, double cellStart, int si, int side, float edge,
            in SceneryCellProfile profile, ScenerySettings s, SceneryPiece[] buffer, ref int n)
        {
            uint slot = (uint)(si * SlotsPerSide) + SlotMid;
            if (Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltPresence) >= s.MidChance * profile.MidDensity)
            {
                return;
            }

            float pick = Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltPick);
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

            float a = Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltScaleA);
            float b = Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltScaleB);
            float sxz;
            float sy;
            switch (model)
            {
                case SceneryModel.GiantTrunk:
                    sxz = Mathf.Lerp(0.55f, 0.95f, a);
                    sy = sxz * Mathf.Lerp(0.9f, 1.1f, b);
                    break;
                case SceneryModel.Bush:
                    sxz = Mathf.Lerp(2f, 3f, a);
                    sy = sxz * Mathf.Lerp(0.8f, 1.1f, b);
                    break;
                case SceneryModel.Rock:
                    sxz = Mathf.Lerp(2.5f, 4f, a);
                    sy = sxz * Mathf.Lerp(0.5f, 0.9f, b);
                    break;
                default:
                    sxz = Mathf.Lerp(1.4f, 2.2f, a);
                    sy = sxz * Mathf.Lerp(0.95f, 1.15f, b);
                    break;
            }

            float footprint = s.FootprintOf(model, sxz);
            float lo = Mathf.Max(edge + footprint + s.ClearanceMarginM, profile.MidMinM + footprint);
            float hi = Mathf.Max(lo, s.MidMaxM);
            float u = Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltLateral);
            float lateral = Mathf.Lerp(lo, hi, u * u);
            double along = cellStart + ((0.1f + (0.8f * Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltAlong))) * s.CellLengthM);
            float yaw = 360f * Roll(seed, stream, cell, SceneryBand.Mid, slot, SaltYaw);
            Add(buffer, ref n, new SceneryPiece(model, SceneryBand.Mid, side, lateral, along, sxz, sy, yaw, 0f, footprint, 0f));
        }

        private static void PlaceVines(
            ulong seed, ulong stream, long cell, double cellStart, int si, int side, float edge,
            in SceneryCellProfile profile, ScenerySettings s, SceneryPiece[] buffer, ref int n)
        {
            for (int k = 0; k < 2; k++)
            {
                uint slot = (uint)(si * SlotsPerSide) + SlotVine + (uint)k;
                if (Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltPresence) >= s.VineChance * profile.OverheadDensity)
                {
                    continue;
                }

                // Thin and short (3.6 to 6 m), hanging from 10 to 13 m: the lowest tip is above 4 m, far from any lane or grab vine.
                float sxz = Mathf.Lerp(0.45f, 0.7f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltScaleA));
                float sy = Mathf.Lerp(0.6f, 1f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltScaleB));
                float footprint = s.FootprintOf(SceneryModel.HangingVine, sxz);
                float lo = edge + footprint + s.ClearanceMarginM;
                float lateral = lo + (7f * Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltLateral));
                double along = cellStart + (((k * 0.5f) + (0.5f * Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltAlong))) * s.CellLengthM);
                float height = Mathf.Lerp(10f, 13f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltExtra));
                float yaw = 360f * Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltYaw);
                Add(buffer, ref n, new SceneryPiece(
                    SceneryModel.HangingVine, SceneryBand.Overhead, side, lateral, along, sxz, sy, yaw, 0f, footprint, height));
            }
        }

        private static void PlaceShaft(
            ulong seed, ulong stream, long cell, double cellStart, in SceneryCellContext context,
            in SceneryCellProfile profile, ScenerySettings s, SceneryPiece[] buffer, ref int n)
        {
            int every = Mathf.Max(1, s.ShaftEveryCells);
            long phase = ((cell % every) + every) % every;
            if (!profile.ShaftEveryCell && phase != 0L)
            {
                return;
            }

            const uint slot = SlotShaft;
            if (Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltPresence) >= profile.ShaftChance)
            {
                return;
            }

            int side = Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltExtra) < 0.5f ? -1 : 1;
            float edge = CorridorEdgeM(side, context.Curvature, context.Beat, s);
            float sxz = Mathf.Lerp(1.5f, 3f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltScaleA));
            float sy = Mathf.Lerp(10f, 15f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltScaleB));

            // The card stands on the ground and leans outward (its top is farther from the path than its foot), so the
            // footprint is just half its width: no part of it is nearer than the foot.
            float footprint = s.FootprintOf(SceneryModel.LightShaft, sxz);
            float lateral = edge + footprint + s.ClearanceMarginM + 1f + (6f * Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltLateral));
            double along = cellStart + (Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltAlong) * s.CellLengthM);
            float lean = Mathf.Lerp(12f, 18f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltYaw));

            // Turn the card toward the camera behind HERO (its normal along -forward) with a little jitter.
            float jitter = Mathf.Lerp(10f, 35f, Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltPick));
            float yaw = Roll(seed, stream, cell, SceneryBand.Overhead, slot, SaltAux) < 0.5f ? jitter : -jitter;

            // A positive roll about forward tips the top toward -x, so the right side (+1) rolls negative.
            float roll = -side * lean;
            Add(buffer, ref n, new SceneryPiece(
                SceneryModel.LightShaft, SceneryBand.Overhead, side, lateral, along, sxz, sy, yaw, roll, footprint, 0f));
        }
    }
}
