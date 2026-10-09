using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Every tuning number of the AURELIA look test (ADR 0004, look test v2 per design/aurelia/ENVIRONMENT_STRATEGY.md):
    /// the run, the follow camera (spec 101 profiles), the curved 225 m layout (periodic keys along the path distance
    /// s), scatter densities, landmarks, light, atmosphere (height fog with sun in-scatter, canopy light), grading,
    /// render settings, water, per-layer budgets and the shot list.
    /// Saved as <c>Assets/_Game/Config/LookTest/LookTestConfig.asset</c> (created by JungleBooze > Look Test > Build
    /// Scene if missing). Layout values apply on Build Scene; run, camera and atmosphere values apply at Play.
    /// Presentation only: there is no gameplay simulation in the look test.
    /// </summary>
    [CreateAssetMenu(fileName = "LookTestConfig", menuName = "JungleBooze/Config/Look Test Config")]
    public sealed class LookTestConfigAsset : ScriptableObject
    {
        [Header("Run (applies at Play)")]
        [Tooltip("Constant forward speed along the path, m/s.")]
        [SerializeField] private float _runSpeedMps = 9f;
        [Tooltip("Landscape camera. Spec 101 asks back 5.5, up 2.4, pitch 9, FOV 55; at 5.5 m Pista fills 27% of the " +
                 "screen height, against 14-18% in ART_DIRECTION 9 and the shot list. Back 9.0 / up 3.6 keeps the " +
                 "spec's pitch and FOV and gives 16% (feet at 28%). Open question for game-designer.")]
        [SerializeField] private LookTestCameraProfile _landscapeCamera = new LookTestCameraProfile(9.0f, 3.6f, 9f, 55f, 0.7f, 3.2f);
        [Tooltip("Portrait camera. Spec 101: back 6.2, up 3.0, pitch 12, FOV 65 (Pista 19%); 9.2 / 5.6 gives the 12% " +
                 "(feet at 22%) of ART_DIRECTION 9.")]
        [SerializeField] private LookTestCameraProfile _portraitCamera = new LookTestCameraProfile(9.2f, 5.6f, 12f, 65f, 0.8f, 3.2f);
        [SerializeField] private float _cameraFarClipM = 2600f;
        [SerializeField] private int _targetFrameRate = 60;
        [Tooltip("Device builds: render scale is chosen so the internal resolution is about this many megapixels " +
                 "(0 = native). The editor always renders at native resolution.")]
        [SerializeField] private float _targetInternalMegapixels = 1.7f;
        [SerializeField] private float _minRenderScale = 0.6f;
        [SerializeField] private float _maxRenderScale = 1f;

        [Header("Layout: path (applies on Build Scene)")]
        [Tooltip("Length of the looping stretch, m.")]
        [SerializeField] private float _loopLengthM = 225f;
        [Tooltip("Segments of the loop (25 m each by default): the unit of streaming and mesh merging.")]
        [SerializeField] private int _segmentCount = 9;
        [Tooltip("A segment jumps one loop ahead once its far end is this far behind the runner, m.")]
        [SerializeField] private float _recycleBehindM = 20f;
        [SerializeField] private int _seed = 20261009;
        [Tooltip("Runnable half width (spec 101 default 3.5 m).")]
        [SerializeField] private float _runnableHalfWidthM = 3.5f;
        [Tooltip("Visible worn trail half width; roots and moss cover the rest of the runnable width (strategy 4.1).")]
        [SerializeField] private float _trailHalfWidthM = 2.3f;
        [Tooltip("Half width of the generated ground strip on each side of the path, m.")]
        [SerializeField] private float _stripHalfWidthM = 52f;
        [SerializeField] private float _groundStepM = 1f;
        [SerializeField] private float _groundBumpM = 0.5f;
        [Tooltip("(s, heading in degrees, + = right). Bends of 8-15° every 30-50 m (strategy 4.1); at the basin the trail aims " +
                 "at the falls (+28°) and turns left over 35 m (radius > strip half width, so the ground strip never folds), " +
                 "which moves the next lap's forest out of the vista.")]
        [SerializeField] private Vector2[] _headingKeys =
        {
            new Vector2(0f, -10f), new Vector2(22f, -4f), new Vector2(48f, -12f), new Vector2(80f, -2f), new Vector2(104f, 7f),
            new Vector2(124f, 3f), new Vector2(148f, -8f), new Vector2(168f, 6f), new Vector2(187f, 28f), new Vector2(205f, 10f),
            new Vector2(222f, -10f),
        };
        [Tooltip("(s, trail height in m). Climbs and drops of 1-4 m; the ford at 124 m is the low point, the basin ledge at 190 m the high point.")]
        [SerializeField] private Vector2[] _heightKeys =
        {
            new Vector2(0f, 2.2f), new Vector2(22f, 1.0f), new Vector2(52f, 3.4f), new Vector2(82f, 3.5f), new Vector2(102f, 2.2f),
            new Vector2(124f, 0f), new Vector2(146f, 1.5f), new Vector2(168f, 4.6f), new Vector2(190f, 5.4f), new Vector2(204f, 5.0f),
        };

        [Header("Layout: enclose and reveal (applies on Build Scene)")]
        [Tooltip("(s, 0..1) canopy enclosure. About 70% of the loop enclosed, 30% open (strategy 4.1).")]
        [SerializeField] private Vector2[] _enclosureKeys =
        {
            new Vector2(0f, 1f), new Vector2(28f, 1f), new Vector2(40f, 0.25f), new Vector2(52f, 0.9f), new Vector2(66f, 0.9f),
            new Vector2(80f, 0.6f), new Vector2(96f, 0.9f), new Vector2(108f, 0.7f), new Vector2(118f, 0.1f), new Vector2(136f, 0.1f),
            new Vector2(148f, 0.85f), new Vector2(160f, 0.85f), new Vector2(170f, 0.2f), new Vector2(178f, 0f), new Vector2(207f, 0f),
            new Vector2(219f, 0.65f),
        };
        [Tooltip("(s, m) height of the left bank above the trail, reached about 12 m out.")]
        [SerializeField] private Vector2[] _bankLeftKeys =
        {
            new Vector2(0f, 4.5f), new Vector2(30f, 4f), new Vector2(55f, 3.5f), new Vector2(72f, 1f), new Vector2(96f, 1.2f),
            new Vector2(108f, 3f), new Vector2(118f, 1.2f), new Vector2(135f, 1f), new Vector2(150f, 3.5f), new Vector2(170f, 2.5f),
            new Vector2(190f, 2f), new Vector2(210f, 4.5f),
        };
        [Tooltip("(s, m) height of the right bank above the trail.")]
        [SerializeField] private Vector2[] _bankRightKeys =
        {
            new Vector2(0f, 3.5f), new Vector2(30f, 3f), new Vector2(55f, 3.5f), new Vector2(75f, 3f), new Vector2(100f, 3f),
            new Vector2(114f, 0.5f), new Vector2(140f, 0.6f), new Vector2(152f, 3f), new Vector2(165f, 0.5f), new Vector2(205f, 0.5f),
            new Vector2(215f, 3f),
        };
        [Tooltip("(s, d) where the plateau ends on the right and drops to the valley or the basin shelf.")]
        [SerializeField] private Vector2[] _rightEdgeKeys =
        {
            new Vector2(0f, 16f), new Vector2(30f, 18f), new Vector2(55f, 20f), new Vector2(80f, 24f), new Vector2(100f, 44f),
            new Vector2(116f, 24f), new Vector2(136f, 28f), new Vector2(150f, 44f), new Vector2(163f, 14f), new Vector2(176f, 6.5f),
            new Vector2(198f, 6.5f), new Vector2(212f, 10f),
        };
        [Tooltip("(s, world y) of the ground below the right edge: the basin shelf near the ledge, the valley elsewhere.")]
        [SerializeField] private Vector2[] _belowRightKeys =
        {
            new Vector2(0f, -30f), new Vector2(20f, -40f), new Vector2(150f, -40f), new Vector2(170f, -24f), new Vector2(190f, -20f),
            new Vector2(208f, -24f),
        };
        [SerializeField] private float _valleyFloorY = -40f;
        [Tooltip("Canopy roof height above the trail, m (min, max).")]
        [SerializeField] private Vector2 _canopyRoofHeightM = new Vector2(13f, 24f);
        [Tooltip("Sunlit openings in the canopy: (s, d, radius m, strength 0..1). The risky route at the fork is sunlit (ART_DIRECTION 6).")]
        [SerializeField] private Vector4[] _sunPatches =
        {
            new Vector4(82f, -7f, 13f, 0.9f), new Vector4(41f, 0f, 9f, 0.7f), new Vector4(60f, 6f, 6f, 0.5f),
        };

        [Header("River and ford (applies on Build Scene)")]
        [SerializeField] private float _riverStartSM = 86f;
        [SerializeField] private float _riverEndSM = 164f;
        [Tooltip("The river crosses the trail here (the ford, shot F3).")]
        [SerializeField] private float _riverCrossSM = 124f;
        [Tooltip("River half width measured along the lateral axis d, m.")]
        [SerializeField] private float _riverHalfWidthM = 6f;
        [SerializeField] private float _riverDepthM = 1.2f;
        [Tooltip("Water depth over the trail at the ford, m (ankle deep).")]
        [SerializeField] private float _fordDepthM = 0.15f;
        [Tooltip("Water surface drop per m of lateral distance (the river runs down to the right, toward the valley).")]
        [SerializeField] private float _riverSlope = 0.03f;
        [SerializeField] private float _riverFlowSpeed = 0.45f;
        [SerializeField] private float _waterfallFlowSpeed = 1.6f;

        [Header("Scatter per 25 m segment (applies on Build Scene)")]
        [SerializeField] private int _wallClumpsPerSegment = 70;
        [SerializeField] private int _frameClumpsPerSegment = 4;
        [SerializeField] private int _canopyTreesPerSegment = 9;
        [SerializeField] private int _understoryTreesPerSegment = 8;
        [SerializeField] private int _roofClumpsPerSegment = 26;
        [SerializeField] private int _vinesPerSegment = 14;
        [SerializeField] private int _edgeRootsPerSegment = 8;
        [SerializeField] private int _boulderPerSegment = 6;
        [SerializeField] private int _fernsPerSegment = 8;
        [SerializeField] private int _plantsPerSegment = 3;
        [SerializeField] private float _windStrengthM = 0.12f;
        [Tooltip("Near detail (CC0 verge ferns and plants) is drawn only in segments starting less than this far ahead, m.")]
        [SerializeField] private float _detailRangeM = 50f;

        [Header("Landmarks (applies on Build Scene)")]
        [Tooltip("Giant stiltwoods: (s, d, scale). The first one arches its stilt roots over the trail (shot F1).")]
        [SerializeField] private Vector3[] _stiltwoods =
        {
            new Vector3(20f, -10f, 1f), new Vector3(63f, 12f, 0.8f), new Vector3(116f, -15f, 1.1f), new Vector3(158f, -11f, 0.8f),
            new Vector3(100f, 16f, 0.7f),
        };
        [Tooltip("Light shafts: (s, d, length m). 2-4 per enclosed view, never across the near 10 m of the trail.")]
        [SerializeField] private Vector3[] _lightShafts =
        {
            new Vector3(38f, -3f, 30f), new Vector3(44f, 5f, 26f), new Vector3(58f, -8f, 24f), new Vector3(76f, -7f, 26f),
            new Vector3(84f, -2f, 22f), new Vector3(100f, 4f, 24f), new Vector3(110f, -6f, 26f), new Vector3(152f, 2f, 26f),
            new Vector3(160f, -6f, 22f), new Vector3(222f, 3f, 24f), new Vector3(10f, 6f, 22f), new Vector3(30f, 2f, 28f),
        };

        [Header("CC0 assets (Poly Haven ids, see tools/assets/cc0_assets.json)")]
        [SerializeField] private string _skyHdriId = "kloofendal_48d_partly_cloudy_puresky";
        [SerializeField] private string _forestFloorTextureId = "leafy_grass";
        [SerializeField] private string _pathTextureId = "forest_ground_05";
        [SerializeField] private string _riverBedTextureId = "ganges_river_pebbles";
        [SerializeField] private string _rockTextureId = "rock_face_03";
        [SerializeField] private string _boulderTextureId = "mossy_rock";
        [SerializeField] private string _barkTextureId = "bark_brown_02";
        [SerializeField] private string _leafCardModelId = "island_tree_02";
        [SerializeField] private string _fernModelId = "fern_02";
        [SerializeField] private string[] _plantModelIds = { "calathea_orbifolia_01", "food_ginger_01" };
        [Tooltip("Albedo tints: forest floor, path, river bed.")]
        [SerializeField] private Color _forestFloorTint = new Color(0.40f, 0.55f, 0.33f, 1f);
        [SerializeField] private Color _pathTint = new Color(0.92f, 0.82f, 0.72f, 1f);
        [SerializeField] private Color _riverBedTint = new Color(0.85f, 0.92f, 0.88f, 1f);
        [Tooltip("Rootstone rock tint (graybox landforms).")]
        [SerializeField] private Color _rootstoneTint = new Color(0.62f, 0.60f, 0.55f, 1f);
        [Tooltip("Leaf tint (canopy and walls).")]
        [SerializeField] private Color _leafTint = new Color(0.78f, 0.92f, 0.66f, 1f);
        [Tooltip("Meters per texture repeat on the ground layers (forest floor, path, river bed). Each must divide the loop length, or the texture jumps at the loop seam.")]
        [SerializeField] private Vector3 _groundTileM = new Vector3(4.5f, 5f, 2.5f);
        [SerializeField] private float _rockTileM = 9f;
        [SerializeField] private float _barkTileM = 2f;

        [Header("Light and sky (applies on Build Scene)")]
        [Tooltip("Sun elevation, degrees (ART_DIRECTION 4.1 / strategy 4.4: low, 25-35°).")]
        [SerializeField] private float _sunElevationDeg = 27f;
        [Tooltip("Sun azimuth from the run direction (+z), degrees, negative = left. Ahead-left backlights the foliage.")]
        [SerializeField] private float _sunAzimuthDeg = -32f;
        [SerializeField] private Color _sunColor = new Color(1f, 0.86f, 0.66f, 1f);
        [SerializeField] private float _sunIntensity = 3.8f;
        [SerializeField] private float _skyExposure = 0.85f;
        [Tooltip("HDRI rotation, degrees (puts the HDRI's own sun near the light's azimuth).")]
        [SerializeField] private float _skyRotationDeg = 0f;
        [SerializeField] private Color _skyTint = new Color(0.92f, 0.98f, 1f, 1f);
        [SerializeField] private float _ambientIntensity = 1f;
        [Tooltip("Bake the sky into ambient light and reflections (no lightmaps). Off = flat gradient fallback.")]
        [SerializeField] private bool _bakeEnvironmentLighting = true;
        [SerializeField] private Color _fallbackSkyAmbient = new Color(0.55f, 0.66f, 0.72f, 1f);
        [SerializeField] private Color _fallbackEquatorAmbient = new Color(0.36f, 0.42f, 0.34f, 1f);
        [SerializeField] private Color _fallbackGroundAmbient = new Color(0.16f, 0.15f, 0.11f, 1f);

        [Header("Atmosphere (applies at Play and in the editor)")]
        [Tooltip("Haze color away from the sun (cool), ART_DIRECTION haze #CFE4EA.")]
        [SerializeField] private Color _fogColor = new Color(0.62f, 0.76f, 0.80f, 1f);
        [Tooltip("Haze color toward the sun (warm in-scatter).")]
        [SerializeField] private Color _fogSunColor = new Color(1.0f, 0.86f, 0.62f, 1f);
        [Tooltip("Fog density at the base height, per m.")]
        [SerializeField] private float _fogDensity = 0.0034f;
        [Tooltip("Density falls by e every 1/falloff m of height (denser in hollows and over the valley).")]
        [SerializeField] private float _fogHeightFalloff = 0.045f;
        [SerializeField] private float _fogBaseHeightM = 0f;
        [Tooltip("No fog closer than this, m.")]
        [SerializeField] private float _fogStartM = 6f;
        [SerializeField] private float _fogMaxOpacity = 0.96f;
        [Tooltip("Sharpness of the warm glow around the sun direction.")]
        [SerializeField] private float _fogSunPower = 6f;
        [Tooltip("Distance used for the sky's horizon haze, m.")]
        [SerializeField] private float _skyFogDistanceM = 2400f;
        [Tooltip("World size of the canopy light pattern (dappled sun under the roof), m per repeat.")]
        [SerializeField] private float _dappleSizeM = 9f;
        [Tooltip("Share of the ground lit by the sun under full canopy (0..1).")]
        [SerializeField] private float _dappleLitFraction = 0.28f;
        [Tooltip("Ambient light under full canopy (1 = open sky).")]
        [SerializeField] private float _canopyAmbient = 0.5f;
        [Tooltip("Tint of light under the canopy (filtered through leaves).")]
        [SerializeField] private Color _canopyTint = new Color(0.88f, 0.96f, 0.80f, 1f);
        [SerializeField] private Color _shaftColor = new Color(1f, 0.82f, 0.5f, 1f);
        [SerializeField] private float _shaftIntensity = 0.55f;
        [SerializeField] private Color _mistColor = new Color(0.86f, 0.92f, 0.94f, 1f);
        [SerializeField] private float _mistOpacity = 0.5f;

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

        [Header("Post-processing and grade (applies on Build Scene)")]
        [Tooltip("On = ACES filmic tonemapping, off = Neutral.")]
        [SerializeField] private bool _acesTonemapping = true;
        [SerializeField] private float _postExposure = 0.45f;
        [SerializeField] private float _contrast = 18f;
        [SerializeField] private float _saturation = 14f;
        [SerializeField] private float _whiteBalanceTemperature = 6f;
        [SerializeField] private float _whiteBalanceTint = -4f;
        [Tooltip("Split toning: shadows (teal-green), highlights (gold). Baked into the grading LUT.")]
        [SerializeField] private Color _splitShadows = new Color(0.42f, 0.52f, 0.55f, 1f);
        [SerializeField] private Color _splitHighlights = new Color(0.92f, 0.72f, 0.42f, 1f);
        [SerializeField] private float _splitBalance = 10f;
        [Tooltip("Shadows / midtones / highlights (rgb = color, a = offset) for the grading LUT.")]
        [SerializeField] private Vector4 _gradeShadows = new Vector4(0.96f, 1.02f, 1.04f, -0.06f);
        [SerializeField] private Vector4 _gradeMidtones = new Vector4(1f, 1.01f, 0.98f, 0f);
        [SerializeField] private Vector4 _gradeHighlights = new Vector4(1.04f, 1.0f, 0.94f, 0.02f);
        [SerializeField] private float _bloomThreshold = 1.0f;
        [SerializeField] private float _bloomIntensity = 0.45f;
        [SerializeField] private float _bloomScatter = 0.7f;
        [SerializeField] private Color _bloomTint = new Color(1f, 0.93f, 0.8f, 1f);
        [SerializeField] private float _vignetteIntensity = 0.26f;

        [Header("Water (applies on Build Scene)")]
        [SerializeField] private Color _waterShallowColor = new Color(0.20f, 0.78f, 0.72f, 1f);
        [SerializeField] private Color _waterDeepColor = new Color(0.05f, 0.42f, 0.46f, 1f);
        [SerializeField] private float _waterOpacity = 0.78f;
        [SerializeField] private float _waterNormalTilingPerM = 0.35f;

        [Header("Budget per view (ENVIRONMENT_STRATEGY 4.2, ADR 0004 Decision 7)")]
        [SerializeField] private int _mainDrawBudget = 250;
        [SerializeField] private int _mainTriangleBudget = 350000;
        [SerializeField] private int _shadowDrawBudget = 100;
        [SerializeField] private int _shadowTriangleBudget = 150000;
        [SerializeField] private LookTestBudgetLine[] _budgetLines =
        {
            new LookTestBudgetLine("L0", "Path and edge dressing", 40, 50000),
            new LookTestBudgetLine("L1", "Foliage walls", 30, 90000),
            new LookTestBudgetLine("L2", "Trunks, stiltwoods, rootstone", 50, 90000),
            new LookTestBudgetLine("L3", "Canopy roof", 10, 25000),
            new LookTestBudgetLine("L4", "Forest wall (impostors)", 10, 2000),
            new LookTestBudgetLine("L5", "Backdrop", 6, 1000),
            new LookTestBudgetLine("L6", "Sky", 1, 0),
            new LookTestBudgetLine("W", "Water, mist, light shafts", 24, 12000),
            new LookTestBudgetLine("FX", "Pista, pickups, effects", 40, 35000),
        };

        [Header("Shots (LookTestBatch.CaptureShots)")]
        [SerializeField] private LookTestShot[] _shots =
        {
            new LookTestShot("F1_out_of_the_roots", 25f, false),
            new LookTestShot("F2_the_fork", 80f, false),
            new LookTestShot("F3_the_ford", 124f, false),
            new LookTestShot("F4_the_basin", 190f, false),
            new LookTestShot("P1_the_basin_portrait", 190f, true),
            new LookTestShot("X_climb_055m", 55f, false),
            new LookTestShot("X_return_215m", 215f, false),
            new LookTestShot("XP_roots_portrait", 25f, true),
        };

        [Header("Overlay (applies at Play)")]
        [SerializeField] private float _statsRefreshSeconds = 0.5f;
        [SerializeField] private bool _showQualityPanel = true;

        public float RunSpeedMps => _runSpeedMps;
        public LookTestCameraProfile LandscapeCamera => _landscapeCamera;
        public LookTestCameraProfile PortraitCamera => _portraitCamera;
        public float CameraFarClipM => _cameraFarClipM;
        public int TargetFrameRate => _targetFrameRate;
        public float TargetInternalMegapixels => _targetInternalMegapixels;
        public float MinRenderScale => _minRenderScale;
        public float MaxRenderScale => _maxRenderScale;

        public float LoopLengthM => _loopLengthM;
        public int SegmentCount => _segmentCount;
        public float SegmentLengthM => _loopLengthM / _segmentCount;
        public float RecycleBehindM => _recycleBehindM;
        public int Seed => _seed;
        public float RunnableHalfWidthM => _runnableHalfWidthM;
        public float TrailHalfWidthM => _trailHalfWidthM;
        public float StripHalfWidthM => _stripHalfWidthM;
        public float GroundStepM => _groundStepM;
        public float GroundBumpM => _groundBumpM;
        public Vector2[] HeadingKeys => _headingKeys;
        public Vector2[] HeightKeys => _heightKeys;
        public Vector2[] EnclosureKeys => _enclosureKeys;
        public Vector2[] BankLeftKeys => _bankLeftKeys;
        public Vector2[] BankRightKeys => _bankRightKeys;
        public Vector2[] RightEdgeKeys => _rightEdgeKeys;
        public Vector2[] BelowRightKeys => _belowRightKeys;
        public float ValleyFloorY => _valleyFloorY;
        public Vector2 CanopyRoofHeightM => _canopyRoofHeightM;
        public Vector4[] SunPatches => _sunPatches;

        public float RiverStartSM => _riverStartSM;
        public float RiverEndSM => _riverEndSM;
        public float RiverCrossSM => _riverCrossSM;
        public float RiverHalfWidthM => _riverHalfWidthM;
        public float RiverDepthM => _riverDepthM;
        public float FordDepthM => _fordDepthM;
        public float RiverSlope => _riverSlope;
        public float RiverFlowSpeed => _riverFlowSpeed;
        public float WaterfallFlowSpeed => _waterfallFlowSpeed;

        public int WallClumpsPerSegment => _wallClumpsPerSegment;
        public int FrameClumpsPerSegment => _frameClumpsPerSegment;
        public int CanopyTreesPerSegment => _canopyTreesPerSegment;
        public int UnderstoryTreesPerSegment => _understoryTreesPerSegment;
        public int RoofClumpsPerSegment => _roofClumpsPerSegment;
        public int VinesPerSegment => _vinesPerSegment;
        public int EdgeRootsPerSegment => _edgeRootsPerSegment;
        public int BouldersPerSegment => _boulderPerSegment;
        public int FernsPerSegment => _fernsPerSegment;
        public int PlantsPerSegment => _plantsPerSegment;
        public float WindStrengthM => _windStrengthM;
        public float DetailRangeM => _detailRangeM;

        public Vector3[] Stiltwoods => _stiltwoods;
        public Vector3[] LightShafts => _lightShafts;

        public string SkyHdriId => _skyHdriId;
        public string ForestFloorTextureId => _forestFloorTextureId;
        public string PathTextureId => _pathTextureId;
        public string RiverBedTextureId => _riverBedTextureId;
        public string RockTextureId => _rockTextureId;
        public string BoulderTextureId => _boulderTextureId;
        public string BarkTextureId => _barkTextureId;
        public string LeafCardModelId => _leafCardModelId;
        public string FernModelId => _fernModelId;
        public string[] PlantModelIds => _plantModelIds;
        public Color ForestFloorTint => _forestFloorTint;
        public Color PathTint => _pathTint;
        public Color RiverBedTint => _riverBedTint;
        public Color RootstoneTint => _rootstoneTint;
        public Color LeafTint => _leafTint;
        public Vector3 GroundTileM => _groundTileM;
        public float RockTileM => _rockTileM;
        public float BarkTileM => _barkTileM;

        public float SunElevationDeg => _sunElevationDeg;
        public float SunAzimuthDeg => _sunAzimuthDeg;
        public Color SunColor => _sunColor;
        public float SunIntensity => _sunIntensity;
        public float SkyExposure => _skyExposure;
        public float SkyRotationDeg => _skyRotationDeg;
        public Color SkyTint => _skyTint;
        public float AmbientIntensity => _ambientIntensity;
        public bool BakeEnvironmentLighting => _bakeEnvironmentLighting;
        public Color FallbackSkyAmbient => _fallbackSkyAmbient;
        public Color FallbackEquatorAmbient => _fallbackEquatorAmbient;
        public Color FallbackGroundAmbient => _fallbackGroundAmbient;

        public Color FogColor => _fogColor;
        public Color FogSunColor => _fogSunColor;
        public float FogDensity => _fogDensity;
        public float FogHeightFalloff => _fogHeightFalloff;
        public float FogBaseHeightM => _fogBaseHeightM;
        public float FogStartM => _fogStartM;
        public float FogMaxOpacity => _fogMaxOpacity;
        public float FogSunPower => _fogSunPower;
        public float SkyFogDistanceM => _skyFogDistanceM;
        public float DappleSizeM => _dappleSizeM;
        public float DappleLitFraction => _dappleLitFraction;
        public float CanopyAmbient => _canopyAmbient;
        public Color CanopyTint => _canopyTint;
        public Color ShaftColor => _shaftColor;
        public float ShaftIntensity => _shaftIntensity;
        public Color MistColor => _mistColor;
        public float MistOpacity => _mistOpacity;

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
        public float WhiteBalanceTint => _whiteBalanceTint;
        public Color SplitShadows => _splitShadows;
        public Color SplitHighlights => _splitHighlights;
        public float SplitBalance => _splitBalance;
        public Vector4 GradeShadows => _gradeShadows;
        public Vector4 GradeMidtones => _gradeMidtones;
        public Vector4 GradeHighlights => _gradeHighlights;
        public float BloomThreshold => _bloomThreshold;
        public float BloomIntensity => _bloomIntensity;
        public float BloomScatter => _bloomScatter;
        public Color BloomTint => _bloomTint;
        public float VignetteIntensity => _vignetteIntensity;

        public Color WaterShallowColor => _waterShallowColor;
        public Color WaterDeepColor => _waterDeepColor;
        public float WaterOpacity => _waterOpacity;
        public float WaterNormalTilingPerM => _waterNormalTilingPerM;

        public int MainDrawBudget => _mainDrawBudget;
        public int MainTriangleBudget => _mainTriangleBudget;
        public int ShadowDrawBudget => _shadowDrawBudget;
        public int ShadowTriangleBudget => _shadowTriangleBudget;
        public LookTestBudgetLine[] BudgetLines => _budgetLines;
        public LookTestShot[] Shots => _shots;

        public float StatsRefreshSeconds => _statsRefreshSeconds;
        public bool ShowQualityPanel => _showQualityPanel;

        /// <summary>Direction the sunlight travels (from the sun into the scene), world space.</summary>
        public Vector3 SunLightDirection
        {
            get
            {
                float el = _sunElevationDeg * Mathf.Deg2Rad;
                float az = _sunAzimuthDeg * Mathf.Deg2Rad;
                var toSun = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
                return -toSun;
            }
        }

        /// <summary>Builds the path from the heading and height keys.</summary>
        public LookTestPath CreatePath()
        {
            return new LookTestPath(_loopLengthM, _headingKeys, _heightKeys);
        }

        private void OnValidate()
        {
            _runSpeedMps = Mathf.Clamp(_runSpeedMps, 1f, 40f);
            _cameraFarClipM = Mathf.Max(_cameraFarClipM, 20f);
            _targetFrameRate = Mathf.Clamp(_targetFrameRate, 30, 120);
            _minRenderScale = Mathf.Clamp(_minRenderScale, 0.25f, 1f);
            _maxRenderScale = Mathf.Clamp(_maxRenderScale, _minRenderScale, 2f);
            _targetInternalMegapixels = Mathf.Max(0f, _targetInternalMegapixels);
            _segmentCount = Mathf.Clamp(_segmentCount, 2, 64);
            _loopLengthM = Mathf.Max(_loopLengthM, 40f);
            _recycleBehindM = Mathf.Clamp(_recycleBehindM, 5f, _loopLengthM * 0.5f);
            _groundStepM = Mathf.Clamp(_groundStepM, 0.25f, 5f);
            _msaaSamples = _msaaSamples >= 8 ? 8 : _msaaSamples >= 4 ? 4 : _msaaSamples >= 2 ? 2 : 1;
            _shadowCascades = Mathf.Clamp(_shadowCascades, 1, 4);
            _shadowResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_shadowResolution, 256, 4096));
            _statsRefreshSeconds = Mathf.Clamp(_statsRefreshSeconds, 0.1f, 5f);
            _fogDensity = Mathf.Max(0f, _fogDensity);
            _fogMaxOpacity = Mathf.Clamp01(_fogMaxOpacity);
            _dappleLitFraction = Mathf.Clamp01(_dappleLitFraction);
        }
    }
}
