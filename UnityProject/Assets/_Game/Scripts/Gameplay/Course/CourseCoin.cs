using System;

namespace JungleBooze.Gameplay.Course
{
    [Serializable]
    public struct CourseCoin
    {
        public float S;
        public float X;
        public float Y;

        public CourseCoin(float s, float x, float y)
        {
            S = s;
            X = x;
            Y = y;
        }
    }
}
