using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>
    /// A hand-authored course in path space (the feel course of spec 101 §6, later test courses). Serialized inside
    /// <c>FeelCourseAsset</c>; turned into a queryable <see cref="CoursePath"/> at run start.
    /// </summary>
    [Serializable]
    public sealed class CourseData
    {
        public string Name = "Course";

        /// <summary>Finish line, m.</summary>
        public float FinishS = 640f;

        /// <summary>Path continues this far past the finish for the coast-out, m.</summary>
        public float RunOutLength = 60f;

        public List<CourseWidthKey> Widths = new List<CourseWidthKey>();
        public List<CourseFloorPatch> Floors = new List<CourseFloorPatch>();
        public List<CourseFork> Forks = new List<CourseFork>();
        public List<CourseObstacle> Obstacles = new List<CourseObstacle>();
        public List<CourseCoin> Coins = new List<CourseCoin>();
        public List<CourseSection> Sections = new List<CourseSection>();
    }
}
