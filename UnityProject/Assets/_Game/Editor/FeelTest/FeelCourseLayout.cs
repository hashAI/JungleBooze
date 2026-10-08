using System;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Editor.FeelTest
{
    /// <summary>
    /// Authoring script for the spec 101 §6 feel course (61 s, gray box). It writes the layout into
    /// <c>Assets/_Game/Config/Movement/FeelCourse.asset</c> once; afterwards the asset is the source of truth and can
    /// be edited by hand. Positions "at s" are obstacle centres. Depths and heights the spec leaves open are
    /// marked [ASSUMED] below.
    /// </summary>
    public static class FeelCourseLayout
    {
        // [ASSUMED] gray-box dimensions not given by the spec.
        private const float LowDepth = 0.5f;
        private const float LowHeight = 0.6f;
        private const float LogDepth = 1.0f;
        private const float LogHeight = 0.9f;
        private const float HighDepth = 0.6f;
        private const float HighBottom = 1.0f;
        private const float HighTopAbove = 1.4f;
        private const float BlockerDepth = 1.2f;
        private const float BlockerHeight = 2.4f;
        private const float ThornsHeight = 0.5f;
        private const float CoinY = 0.9f;
        private const float GroundCoinY = 0.35f;
        private const float FullWidthOverhang = 0.3f;

        public static CourseData Create()
        {
            var c = new CourseData { Name = "Feel course (spec 101 §6)", FinishS = 640f, RunOutLength = 80f };

            // Edges: C1 is 8 m wide, then 7 m; C9 narrows to a 2.4 m ledge; the C12 fork shifts the outer edges.
            AddWidth(c, 0f, 4f);
            AddWidth(c, 36f, 4f);
            AddWidth(c, 44f, 3.5f);
            AddWidth(c, 380f, 3.5f);
            AddWidth(c, 404f, 1.2f);
            AddWidth(c, 430f, 1.2f);
            AddWidth(c, 440f, 3.5f);
            AddWidth(c, 520f, 3.5f);
            c.Widths.Add(new CourseWidthKey(530f, -4.1f, 3.0f));
            c.Widths.Add(new CourseWidthKey(590f, -4.1f, 3.0f));
            AddWidth(c, 600f, 3.5f);
            AddWidth(c, 760f, 3.5f);

            c.Sections.Add(new CourseSection("C1 Start", 0f, 40f));
            c.Sections.Add(new CourseSection("C2 Weave", 40f, 100f));
            c.Sections.Add(new CourseSection("C3 Slalom", 100f, 150f));
            c.Sections.Add(new CourseSection("C4 Jumps", 150f, 210f));
            c.Sections.Add(new CourseSection("C5 Slides", 210f, 260f));
            c.Sections.Add(new CourseSection("C6 Combos", 260f, 300f + 3f));
            c.Sections.Add(new CourseSection("C7 Gaps", 303f, 340f));
            c.Sections.Add(new CourseSection("C8 Dodge", 340f, 380f));
            c.Sections.Add(new CourseSection("C9 Ledge", 380f, 440f));
            c.Sections.Add(new CourseSection("C10 Fast-fall", 440f, 470f));
            c.Sections.Add(new CourseSection("C11 Mix", 470f, 520f));
            c.Sections.Add(new CourseSection("C12 Fork", 520f, 600f));
            c.Sections.Add(new CourseSection("C13 Finish", 600f, 720f));

            // C1 Start: coin line on the centre.
            for (float s = 10f; s <= 38f; s += 2.5f)
            {
                c.Coins.Add(new CourseCoin(s, 0f, CoinY));
            }

            // C2 Weave: x = 2.5·sin(2π(s−40)/30).
            for (float s = 42f; s <= 98f; s += 2f)
            {
                float x = 2.5f * (float)Math.Sin(2.0 * Math.PI * (s - 40f) / 30f);
                c.Coins.Add(new CourseCoin(s, x, CoinY));
            }

            // C3 Slalom: 1.4 m blockers.
            Blocker(c, "C3 slalom rock L 110", 110f, -1.5f - 0.7f, -1.5f + 0.7f);
            Blocker(c, "C3 slalom rock R 125", 125f, 1.5f - 0.7f, 1.5f + 0.7f);
            Blocker(c, "C3 slalom rock L 140", 140f, -1.5f - 0.7f, -1.5f + 0.7f);

            // C4 Jumps.
            FullLow(c, "C4 root 160", 160f, LowDepth, LowHeight, false);
            FullLow(c, "C4 root 185", 185f, LowDepth, LowHeight, false);
            FullLow(c, "C4 log 205 (walkable)", 205f, LogDepth, LogHeight, true);

            // C5 Slides.
            FullHigh(c, "C5 branch 220", 220f, 0f);
            FullHigh(c, "C5 branch 240", 240f, 0f);

            // C6 Combos.
            FullHigh(c, "C6 branch 270", 270f, 0f);
            FullLow(c, "C6 root 280", 280f, LowDepth, LowHeight, false);
            FullLow(c, "C6 root 292", 292f, LowDepth, LowHeight, false);
            FullHigh(c, "C6 branch 300", 300f, 0f);

            // C7 Gaps (full width).
            Gap(c, 315f, 318f, -10f, 10f);
            Gap(c, 330f, 334.5f, -10f, 10f);

            // C8 Dodge: staggered blockers to the path edges.
            Box(c, "C8 wall right 355", ObstacleClass.Blocker, 355f, 1.0f, -0.5f, 3.5f + FullWidthOverhang, 0f, BlockerHeight, false);
            Box(c, "C8 wall left 365", ObstacleClass.Blocker, 365f, 1.0f, -3.5f - FullWidthOverhang, 0.5f, 0f, BlockerHeight, false);

            // C9 Ledge coins.
            for (float s = 406f; s <= 428f; s += 2f)
            {
                c.Coins.Add(new CourseCoin(s, 0f, CoinY));
            }

            // C10 Fast-fall: log, ground coin cluster (a normal arc overflies it), branch.
            FullLow(c, "C10 log 450 (walkable)", 450f, LogDepth, LogHeight, true);
            for (float s = 452f; s <= 455.01f; s += 1.5f)
            {
                c.Coins.Add(new CourseCoin(s, -0.6f, GroundCoinY));
                c.Coins.Add(new CourseCoin(s, 0f, GroundCoinY));
                c.Coins.Add(new CourseCoin(s, 0.6f, GroundCoinY));
            }

            FullHigh(c, "C10 branch 462", 462f, 0f);

            // C11 Mix.
            FullLow(c, "C11 root 480", 480f, LowDepth, LowHeight, false);
            Blocker(c, "C11 rock 490", 490f, 1.0f - 0.6f, 1.0f + 0.6f);
            FullHigh(c, "C11 branch 500", 500f, 0f);
            Box(c, "C11 thorns 506", ObstacleClass.Thorns, 506f, 2.0f, -3.5f - FullWidthOverhang, -1.0f, 0f, ThornsHeight, false);
            FullLow(c, "C11 root 512", 512f, LowDepth, LowHeight, false);

            // C12 Fork: divider 1.2 m at x = 0 from 530 to 590; left/safe 3.5 m, right/risky 2.4 m raised to +1.0 m.
            c.Forks.Add(new CourseFork
            {
                Name = "C12 fork",
                SFront = 530f,
                SMerge = 590f,
                DividerCenterX = 0f,
                DividerHalfWidth = 0.6f,
                LeftXMin = -4.1f,
                LeftXMax = -0.6f,
                RightXMin = 0.6f,
                RightXMax = 3.0f,
                SafeSide = -1,
            });
            Box(c, "C12 safe root 560", ObstacleClass.Low, 560f, LowDepth, -4.1f - FullWidthOverhang, -0.6f, 0f, LowHeight, false);
            Ramp(c, 535f, 545f, 0.6f, 3.0f, 0f, 1f);
            Ramp(c, 545f, 582f, 0.6f, 3.0f, 1f, 1f);
            Ramp(c, 582f, 590f, 0.6f, 3.0f, 1f, 0f); // [ASSUMED] ramp back down before the merge
            Gap(c, 560f, 562.5f, 0.6f, 3.0f);
            Box(c, "C12 risky branch 575", ObstacleClass.High, 575f, HighDepth, 0.6f, 3.0f + FullWidthOverhang, 1f + HighBottom, 1f + HighBottom + HighTopAbove, false);
            for (float s = 538f; s <= 586f; s += 3f)
            {
                c.Coins.Add(new CourseCoin(s, -2.35f, CoinY));
                if (s < 557f || s > 566f)
                {
                    float floor = RampHeight(s);
                    c.Coins.Add(new CourseCoin(s, 1.3f, floor + CoinY));
                    c.Coins.Add(new CourseCoin(s, 2.3f, floor + CoinY));
                }
            }

            // C13 Finish.
            for (float s = 606f; s <= 636f; s += 2.5f)
            {
                c.Coins.Add(new CourseCoin(s, 0f, CoinY));
            }

            return c;
        }

        private static float RampHeight(float s)
        {
            if (s < 535f || s >= 590f)
            {
                return 0f;
            }

            if (s < 545f)
            {
                return (s - 535f) / 10f;
            }

            if (s < 582f)
            {
                return 1f;
            }

            return 1f - ((s - 582f) / 8f);
        }

        private static void AddWidth(CourseData c, float s, float half)
        {
            c.Widths.Add(new CourseWidthKey(s, -half, half));
        }

        private static void Blocker(CourseData c, string label, float s, float xMin, float xMax)
        {
            Box(c, label, ObstacleClass.Blocker, s, BlockerDepth, xMin, xMax, 0f, BlockerHeight, false);
        }

        private static void FullLow(CourseData c, string label, float s, float depth, float height, bool walkable)
        {
            Box(c, label, ObstacleClass.Low, s, depth, -4f - FullWidthOverhang, 4f + FullWidthOverhang, 0f, height, walkable);
        }

        private static void FullHigh(CourseData c, string label, float s, float floor)
        {
            Box(c, label, ObstacleClass.High, s, HighDepth, -4f - FullWidthOverhang, 4f + FullWidthOverhang, floor + HighBottom, floor + HighBottom + HighTopAbove, false);
        }

        private static void Box(CourseData c, string label, ObstacleClass kind, float s, float depth, float xMin, float xMax, float yMin, float yMax, bool walkable)
        {
            c.Obstacles.Add(new CourseObstacle
            {
                Label = label,
                Class = kind,
                SMin = s - (depth * 0.5f),
                SMax = s + (depth * 0.5f),
                XMin = xMin,
                XMax = xMax,
                YMin = yMin,
                YMax = yMax,
                WalkableTop = walkable,
            });
        }

        private static void Gap(CourseData c, float sMin, float sMax, float xMin, float xMax)
        {
            c.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Gap, SMin = sMin, SMax = sMax, XMin = xMin, XMax = xMax });
        }

        private static void Ramp(CourseData c, float sMin, float sMax, float xMin, float xMax, float y0, float y1)
        {
            c.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Ramp, SMin = sMin, SMax = sMax, XMin = xMin, XMax = xMax, Y0 = y0, Y1 = y1 });
        }
    }
}
