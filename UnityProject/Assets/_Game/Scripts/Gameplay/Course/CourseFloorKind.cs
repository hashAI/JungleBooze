namespace JungleBooze.Gameplay.Course
{
    public enum CourseFloorKind
    {
        /// <summary>No floor over the patch.</summary>
        Gap = 0,

        /// <summary>Floor height linear from Y0 at SMin to Y1 at SMax.</summary>
        Ramp = 1,
    }
}
