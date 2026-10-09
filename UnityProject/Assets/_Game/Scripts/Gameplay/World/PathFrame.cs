using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A point of the view-side path: world-space centreline position (x, z) and heading (radians, yaw from +z toward
    /// +x). Path space (s, x, y) maps to world as position + right · x + up · y.
    /// </summary>
    public struct PathFrame
    {
        public float X;
        public float Z;
        public float Heading;

        public float RightX => (float)Math.Cos(Heading);

        public float RightZ => -(float)Math.Sin(Heading);

        public float ForwardX => (float)Math.Sin(Heading);

        public float ForwardZ => (float)Math.Cos(Heading);

        public float HeadingDeg => Heading * (180f / (float)Math.PI);

        /// <summary>World x/z of the lateral offset <paramref name="lateral"/>.</summary>
        public void Offset(float lateral, out float wx, out float wz)
        {
            float c = (float)Math.Cos(Heading);
            float s = (float)Math.Sin(Heading);
            wx = X + (c * lateral);
            wz = Z - (s * lateral);
        }

        public static PathFrame Compose(in PathFrame parent, float localX, float localZ, float localHeading)
        {
            float c = (float)Math.Cos(parent.Heading);
            float s = (float)Math.Sin(parent.Heading);
            return new PathFrame
            {
                X = parent.X + (c * localX) + (s * localZ),
                Z = parent.Z - (s * localX) + (c * localZ),
                Heading = parent.Heading + localHeading,
            };
        }
    }
}
