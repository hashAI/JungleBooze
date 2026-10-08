using System;

namespace JungleBooze.Gameplay.Course
{
    /// <summary>
    /// A route split (spec 102 §3.3). Inside [SFront, SMerge) each branch has its own edges; the runner's x vs the
    /// divider centre picks the branch. Before SFront the edges blend from the course keys to the outer branch edges.
    /// </summary>
    [Serializable]
    public struct CourseFork
    {
        public string Name;
        public float SFront;
        public float SMerge;
        public float DividerCenterX;
        public float DividerHalfWidth;
        public float LeftXMin;
        public float LeftXMax;
        public float RightXMin;
        public float RightXMax;

        /// <summary>−1 = left is the safe branch, +1 = right.</summary>
        public int SafeSide;
    }
}
