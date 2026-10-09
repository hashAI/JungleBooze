using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Editor.Expedition
{
    /// <summary>
    /// Small authoring DSL for chunk variants in the notation of spec 103 §3.0. Obstacle heights are relative to the
    /// floor under the obstacle (raised ridges, canopy), "full" means the lane or path width at that s.
    /// Default depths: 0.6 m (logs/walkable 2.0 m), High = bottom b … b + 1.5 m, Blocker 2.5 m tall.
    /// </summary>
    internal sealed class ChunkLayoutBuilder
    {
        public const float Depth = 0.6f;
        public const float LogDepth = 2.0f;
        public const float Full = 99f;

        private readonly ChunkVariant _v;
        private readonly float _length;

        public ChunkLayoutBuilder(string name, float length, bool scriptOnly = false, float lengthOverride = 0f)
        {
            _length = lengthOverride > 0f ? lengthOverride : length;
            _v = new ChunkVariant { Name = name, ScriptOnly = scriptOnly, Length = lengthOverride };
            Width(0f, -3.5f, 3.5f);
            Width(_length, -3.5f, 3.5f);
        }

        public ChunkVariant Variant => _v;

        public ChunkLayoutBuilder Placeholder(string note)
        {
            _v.Placeholder = true;
            _v.PlaceholderNote = note;
            return this;
        }

        public ChunkLayoutBuilder Width(float s, float xMin, float xMax)
        {
            _v.Widths.RemoveAll(k => Math.Abs(k.S - s) < 1e-4f);
            _v.Widths.Add(new CourseWidthKey(s, xMin, xMax));
            _v.Widths.Sort((a, b) => a.S.CompareTo(b.S));
            return this;
        }

        public ChunkLayoutBuilder Half(float s, float half)
        {
            return Width(s, -half, half);
        }

        public ChunkLayoutBuilder Floor(float s0, float s1, float y0, float y1, float xMin = -Full, float xMax = Full)
        {
            _v.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Ramp, SMin = s0, SMax = s1, XMin = xMin, XMax = xMax, Y0 = y0, Y1 = y1 });
            return this;
        }

        public ChunkLayoutBuilder Gap(float s0, float s1, float xMin = -Full, float xMax = Full)
        {
            _v.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Gap, SMin = s0, SMax = s1, XMin = xMin, XMax = xMax });
            return this;
        }

        public ChunkLayoutBuilder Low(float s, float top, float xMin = -Full, float xMax = Full, bool walk = false, float depth = 0f, string label = null)
        {
            float d = depth > 0f ? depth : walk ? LogDepth : Depth;
            Clamp(s, ref xMin, ref xMax);
            float y = FloorAt(s, (xMin + xMax) * 0.5f);
            return Add(new CourseObstacle { Label = label ?? (walk ? "Log " : "Low ") + top.ToString("0.0#") + " @" + s, Class = ObstacleClass.Low, SMin = s, SMax = s + d, XMin = xMin, XMax = xMax, YMin = y, YMax = y + top, WalkableTop = walk });
        }

        public ChunkLayoutBuilder High(float s, float bottom = 1.0f, float xMin = -Full, float xMax = Full, string label = null)
        {
            Clamp(s, ref xMin, ref xMax);
            float y = FloorAt(s, (xMin + xMax) * 0.5f);
            return Add(new CourseObstacle { Label = label ?? "High @" + s, Class = ObstacleClass.High, SMin = s, SMax = s + Depth, XMin = xMin, XMax = xMax, YMin = y + bottom, YMax = y + bottom + 1.5f });
        }

        /// <summary>Blk w @x.</summary>
        public ChunkLayoutBuilder Blk(float s, float width, float centerX, string label = null)
        {
            return BlkSpan(s, centerX - (width * 0.5f), centerX + (width * 0.5f), label ?? "Rock " + width.ToString("0.0") + " @" + s);
        }

        public ChunkLayoutBuilder BlkSpan(float s, float xMin, float xMax, string label = null)
        {
            Clamp(s, ref xMin, ref xMax);
            float y = FloorAt(s, (xMin + xMax) * 0.5f);
            return Add(new CourseObstacle { Label = label ?? "Blocker @" + s, Class = ObstacleClass.Blocker, SMin = s, SMax = s + Depth, XMin = xMin, XMax = xMax, YMin = y, YMax = y + 2.5f });
        }

        public ChunkLayoutBuilder Thorns(float s0, float s1, float xMin = -Full, float xMax = Full, float height = 0.5f)
        {
            Clamp(s0, ref xMin, ref xMax);
            float y = FloorAt(s0, (xMin + xMax) * 0.5f);
            return Add(new CourseObstacle { Label = "Thorns @" + s0, Class = ObstacleClass.Thorns, SMin = s0, SMax = s1, XMin = xMin, XMax = xMax, YMin = y, YMax = y + height });
        }

        public ChunkLayoutBuilder Divider(string name, float sFront, float sMerge, float xMin, float xMax, int safeSide)
        {
            _v.Dividers.Add(new ChunkDivider { Name = name, SFront = sFront, SMerge = sMerge, CenterX = (xMin + xMax) * 0.5f, HalfWidth = (xMax - xMin) * 0.5f, SafeSide = safeSide });
            return this;
        }

        public ChunkLayoutBuilder Route(string name, RouteType type, params RouteStep[] steps)
        {
            var route = new ChunkRoute { Name = name, Type = type };
            route.Steps.AddRange(steps);
            _v.Routes.Add(route);
            return this;
        }

        public ChunkLayoutBuilder LockedRoute(string name, RouteType type, AbilityFlags ability, string note)
        {
            _v.Routes.Add(new ChunkRoute { Name = name, Type = type, Locked = true, RequiredAbility = ability, Note = note });
            return this;
        }

        public ChunkLayoutBuilder Coins(CoinPattern pattern)
        {
            _v.Coins.Add(pattern);
            return this;
        }

        public ChunkLayoutBuilder Line(float s0, float s1, float x, float step = 1.5f, float y = 0f)
        {
            CoinPattern p = CoinPattern.Line(s0, s1, x, step);
            p.Y = y;
            return Coins(p);
        }

        public ChunkLayoutBuilder Weave(float s0, float s1, float amp, float period, float x = 0f, float step = 2.5f)
        {
            return Coins(CoinPattern.Weave(s0, s1, amp, period, x, step));
        }

        public ChunkLayoutBuilder Arc(float s, float x = 0f, int count = 3)
        {
            return Coins(CoinPattern.Arc(s, x, count));
        }

        public ChunkLayoutBuilder Under(float s, int count = 3, float x = 0f)
        {
            return Coins(CoinPattern.Under(s, count, x));
        }

        public ChunkLayoutBuilder Point(float s, float x, float y)
        {
            return Coins(CoinPattern.Point(s, x, y));
        }

        public ChunkLayoutBuilder Crystal(float s, float x, float y, bool always)
        {
            _v.Crystals.Add(new CrystalAnchor(s, x, y, always));
            return this;
        }

        public ChunkLayoutBuilder PowerUp(float s, float x, float y, bool onRisky)
        {
            _v.PowerUps.Add(new PowerUpSlot(s, x, y, onRisky));
            return this;
        }

        public ChunkLayoutBuilder Discovery(string entry, float s, float xMin = -Full, float xMax = Full, bool placeholder = false, AbilityFlags ability = AbilityFlags.None, string note = "")
        {
            _v.Discoveries.Add(new DiscoveryTrigger { EntryId = entry, S = s, XMin = xMin, XMax = xMax, Placeholder = placeholder, RequiredAbility = ability, Note = note });
            return this;
        }

        public ChunkLayoutBuilder Zone(TraversalMode mode, float s0, float s1, float xMin, float xMax, float value, bool placeholder, string note)
        {
            _v.Traversal.Add(new TraversalZone { Mode = mode, SMin = s0, SMax = s1, XMin = xMin, XMax = xMax, Value = value, Note = (placeholder ? "[Part B placeholder] " : string.Empty) + note });
            return this;
        }

        public ChunkLayoutBuilder Help(HelpMove move, float s, float xMin = -Full, float xMax = Full)
        {
            _v.Help.Add(new HelpMarker { Move = move, S = s, XMin = xMin, XMax = xMax });
            return this;
        }

        public static RouteStep Step(int divider, int side)
        {
            return new RouteStep(divider, side);
        }

        /// <summary>Outer path edges at s from the width keys authored so far.</summary>
        public void OuterAt(float s, out float xMin, out float xMax)
        {
            List<CourseWidthKey> keys = _v.Widths;
            if (s <= keys[0].S)
            {
                xMin = keys[0].XMin;
                xMax = keys[0].XMax;
                return;
            }

            for (int i = 1; i < keys.Count; i++)
            {
                if (s < keys[i].S)
                {
                    float t = (s - keys[i - 1].S) / (keys[i].S - keys[i - 1].S);
                    xMin = keys[i - 1].XMin + ((keys[i].XMin - keys[i - 1].XMin) * t);
                    xMax = keys[i - 1].XMax + ((keys[i].XMax - keys[i - 1].XMax) * t);
                    return;
                }
            }

            xMin = keys[keys.Count - 1].XMin;
            xMax = keys[keys.Count - 1].XMax;
        }

        private float FloorAt(float s, float x)
        {
            for (int i = _v.Floors.Count - 1; i >= 0; i--)
            {
                CourseFloorPatch p = _v.Floors[i];
                if (p.Kind == CourseFloorKind.Ramp && p.Contains(s, x))
                {
                    return p.HeightAt(s);
                }
            }

            return 0f;
        }

        /// <summary>"Full" (±99) becomes the path (or lane) edge at s.</summary>
        private void Clamp(float s, ref float xMin, ref float xMax)
        {
            OuterAt(s, out float a, out float b);
            if (xMin <= -Full + 1f)
            {
                xMin = a;
            }

            if (xMax >= Full - 1f)
            {
                xMax = b;
            }
        }

        private ChunkLayoutBuilder Add(CourseObstacle obstacle)
        {
            _v.Obstacles.Add(obstacle);
            return this;
        }
    }
}
