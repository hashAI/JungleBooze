using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Hitbox of one archetype for one lane (spec 002 section 5.1): centred on the lane centre (movers: on their
    /// current x), starting at the obstacle front.
    /// </summary>
    [Serializable]
    public struct ObstacleShape
    {
        public float WidthM;
        public float DepthM;
        public float BottomM;
        public float TopM;

        public ObstacleShape(float widthM, float depthM, float bottomM, float topM)
        {
            WidthM = widthM;
            DepthM = depthM;
            BottomM = bottomM;
            TopM = topM;
        }
    }
}
