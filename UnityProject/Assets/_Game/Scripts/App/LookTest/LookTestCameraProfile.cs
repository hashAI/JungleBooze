using System;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>Follow-camera numbers for one orientation (spec 101 section 5 table).</summary>
    [Serializable]
    public struct LookTestCameraProfile
    {
        [Tooltip("Offset back along the path heading, m.")]
        public float BackM;
        [Tooltip("Height above the runner's ground, m.")]
        public float UpM;
        [Tooltip("Pitch down, degrees.")]
        public float PitchDeg;
        [Tooltip("Vertical field of view, degrees.")]
        public float VerticalFovDeg;
        [Tooltip("Share of the runner's lateral offset the camera follows (spec 101: 0.70 landscape, 0.80 portrait).")]
        public float LateralFollow;
        [Tooltip("Path-heading and ground smoothing expressed as a lag along the path, m (≈ half-life / ln 2 × speed).")]
        public float FollowLagM;

        public LookTestCameraProfile(float backM, float upM, float pitchDeg, float verticalFovDeg, float lateralFollow, float followLagM)
        {
            BackM = backM;
            UpM = upM;
            PitchDeg = pitchDeg;
            VerticalFovDeg = verticalFovDeg;
            LateralFollow = lateralFollow;
            FollowLagM = followLagM;
        }
    }
}
