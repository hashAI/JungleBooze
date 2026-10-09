using System;
using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Validator rules added with the traversal slice (spec 102 §4.2, spec 103 §4.7, §5.4, §6): V3 and W5 (human
    /// lateral margins), V7 (camera visibility), V13 (vines), V14 (canopy beams), W1–W4 (water), curvature, and the
    /// Deep Breath passage bot run. Tools only (allocates).
    /// </summary>
    public sealed partial class ChunkValidator
    {
        /// <summary>Aspect ratios checked for V7: the narrowest supported landscape and portrait screens.</summary>
        public const float LandscapeAspect = 16f / 9f;

        public const float PortraitAspect = 9f / 19.5f;

        /// <summary>
        /// Gaps as the runner meets them: each gap patch minus later floor patches that cover it, sampled along the
        /// lane centre (a full-width "air" patch with beams on top becomes the beam gaps).
        /// </summary>
        public static List<CourseFloorPatch> EffectiveGaps(ChunkRuntime c)
        {
            var list = new List<CourseFloorPatch>();
            const float step = 0.05f;
            for (int i = 0; i < c.FloorCount; i++)
            {
                CourseFloorPatch p = c.GetFloor(i);
                if (p.Kind != CourseFloorKind.Gap)
                {
                    continue;
                }

                float runStart = float.NaN;
                int steps = (int)Math.Ceiling((p.SMax - p.SMin) / step);
                for (int k = 0; k <= steps; k++)
                {
                    float s = Math.Min(p.SMax, p.SMin + (k * step));
                    float sc = Math.Min(s, p.SMax - 1e-3f);
                    c.GetOuterBounds(sc, out float a, out float b);
                    float probe = Mathf.Clamp((p.XMin + p.XMax) * 0.5f, a, b);
                    c.GetLateralBounds(sc, probe, out float la, out float lb);
                    float x = Mathf.Clamp((la + lb) * 0.5f, p.XMin, p.XMax);
                    bool open = s < p.SMax && !c.TryGetFloor(sc, x, out _);
                    if (open && float.IsNaN(runStart))
                    {
                        runStart = s;
                    }
                    else if (!open && !float.IsNaN(runStart))
                    {
                        if (s - runStart > step * 0.5f)
                        {
                            list.Add(new CourseFloorPatch { Kind = CourseFloorKind.Gap, SMin = runStart, SMax = Math.Min(s, p.SMax), XMin = p.XMin, XMax = p.XMax });
                        }

                        runStart = float.NaN;
                    }
                }
            }

            return list;
        }

        /// <summary>A gap spanned by a vine (exempt from V4; V13 governs it).</summary>
        public static bool IsVineGap(ChunkRuntime c, in CourseFloorPatch gap)
        {
            for (int i = 0; i < c.VineCount; i++)
            {
                VineAnchor v = c.GetVine(i);
                if (gap.SMin >= v.LipS - 0.5f && gap.SMax <= v.LandingS + 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- V3 / W5: human-margin lateral shifts between steer-only constraints ----

        private struct SteerConstraint
        {
            public float S;
            public float X;
            public bool Water;
            public float Current;
            public List<float> Free;
        }

        private void CheckLateral(ChunkRuntime c, float vHigh, ValidationReport report)
        {
            float half = _movement.Hitbox.Width * 0.5f;
            float margin = _movement.Lateral.EdgeMargin;
            var constraints = new List<SteerConstraint>();
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                if (o.Class != ObstacleClass.Blocker && o.Class != ObstacleClass.Rock)
                {
                    continue;
                }

                bool grouped = false;
                for (int k = 0; k < constraints.Count; k++)
                {
                    if (Math.Abs(constraints[k].S - o.SMin) < 0.5f && Compatible(c, new RequiredAction(constraints[k].S, constraints[k].X, true), new RequiredAction(o.SMin, o.CenterX, true)))
                    {
                        grouped = true;
                    }
                }

                if (grouped)
                {
                    continue;
                }

                float s = o.SMin + 0.01f;
                c.GetLateralBounds(s, o.CenterX, out float laneMin, out float laneMax);
                var blocked = new List<float>();
                for (int k = 0; k < c.ObstacleCount; k++)
                {
                    ObstacleBox other = c.GetObstacle(k);
                    if ((other.Class != ObstacleClass.Blocker && other.Class != ObstacleClass.Rock) || other.SMin > s + 0.5f || other.SMax < s ||
                        other.XMax <= laneMin || other.XMin >= laneMax)
                    {
                        continue;
                    }

                    float shrink = _movement.Hitbox.ObstacleShrinkX;
                    blocked.Add(other.XMin + shrink - half);
                    blocked.Add(other.XMax - shrink + half);
                }

                bool water = c.TryGetWater(s, o.CenterX, out _);
                c.GetCurrentAt(s, out float cx, out _);
                constraints.Add(new SteerConstraint
                {
                    S = o.SMin,
                    X = o.CenterX,
                    Water = water,
                    Current = water ? Math.Abs(cx) : 0f,
                    Free = FreeIntervals(blocked, laneMin + margin, laneMax - margin),
                });
            }

            constraints.Sort((u, w) => u.S.CompareTo(w.S));
            for (int i = 1; i < constraints.Count; i++)
            {
                SteerConstraint b = constraints[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    SteerConstraint a = constraints[j];
                    if (!Compatible(c, new RequiredAction(a.S, a.X, true), new RequiredAction(b.S, b.X, true)))
                    {
                        continue;
                    }

                    float shift = IntervalDistance(a.Free, b.Free);
                    float v = b.Water ? (_movement.Swim.SpeedFactor * vHigh) : vHigh;
                    float dt = (b.S - a.S) / Math.Max(1f, v);
                    float rate = b.Water ? _director.HumanSwimMargin * (_movement.Swim.VLatMax - b.Current) : _director.HumanLateralRate;
                    float allowed = rate * (dt - _director.HumanReactionTime);
                    if (shift > allowed + 0.01f)
                    {
                        report.Add(b.Water ? "W5" : "V3", b.S, "lateral shift " + F(shift) + " m in " + F(dt) + " s needs > " + F(rate) + " m/s human margin (" + F(a.S) + " → " + F(b.S) + ")");
                    }

                    break;
                }
            }
        }

        private static List<float> FreeIntervals(List<float> blocked, float lo, float hi)
        {
            var free = new List<float>();
            int n = blocked.Count / 2;
            var starts = new float[n];
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                starts[i] = blocked[2 * i];
                ends[i] = blocked[(2 * i) + 1];
            }

            Array.Sort(starts, ends);
            float cursor = lo;
            for (int i = 0; i < n; i++)
            {
                if (starts[i] > cursor)
                {
                    free.Add(cursor);
                    free.Add(Math.Min(starts[i], hi));
                }

                cursor = Math.Max(cursor, ends[i]);
                if (cursor >= hi)
                {
                    break;
                }
            }

            if (cursor < hi)
            {
                free.Add(cursor);
                free.Add(hi);
            }

            return free;
        }

        private static float IntervalDistance(List<float> a, List<float> b)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < a.Count; i += 2)
            {
                for (int j = 0; j + 1 < b.Count; j += 2)
                {
                    float d = a[i + 1] < b[j] ? b[j] - a[i + 1] : b[j + 1] < a[i] ? a[i] - b[j + 1] : 0f;
                    best = Math.Min(best, d);
                }
            }

            return best == float.MaxValue ? 0f : best;
        }

        // ---- W1–W4 (and W5 currents) ----

        private void CheckWater(ChunkRuntime c, in PhaseRule rule, float vLow, float vHigh, ValidationReport report)
        {
            if (c.WaterCount == 0)
            {
                return;
            }

            SwimConfig sw = _movement.Swim;
            for (int i = 0; i < c.CurrentCount; i++)
            {
                WaterCurrent cur = c.GetCurrent(i);
                if (Math.Abs(cur.Lateral) > sw.MaxLateralCurrent + 1e-3f || Math.Abs(cur.Forward) > sw.MaxForwardCurrent + 1e-3f)
                {
                    report.Add("W5", cur.SMin, "current " + F(cur.Lateral) + "/" + F(cur.Forward) + " m/s exceeds " + F(sw.MaxLateralCurrent) + "/" + F(sw.MaxForwardCurrent));
                }
            }

            // W2: no Crash-capable blocker and no gap in a water volume.
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                if (c.TryGetWater(o.SMin, o.CenterX, out _) && o.Class == ObstacleClass.Blocker)
                {
                    report.Add("W2", o.SMin, "blocker in water: " + c.GetLabel(i));
                }
            }

            List<CourseFloorPatch> gaps = EffectiveGaps(c);
            for (int i = 0; i < gaps.Count; i++)
            {
                if (c.TryGetWater(gaps[i].SMin, Mathf.Clamp(0f, gaps[i].XMin, gaps[i].XMax), out _))
                {
                    report.Add("W2", gaps[i].SMin, "gap in water");
                }
            }

            // W1 / W3: full-width water obstacles name their answer and keep the action gap at swim speed.
            var actions = new List<RequiredAction>();
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                if (!RunnerSimulation.IsWaterClass(o.Class))
                {
                    continue;
                }

                float s = o.SMin + 0.01f;
                c.GetLateralBounds(s, o.CenterX, out float a, out float b);
                bool full = o.XMin <= a + LaneMargin && o.XMax >= b - LaneMargin;
                if (!full)
                {
                    continue;
                }

                if (o.Class == ObstacleClass.Rock)
                {
                    report.Add("W3", o.SMin, "full-width rock has no dive/leap answer: " + c.GetLabel(i));
                    continue;
                }

                actions.Add(new RequiredAction(o.SMin, o.CenterX, o.Class == ObstacleClass.Snag));
            }

            for (int i = 1; i < actions.Count; i++)
            {
                c.GetCurrentAt(actions[i - 1].S, out _, out float cs);
                float vSwim = (sw.SpeedFactor * vHigh) + cs;
                float dt = (actions[i].S - actions[i - 1].S) / Math.Max(1f, vSwim);
                if (dt + Tolerance < rule.MinActionGap)
                {
                    report.Add("W1", actions[i].S, "water actions " + F(actions[i - 1].S) + " → " + F(actions[i].S) + " are " + F(dt) + " s apart at swim speed " + F(vSwim) + " m/s");
                }
            }

            // W4: clear water after entry and before exit (depth along the centre line).
            float entry = float.NaN;
            float exit = float.NaN;
            for (float s = 0f; s <= c.Length; s += 0.25f)
            {
                if (!c.TryGetWater(s, 0f, out float surface))
                {
                    continue;
                }

                float depth = c.TryGetFloor(s, 0f, out float floor) ? surface - floor : 100f;
                if (float.IsNaN(entry) && depth >= sw.EnterDepth)
                {
                    entry = s;
                }

                if (depth >= sw.ExitDepth)
                {
                    exit = s;
                }
            }

            if (!float.IsNaN(entry))
            {
                float clear = _director.WaterEntryClearance;
                for (int i = 0; i < c.ObstacleCount; i++)
                {
                    ObstacleBox o = c.GetObstacle(i);
                    if ((o.SMax >= entry && o.SMin <= entry + clear) || (o.SMax >= exit - clear && o.SMin <= exit))
                    {
                        report.Add("W4", o.SMin, "obstacle within " + F(clear) + " m of the water entry (" + F(entry) + ") or exit (" + F(exit) + "): " + c.GetLabel(i));
                    }
                }
            }
        }

        // ---- V14: canopy beams ----

        private void CheckBeams(ChunkRuntime c, in PhaseRule rule, float vLow, float vHigh, ValidationReport report)
        {
            CanopyConfig cc = _movement.Canopy;
            var beams = new List<CourseFloorPatch>();
            for (int i = 0; i < c.FloorCount; i++)
            {
                CourseFloorPatch p = c.GetFloor(i);
                if (p.Kind == CourseFloorKind.Ramp && p.XMax - p.XMin < 9f && c.IsCanopy(p.SMin + 0.01f))
                {
                    beams.Add(p);
                }
            }

            if (beams.Count == 0)
            {
                return;
            }

            beams.Sort((a, b) => a.SMin.CompareTo(b.SMin));
            List<RequiredAction> actions = RequiredActions(c);
            float margin = _movement.Lateral.EdgeMargin;
            for (int i = 0; i < beams.Count; i++)
            {
                CourseFloorPatch b = beams[i];
                float width = b.XMax - b.XMin;
                if (width < cc.MinBeamWidth - 0.01f || width > cc.MaxBeamWidth + 0.01f)
                {
                    report.Add("V14", b.SMin, "beam width " + F(width) + " m outside " + F(cc.MinBeamWidth) + "–" + F(cc.MaxBeamWidth));
                }

                if (width < cc.MinCorridor - 0.01f)
                {
                    report.Add("V14", b.SMin, "free corridor on the beam " + F(width) + " m (< " + F(cc.MinCorridor) + ")");
                }

                if (i == 0)
                {
                    continue;
                }

                CourseFloorPatch a = beams[i - 1];
                float gap = b.SMin - a.SMax;
                if (gap < 0.05f)
                {
                    continue;
                }

                if (gap < cc.MinGap - 0.01f || gap > cc.MaxGap + 0.01f || gap > (0.75f * 0.60f * vLow) + 0.01f)
                {
                    report.Add("V14", a.SMax, "beam gap " + F(gap) + " m outside " + F(cc.MinGap) + "–" + F(cc.MaxGap) + " (and V4 at " + F(vLow) + " m/s)");
                }

                float offset = Math.Abs(((b.XMin + b.XMax) * 0.5f) - ((a.XMin + a.XMax) * 0.5f));
                if (offset > cc.MaxBeamOffset + 0.01f)
                {
                    report.Add("V14", b.SMin, "next beam offset " + F(offset) + " m (> " + F(cc.MaxBeamOffset) + ")");
                }

                // Required shift (review S11): from the worst point of beam a (the far edge) into the landing window of
                // beam b (with the assist).
                float fromMin = a.XMin + margin;
                float fromMax = a.XMax - margin;
                float toMin = b.XMin - cc.BeamLandingAssist;
                float toMax = b.XMax + cc.BeamLandingAssist;
                float shift = Math.Max(0f, Math.Max(toMin - fromMin, fromMax - toMax));
                float previous = a.SMin;
                for (int k = 0; k < actions.Count; k++)
                {
                    if (actions[k].S < a.SMax - 0.01f && actions[k].S > previous)
                    {
                        previous = actions[k].S;
                    }
                }

                // Ground time from the last action to the lip at the full human rate, air time over the gap at
                // AirLateralFactor; the reaction time is taken from the ground part first.
                float v = Math.Max(1f, vHigh);
                float tGround = Math.Max(0f, a.SMax - previous) / v;
                float tAir = gap / v;
                float reactGround = Math.Min(tGround, _director.HumanReactionTime);
                float reactAir = Math.Min(tAir, _director.HumanReactionTime - reactGround);
                float dt = tGround + tAir;
                float allowed = _director.HumanLateralRate * ((tGround - reactGround) + (_movement.Lateral.AirLateralFactor * (tAir - reactAir)));
                if (shift > allowed + 0.01f)
                {
                    report.Add("V14", b.SMin, "lateral shift " + F(shift) + " m to the next beam in " + F(dt) + " s exceeds the human margin");
                }
            }
        }

        // ---- V15: revive points (review S4) ----

        /// <summary>
        /// V15: a death at any gap must leave a revive point with a clear run-in (<see cref="RunnerSimulation.ReviveRunIn"/>
        /// at <paramref name="vHigh"/>): the floor before each non-vine gap, back to the previous gap in the same lane
        /// (or the chunk entry), must be at least that long within the simulation's safe-point reach.
        /// </summary>
        private void CheckRevivePoints(ChunkRuntime c, float vHigh, ValidationReport report)
        {
            float runIn = RunnerSimulation.ReviveRunIn(_movement, vHigh);
            float back = _movement.Health.ReviveBackDistance;
            List<CourseFloorPatch> gaps = EffectiveGaps(c);
            gaps.Sort((u, w) => u.SMin.CompareTo(w.SMin));
            for (int i = 0; i < gaps.Count; i++)
            {
                CourseFloorPatch g = gaps[i];
                if (IsVineGap(c, g))
                {
                    continue;
                }

                float floorStart = 0f;
                for (int k = i - 1; k >= 0; k--)
                {
                    CourseFloorPatch p = gaps[k];
                    if (p.XMax > g.XMin && p.XMin < g.XMax)
                    {
                        // A vine gap ends at its landing platform (the swing is automatic).
                        floorStart = p.SMax;
                        break;
                    }
                }

                float run = g.SMin - floorStart;
                if (run + 0.01f < runIn || g.SMin - back - floorStart < 0f)
                {
                    report.Add("V15", g.SMin, "only " + F(run) + " m of floor before the gap: a revive needs " + F(runIn) + " m clear run-in at " + F(vHigh) + " m/s");
                }
            }
        }

        // ---- V13: vines ----

        private void CheckVineLayout(ChunkRuntime c, ValidationReport report)
        {
            VineConfig vc = _movement.Vine;
            for (int i = 0; i < c.VineCount; i++)
            {
                VineAnchor v = c.GetVine(i);
                for (float s = v.LipS - 8f; s <= v.LipS - 0.1f; s += 0.5f)
                {
                    c.GetOuterBounds(s, out float a, out float b);
                    if (b - a > vc.FunnelMaxWidth + 0.01f || Math.Abs(((a + b) * 0.5f) - v.X) > 0.05f)
                    {
                        report.Add("V13", s, "takeoff funnel " + F(b - a) + " m wide, centre " + F((a + b) * 0.5f) + " (must be ≤ " + F(vc.FunnelMaxWidth) + " m centred on the vine at " + F(v.X) + ")");
                        break;
                    }
                }

                if (v.LandingS < v.AnchorS + vc.MinLandingOffset - 0.01f)
                {
                    report.Add("V13", v.LandingS, "landing platform starts " + F(v.LandingS - v.AnchorS) + " m after the anchor (< " + F(vc.MinLandingOffset) + ")");
                }

                if (!c.TryGetFloor(v.LandingS + 0.5f, v.X, out _))
                {
                    report.Add("V13", v.LandingS, "no landing floor at the platform start");
                }
            }
        }

        /// <summary>V13 sweep: every release tick (held early, the window, auto) at both speeds.</summary>
        public void CheckVines(ChunkRuntime c, float vLow, float vHigh, ValidationReport report)
        {
            if (c.VineCount == 0)
            {
                return;
            }

            var probe = new RunnerSimulation(_movement, new WorldPath(), 1f / 60f, null);
            for (int i = 0; i < c.VineCount; i++)
            {
                for (int sp = 0; sp < 2; sp++)
                {
                    float speed = sp == 0 ? vLow : vHigh;
                    TryVine(c, i, speed, 10, report);
                    for (int k = probe.ReleaseOpenTick; k <= probe.SwingTicks; k++)
                    {
                        TryVine(c, i, speed, k, report);
                    }

                    TryVine(c, i, speed, -1, report);
                }
            }
        }

        /// <summary>One vine trial: swipe up on swing tick <paramref name="releaseTick"/> (−1 = never: auto-release).</summary>
        public VineTrial TryVine(ChunkRuntime c, int vineIndex, float speed, int releaseTick, ValidationReport report)
        {
            VineAnchor anchor = c.GetVine(vineIndex);
            var path = new WorldPath();
            path.Append(c, new ChunkPick { Entry = c.LibraryIndex, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            var sim = new RunnerSimulation(_movement, path, 1f / 60f, new RunEventBuffer(16)) { MuteEvents = true };
            sim.Reset(new RunOptions { ForcedSpeed = speed, SkipStartRamp = true, StartS = anchor.LipS - 20f, StartX = anchor.X });
            float takeoff = c.GetVineTakeoffY(vineIndex);
            var trial = new VineTrial { ReleaseTick = -1 };
            bool sent = false;
            bool swung = false;
            var crystalTaken = new bool[path.NextCrystalId];
            int[] ids = new int[32];
            float corridor = _movement.Vine.SwingCorridor;
            for (int t = 0; t < 900; t++)
            {
                InputCommand command = InputCommand.None;
                if (sim.State.Mode == MoveMode.Swing)
                {
                    int next = sim.SwingTick + 1;
                    if (!sent && releaseTick >= 0 && next == releaseTick)
                    {
                        command = InputCommand.Jump;
                        sent = true;
                    }
                }

                sim.Step(InputFrame.FromCommands(command));
                ref readonly RunnerState st = ref sim.State;
                if (st.Mode == MoveMode.Swing)
                {
                    swung = true;
                }

                if (swung && (st.Mode == MoveMode.Swing || st.VineAir))
                {
                    if (st.Releases > 0 && trial.ReleaseTick < 0)
                    {
                        trial.ReleaseTick = (int)(st.Tick - st.GrabTick);
                        trial.Perfect = st.LastReleasePerfect;
                    }

                    // 1.0 m clear corridor around the arc and the release trajectory.
                    sim.GetHitbox(out float s0, out float s1, out float x0, out float x1, out float y0, out float y1);
                    int n = path.FindObstacles(s0 - corridor, s1 + corridor, ids);
                    for (int k = 0; k < n; k++)
                    {
                        ObstacleBox o = path.GetObstacle(ids[k]);
                        if (o.SMin < s1 + corridor && o.SMax > s0 - corridor && o.XMin < x1 + corridor && o.XMax > x0 - corridor && o.YMin < y1 + corridor && o.YMax > y0 - corridor)
                        {
                            trial.CorridorBreach = true;
                        }
                    }

                    // Column crystals (the tracker's pad).
                    for (int id = path.FirstCrystalId; id < path.NextCrystalId; id++)
                    {
                        CoinPoint cr = path.GetCrystal(id);
                        if (!crystalTaken[id] && InColumn(anchor, takeoff, cr) &&
                            Math.Abs(cr.S - ((s0 + s1) * 0.5f)) <= ((s1 - s0) * 0.5f) + _crystalPad && Math.Abs(cr.X - ((x0 + x1) * 0.5f)) <= ((x1 - x0) * 0.5f) + _crystalPad &&
                            cr.Y >= y0 - _crystalPad && cr.Y <= y1 + _crystalPad)
                        {
                            crystalTaken[id] = true;
                            trial.ColumnItems++;
                        }
                    }
                }

                if (st.Dead || (swung && st.Mode == MoveMode.Run && !st.VineAir && st.Grounded))
                {
                    break;
                }
            }

            ref readonly RunnerState end = ref sim.State;
            for (int id = path.FirstCoinId; id < path.NextCoinId; id++)
            {
                if (sim.IsCoinCollected(id) && InColumn(anchor, takeoff, path.GetCoin(id)))
                {
                    trial.ColumnItems++;
                }
            }

            trial.Landed = swung && !end.Dead && end.Grounded && end.Mode == MoveMode.Run;
            trial.LandingS = end.S;
            trial.Hits = end.Hits;
            trial.Dead = end.Dead;
            if (report != null)
            {
                string at = " (vine " + (vineIndex + 1) + ", release " + (releaseTick < 0 ? "auto" : "tick " + releaseTick) + ", " + F(speed) + " m/s)";
                if (!swung)
                {
                    report.Add("V13", anchor.LipS, "never grabbed the vine" + at);
                }
                else if (!trial.Landed || trial.Hits > 0)
                {
                    report.Add("V13", end.S, "release did not land cleanly: " + (end.Dead ? end.Cause.ToString() : end.Hits + " hit(s)") + at);
                }
                else if (trial.LandingS < anchor.LandingS)
                {
                    report.Add("V13", end.S, "landed before the platform" + at);
                }

                if (trial.CorridorBreach)
                {
                    report.Add("V13", anchor.AnchorS, "obstacle within the 1.0 m swing corridor" + at);
                }

                if (trial.Perfect && trial.ColumnItems == 0)
                {
                    report.Add("V13", anchor.ColumnS0, "a Perfect arc missed the perfect column" + at);
                }

                if (!trial.Perfect && trial.ColumnItems > 0)
                {
                    report.Add("V13", anchor.ColumnS0, "a Good arc reached the perfect column" + at);
                }

                report.BotRuns++;
            }

            return trial;
        }

        private static bool InColumn(in VineAnchor v, float takeoff, in CoinPoint item)
        {
            return item.S >= v.ColumnS0 - 0.6f && item.S <= v.ColumnS1 + 0.6f && item.Y - takeoff >= 2.0f;
        }

        /// <summary>Result of one vine release trial (V13).</summary>
        public struct VineTrial
        {
            public int ReleaseTick;
            public bool Perfect;
            public bool Landed;
            public bool Dead;
            public int Hits;
            public float LandingS;
            public int ColumnItems;
            public bool CorridorBreach;
        }

        // ---- Deep Breath passage (V1 with the ability) ----

        private void CheckDeepDives(ChunkRuntime c, float speed, ValidationReport report)
        {
            if (c.DeepDiveCount == 0)
            {
                return;
            }

            RunnerState end = RunBot(c, -1, speed, report, true);
            if (end.Dead || end.Hits > 0)
            {
                return;
            }

            // The bot must actually have taken the passage (the D-04 crossing is checked by the run tests).
            var path = new WorldPath();
            path.Append(c, new ChunkPick { Entry = c.LibraryIndex, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            var sim = new RunnerSimulation(_movement, path, 1f / 60f, new RunEventBuffer(16)) { MuteEvents = true };
            sim.Reset(new RunOptions { ForcedSpeed = speed, SkipStartRamp = true, DeepBreath = true, DeepDiveDepth = -2.5f, DeepDiveTime = 2.4f });
            var bot = new PerfectBot(sim, false) { StrictWaterAnswers = true, UseDeepDives = true };
            bool deep = false;
            for (int t = 0; t < 60 * 120 && sim.State.S < c.Length && !sim.State.Dead; t++)
            {
                sim.Step(bot.ReadInput(sim.State.Tick));
                deep |= sim.State.Mode == MoveMode.DeepDive;
            }

            if (!deep)
            {
                report.Add("V1", 0f, "the Perfect bot with Deep Breath never entered a Deep Breath zone");
            }
        }

        // ---- V8: view-side curvature ----

        private void CheckCurve(ChunkRuntime c, ValidationReport report)
        {
            List<CurveKey> keys = c.Variant.Curve;
            if (keys == null || keys.Count == 0)
            {
                return;
            }

            if (c.Curve.MaxCurvature > _director.MaxCurvature + 1e-5f)
            {
                report.Add("V8", 0f, "curve radius " + F(1f / c.Curve.MaxCurvature) + " m (< " + F(1f / _director.MaxCurvature) + " m)");
            }

            for (float s = 0f; s <= c.Length; s += 0.5f)
            {
                if ((s < SeamZone || s > c.Length - SeamZone) && Math.Abs(PathCurve.CurvatureAt(keys, s)) > 1e-6)
                {
                    report.Add("V8", s, "curvature in a seam zone (seams are straight)");
                    break;
                }
            }
        }

        // ---- V7: camera visibility ----

        private void CheckVisibility(ChunkRuntime c, in PhaseRule rule, float vLow, float vHigh, ValidationReport report)
        {
            if (_cameras.Count == 0)
            {
                return;
            }

            float lead = rule.Visibility > 0f ? rule.Visibility : 1.5f;
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                for (int cam = 0; cam < _cameras.Count; cam++)
                {
                    float aspect = cam == 0 ? LandscapeAspect : PortraitAspect;
                    for (int sp = 0; sp < 2; sp++)
                    {
                        float v = sp == 0 ? vLow : vHigh;
                        if (!ObstacleVisible(c, i, o, _cameras[cam], aspect, v, lead, out string why))
                        {
                            report.Add("V7", o.SMin, c.GetLabel(i) + " not visible " + F(lead) + " s before contact (" + _cameras[cam].Name + ", " + F(v) + " m/s): " + why);
                        }
                    }
                }
            }
        }

        private bool ObstacleVisible(ChunkRuntime c, int index, in ObstacleBox o, CameraProfile profile, float aspect, float speed, float lead, out string why)
        {
            why = string.Empty;
            float s = o.SMin + 0.01f;
            c.GetLateralBounds(s, o.CenterX, out float laneMin, out float laneMax);
            float laneCentre = (laneMin + laneMax) * 0.5f;
            float sr = o.SMin - (speed * lead);
            float xr;
            float ground;
            if (sr >= 0f)
            {
                c.GetLateralBounds(sr, laneCentre, out float rMin, out float rMax);
                xr = Mathf.Clamp(laneCentre, rMin + 0.3f, Math.Max(rMin + 0.3f, rMax - 0.3f));
                if (c.TryGetWater(sr, xr, out float surface))
                {
                    ground = surface;
                }
                else if (!c.TryGetFloor(sr, xr, out ground))
                {
                    ground = c.BaseHeight(sr, xr);
                }
            }
            else
            {
                // Review S5: the viewpoint is inside the previous chunk. Seams only guarantee a 7 m wide, flat,
                // straight run of SeamZone metres; before that the previous chunk may curve at the tightest allowed
                // radius either way. Every case must see the obstacle.
                xr = Mathf.Clamp(laneCentre, -SeamHalfWidth + 0.3f, SeamHalfWidth - 0.3f);
                ground = 0f;
            }

            int cases = sr < SeamZone + 10f ? 3 : 1; // the camera sits up to ~10 m behind the runner
            for (int k = 0; k < cases; k++)
            {
                float kappa = k == 0 ? 0f : k == 1 ? _director.MaxCurvature : -_director.MaxCurvature;
                if (!VisibleFrom(c, index, o, profile, aspect, speed, laneCentre, laneMin, laneMax, sr, xr, ground, kappa, out why))
                {
                    if (cases > 1)
                    {
                        why += k == 0 ? " (straight run-in)" : " (previous chunk curving " + (k == 1 ? "one way" : "the other way") + ")";
                    }

                    return false;
                }
            }

            return true;
        }

        private bool VisibleFrom(ChunkRuntime c, int index, in ObstacleBox o, CameraProfile profile, float aspect, float speed, float laneCentre, float laneMin, float laneMax, float sr, float xr, float ground, float kappa, out string why)
        {
            why = string.Empty;
            var rig = new CameraRigModel(profile, _movement.Speed.V0, _movement.Speed.VMax, _movement.Lateral.VLatMax);
            ExtendedFrame(c, sr, kappa, out _, out _, out float runnerHeading);
            rig.Snap(new CameraTargetInput { S = sr, X = xr, Y = ground, GroundY = ground, Speed = speed, PathYawDeg = runnerHeading * Mathf.Rad2Deg });
            CameraPose pose = rig.Pose;
            Vector3 camera = ExtendedPoint(c, pose.S, pose.X, pose.Y, kappa);
            Quaternion rotation = Quaternion.Euler(pose.PitchDeg, pose.YawDeg, pose.RollDeg);
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(camera, rotation, Vector3.one).inverse;
            Matrix4x4 vp = Matrix4x4.Perspective(pose.FovDeg, aspect, profile.NearClip, profile.FarClip) * view;

            // Sample points across the obstacle (centre of the lane, both ends) at two heights: a visible part is enough.
            bool hanging = o.Class == ObstacleClass.High || o.Class == ObstacleClass.LowBranch;
            float yLow = hanging ? o.YMin + 0.05f : o.YMin + (Math.Min(1.0f, o.YMax - o.YMin) * 0.5f);
            float yHigh = hanging ? o.YMin + 0.3f : Math.Min(o.YMax, o.YMin + 1.5f) - 0.05f;
            float xa = Mathf.Clamp(laneCentre, o.XMin, o.XMax);
            bool inFrustum = false;
            for (int k = 0; k < 6; k++)
            {
                float px = (k % 3) == 0 ? xa : (k % 3) == 1 ? Math.Max(o.XMin + 0.2f, laneMin) : Math.Min(o.XMax - 0.2f, laneMax);
                float y = k < 3 ? yLow : yHigh;
                if (!CameraMath.Contains(vp, CurvedPoint(c, o.SMin, px, y)))
                {
                    continue;
                }

                inFrustum = true;
                if (!Occluded(c, index, pose.S, pose.X, pose.Y, o.SMin, px, y))
                {
                    return true;
                }
            }

            why = inFrustum ? "occluded" : "outside the frustum";
            return false;
        }

        /// <summary>
        /// Centreline frame at s, extended before the chunk entry (s &lt; 0) by a straight seam zone and then an arc of
        /// curvature <paramref name="kappa"/> (review S5). Heading in radians; forward = (sin h, cos h).
        /// </summary>
        private static void ExtendedFrame(ChunkRuntime c, float s, float kappa, out float cx, out float cz, out float heading)
        {
            if (s >= 0f)
            {
                c.Curve.Evaluate(s, out cx, out cz, out heading);
                return;
            }

            c.Curve.Evaluate(0f, out float x0, out float z0, out float h0);
            float straight = Math.Min(-s, SeamZone);
            float px = x0 - ((float)Math.Sin(h0) * straight);
            float pz = z0 - ((float)Math.Cos(h0) * straight);
            float arc = -s - straight;
            if (arc <= 0f || Math.Abs(kappa) < 1e-6f)
            {
                cx = px - ((float)Math.Sin(h0) * arc);
                cz = pz - ((float)Math.Cos(h0) * arc);
                heading = h0;
                return;
            }

            // Walking back along an arc that ends (forward) at heading h0: start heading hs = h0 − κ·L.
            float hs = h0 - (kappa * arc);
            cx = px - (((float)Math.Cos(hs) - (float)Math.Cos(h0)) / kappa);
            cz = pz - (((float)Math.Sin(h0) - (float)Math.Sin(hs)) / kappa);
            heading = hs;
        }

        private static Vector3 ExtendedPoint(ChunkRuntime c, float s, float x, float y, float kappa)
        {
            ExtendedFrame(c, s, kappa, out float cx, out float cz, out float h);
            float cos = (float)Math.Cos(h);
            float sin = (float)Math.Sin(h);
            return new Vector3(cx + (cos * x), y, cz - (sin * x));
        }

        private static Vector3 CurvedPoint(ChunkRuntime c, float s, float x, float y)
        {
            c.Curve.Evaluate(s, out float cx, out float cz, out float h);
            float cos = (float)Math.Cos(h);
            float sin = (float)Math.Sin(h);
            return new Vector3(cx + (cos * x), y, cz - (sin * x));
        }

        /// <summary>Path-space ray test against other obstacles, dividers and rising floor.</summary>
        private static bool Occluded(ChunkRuntime c, int self, float s0, float x0, float y0, float s1, float x1, float y1)
        {
            const int samples = 48;
            for (int k = 1; k < samples; k++)
            {
                float u = (float)k / samples;
                float s = s0 + ((s1 - s0) * u);
                float x = x0 + ((x1 - x0) * u);
                float y = y0 + ((y1 - y0) * u);
                if (s > s1 - 0.3f)
                {
                    break;
                }

                if (s >= 0f && c.TryGetFloor(s, x, out float floor) && y < floor - 0.05f)
                {
                    return true;
                }

                for (int i = 0; i < c.ObstacleCount; i++)
                {
                    if (i == self)
                    {
                        continue;
                    }

                    ObstacleBox o = c.GetObstacle(i);

                    // Hanging obstacles are a beam with strands (the collision box is taller than what hides things).
                    float top = o.Class == ObstacleClass.High || o.Class == ObstacleClass.LowBranch ? Math.Min(o.YMax, o.YMin + 0.35f) : o.YMax;
                    if (s > o.SMin + 0.05f && s < o.SMax - 0.05f && x > o.XMin + 0.05f && x < o.XMax - 0.05f && y > o.YMin + 0.05f && y < top - 0.05f)
                    {
                        return true;
                    }
                }

                for (int d = 0; d < c.DividerCount; d++)
                {
                    ChunkDivider dv = c.GetDivider(d);
                    if (s > dv.SFront && s < dv.SMerge && x > dv.XMin && x < dv.XMax)
                    {
                        c.TryGetFloor(dv.SFront, dv.CenterX, out float dy);
                        float top = s < dv.SFront + 1.2f ? dy + 3.2f : dy + 1.0f;
                        if (y < top)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
