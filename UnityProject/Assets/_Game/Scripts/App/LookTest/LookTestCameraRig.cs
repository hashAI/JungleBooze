using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Where the stand-in runner and the follow camera are at a given run time and distance (ADR 0004 look test).
    /// Pure math shared by <see cref="LookTestRoot"/> (Play) and the editor screenshot tool, so captured frames use
    /// exactly the game camera. Stretch space: run direction +z, path centered on x = 0, ground y = 0.
    /// </summary>
    public static class LookTestCameraRig
    {
        public const float WeaveAmplitudeM = 1.6f;
        public const float WeavePeriodS = 7f;

        /// <summary>The camera follows half of the runner's side-to-side weave, so she visibly crosses the screen.</summary>
        public const float CameraFollowX = 0.5f;

        /// <summary>Runner's side offset from the path center after <paramref name="runTimeS"/> seconds.</summary>
        public static float RunnerX(float runTimeS)
        {
            return WeaveAmplitudeM * Mathf.Sin(runTimeS * (2f * Mathf.PI / WeavePeriodS));
        }

        /// <summary>Runner's feet position.</summary>
        public static Vector3 RunnerPosition(float runTimeS, float distanceM)
        {
            return new Vector3(RunnerX(runTimeS), 0f, distanceM);
        }

        /// <summary>Camera position the follow camera settles to (before smoothing).</summary>
        public static Vector3 CameraTarget(LookTestConfigAsset config, float runnerX, float distanceM)
        {
            return new Vector3(runnerX * CameraFollowX, config.CameraOffsetUpM, distanceM - config.CameraOffsetBehindM);
        }

        /// <summary>Point the camera looks at (landscape).</summary>
        public static Vector3 LookAtPoint(LookTestConfigAsset config, float runnerX, float distanceM)
        {
            return LookAtPoint(config, runnerX, distanceM, 2f);
        }

        /// <summary>Point the camera looks at for a screen aspect (portrait aims higher to frame the forest).</summary>
        public static Vector3 LookAtPoint(LookTestConfigAsset config, float runnerX, float distanceM, float aspect)
        {
            float height = aspect >= 1f ? config.CameraLookAtHeightM : config.CameraPortraitLookAtHeightM;
            return new Vector3(runnerX * CameraFollowX, height, distanceM + config.CameraLookAheadM);
        }

        /// <summary>
        /// Vertical field of view for an aspect ratio. Landscape uses the configured vertical FOV; portrait keeps
        /// the landscape horizontal FOV's width at the configured portrait horizontal angle so the path is not cropped.
        /// </summary>
        public static float VerticalFov(LookTestConfigAsset config, float aspect)
        {
            if (aspect >= 1f)
            {
                return config.CameraFovDeg;
            }

            float horizontalRad = config.CameraPortraitHorizontalFovDeg * Mathf.Deg2Rad;
            float verticalRad = 2f * Mathf.Atan(Mathf.Tan(horizontalRad * 0.5f) / Mathf.Max(0.01f, aspect));
            return Mathf.Clamp(verticalRad * Mathf.Rad2Deg, config.CameraFovDeg, 100f);
        }
    }
}
