using System;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>Path edges at a distance; linear between keys.</summary>
    [Serializable]
    public struct CourseWidthKey
    {
        public float S;
        public float XMin;
        public float XMax;

        public CourseWidthKey(float s, float xMin, float xMax)
        {
            S = s;
            XMin = xMin;
            XMax = xMax;
        }
    }
}
