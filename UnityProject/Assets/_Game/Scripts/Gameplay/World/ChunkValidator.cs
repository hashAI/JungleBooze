using System;
using System.Collections.Generic;
using System.Globalization;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Offline chunk validator (spec 102 §4; editor setup and CI, never at runtime). Static rules: V2 (action gaps),
    /// V3 (human-margin lateral rate), V4 (gap lengths, clear run-up), V5 (jump↔slide spacing), V6 (free corridor),
    /// V7 (camera visibility, both profiles, curved frame), V8 (seams, widths, narrowing, curvature), V9 (dividers),
    /// V10 (secret entrances), V12 (locked routes have no path), V14 (canopy beams), V15 (revive run-in), W1–W5 (swim). V1: the Perfect
    /// bot drives the real simulation through every open route (and the Deep Breath passage) at each requested speed
    /// and must take 0 hits. V13 (vines): every release tick at both speeds lands on the platform with 0 hits, keeps
    /// the 1 m corridor, and only Perfect arcs reach the perfect column. V11 is the director's (pick time).
    /// Allocates; tools only.
    /// </summary>
    public sealed partial class ChunkValidator
    {
        public const float SeamZone = 6f;
        public const float SeamHalfWidth = 3.5f;
        public const float MaxNarrowing = 0.10f;
        public const float LaneMargin = 0.3f;
        public const float JumpSlideGap = 0.75f;
        public const float Tolerance = 0.011f;

        private readonly MovementConfig _movement;
        private readonly WorldDirectorConfig _director;
        private readonly SpeedCurve _speed;
        private readonly List<CameraRig.CameraProfile> _cameras;
        private readonly float _crystalPad;

        public ChunkValidator(MovementConfig movement, WorldDirectorConfig director, IList<CameraRig.CameraProfile> cameras = null, float crystalPad = 0.4f)
        {
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _director = director ?? throw new ArgumentNullException(nameof(director));
            _speed = new SpeedCurve(movement.Speed);
            _cameras = cameras != null ? new List<CameraRig.CameraProfile>(cameras) : new List<CameraRig.CameraProfile>();
            _crystalPad = crystalPad;
        }

        /// <summary>Lowest and highest speed of a phase range (spec 102 §4.1).</summary>
        public void SpeedExtremes(DifficultyPhase min, DifficultyPhase max, out float low, out float high)
        {
            low = _speed.Evaluate(_director.RuleFor(min).StartDistance);
            int next = -1;
            for (int i = 0; i < _director.Phases.Count; i++)
            {
                if (_director.Phases[i].Phase == max && i + 1 < _director.Phases.Count)
                {
                    next = i + 1;
                }
            }

            high = next >= 0 ? _speed.Evaluate(_director.Phases[next].StartDistance) : _movement.Speed.VMax;
        }

        /// <summary>Full validation of a pool variant over its phase range: static rules + the bot at both extremes.</summary>
        public ValidationReport ValidatePool(ChunkRuntime chunk)
        {
            ChunkDefinition def = chunk.Definition;
            var report = new ValidationReport(chunk.Id, chunk.VariantName);
            for (DifficultyPhase p = def.PhaseMin; p <= def.PhaseMax; p++)
            {
                SpeedExtremes(p, p, out float low, out float high);
                CheckStatic(chunk, _director.RuleFor(p), low, high, report);
            }

            SpeedExtremes(def.PhaseMin, def.PhaseMax, out float vLow, out float vHigh);
            CheckVisibility(chunk, _director.RuleFor(def.PhaseMin), vLow, vHigh, report);
            CheckBot(chunk, vLow, report);
            CheckBot(chunk, vHigh, report);
            CheckVines(chunk, vLow, vHigh, report);
            CheckDeepDives(chunk, vLow, report);
            return report;
        }

        /// <summary>
        /// Script-only variants (spec 103 §10.1): static rules under the entry's phase rules and the bot at the met
        /// speed −0.5, +0.5 m/s and at 0.80× (stumble).
        /// </summary>
        public ValidationReport ValidateScripted(ChunkRuntime chunk, DifficultyPhase rules, float startDistance)
        {
            var report = new ValidationReport(chunk.Id, chunk.VariantName);
            float vStart = _speed.Evaluate(startDistance);
            float vEnd = _speed.Evaluate(startDistance + chunk.Length);
            CheckStatic(chunk, _director.RuleFor(rules), Math.Max(1f, vStart - 0.5f), vEnd + 0.5f, report);
            CheckVisibility(chunk, _director.RuleFor(rules), Math.Max(1f, vStart - 0.5f), vEnd + 0.5f, report);
            CheckBot(chunk, Math.Max(1f, vStart - 0.5f), report);
            CheckBot(chunk, vEnd + 0.5f, report);
            CheckBot(chunk, 0.8f * vStart, report);
            CheckVines(chunk, Math.Max(1f, vStart - 0.5f), vEnd + 0.5f, report);
            CheckDeepDives(chunk, vStart, report);
            return report;
        }

        /// <summary>Static rules at a phase's rules and speed extremes.</summary>
        public void CheckStatic(ChunkRuntime chunk, in PhaseRule rule, float vLow, float vHigh, ValidationReport report)
        {
            CheckSeams(chunk, report);
            CheckWidths(chunk, report);
            CheckDividers(chunk, report);
            CheckGaps(chunk, vLow, vHigh, report);
            CheckActions(chunk, rule, vHigh, report);
            CheckCorridors(chunk, rule, report);
            CheckSecrets(chunk, report);
            CheckCoins(chunk, report);
            CheckLateral(chunk, vHigh, report);
            CheckWater(chunk, rule, vLow, vHigh, report);
            CheckBeams(chunk, rule, vLow, vHigh, report);
            CheckRevivePoints(chunk, vHigh, report);
            CheckVineLayout(chunk, report);
            CheckCurve(chunk, report);
        }

        /// <summary>Content check: no coin inside a blocker (a coin there lures players into a crash).</summary>
        private void CheckCoins(ChunkRuntime c, ValidationReport report)
        {
            for (int i = 0; i < c.CoinCount; i++)
            {
                CoinPoint coin = c.GetCoin(i);
                for (int k = 0; k < c.ObstacleCount; k++)
                {
                    ObstacleBox o = c.GetObstacle(k);
                    if (o.Class == ObstacleClass.Blocker && coin.S >= o.SMin - 0.5f && coin.S <= o.SMax + 0.5f && coin.X > o.XMin - 0.2f && coin.X < o.XMax + 0.2f && coin.Y < o.YMax)
                    {
                        report.Add("COIN", coin.S, "coin inside " + c.GetLabel(k));
                    }
                }
            }
        }

        private static void CheckSeams(ChunkRuntime c, ValidationReport report)
        {
            for (int k = 0; k < 2; k++)
            {
                float s = k == 0 ? 0f : c.Length;
                c.GetOuterBounds(s, out float a, out float b);
                if (Math.Abs(a + SeamHalfWidth) > 0.01f || Math.Abs(b - SeamHalfWidth) > 0.01f)
                {
                    report.Add("V8", s, "seam width " + F(b - a) + " m (must be 7.0 m centred)");
                }
            }

            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                if (o.SMin < SeamZone || o.SMax > c.Length - SeamZone)
                {
                    report.Add("V8", o.SMin, "obstacle in a seam zone: " + c.GetLabel(i));
                }
            }

            for (int i = 0; i < c.FloorCount; i++)
            {
                CourseFloorPatch p = c.GetFloor(i);
                if (p.SMin < SeamZone || p.SMax > c.Length - SeamZone)
                {
                    if (p.Kind == CourseFloorKind.Gap || Math.Abs(p.Y0) > 0.001f || Math.Abs(p.Y1) > 0.001f)
                    {
                        report.Add("V8", p.SMin, "gap or raised floor in a seam zone");
                    }
                }
            }
        }

        private static void CheckWidths(ChunkRuntime c, ValidationReport report)
        {
            for (int i = 1; i < c.WidthKeyCount; i++)
            {
                CourseWidthKey a = c.GetWidthKey(i - 1);
                CourseWidthKey b = c.GetWidthKey(i);
                float ds = b.S - a.S;
                if (ds <= 0f)
                {
                    continue;
                }

                // Narrowing only (widening is free): xMin moving right or xMax moving left. Canopy beam starts are
                // exempt (V14 governs beams).
                if (c.IsCanopy(a.S) || c.IsCanopy(b.S))
                {
                    continue;
                }

                if ((b.XMin - a.XMin) / ds > MaxNarrowing + 1e-3f || (a.XMax - b.XMax) / ds > MaxNarrowing + 1e-3f)
                {
                    report.Add("V8", a.S, "path narrows faster than 0.10 m/m per side");
                }
            }

            var edges = new List<float>(8);
            for (float s = 0f; s <= c.Length; s += 1f)
            {
                c.GetOuterBounds(s, out float outerMin, out float outerMax);
                edges.Clear();
                edges.Add(outerMin);
                for (int d = 0; d < c.DividerCount; d++)
                {
                    ChunkDivider dv = c.GetDivider(d);
                    if (s >= dv.SFront && s < dv.SMerge)
                    {
                        edges.Add(dv.XMin);
                        edges.Add(dv.XMax);
                    }
                }

                edges.Add(outerMax);
                edges.Sort();
                for (int i = 0; i + 1 < edges.Count; i += 2)
                {
                    float w = edges[i + 1] - edges[i];
                    if (w > 0.01f && (w < 1.6f - 1e-3f || w > 9.0f + 1e-3f))
                    {
                        report.Add("V8", s, "lane width " + F(w) + " m outside 1.6–9.0");
                        return;
                    }
                }
            }
        }

        private static void CheckDividers(ChunkRuntime c, ValidationReport report)
        {
            for (int i = 0; i < c.DividerCount; i++)
            {
                ChunkDivider d = c.GetDivider(i);
                if (d.HalfWidth * 2f < 1.2f - 1e-3f)
                {
                    report.Add("V9", d.SFront, "divider narrower than 1.2 m");
                }

                if (d.SMerge - d.SFront < 30f)
                {
                    report.Add("V9", d.SFront, "branch shorter than 30 m");
                }

                if (d.SMerge > c.Length - SeamZone || d.SFront < SeamZone)
                {
                    report.Add("V9", d.SFront, "branch does not merge inside the chunk");
                }
            }

            for (int r = 0; r < c.RouteCount; r++)
            {
                ChunkRoute route = c.GetRoute(r);
                if (route.Locked && route.Steps.Count > 0)
                {
                    report.Add("V12", 0f, "locked route " + route.Name + " has a path");
                }
            }
        }

        private void CheckGaps(ChunkRuntime c, float vLow, float vHigh, ValidationReport report)
        {
            float maxLength = 0.75f * 0.60f * vLow;
            List<CourseFloorPatch> gaps = EffectiveGaps(c);
            for (int i = 0; i < gaps.Count; i++)
            {
                CourseFloorPatch p = gaps[i];
                if (IsVineGap(c, p))
                {
                    // Vine gaps are exempt from V4; V13 governs them.
                    continue;
                }

                float length = p.SMax - p.SMin;
                if (length < 1f - 1e-3f || length > maxLength + 1e-3f)
                {
                    report.Add("V4", p.SMin, "gap " + F(length) + " m outside 1.0–" + F(maxLength) + " m at " + F(vLow) + " m/s");
                }

                // ≥ 0.35 s of clear floor before the gap (in the gap's x range).
                float clear = 0.35f * vHigh;
                for (int k = 0; k < c.ObstacleCount; k++)
                {
                    ObstacleBox o = c.GetObstacle(k);
                    if (o.SMax > p.SMin - clear && o.SMin < p.SMin && o.XMax > p.XMin && o.XMin < p.XMax && !(o.WalkableTop && o.SMax >= p.SMin))
                    {
                        report.Add("V4", p.SMin, "less than 0.35 s of clear floor before the gap (" + c.GetLabel(k) + ")");
                    }
                }
            }
        }

        /// <summary>V2 and V5 on required actions (full-lane jump/slide obstacles and gaps), per route.</summary>
        private void CheckActions(ChunkRuntime c, in PhaseRule rule, float vHigh, ValidationReport report)
        {
            List<RequiredAction> actions = RequiredActions(c);
            bool challengeException = c.Definition.Category == ChunkCategory.Challenge && c.Definition.Rating >= 6;
            for (int i = 0; i < actions.Count; i++)
            {
                for (int j = i + 1; j < actions.Count; j++)
                {
                    RequiredAction a = actions[i];
                    RequiredAction b = actions[j];
                    if (!Compatible(c, a, b))
                    {
                        continue;
                    }

                    float dt = (b.S - a.S) / vHigh;
                    if (dt + Tolerance < rule.MinActionGap)
                    {
                        report.Add("V2", b.S, "actions " + F(a.S) + " → " + F(b.S) + " are " + F(dt) + " s apart at " + F(vHigh) + " m/s (< " + F(rule.MinActionGap) + " s, " + rule.Phase + ")");
                    }

                    if (a.Jump != b.Jump && dt + Tolerance < JumpSlideGap && !challengeException)
                    {
                        report.Add("V5", b.S, "jump/slide " + F(a.S) + " → " + F(b.S) + " only " + F(dt) + " s apart");
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// V6 [interpretation 2026-10-09]: blockers must leave a free corridor ≥ 1.2 m (1.6 m in Learning) in the lane;
        /// partial Low/High/Thorns may narrow it further because they can be jumped or slid (spec 103 §3.3 "steer
        /// left or jump"), but where obstacles close the whole lane they must all be jumpable or all slidable.
        /// </summary>
        private void CheckCorridors(ChunkRuntime c, in PhaseRule rule, ValidationReport report)
        {
            float minCorridor = rule.Phase == DifficultyPhase.Learning ? 1.6f : 1.2f;
            float waterCorridor = 1.6f;
            var blockers = new List<float>(16);
            var all = new List<float>(16);
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                float s = o.SMin + 0.01f;
                c.GetLateralBounds(s, o.CenterX, out float laneMin, out float laneMax);
                blockers.Clear();
                all.Clear();
                bool allJump = true;
                bool allSlide = true;
                bool anyBlocker = false;
                for (int k = 0; k < c.ObstacleCount; k++)
                {
                    ObstacleBox other = c.GetObstacle(k);
                    if (other.SMin > s || other.SMax < s || other.XMax <= laneMin || other.XMin >= laneMax)
                    {
                        continue;
                    }

                    float x0 = Math.Max(laneMin, other.XMin);
                    float x1 = Math.Min(laneMax, other.XMax);
                    all.Add(x0);
                    all.Add(x1);
                    if (other.Class == ObstacleClass.Blocker || other.Class == ObstacleClass.Rock)
                    {
                        anyBlocker = true;
                        blockers.Add(x0);
                        blockers.Add(x1);
                    }

                    allJump &= other.Class == ObstacleClass.Low || other.Class == ObstacleClass.Thorns || other.Class == ObstacleClass.Snag || other.Class == ObstacleClass.FloatingLog;
                    allSlide &= other.Class == ObstacleClass.High || other.Class == ObstacleClass.LowBranch || other.Class == ObstacleClass.FloatingLog;
                }

                if (anyBlocker)
                {
                    float free = LargestFree(blockers, laneMin, laneMax);
                    bool inWater = c.TryGetWater(s, o.CenterX, out _);
                    if (inWater && free + 1e-3f < waterCorridor)
                    {
                        report.Add("W5", o.SMin, "free corridor " + F(free) + " m (< 1.6) at " + c.GetLabel(i));
                    }
                    else if (free + 1e-3f < minCorridor)
                    {
                        report.Add("V6", o.SMin, "free corridor " + F(free) + " m (< " + F(minCorridor) + ") at " + c.GetLabel(i));
                    }
                }

                if (LargestFree(all, laneMin, laneMax) < 0.01f && !allJump && !allSlide)
                {
                    report.Add("V6", o.SMin, "lane closed by a mix of obstacles that no single action passes at " + c.GetLabel(i));
                }
            }
        }

        private static void CheckSecrets(ChunkRuntime c, ValidationReport report)
        {
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                if (o.Class == ObstacleClass.Blocker && c.RouteAt(o.SMin + 0.01f, o.CenterX) == RouteType.Secret)
                {
                    report.Add("V10", o.SMin, "blocker in a secret entrance");
                }
            }

            List<CourseFloorPatch> gaps = EffectiveGaps(c);
            for (int i = 0; i < gaps.Count; i++)
            {
                CourseFloorPatch p = gaps[i];
                if (c.RouteAt(p.SMin + 0.01f, (p.XMin + p.XMax) * 0.5f) == RouteType.Secret)
                {
                    report.Add("V10", p.SMin, "gap in a secret entrance");
                }
            }
        }

        /// <summary>V1: the Perfect bot through every open route (or the single line) at one speed, 0 hits.</summary>
        public void CheckBot(ChunkRuntime c, float speed, ValidationReport report)
        {
            int routes = 0;
            for (int r = 0; r < c.RouteCount; r++)
            {
                ChunkRoute route = c.GetRoute(r);
                if (route.Locked || route.Steps.Count == 0)
                {
                    continue;
                }

                routes++;
                RunBot(c, r, speed, report);
            }

            if (routes == 0)
            {
                RunBot(c, -1, speed, report);
            }
        }

        /// <summary>Runs the bot over a single-chunk path. Returns the final state.</summary>
        public RunnerState RunBot(ChunkRuntime c, int route, float speed, ValidationReport report, bool deepBreath = false)
        {
            var path = new WorldPath();
            path.Append(c, new ChunkPick { Entry = c.LibraryIndex, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            var sim = new RunnerSimulation(_movement, path, 1f / 60f, new RunEventBuffer(16)) { MuteEvents = true };
            sim.Reset(new RunOptions { ForcedSpeed = speed, SkipStartRamp = true, DeepBreath = deepBreath, DeepDiveDepth = -2.5f, DeepDiveTime = 2.4f });
            var bot = new PerfectBot(sim, false)
            {
                ForkPreference = new WorldRoutePreference(path) { FixedRoute = route },
                StrictWaterAnswers = true,
                UseDeepDives = deepBreath,
            };

            string label = (route >= 0 ? c.GetRoute(route).Name : "main") + (deepBreath ? " + Deep Breath" : string.Empty);
            int maxTicks = (int)((c.Length + 20f) / Math.Max(1f, speed) * 60f * 1.5f) + 120;
            bool wrongSide = false;
            for (int t = 0; t < maxTicks && sim.State.S < c.Length + 5f && !sim.State.Dead; t++)
            {
                float before = sim.State.S;
                sim.Step(bot.ReadInput(sim.State.Tick));
                if (route >= 0)
                {
                    ChunkRoute r = c.GetRoute(route);
                    for (int k = 0; k < r.Steps.Count; k++)
                    {
                        ChunkDivider d = c.GetDivider(r.Steps[k].Divider);
                        if (before < d.SFront && sim.State.S >= d.SFront && ChunkRuntime.SideOf(d, sim.State.X) != r.Steps[k].Side)
                        {
                            wrongSide = true;
                        }
                    }
                }
            }

            RunnerState st = sim.State;
            string at = " (" + label + " at " + F(speed) + " m/s)";
            if (st.Dead)
            {
                report.Add("V1", st.S, "Perfect bot died: " + st.Cause + (st.DeathObstacle >= 0 ? " " + path.GetObstacleLabel(st.DeathObstacle) : string.Empty) + at);
            }
            else if (st.Hits > 0)
            {
                report.Add("V1", st.S, "Perfect bot took " + st.Hits + " hit(s), last " + path.GetObstacleLabel(st.LastHitObstacle) + at);
            }

            if (wrongSide)
            {
                report.Add("V1", 0f, "Perfect bot could not take route" + at);
            }

            if (st.S < c.Length && !st.Dead)
            {
                report.Add("V1", st.S, "Perfect bot did not reach the end" + at);
            }

            report.BotRuns++;
            report.ProbeSteps += bot.ProbeSteps;
            return st;
        }

        /// <summary>Full-lane jump/slide obstacles and gaps (the actions a route can't steer around).</summary>
        public static List<RequiredAction> RequiredActions(ChunkRuntime c)
        {
            var list = new List<RequiredAction>();
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                if (o.Class == ObstacleClass.Blocker || RunnerSimulation.IsWaterClass(o.Class))
                {
                    // Steer-only; water obstacles are W1's.
                    continue;
                }

                float s = o.SMin + 0.01f;
                c.GetLateralBounds(s, o.CenterX, out float a, out float b);
                if (CoversLane(c, s, o.CenterX, a, b) && !HasActionNear(list, o.SMin, a, b))
                {
                    list.Add(new RequiredAction(o.SMin, o.CenterX, o.Class != ObstacleClass.High));
                }
            }

            List<CourseFloorPatch> gaps = EffectiveGaps(c);
            for (int i = 0; i < gaps.Count; i++)
            {
                CourseFloorPatch p = gaps[i];
                if (IsVineGap(c, p))
                {
                    continue;
                }

                float x = (p.XMin + p.XMax) * 0.5f;
                c.GetLateralBounds(p.SMin + 0.01f, x, out float a, out float b);
                if (p.XMin <= a + LaneMargin && p.XMax >= b - LaneMargin)
                {
                    list.Add(new RequiredAction(p.SMin, x, true));
                }
            }

            list.Sort((u, v) => u.S.CompareTo(v.S));
            return list;
        }

        /// <summary>Obstacles side by side at the same s (thorns + root) are one action.</summary>
        private static bool HasActionNear(List<RequiredAction> list, float s, float laneMin, float laneMax)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Math.Abs(list[i].S - s) < 0.5f && list[i].X >= laneMin && list[i].X <= laneMax)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CoversLane(ChunkRuntime c, float s, float x, float laneMin, float laneMax)
        {
            // Union of all non-blocker obstacles at s inside the lane covers it (within the steering margin).
            float covered = laneMin + LaneMargin;
            for (int guard = 0; guard < 16; guard++)
            {
                bool advanced = false;
                for (int k = 0; k < c.ObstacleCount; k++)
                {
                    ObstacleBox o = c.GetObstacle(k);
                    if (o.SMin > s || o.SMax < s || o.Class == ObstacleClass.Blocker || o.Class == ObstacleClass.Rock)
                    {
                        continue;
                    }

                    if (o.XMin <= covered && o.XMax > covered)
                    {
                        covered = o.XMax;
                        advanced = true;
                    }
                }

                if (covered >= laneMax - LaneMargin)
                {
                    return true;
                }

                if (!advanced)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool Compatible(ChunkRuntime c, in RequiredAction a, in RequiredAction b)
        {
            for (int d = 0; d < c.DividerCount; d++)
            {
                ChunkDivider dv = c.GetDivider(d);
                bool aIn = a.S >= dv.SFront && a.S < dv.SMerge;
                bool bIn = b.S >= dv.SFront && b.S < dv.SMerge;
                if (aIn && bIn && ChunkRuntime.SideOf(dv, a.X) != ChunkRuntime.SideOf(dv, b.X))
                {
                    return false;
                }
            }

            return true;
        }

        private static float LargestFree(List<float> spans, float min, float max)
        {
            // spans = [x0, x1, x0, x1, …]; sort intervals by start and sweep.
            int n = spans.Count / 2;
            var starts = new float[n];
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                starts[i] = spans[2 * i];
                ends[i] = spans[(2 * i) + 1];
            }

            Array.Sort(starts, ends);
            float best = 0f;
            float cursor = min;
            for (int i = 0; i < n; i++)
            {
                if (starts[i] > cursor)
                {
                    best = Math.Max(best, starts[i] - cursor);
                }

                cursor = Math.Max(cursor, ends[i]);
            }

            return Math.Max(best, max - cursor);
        }

        private static string F(float v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
