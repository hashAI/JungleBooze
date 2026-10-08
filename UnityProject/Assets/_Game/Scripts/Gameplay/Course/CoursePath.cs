using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>
    /// Queryable, immutable course built from <see cref="CourseData"/> (setup time). Implements
    /// <see cref="IPathQuery"/> without allocating: obstacles and coins are sorted arrays searched by binary search.
    /// Obstacle ids are indices in s order.
    /// </summary>
    public sealed class CoursePath : IPathQuery
    {
        private readonly CourseWidthKey[] _widths;
        private readonly CourseFloorPatch[] _floors;
        private readonly CourseFork[] _forks;
        private readonly ForkPoint[] _forkPoints;
        private readonly ObstacleBox[] _obstacles;
        private readonly string[] _labels;
        private readonly CoinPoint[] _coins;
        private readonly CourseSection[] _sections;
        private readonly float _maxObstacleLength;

        public CoursePath(CourseData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Name = data.Name;
            FinishS = data.FinishS;
            EndS = data.FinishS + Math.Max(0f, data.RunOutLength);

            var widths = new List<CourseWidthKey>(data.Widths);
            widths.Sort((a, b) => a.S.CompareTo(b.S));
            if (widths.Count == 0)
            {
                widths.Add(new CourseWidthKey(0f, -3.5f, 3.5f));
            }

            _widths = widths.ToArray();
            _floors = data.Floors.ToArray();
            _forks = data.Forks.ToArray();
            _forkPoints = new ForkPoint[_forks.Length];
            for (int i = 0; i < _forks.Length; i++)
            {
                CourseFork f = _forks[i];
                _forkPoints[i] = new ForkPoint(f.SFront, f.SMerge, f.DividerCenterX, f.DividerHalfWidth, f.SafeSide);
            }

            var obstacles = new List<CourseObstacle>(data.Obstacles);

            // Stable sort by SMin (List.Sort is not stable): order by (SMin, original index).
            var order = new int[obstacles.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int c = obstacles[a].SMin.CompareTo(obstacles[b].SMin);
                return c != 0 ? c : a.CompareTo(b);
            });

            _obstacles = new ObstacleBox[order.Length];
            _labels = new string[order.Length];
            float maxLength = 0f;
            for (int id = 0; id < order.Length; id++)
            {
                CourseObstacle o = obstacles[order[id]];
                _obstacles[id] = new ObstacleBox(id, o.Class, o.SMin, o.SMax, o.XMin, o.XMax, o.YMin, o.YMax, o.WalkableTop && o.Class == ObstacleClass.Low);
                _labels[id] = string.IsNullOrEmpty(o.Label) ? o.Class + " @" + o.SMin.ToString("0") : o.Label;
                maxLength = Math.Max(maxLength, o.SMax - o.SMin);
            }

            _maxObstacleLength = maxLength;

            var coins = new List<CourseCoin>(data.Coins);
            coins.Sort((a, b) => a.S.CompareTo(b.S));
            _coins = new CoinPoint[coins.Count];
            for (int i = 0; i < coins.Count; i++)
            {
                _coins[i] = new CoinPoint(i, coins[i].S, coins[i].X, coins[i].Y);
            }

            _sections = data.Sections.ToArray();
        }

        public string Name { get; }

        public float FinishS { get; }

        /// <summary>End of the authored path (finish + run-out).</summary>
        public float EndS { get; }

        public int ObstacleCount => _obstacles.Length;

        public int CoinCount => _coins.Length;

        public int ForkCount => _forks.Length;

        public int SectionCount => _sections.Length;

        public int FloorPatchCount => _floors.Length;

        public int WidthKeyCount => _widths.Length;

        public CourseSection GetSection(int index)
        {
            return _sections[index];
        }

        public CourseFloorPatch GetFloorPatch(int index)
        {
            return _floors[index];
        }

        public CourseWidthKey GetWidthKey(int index)
        {
            return _widths[index];
        }

        public CourseFork GetForkData(int index)
        {
            return _forks[index];
        }

        public string GetObstacleLabel(int id)
        {
            return id >= 0 && id < _labels.Length ? _labels[id] : "-";
        }

        /// <summary>Name of the section containing s, or empty.</summary>
        public string SectionAt(float s)
        {
            for (int i = 0; i < _sections.Length; i++)
            {
                if (s >= _sections[i].SMin && s < _sections[i].SMax)
                {
                    return _sections[i].Name;
                }
            }

            return string.Empty;
        }

        public ForkPoint GetFork(int index)
        {
            return _forkPoints[index];
        }

        public ObstacleBox GetObstacle(int id)
        {
            return _obstacles[id];
        }

        public CoinPoint GetCoin(int id)
        {
            return _coins[id];
        }

        /// <summary>Outer edges from the width keys only (ignores fork branches).</summary>
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

        public void GetLateralBounds(float s, float x, out float xMin, out float xMax)
        {
            for (int i = 0; i < _forks.Length; i++)
            {
                CourseFork f = _forks[i];
                if (s >= f.SFront && s < f.SMerge)
                {
                    bool right = x > f.DividerCenterX || (x == f.DividerCenterX && f.SafeSide > 0);
                    xMin = right ? f.RightXMin : f.LeftXMin;
                    xMax = right ? f.RightXMax : f.LeftXMax;
                    return;
                }
            }

            GetOuterBounds(s, out xMin, out xMax);
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

        public bool TryFindFloorAhead(float s, float x, float reach, out float lipS, out float lipY)
        {
            for (int i = _floors.Length - 1; i >= 0; i--)
            {
                CourseFloorPatch patch = _floors[i];
                if (patch.Kind != CourseFloorKind.Gap || !patch.Contains(s, x))
                {
                    continue;
                }

                if (patch.SMax - s <= reach && TryGetFloor(patch.SMax, x, out lipY))
                {
                    lipS = patch.SMax;
                    return true;
                }

                break;
            }

            lipS = 0f;
            lipY = 0f;
            return false;
        }

        public int FindObstacles(float sMin, float sMax, int[] results)
        {
            int count = 0;
            int i = LowerBoundObstacle(sMin - _maxObstacleLength);
            for (; i < _obstacles.Length && _obstacles[i].SMin <= sMax; i++)
            {
                if (_obstacles[i].SMax >= sMin)
                {
                    if (count == results.Length)
                    {
                        break;
                    }

                    results[count++] = i;
                }
            }

            return count;
        }

        public int FindCoins(float sMin, float sMax, int[] results)
        {
            int lo = 0;
            int hi = _coins.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_coins[mid].S < sMin)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            int count = 0;
            for (int i = lo; i < _coins.Length && _coins[i].S <= sMax && count < results.Length; i++)
            {
                results[count++] = i;
            }

            return count;
        }

        private int LowerBoundObstacle(float s)
        {
            int lo = 0;
            int hi = _obstacles.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_obstacles[mid].SMin < s)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }
    }
}
