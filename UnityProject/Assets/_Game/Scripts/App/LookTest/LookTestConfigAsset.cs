using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Every tuning number of the AURELIA Phase 0 look test (ADR 0004): the run through the stretch, the stretch
    /// layout used by the scene builder, scatter counts, light, sky, fog, render settings, post-processing and water.
    /// Saved as <c>Assets/_Game/Config/LookTest/LookTestConfig.asset</c> (created by
    /// JungleBooze > Look Test > Build Scene if missing). Change values, then build the scene again; run-time values
    /// (speed, camera, frame rate, render scale) apply on the next Play without a rebuild.
    /// Presentation only: the deterministic simulation is not affected except through its normal config path
    /// (the run uses a constant speed curve built from <see cref="RunSpeedMps"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "LookTestConfig", menuName = "JungleBooze/Config/Look Test Config")]
    public sealed class LookTestConfigAsset : ScriptableObject
    {
        [Header("Run (applies at Play)")]
        [Tooltip("Constant forward speed through the stretch, m/s.")]
        [SerializeField] private float _runSpeedMps = 9f;
        [Tooltip("Vertical field of view for landscape, degrees.")]
        [SerializeField] private float _cameraFovDeg = 47f;
        [Tooltip("Portrait screens: horizontal field of view, degrees (the vertical FOV grows to keep this width).")]
        [SerializeField] private float _cameraPortraitHorizontalFovDeg = 42f;
        [Tooltip("Portrait screens: look-at height, m (higher than landscape so the tall frame shows forest, not only path).")]
        [SerializeField] private float _cameraPortraitLookAtHeightM = 3.4f;
        [SerializeField] private float _cameraOffsetBehindM = 5.5f;
        [SerializeField] private float _cameraOffsetUpM = 2.5f;
        [SerializeField] private float _cameraLookAheadM = 10f;
        [SerializeField] private float _cameraLookAtHeightM = 1.3f;
        [SerializeField] private float _cameraFarClipM = 230f;
        [SerializeField] private int _targetFrameRate = 60;
        [Tooltip("Device builds: render scale is chosen so the internal resolution is about this many megapixels " +
                 "(0 = native). The editor always renders at native resolution.")]
        [SerializeField] private float _targetInternalMegapixels = 1.7f;
        [SerializeField] private float _minRenderScale = 0.6f;
        [SerializeField] private float _maxRenderScale = 1f;

        [Header("Stretch layout (applies on Build Scene)")]
        [Tooltip("Length of the looping stretch, m. Keep texture tiling × loop length a whole number.")]
        [SerializeField] private float _loopLengthM = 200f;
        [SerializeField] private int _segmentCount = 8;
        [Tooltip("A segment jumps ahead once its far end is this far behind the runner, m.")]
        [SerializeField] private float _recycleBehindM = 22f;
        [SerializeField] private int _seed = 20261008;
        [SerializeField] private float _pathHalfWidthM = 3f;
        [SerializeField] private float _groundHalfWidthM = 70f;
        [SerializeField] private float _groundStepZM = 1f;
        [Tooltip("Rise of the left forest slope per meter.")]
        [SerializeField] private float _leftSlope = 0.12f;
        [SerializeField] private float _groundBumpM = 0.6f;

        [Header("River, cliff, waterfall (applies on Build Scene)")]
        [SerializeField] private float _riverCenterXM = 17f;
        [SerializeField] private float _riverMeanderM = 3f;
        [SerializeField] private float _riverHalfWidthM = 4f;
        [SerializeField] private float _riverBedDepthM = 1.6f;
        [SerializeField] private float _waterLevelM = -0.55f;
        [Tooltip("Normal-map scroll of the river toward the runner, uv per second.")]
        [SerializeField] private float _riverFlowSpeed = 0.35f;
        [SerializeField] private float _cliffStartZM = 88f;
        [SerializeField] private float _cliffEndZM = 182f;
        [SerializeField] private float _cliffHeightM = 16f;
        [SerializeField] private float _cliffSetbackM = 6f;
        [Tooltip("Length over which the cliff rises from the ground at each end, m (long ramps read as a slope, not a wall end).")]
        [SerializeField] private float _cliffRampM = 35f;
        [SerializeField] private float _waterfallZM = 135f;
        [SerializeField] private float _waterfallWidthM = 5f;
        [SerializeField] private float _waterfallFlowSpeed = 1.6f;
        [SerializeField] private int _mistParticles = 28;

        [Header("Scatter per loop (applies on Build Scene)")]
        [SerializeField] private int _treeCount = 110;
        [SerializeField] private float _trunkHeightMinM = 16f;
        [SerializeField] private float _trunkHeightMaxM = 30f;
        [SerializeField] private float _trunkRadiusMinM = 0.45f;
        [SerializeField] private float _trunkRadiusMaxM = 1.2f;
        [SerializeField] private int _canopyCardsPerTree = 64;
        [SerializeField] private float _canopyCardSizeM = 4f;
        [SerializeField] private int _boulderCount = 36;
        [SerializeField] private int _heroRockCount = 6;
        [SerializeField] private int _fernCount = 420;
        [SerializeField] private int _plantCount = 150;
        [SerializeField] private float _fernHeightMinM = 0.9f;
        [SerializeField] private float _fernHeightMaxM = 1.9f;
        [SerializeField] private float _plantHeightMinM = 0.8f;
        [SerializeField] private float _plantHeightMaxM = 2.2f;
        [Tooltip("Plants are culled below this fraction of screen height (single-level LODGroup).")]
        [SerializeField] private float _plantCullScreenFraction = 0.035f;
        [SerializeField] private float _rockCullScreenFraction = 0.015f;
        [SerializeField] private float _windStrengthM = 0.12f;
        [Tooltip("Smaller trees (6-12 m) that fill the middle layer between undergrowth and canopy.")]
        [SerializeField] private int _understoryTreeCount = 60;
        [SerializeField] private float _understoryHeightMinM = 6f;
        [SerializeField] private float _understoryHeightMaxM = 12f;

        [Header("CC0 assets (Poly Haven ids, see tools/assets/cc0_assets.json)")]
        [SerializeField] private string _skyHdriId = "kloofendal_48d_partly_cloudy_puresky";
        [SerializeField] private string _forestFloorTextureId = "leafy_grass";
        [SerializeField] private string _pathTextureId = "forest_ground_05";
        [SerializeField] private string _riverBedTextureId = "ganges_river_pebbles";
        [SerializeField] private string _cliffTextureId = "rock_face_03";
        [SerializeField] private string _boulderTextureId = "mossy_rock";
        [SerializeField] private string _barkTextureId = "bark_brown_02";
        [SerializeField] private string _leafCardModelId = "island_tree_02";
        [SerializeField] private string _fernModelId = "fern_02";
        [SerializeField] private string[] _plantModelIds = { "food_ginger_01", "anthurium_botany_01", "calathea_orbifolia_01", "shrub_sorrel_01" };
        [SerializeField] private string[] _rockModelIds = { "rock_07", "rock_09" };
        [Tooltip("Albedo tints (palette grading per layer): forest floor, path, river bed.")]
        [SerializeField] private Color _forestFloorTint = new Color(0.46f, 0.60f, 0.36f, 1f);
        [SerializeField] private Color _pathTint = new Color(0.95f, 0.86f, 0.76f, 1f);
        [SerializeField] private Color _riverBedTint = new Color(0.9f, 0.95f, 0.9f, 1f);
        [Tooltip("Albedo tint for scanned CC0 rocks (pulls pale scans toward the rootstone value, ART_DIRECTION 3.1).")]
        [SerializeField] private Color _scannedRockTint = new Color(0.62f, 0.6f, 0.54f, 1f);
        [Tooltip("Meters per texture repeat on the ground layers (forest floor, path, river bed).")]
        [SerializeField] private Vector3 _groundTileM = new Vector3(4f, 5f, 2.5f);
        [SerializeField] private float _cliffTileM = 8f;
        [SerializeField] private float _barkTileM = 2f;

        [Header("Light and sky (applies on Build Scene)")]
        [SerializeField] private float _sunPitchDeg = 42f;
        [SerializeField] private float _sunYawDeg = -38f;
        [SerializeField] private Color _sunColor = new Color(1f, 0.93f, 0.82f, 1f);
        [SerializeField] private float _sunIntensity = 2.6f;
        [SerializeField] private float _skyExposure = 1f;
        [SerializeField] private float _skyRotationDeg = 0f;
        [SerializeField] private float _ambientIntensity = 1f;
        [Tooltip("Bake the sky into ambient light and reflections (no lightmaps). Off = flat gradient fallback.")]
        [SerializeField] private bool _bakeEnvironmentLighting = true;
        [SerializeField] private Color _fallbackSkyAmbient = new Color(0.55f, 0.66f, 0.72f, 1f);
        [SerializeField] private Color _fallbackEquatorAmbient = new Color(0.36f, 0.42f, 0.34f, 1f);
        [SerializeField] private Color _fallbackGroundAmbient = new Color(0.16f, 0.15f, 0.11f, 1f);
        [SerializeField] private Color _fogColor = new Color(0.56f, 0.70f, 0.70f, 1f);
        [SerializeField] private float _fogDensity = 0.0075f;

        [Header("Render pipeline URP-Realistic (applies on Build Scene)")]
        [SerializeField] private bool _hdr = true;
        [Tooltip("1, 2, 4 or 8.")]
        [SerializeField] private int _msaaSamples = 4;
        [SerializeField] private float _shadowDistanceM = 45f;
        [Tooltip("256, 512, 1024, 2048 or 4096.")]
        [SerializeField] private int _shadowResolution = 2048;
        [Tooltip("1 to 4.")]
        [SerializeField] private int _shadowCascades = 1;
        [SerializeField] private bool _softShadows = true;

        [Header("Post-processing (applies on Build Scene)")]
        [Tooltip("On = ACES filmic tonemapping, off = Neutral.")]
        [SerializeField] private bool _acesTonemapping = true;
        [SerializeField] private float _postExposure = 0.25f;
        [SerializeField] private float _contrast = 12f;
        [SerializeField] private float _saturation = 8f;
        [SerializeField] private float _whiteBalanceTemperature = 4f;
        [SerializeField] private float _bloomThreshold = 1.1f;
        [SerializeField] private float _bloomIntensity = 0.35f;
        [SerializeField] private float _bloomScatter = 0.6f;
        [SerializeField] private float _vignetteIntensity = 0.2f;

        [Header("Water (applies on Build Scene)")]
        [SerializeField] private Color _waterShallowColor = new Color(0.22f, 0.75f, 0.68f, 1f);
        [SerializeField] private Color _waterDeepColor = new Color(0.07f, 0.40f, 0.43f, 1f);
        [SerializeField] private float _waterOpacity = 0.72f;
        [SerializeField] private float _waterNormalTilingPerM = 0.35f;

        [Header("Overlay (applies at Play)")]
        [SerializeField] private float _statsRefreshSeconds = 0.5f;
        [SerializeField] private bool _showQualityPanel = true;

        public float RunSpeedMps => _runSpeedMps;
        public float CameraFovDeg => _cameraFovDeg;

        public float CameraPortraitHorizontalFovDeg => _cameraPortraitHorizontalFovDeg;

        public float CameraPortraitLookAtHeightM => _cameraPortraitLookAtHeightM;
        public float CameraOffsetBehindM => _cameraOffsetBehindM;
        public float CameraOffsetUpM => _cameraOffsetUpM;
        public float CameraLookAheadM => _cameraLookAheadM;
        public float CameraLookAtHeightM => _cameraLookAtHeightM;
        public float CameraFarClipM => _cameraFarClipM;
        public int TargetFrameRate => _targetFrameRate;
        public float TargetInternalMegapixels => _targetInternalMegapixels;
        public float MinRenderScale => _minRenderScale;
        public float MaxRenderScale => _maxRenderScale;

        public float LoopLengthM => _loopLengthM;
        public int SegmentCount => _segmentCount;
        public float RecycleBehindM => _recycleBehindM;
        public int Seed => _seed;
        public float PathHalfWidthM => _pathHalfWidthM;
        public float GroundHalfWidthM => _groundHalfWidthM;
        public float GroundStepZM => _groundStepZM;
        public float LeftSlope => _leftSlope;
        public float GroundBumpM => _groundBumpM;

        public float RiverCenterXM => _riverCenterXM;
        public float RiverMeanderM => _riverMeanderM;
        public float RiverHalfWidthM => _riverHalfWidthM;
        public float RiverBedDepthM => _riverBedDepthM;
        public float WaterLevelM => _waterLevelM;
        public float RiverFlowSpeed => _riverFlowSpeed;
        public float CliffStartZM => _cliffStartZM;
        public float CliffEndZM => _cliffEndZM;
        public float CliffHeightM => _cliffHeightM;
        public float CliffSetbackM => _cliffSetbackM;

        public float CliffRampM => _cliffRampM;
        public float WaterfallZM => _waterfallZM;
        public float WaterfallWidthM => _waterfallWidthM;
        public float WaterfallFlowSpeed => _waterfallFlowSpeed;
        public int MistParticles => _mistParticles;

        public int TreeCount => _treeCount;
        public float TrunkHeightMinM => _trunkHeightMinM;
        public float TrunkHeightMaxM => _trunkHeightMaxM;
        public float TrunkRadiusMinM => _trunkRadiusMinM;
        public float TrunkRadiusMaxM => _trunkRadiusMaxM;
        public int CanopyCardsPerTree => _canopyCardsPerTree;
        public float CanopyCardSizeM => _canopyCardSizeM;
        public int BoulderCount => _boulderCount;
        public int HeroRockCount => _heroRockCount;
        public int FernCount => _fernCount;
        public int PlantCount => _plantCount;
        public float FernHeightMinM => _fernHeightMinM;
        public float FernHeightMaxM => _fernHeightMaxM;
        public float PlantHeightMinM => _plantHeightMinM;
        public float PlantHeightMaxM => _plantHeightMaxM;
        public float PlantCullScreenFraction => _plantCullScreenFraction;
        public float RockCullScreenFraction => _rockCullScreenFraction;
        public float WindStrengthM => _windStrengthM;

        public int UnderstoryTreeCount => _understoryTreeCount;

        public float UnderstoryHeightMinM => _understoryHeightMinM;

        public float UnderstoryHeightMaxM => _understoryHeightMaxM;

        public Color ForestFloorTint => _forestFloorTint;

        public Color PathTint => _pathTint;

        public Color RiverBedTint => _riverBedTint;

        public Color ScannedRockTint => _scannedRockTint;

        public string SkyHdriId => _skyHdriId;
        public string ForestFloorTextureId => _forestFloorTextureId;
        public string PathTextureId => _pathTextureId;
        public string RiverBedTextureId => _riverBedTextureId;
        public string CliffTextureId => _cliffTextureId;
        public string BoulderTextureId => _boulderTextureId;
        public string BarkTextureId => _barkTextureId;
        public string LeafCardModelId => _leafCardModelId;
        public string FernModelId => _fernModelId;
        public string[] PlantModelIds => _plantModelIds;
        public string[] RockModelIds => _rockModelIds;
        public Vector3 GroundTileM => _groundTileM;
        public float CliffTileM => _cliffTileM;
        public float BarkTileM => _barkTileM;

        public float SunPitchDeg => _sunPitchDeg;
        public float SunYawDeg => _sunYawDeg;
        public Color SunColor => _sunColor;
        public float SunIntensity => _sunIntensity;
        public float SkyExposure => _skyExposure;
        public float SkyRotationDeg => _skyRotationDeg;
        public float AmbientIntensity => _ambientIntensity;
        public bool BakeEnvironmentLighting => _bakeEnvironmentLighting;
        public Color FallbackSkyAmbient => _fallbackSkyAmbient;
        public Color FallbackEquatorAmbient => _fallbackEquatorAmbient;
        public Color FallbackGroundAmbient => _fallbackGroundAmbient;
        public Color FogColor => _fogColor;
        public float FogDensity => _fogDensity;

        public bool Hdr => _hdr;
        public int MsaaSamples => _msaaSamples;
        public float ShadowDistanceM => _shadowDistanceM;
        public int ShadowResolution => _shadowResolution;
        public int ShadowCascades => _shadowCascades;
        public bool SoftShadows => _softShadows;

        public bool AcesTonemapping => _acesTonemapping;
        public float PostExposure => _postExposure;
        public float Contrast => _contrast;
        public float Saturation => _saturation;
        public float WhiteBalanceTemperature => _whiteBalanceTemperature;
        public float BloomThreshold => _bloomThreshold;
        public float BloomIntensity => _bloomIntensity;
        public float BloomScatter => _bloomScatter;
        public float VignetteIntensity => _vignetteIntensity;

        public Color WaterShallowColor => _waterShallowColor;
        public Color WaterDeepColor => _waterDeepColor;
        public float WaterOpacity => _waterOpacity;
        public float WaterNormalTilingPerM => _waterNormalTilingPerM;

        public float StatsRefreshSeconds => _statsRefreshSeconds;
        public bool ShowQualityPanel => _showQualityPanel;

        private void OnValidate()
        {
            _runSpeedMps = Mathf.Clamp(_runSpeedMps, 1f, 40f);
            _cameraFovDeg = Mathf.Clamp(_cameraFovDeg, 30f, 100f);
            _cameraPortraitHorizontalFovDeg = Mathf.Clamp(_cameraPortraitHorizontalFovDeg, 40f, 100f);
            _cameraFarClipM = Mathf.Max(_cameraFarClipM, 20f);
            _targetFrameRate = Mathf.Clamp(_targetFrameRate, 30, 120);
            _minRenderScale = Mathf.Clamp(_minRenderScale, 0.25f, 1f);
            _maxRenderScale = Mathf.Clamp(_maxRenderScale, _minRenderScale, 2f);
            _targetInternalMegapixels = Mathf.Max(0f, _targetInternalMegapixels);
            _segmentCount = Mathf.Clamp(_segmentCount, 2, 64);
            _loopLengthM = Mathf.Max(_loopLengthM, 40f);
            _recycleBehindM = Mathf.Clamp(_recycleBehindM, 5f, _loopLengthM * 0.5f);
            _groundStepZM = Mathf.Clamp(_groundStepZM, 0.25f, 5f);
            _msaaSamples = _msaaSamples >= 8 ? 8 : _msaaSamples >= 4 ? 4 : _msaaSamples >= 2 ? 2 : 1;
            _shadowCascades = Mathf.Clamp(_shadowCascades, 1, 4);
            _shadowResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_shadowResolution, 256, 4096));
            _statsRefreshSeconds = Mathf.Clamp(_statsRefreshSeconds, 0.1f, 5f);
        }
    }
}
