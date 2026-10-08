using UnityEngine;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>
    /// Maps a <see cref="CameraPose"/> on a straight path to a world transform and tests visibility. The feel
    /// course is straight (path space = world space: x lateral, y up, z = s); curved paths will map through the
    /// path spline in the view (spec 101 §2.1).
    /// </summary>
    public static class CameraMath
    {
        public static Vector3 Position(in CameraPose pose)
        {
            return new Vector3(pose.X + pose.ShakeX, pose.Y + pose.ShakeY, pose.S);
        }

        public static Quaternion Rotation(in CameraPose pose)
        {
            return Quaternion.Euler(pose.PitchDeg, pose.YawDeg, pose.RollDeg + pose.ShakeRollDeg);
        }

        /// <summary>World → clip matrix for the pose (Unity camera conventions).</summary>
        public static Matrix4x4 ViewProjection(in CameraPose pose, float aspect, float near, float far)
        {
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(Position(pose), Rotation(pose), Vector3.one).inverse;
            Matrix4x4 projection = Matrix4x4.Perspective(pose.FovDeg, aspect, near, far);
            return projection * view;
        }

        /// <summary>True if the point is inside the view frustum.</summary>
        public static bool Contains(in Matrix4x4 viewProjection, Vector3 point)
        {
            Vector4 clip = viewProjection * new Vector4(point.x, point.y, point.z, 1f);
            if (clip.w <= 0f)
            {
                return false;
            }

            float x = clip.x / clip.w;
            float y = clip.y / clip.w;
            float z = clip.z / clip.w;
            return x >= -1f && x <= 1f && y >= -1f && y <= 1f && z >= -1f && z <= 1f;
        }

        /// <summary>Screen-space height fraction (0 = bottom, 1 = top) of a point, or NaN if behind the camera.</summary>
        public static float ScreenY(in Matrix4x4 viewProjection, Vector3 point)
        {
            Vector4 clip = viewProjection * new Vector4(point.x, point.y, point.z, 1f);
            return clip.w <= 0f ? float.NaN : ((clip.y / clip.w) + 1f) * 0.5f;
        }
    }
}
