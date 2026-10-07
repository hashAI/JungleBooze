using JungleBooze.Gameplay.Views;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Designer-facing environment look tuning (ground, jungle walls, sky haze, key light, ambient), saved as
    /// <c>Assets/_Game/Config/Resources/EnvironmentLook.asset</c>. Defaults are the look-pass start values.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentLook", menuName = "JungleBooze/Config/Environment Look")]
    public sealed class EnvironmentLookConfigAsset : ScriptableObject
    {
        [Header("Ground")]
        [SerializeField] private float _trailHalfWidthM = 5.4f;
        [SerializeField] private float _groundTileM = 6f;
        [SerializeField] private float _groundBehindM = 18f;
        [SerializeField] private float _groundExtraAheadM = 24f;
        [SerializeField] private float _floorInnerM = 4.6f;
        [SerializeField] private float _floorOuterM = 44f;
        [SerializeField] private float _floorDropM = 0.02f;
        [SerializeField] private int _groundAnisoLevel = 4;

        [Header("Jungle walls")]
        [SerializeField] private float _wallSegmentM = 12f;
        [SerializeField] private float _wallInsetM = 0f;
        [SerializeField] private float _wallBehindM = 12f;
        [SerializeField] private float _wallHeightScaleMin = 0.92f;
        [SerializeField] private float _wallHeightScaleSpan = 0.22f;
        [SerializeField] private bool _wallMirror = true;

        [Header("Sky")]
        [SerializeField] private float _farRingHaze = 0.72f;
        [SerializeField] private float _nearRingHaze = 0.55f;

        [Header("Light")]
        [SerializeField] private float _keyPitchDeg = 38f;
        [SerializeField] private float _keyYawDeg = -35f;
        [SerializeField] private float _keyIntensity = 1.0f;
        [SerializeField] private float _ambientSkyWhiteMix = 0.6f;
        [SerializeField] private float _ambientEquatorMix = 0.5f;
        [SerializeField] private float _ambientGroundInkMix = 0f;

        public EnvironmentLookConfig ToConfig()
        {
            return new EnvironmentLookConfig
            {
                TrailHalfWidthM = _trailHalfWidthM,
                GroundTileM = _groundTileM,
                GroundBehindM = _groundBehindM,
                GroundExtraAheadM = _groundExtraAheadM,
                FloorInnerM = _floorInnerM,
                FloorOuterM = _floorOuterM,
                FloorDropM = _floorDropM,
                GroundAnisoLevel = _groundAnisoLevel,
                WallSegmentM = _wallSegmentM,
                WallInsetM = _wallInsetM,
                WallBehindM = _wallBehindM,
                WallHeightScaleMin = _wallHeightScaleMin,
                WallHeightScaleSpan = _wallHeightScaleSpan,
                WallMirror = _wallMirror,
                FarRingHaze = _farRingHaze,
                NearRingHaze = _nearRingHaze,
                KeyPitchDeg = _keyPitchDeg,
                KeyYawDeg = _keyYawDeg,
                KeyIntensity = _keyIntensity,
                AmbientSkyWhiteMix = _ambientSkyWhiteMix,
                AmbientEquatorMix = _ambientEquatorMix,
                AmbientGroundInkMix = _ambientGroundInkMix,
            };
        }
    }
}
