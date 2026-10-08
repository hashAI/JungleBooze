using System;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>
    /// A floor change over [SMin, SMax) × [XMin, XMax]. The default floor is flat at y = 0; later patches win.
    /// </summary>
    [Serializable]
    public struct CourseFloorPatch
    {
        public CourseFloorKind Kind;
        public float SMin;
        public float SMax;
        public float XMin;
        public float XMax;
        public float Y0;
        public float Y1;

        public bool Contains(float s, float x)
        {
            return s >= SMin && s < SMax && x >= XMin && x <= XMax;
        }

        public float HeightAt(float s)
        {
            float length = SMax - SMin;
            float t = length > 0f ? (s - SMin) / length : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return Y0 + ((Y1 - Y0) * t);
        }
    }
}
