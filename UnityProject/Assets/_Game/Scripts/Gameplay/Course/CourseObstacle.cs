using System;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>An authored obstacle box (path space). Label shows in hit reports and the debug overlay.</summary>
    [Serializable]
    public struct CourseObstacle
    {
        public string Label;
        public ObstacleClass Class;
        public float SMin;
        public float SMax;
        public float XMin;
        public float XMax;
        public float YMin;
        public float YMax;
        public bool WalkableTop;
    }
}
