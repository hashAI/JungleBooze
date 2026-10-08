using System;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>A named stretch of the course (C1 … C13), for reports and the debug overlay.</summary>
    [Serializable]
    public struct CourseSection
    {
        public string Name;
        public float SMin;
        public float SMax;

        public CourseSection(string name, float sMin, float sMax)
        {
            Name = name;
            SMin = sMin;
            SMax = sMax;
        }
    }
}
