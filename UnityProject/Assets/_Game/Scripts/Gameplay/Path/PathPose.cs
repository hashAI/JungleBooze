using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Pose of the route centerline at one <c>s</c> (spec 003 section 3.1), in WorldRoot space. All four frame
    /// vectors include the bank. A plain struct, so queries never allocate.
    /// </summary>
    public struct PathPose
    {
        /// <summary>Centerline position C (floating-origin offset applied).</summary>
        public Vector3 Center;

        /// <summary>Unit tangent T (forward).</summary>
        public Vector3 Tangent;

        /// <summary>Unit right N, along the banked surface.</summary>
        public Vector3 Right;

        /// <summary>Unit up U, normal of the banked surface.</summary>
        public Vector3 Up;

        /// <summary>Curvature in rad/m, positive = turning right.</summary>
        public float Curvature;

        /// <summary>Grade in percent (positive = climbing).</summary>
        public float GradePct;

        /// <summary>Bank in degrees.</summary>
        public float BankDeg;

        public PathLayer Layer;

        public PathSurface Surface;

        /// <summary>Half of the playable width in m (3.6).</summary>
        public float HalfWidthM;
    }
}
