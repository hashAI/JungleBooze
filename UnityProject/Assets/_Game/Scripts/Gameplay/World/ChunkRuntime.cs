using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Immutable, queryable form of one chunk variant (built once at setup from <see cref="ChunkDefinition"/>), all
    /// chunk-local. Coin patterns are expanded to points, obstacles sorted by s, heights resolved against the floor.
    /// <see cref="WorldPath"/> places instances of it along the run; every query here is allocation-free.
    /// </summary>
    public sealed class ChunkRuntime
    {
        public const float DefaultCoinHeight = 0.9f;
        public const float UnderCoinHeight = 0.3f;
        public const float DefaultLineStep = 1.5f;
        public const float DefaultWeaveStep = 2.5f;
        public const float ArcSpacing = 1.5f;
        public const float ArcBase = 1.0f;
        public const float ArcRise = 1.0f;
        public const float ArcHalfSpan = 3.0f;

        private readonly CourseWidthKey[] _widths;
        private readonly CourseFloorPatch[] _floors;
        private readonly ChunkDivider[] _dividers;
        private readonly ChunkRoute[] _routes;
        private readonly ObstacleBox[] _obstacles;
        private readonly string[] _labels;
        private readonly CoinPoint[] _coins;
        private readonly CrystalAnchor[] _crystals;
        private readonly PowerUpSlot[] _powerUps;
        private readonly DiscoveryTrigger[] _discoveries;
        private readonly TraversalZone[] _traversal;
        private readonly HelpMarker[] _help;
        private readonly WaterVolume[] _water;
        private readonly WaterCurrent[] _currents;
        private readonly DeepDiveZone[] _deepDives;
        private readonly VineAnchor[] _vines;
        private readonly float[] _vineTakeoffY;
        private readonly CreatureSpawn[] _creatures;
        private readonly TraversalZone[] _canopy;

        /// <summary>Per-chunk id stride for vines, deep-dive zones and creature spawns (id = serial × stride + index).</summary>
        public const int LocalIdStride = 8;

        public ChunkRuntime(ChunkDefinition definition, int definitionIndex, int variantIndex, int libraryIndex)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (variantIndex < 0 || variantIndex >= definition.Variants.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(variantIndex));
            }

            DefinitionIndex = definitionIndex;
            VariantIndex = variantIndex;
            LibraryIndex = libraryIndex;
            Variant = definition.Variants[variantIndex];
            Length = definition.LengthOf(variantIndex);

            var widths = new List<CourseWidthKey>(Variant.Widths);
            widths.Sort((a, b) => a.S.CompareTo(b.S));
            if (widths.Count == 0)
            {
                widths.Add(new CourseWidthKey(0f, -3.5f, 3.5f));
            }

            _widths = widths.ToArray();
            _floors = Variant.Floors.ToArray();
            _dividers = Variant.Dividers.ToArray();
            _routes = Variant.Routes.ToArray();
            _traversal = Variant.Traversal.ToArray();
            _help = Variant.Help.ToArray();
            _discoveries = Variant.Discoveries.ToArray();
            _water = (Variant.Water ?? new List<WaterVolume>()).ToArray();
            _currents = (Variant.Currents ?? new List<WaterCurrent>()).ToArray();
            _deepDives = (Variant.DeepDives ?? new List<DeepDiveZone>()).ToArray();
            _vines = (Variant.Vines ?? new List<VineAnchor>()).ToArray();
            _creatures = (Variant.Creatures ?? new List<CreatureSpawn>()).ToArray();
            if (_vines.Length > LocalIdStride || _deepDives.Length > LocalIdStride || _creatures.Length > LocalIdStride)
            {
                throw new ArgumentException(definition.Id + "/" + Variant.Name + ": at most " + LocalIdStride + " vines, deep-dive zones and creature spawns per chunk.");
            }

            var canopy = new List<TraversalZone>();
            for (int i = 0; i < _traversal.Length; i++)
            {
                if (_traversal[i].Mode == TraversalMode.Canopy)
                {
                    canopy.Add(_traversal[i]);
                }
            }

            _canopy = canopy.ToArray();
            Curve = new PathCurve(Variant.Curve, Length);

            // Obstacles: stable sort by SMin, ids = local index.
            List<CourseObstacle> source = Variant.Obstacles;
            var order = new int[source.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int c = source[a].SMin.CompareTo(source[b].SMin);
                return c != 0 ? c : a.CompareTo(b);
            });

            _obstacles = new ObstacleBox[order.Length];
            _labels = new string[order.Length];
            float maxLength = 0f;
            FirstActionS = Length;
            LastActionS = 0f;
            for (int id = 0; id < order.Length; id++)
            {
                CourseObstacle o = source[order[id]];
                _obstacles[id] = new ObstacleBox(id, o.Class, o.SMin, o.SMax, o.XMin, o.XMax, o.YMin, o.YMax, o.WalkableTop && o.Class == ObstacleClass.Low);
                _labels[id] = (string.IsNullOrEmpty(definition.Label) ? definition.Id : definition.Label) + " " +
                              (string.IsNullOrEmpty(o.Label) ? o.Class + " @" + o.SMin.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : o.Label);
                maxLength = Math.Max(maxLength, o.SMax - o.SMin);
                FirstActionS = Math.Min(FirstActionS, o.SMin);
                LastActionS = Math.Max(LastActionS, o.SMax);
            }

            for (int i = 0; i < _floors.Length; i++)
            {
                if (_floors[i].Kind == CourseFloorKind.Gap)
                {
                    FirstActionS = Math.Min(FirstActionS, _floors[i].SMin);
                    LastActionS = Math.Max(LastActionS, _floors[i].SMax);
                }
            }

            if (LastActionS < FirstActionS)
            {
                LastActionS = FirstActionS;
            }

            MaxObstacleLength = maxLength;
            _vineTakeoffY = new float[_vines.Length];
            for (int i = 0; i < _vines.Length; i++)
            {
                TryGetFloor(_vines[i].LipS - 0.5f, _vines[i].X, out _vineTakeoffY[i]);
            }

            _coins = ExpandCoins();

            _crystals = Variant.Crystals.ToArray();
            for (int i = 0; i < _crystals.Length; i++)
            {
                _crystals[i].Y += BaseHeight(_crystals[i].S, _crystals[i].X);
            }

            _powerUps = Variant.PowerUps.ToArray();
            for (int i = 0; i < _powerUps.Length; i++)
            {
                _powerUps[i].Y += BaseHeight(_powerUps[i].S, _powerUps[i].X);
            }
        }

        public ChunkDefinition Definition { get; }

        public ChunkVariant Variant { get; }

        public int DefinitionIndex { get; }

        public int VariantIndex { get; }

        /// <summary>Index in <see cref="ChunkLibrary.Entries"/>.</summary>
        public int LibraryIndex { get; }

        public string Id => Definition.Id;

        public string VariantName => Variant.Name;

        public float Length { get; }

        public bool Placeholder => Variant.Placeholder;

        /// <summary>s of the first obstacle or gap (entry action margin in metres).</summary>
        public float FirstActionS { get; }

        /// <summary>s of the end of the last obstacle or gap.</summary>
        public float LastActionS { get; }

        public float EntryMarginM => FirstActionS;

        public float ExitMarginM => Length - LastActionS;

        public float MaxObstacleLength { get; }

        public int WidthKeyCount => _widths.Length;

        public int FloorCount => _floors.Length;

        public int DividerCount => _dividers.Length;

        public int RouteCount => _routes.Length;

        public int ObstacleCount => _obstacles.Length;

        public int CoinCount => _coins.Length;

        public int CrystalCount => _crystals.Length;

        public int PowerUpSlotCount => _powerUps.Length;

        public int DiscoveryCount => _discoveries.Length;

        public int TraversalCount => _traversal.Length;

        public int HelpCount => _help.Length;

        public CourseWidthKey GetWidthKey(int i) => _widths[i];

        public CourseFloorPatch GetFloor(int i) => _floors[i];

        public ChunkDivider GetDivider(int i) => _dividers[i];

        public ChunkRoute GetRoute(int i) => _routes[i];

        public ObstacleBox GetObstacle(int i) => _obstacles[i];

        public string GetLabel(int i) => _labels[i];

        public CoinPoint GetCoin(int i) => _coins[i];

        public CrystalAnchor GetCrystal(int i) => _crystals[i];

        public PowerUpSlot GetPowerUpSlot(int i) => _powerUps[i];

        public DiscoveryTrigger GetDiscovery(int i) => _discoveries[i];

        public TraversalZone GetTraversal(int i) => _traversal[i];

        public HelpMarker GetHelp(int i) => _help[i];

        /// <summary>The centreline's view-side curve (local frame).</summary>
        public PathCurve Curve { get; }

        public int WaterCount => _water.Length;

        public int CurrentCount => _currents.Length;

        public int DeepDiveCount => _deepDives.Length;

        public int VineCount => _vines.Length;

        public int CreatureCount => _creatures.Length;

        public WaterVolume GetWater(int i) => _water[i];

        public WaterCurrent GetCurrent(int i) => _currents[i];

        public DeepDiveZone GetDeepDive(int i) => _deepDives[i];

        public VineAnchor GetVine(int i) => _vines[i];

        /// <summary>Floor height of the vine's takeoff (just before the lip).</summary>
        public float GetVineTakeoffY(int i) => _vineTakeoffY[i];

        public CreatureSpawn GetCreature(int i) => _creatures[i];

        /// <summary>Water surface at local (s, x), if inside a water volume.</summary>
        public bool TryGetWater(float s, float x, out float surfaceY)
        {
            for (int i = 0; i < _water.Length; i++)
            {
                if (_water[i].Contains(s, x))
                {
                    surfaceY = _water[i].SurfaceY;
                    return true;
                }
            }

            surfaceY = 0f;
            return false;
        }

        /// <summary>Sum of the currents active at local s.</summary>
        public void GetCurrentAt(float s, out float lateral, out float forward)
        {
            lateral = 0f;
            forward = 0f;
            for (int i = 0; i < _currents.Length; i++)
            {
                if (s >= _currents[i].SMin && s < _currents[i].SMax)
                {
                    lateral += _currents[i].Lateral;
                    forward += _currents[i].Forward;
                }
            }
        }

        public int FindDeepDive(float s, float x)
        {
            for (int i = 0; i < _deepDives.Length; i++)
            {
                DeepDiveZone z = _deepDives[i];
                if (s >= z.SMin && s <= z.SMax && x >= z.XMin && x <= z.XMax)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Index of the vine whose grab window [lip − before, lip + after] contains local s, or −1.</summary>
        public int FindVine(float s, float before, float after)
        {
            for (int i = 0; i < _vines.Length; i++)
            {
                if (s >= _vines[i].LipS - before && s <= _vines[i].LipS + after)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool IsCanopy(float s)
        {
            for (int i = 0; i < _canopy.Length; i++)
            {
                if (s >= _canopy[i].SMin && s < _canopy[i].SMax)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Outer path edges (ignoring dividers).</summary>
        public void GetOuterBounds(float s, out float xMin, out float xMax)
        {
            if (s <= _widths[0].S)
            {
                xMin = _widths[0].XMin;
                xMax = _widths[0].XMax;
                return;
            }

            for (int i = 1; i < _widths.Length; i++)
            {
                if (s < _widths[i].S)
                {
                    CourseWidthKey a = _widths[i - 1];
                    CourseWidthKey b = _widths[i];
                    float t = (s - a.S) / (b.S - a.S);
                    xMin = a.XMin + ((b.XMin - a.XMin) * t);
                    xMax = a.XMax + ((b.XMax - a.XMax) * t);
                    return;
                }
            }

            CourseWidthKey last = _widths[_widths.Length - 1];
            xMin = last.XMin;
            xMax = last.XMax;
        }

        /// <summary>The lane containing x: outer edges clipped by every divider active at s (spec 102 §3.3).</summary>
        public void GetLateralBounds(float s, float x, out float xMin, out float xMax)
        {
            GetOuterBounds(s, out xMin, out xMax);
            for (int i = 0; i < _dividers.Length; i++)
            {
                ChunkDivider d = _dividers[i];
                if (s < d.SFront || s >= d.SMerge)
                {
                    continue;
                }

                if (SideOf(d, x) > 0)
                {
                    if (d.XMax > xMin)
                    {
                        xMin = d.XMax;
                    }
                }
                else if (d.XMin < xMax)
                {
                    xMax = d.XMin;
                }
            }
        }

        /// <summary>−1 left / +1 right of a divider centre (tie → the safe side).</summary>
        public static int SideOf(in ChunkDivider d, float x)
        {
            if (x > d.CenterX)
            {
                return 1;
            }

            if (x < d.CenterX)
            {
                return -1;
            }

            return d.SafeSide < 0 ? -1 : 1;
        }

        public bool TryGetFloor(float s, float x, out float floorY)
        {
            for (int i = _floors.Length - 1; i >= 0; i--)
            {
                if (_floors[i].Contains(s, x))
                {
                    if (_floors[i].Kind == CourseFloorKind.Gap)
                    {
                        floorY = 0f;
                        return false;
                    }

                    floorY = _floors[i].HeightAt(s);
                    return true;
                }
            }

            floorY = 0f;
            return true;
        }

        /// <summary>
        /// From a point over a gap: the nearest s in (s, s + reach] where floor starts again at x (the end of a gap
        /// patch or the start of a floor patch laid over open air, e.g. a canopy beam), and its height.
        /// </summary>
        public bool TryFindFloorAhead(float s, float x, float reach, out float lipS, out float lipY)
        {
            lipS = 0f;
            lipY = 0f;
            if (TryGetFloor(s, x, out _))
            {
                return false;
            }

            float best = float.MaxValue;
            for (int i = 0; i < _floors.Length; i++)
            {
                CourseFloorPatch patch = _floors[i];
                if (x < patch.XMin || x > patch.XMax)
                {
                    continue;
                }

                float candidate = patch.Kind == CourseFloorKind.Gap ? patch.SMax : patch.SMin;
                if (candidate > s && candidate <= s + reach && candidate < best && TryGetFloor(candidate, x, out _))
                {
                    best = candidate;
                }
            }

            if (best == float.MaxValue)
            {
                return false;
            }

            lipS = best;
            TryGetFloor(best, x, out lipY);
            return true;
        }

        /// <summary>
        /// Route of the lane at (s, x): <see cref="RouteType.Main"/> outside splits, else the unlocked route whose
        /// steps match the most dividers active at s (ties → the earlier, safer-listed route).
        /// </summary>
        public RouteType RouteAt(float s, float x)
        {
            int index = RouteIndexAt(s, x);
            return index >= 0 ? _routes[index].Type : RouteType.Main;
        }

        /// <summary>Index of the route at (s, x) or −1 (no split active).</summary>
        public int RouteIndexAt(float s, float x)
        {
            bool any = false;
            for (int i = 0; i < _dividers.Length; i++)
            {
                if (s >= _dividers[i].SFront && s < _dividers[i].SMerge)
                {
                    any = true;
                    break;
                }
            }

            if (!any)
            {
                return -1;
            }

            int best = -1;
            int bestScore = -1;
            for (int r = 0; r < _routes.Length; r++)
            {
                ChunkRoute route = _routes[r];
                if (route.Locked || route.Steps.Count == 0)
                {
                    continue;
                }

                int score = 0;
                bool ok = true;
                for (int k = 0; k < route.Steps.Count; k++)
                {
                    RouteStep step = route.Steps[k];
                    if (step.Divider < 0 || step.Divider >= _dividers.Length)
                    {
                        continue;
                    }

                    ChunkDivider d = _dividers[step.Divider];
                    if (s < d.SFront || s >= d.SMerge)
                    {
                        continue;
                    }

                    if (SideOf(d, x) != step.Side)
                    {
                        ok = false;
                        break;
                    }

                    score++;
                }

                if (ok && score > bestScore)
                {
                    best = r;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>The s where a route is decided (its last divider front) and merges (its first divider's merge).</summary>
        public bool TryGetRouteSpan(int route, out float decideS, out float mergeS)
        {
            decideS = 0f;
            mergeS = 0f;
            if (route < 0 || route >= _routes.Length || _routes[route].Steps.Count == 0)
            {
                return false;
            }

            List<RouteStep> steps = _routes[route].Steps;
            decideS = float.MinValue;
            mergeS = float.MaxValue;
            for (int k = 0; k < steps.Count; k++)
            {
                ChunkDivider d = _dividers[steps[k].Divider];
                decideS = Math.Max(decideS, d.SFront);
                mergeS = Math.Min(mergeS, d.SMerge);
            }

            // A nested route (secret off the safe lane) merges where its own divider ends.
            ChunkDivider last = _dividers[steps[steps.Count - 1].Divider];
            mergeS = Math.Max(mergeS, last.SMerge);
            return true;
        }

        /// <summary>
        /// Floor (or walkable top) height under a point; gaps count as 0; inside a water volume the water surface
        /// (pickups float on it; underwater items use a negative height). Setup only.
        /// </summary>
        public float BaseHeight(float s, float x)
        {
            if (TryGetWater(s, x, out float surface))
            {
                return surface;
            }

            float y;
            if (!TryGetFloor(s, x, out y))
            {
                // Over a gap: the height of the floor before it (arcs over canopy gaps sit at beam height).
                y = 0f;
                for (float back = 0.5f; back <= 12f; back += 0.5f)
                {
                    if (TryGetFloor(s - back, x, out float before) || TryGetFloor(s - back, 0f, out before))
                    {
                        y = before;
                        break;
                    }
                }
            }

            for (int i = 0; i < _obstacles.Length; i++)
            {
                ObstacleBox o = _obstacles[i];
                if (o.WalkableTop && s >= o.SMin && s <= o.SMax && x >= o.XMin && x <= o.XMax && o.YMax > y)
                {
                    y = o.YMax;
                }
            }

            return y;
        }

        private CoinPoint[] ExpandCoins()
        {
            var points = new List<CoinPoint>();
            for (int p = 0; p < Variant.Coins.Count; p++)
            {
                CoinPattern c = Variant.Coins[p];
                switch (c.Kind)
                {
                    case CoinPatternKind.Line:
                    case CoinPatternKind.Weave:
                    {
                        float step = c.Step > 0f ? c.Step : c.Kind == CoinPatternKind.Line ? DefaultLineStep : DefaultWeaveStep;
                        float height = c.Y != 0f ? c.Y : DefaultCoinHeight;
                        int count = Math.Max(1, (int)Math.Floor(((c.S1 - c.S0) / step) + 1e-3) + 1);
                        for (int i = 0; i < count; i++)
                        {
                            float s = c.S0 + (i * step);
                            float x = c.X;
                            if (c.Kind == CoinPatternKind.Weave && c.Period > 0f)
                            {
                                x += c.Amp * (float)Math.Sin(2.0 * Math.PI * (s - c.S0) / c.Period);
                            }

                            Add(points, s, x, height);
                        }

                        break;
                    }

                    case CoinPatternKind.Arc:
                    {
                        int n = Math.Max(1, c.Count);
                        for (int i = 0; i < n; i++)
                        {
                            float o = (i - ((n - 1) * 0.5f)) * ArcSpacing;
                            float u = o / ArcHalfSpan;
                            float lift = ArcRise * Math.Max(0f, 1f - (u * u));
                            Add(points, c.S0 + o, c.X, (c.Y != 0f ? c.Y : ArcBase) + lift);
                        }

                        break;
                    }

                    case CoinPatternKind.Under:
                    {
                        int n = Math.Max(1, c.Count);
                        for (int i = 0; i < n; i++)
                        {
                            float o = (i - ((n - 1) * 0.5f)) * 1.0f;
                            Add(points, c.S0 + 0.3f + o, c.X, c.Y != 0f ? c.Y : UnderCoinHeight);
                        }

                        break;
                    }

                    default:
                        Add(points, c.S0, c.X, c.Y != 0f ? c.Y : DefaultCoinHeight);
                        break;
                }
            }

            // Stable sort by s.
            var order = new int[points.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int cmp = points[a].S.CompareTo(points[b].S);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            var result = new CoinPoint[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                CoinPoint p = points[order[i]];
                result[i] = new CoinPoint(i, p.S, p.X, p.Y);
            }

            return result;
        }

        private void Add(List<CoinPoint> points, float s, float x, float height)
        {
            if (s < 0f || s > Length)
            {
                return;
            }

            points.Add(new CoinPoint(points.Count, s, x, BaseHeight(s, x) + height));
        }
    }
}
