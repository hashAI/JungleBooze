using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Where the stand-in runner and the follow camera are for a run distance (look test v2). Pure math shared by
    /// <see cref="LookTestRoot"/> (Play) and the editor screenshot tool, so captured frames use exactly the game camera.
    /// The camera follows spec 101 section 5: offset back along the (lagged) path heading, height above the runner's
    /// ground, fixed pitch, vertical FOV per orientation, partial lateral follow. The time smoothing of spec 101
    /// (yaw half-life 0.25 s, ground 0.30 s) is modelled as a lag in meters along the path, so a still frame
    /// and a running frame agree at constant speed.
    /// </summary>
    public static class LookTestCameraRig
    {
        public const float WeaveAmplitudeM = 1.6f;
        public const float WeavePeriodS = 7f;

        /// <summary>Runner's lateral offset from the path centre after <paramref name="runTimeS"/> seconds.</summary>
        public static float RunnerX(float runTimeS)
        {
            return WeaveAmplitudeM * Mathf.Sin(runTimeS * (2f * Mathf.PI / WeavePeriodS));
        }

        /// <summary>Runner's feet in the world.</summary>
        public static Vector3 RunnerPosition(LookTestPath path, double s, float runnerX)
        {
            return path.World(s, runnerX);
        }

        /// <summary>Landscape profile for aspect ≥ 1, portrait otherwise.</summary>
        public static LookTestCameraProfile Profile(LookTestConfigAsset config, float aspect)
        {
            return aspect >= 1f ? config.LandscapeCamera : config.PortraitCamera;
        }

        /// <summary>Settled camera pose for the runner at (s, runnerX).</summary>
        public static void Pose(LookTestPath path, LookTestCameraProfile profile, double s, float runnerX, out Vector3 position, out Quaternion rotation)
        {
            double lagged = s - profile.FollowLagM;
            float yaw = path.HeadingRad(lagged);
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 anchor = path.World(s, runnerX * profile.LateralFollow);
            anchor.y = path.Height(lagged) + profile.UpM;
            position = anchor - forward * profile.BackM;
            rotation = Quaternion.Euler(profile.PitchDeg, yaw * Mathf.Rad2Deg, 0f);
        }

        /// <summary>
        /// Screen-height fraction (0..1) covered by a figure of <paramref name="heightM"/> standing on flat ground at
        /// the runner's spot, and the screen y of the feet (0 = bottom). Used to check art direction section 9.
        /// </summary>
        public static Vector2 FigureOnScreen(LookTestCameraProfile profile, float heightM)
        {
            float feet = ScreenY(profile, 0f);
            float head = ScreenY(profile, heightM);
            return new Vector2(head - feet, feet);
        }

        /// <summary>Screen y (0 bottom, 1 top) of the horizon.</summary>
        public static float HorizonScreenY(LookTestCameraProfile profile)
        {
            float t = Mathf.Tan(profile.VerticalFovDeg * 0.5f * Mathf.Deg2Rad);
            return 0.5f + 0.5f * Mathf.Tan(profile.PitchDeg * Mathf.Deg2Rad) / t;
        }

        private static float ScreenY(LookTestCameraProfile profile, float y)
        {
            float p = profile.PitchDeg * Mathf.Deg2Rad;
            float dz = profile.BackM;
            float dy = y - profile.UpM;
            float zc = dz * Mathf.Cos(p) - dy * Mathf.Sin(p);
            float yc = dz * Mathf.Sin(p) + dy * Mathf.Cos(p);
            float t = Mathf.Tan(profile.VerticalFovDeg * 0.5f * Mathf.Deg2Rad);
            return 0.5f + 0.5f * (yc / Mathf.Max(0.01f, zc)) / t;
        }
    }
}
