using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// The one helper every view uses to place things in the world from simulation coordinates (spec 003 section 2
    /// principle 2, risk R10): <c>s</c> = distance along the route (the simulation's z), <c>x</c> = lateral,
    /// <c>y</c> = height. Views sample the pose once per object and use <see cref="Point"/> and
    /// <see cref="Orientation"/>; no view may use the simulation z as a world z any more.
    /// With the straight route the mapping is the identity, so nothing moves. No allocation.
    /// </summary>
    public static class PathPlacement
    {
        private static PathFrame _identity;

        /// <summary>
        /// A shared straight frame for views that were never given one (tests, tools). Created on first use
        /// (a runtime instance of the default <see cref="RouteTuning"/>), so call it from Init, not a field initializer.
        /// </summary>
        public static PathFrame Identity
        {
            get
            {
                if (_identity == null)
                {
                    _identity = new PathFrame(RouteTuning.CreateDefault(), new StraightRouteSource());
                }

                return _identity;
            }
        }

        /// <summary><paramref name="frame"/>, or the shared straight frame when it is null.</summary>
        public static PathFrame OrIdentity(PathFrame frame)
        {
            return frame ?? Identity;
        }

        /// <summary><c>C + x * N + y * U</c> for a pose already sampled (same as <see cref="PathFrame.ToWorld"/>).</summary>
        public static Vector3 Point(in PathPose pose, float x, float y)
        {
            return pose.Center + (pose.Right * x) + (pose.Up * y);
        }

        /// <summary>Frame rotation (forward = tangent, up = banked up) for a pose already sampled (same as <see cref="PathFrame.RotationAt"/>).</summary>
        public static Quaternion Orientation(in PathPose pose)
        {
            return Quaternion.LookRotation(pose.Tangent, pose.Up);
        }

        /// <summary>Compass heading of the route at a pose in degrees: 0 = along +z, positive turns toward +x.</summary>
        public static float HeadingYawDeg(in PathPose pose)
        {
            return Mathf.Atan2(pose.Tangent.x, pose.Tangent.z) * Mathf.Rad2Deg;
        }

        /// <summary>Puts <paramref name="target"/> (a child of an identity parent) at (s, x, y) with the route rotation there.</summary>
        public static void Place(PathFrame frame, Transform target, double s, float x, float y)
        {
            frame.Sample(s, out PathPose pose);
            target.localPosition = Point(pose, x, y);
            target.localRotation = Orientation(pose);
        }
    }
}
