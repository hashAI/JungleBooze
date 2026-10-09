using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Every number of the hero basin scene (keyframe F4_e, ADR 0008): cameras, Pista, sun and atmosphere, grade,
    /// water, the arch, falls, terraces, terrain, backdrop layers, plants. World frame: Pista stands at the origin on
    /// the ledge looking along +z; y = 0 is the ledge top. Saved as Assets/_Game/Config/HeroBasin/HeroBasinConfig.asset
    /// (created by the builder if missing; delete it to take new defaults). Presentation only.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroBasinConfig", menuName = "JungleBooze/Config/Hero Basin Config")]
    public sealed class HeroBasinConfigAsset : ScriptableObject
    {
        [Tooltip("Rendering style (ADR 0009): Realistic = iteration 3 (F4_e); Painterly = the painterly trial (F4_f), "
            + "built into its own scene, materials and meshes. The painterly values are in the Painterly block below.")]
        [SerializeField] private HeroBasinStyle _style = HeroBasinStyle.Realistic;
        [SerializeField] private HeroPainterlyLook _painterly = new HeroPainterlyLook();
        [Tooltip("Painterly set-dressing pass (HERO_BASIN_DRESSING.md): counts and screen regions. Painterly style only.")]
        [SerializeField] private HeroDressing _dressing = new HeroDressing();
        [Tooltip("Painterly v4 cheap tricks (ADR 0011): arch matte card, pillar impostors, painted foam and cascades.")]
        [SerializeField] private HeroCards _cards = new HeroCards();

        [Header("Cameras (position, euler: pitch + = down, yaw + = right; vertical FOV)")]
        [SerializeField] private Vector3 _landscapePosition = new Vector3(1.3f, 1.9f, -4.7f);
        [SerializeField] private Vector3 _landscapeEuler = new Vector3(-2f, -1f, 0f);
        [SerializeField] private float _landscapeFovDeg = 50f;
        [SerializeField] private Vector3 _portraitPosition = new Vector3(0.9f, 2.2f, -6.8f);
        [SerializeField] private Vector3 _portraitEuler = new Vector3(-9f, 1f, 0f);
        [SerializeField] private float _portraitFovDeg = 70f;
        [SerializeField] private float _farClipM = 4200f;

        [Header("Pista")]
        [SerializeField] private Vector3 _pistaPosition = Vector3.zero;
        [SerializeField] private float _pistaYawDeg = 8f;
        [Tooltip("Idle clip time (s) sampled for the still pose.")]
        [SerializeField] private float _pistaIdleTimeS = 1.2f;

        [Header("Sun (keyframe: low, upper left, backlighting)")]
        [SerializeField] private float _sunElevationDeg = 20f;
        [SerializeField] private float _sunAzimuthDeg = -31f;
        [SerializeField] private Color _sunColor = new Color(1f, 0.80f, 0.56f, 1f);
        [SerializeField] private float _sunIntensity = 3.6f;
        [Tooltip("Key light direction (the directional light and shadows). The keyframe is art-directed: the visible sun "
            + "(sky glow, fog in-scatter, shafts) is ahead-left, but the arch and Pista are lit from the left. Equal to the "
            + "sun angles = physically consistent.")]
        [SerializeField] private float _keyElevationDeg = 20f;
        [SerializeField] private float _keyAzimuthDeg = -31f;
        [SerializeField] private float _skyExposure = 0.9f;
        [SerializeField] private float _ambientIntensity = 1.05f;

        [Header("Atmosphere")]
        [SerializeField] private Color _fogColor = new Color(0.70f, 0.78f, 0.78f, 1f);
        [SerializeField] private Color _fogSunColor = new Color(1f, 0.88f, 0.70f, 1f);
        [SerializeField] private float _fogDensity = 0.0011f;
        [SerializeField] private float _fogHeightFalloff = 0.02f;
        [SerializeField] private float _fogBaseHeightM = -15f;
        [SerializeField] private float _fogStartM = 30f;
        [SerializeField] private float _fogMaxOpacity = 0.6f;
        [SerializeField] private float _fogSunPower = 6f;
        [SerializeField] private Color _mistColor = new Color(0.96f, 0.95f, 0.90f, 1f);
        [SerializeField] private float _mistOpacity = 0.24f;
        [Tooltip("Share of the warm sun in-scatter on mist and spray cards (lower = whiter spray, keyframe falls).")]
        [SerializeField] private float _mistSunScatter = 1f;
        [SerializeField] private float _mistScatterNeutral = 0f;
        [SerializeField] private Color _shaftColor = new Color(1f, 0.80f, 0.50f, 1f);
        [SerializeField] private float _shaftIntensity = 0.5f;
        [Tooltip("Light shafts: (x, ground y, z, length m) of the lit end; the card runs toward the sun.")]
        [SerializeField] private Vector4[] _shafts =
        {
            new Vector4(-20f, -10f, 60f, 90f), new Vector4(-8f, -14f, 80f, 100f), new Vector4(-34f, -4f, 90f, 100f),
            new Vector4(2f, -14f, 110f, 110f), new Vector4(-14f, -12f, 120f, 110f),
        };
        [SerializeField] private Vector2 _shaftWidthM = new Vector2(6f, 12f);

        [Header("Grade")]
        [SerializeField] private float _postExposure = 0.1f;
        [SerializeField] private float _contrast = 30f;
        [SerializeField] private float _saturation = 22f;
        [SerializeField] private float _whiteBalanceTemperature = 4f;
        [SerializeField] private float _whiteBalanceTint = -3f;
        [SerializeField] private Color _splitShadows = new Color(0.40f, 0.52f, 0.55f, 1f);
        [SerializeField] private Color _splitHighlights = new Color(0.95f, 0.74f, 0.44f, 1f);
        [SerializeField] private float _bloomThreshold = 0.95f;
        [SerializeField] private float _bloomIntensity = 0.7f;
        [SerializeField] private float _vignetteIntensity = 0.24f;
        [SerializeField] private bool _acesTonemapping = true;
        [SerializeField] private float _splitBalance = 10f;
        [Tooltip("Lift / gamma / gain style trackballs: (r, g, b, offset).")]
        [SerializeField] private Vector4 _gradeShadows = new Vector4(0.96f, 1.02f, 1.04f, -0.06f);
        [SerializeField] private Vector4 _gradeMidtones = new Vector4(1f, 1.01f, 0.98f, 0f);
        [SerializeField] private Vector4 _gradeHighlights = new Vector4(1.04f, 1.0f, 0.94f, 0.02f);
        [SerializeField] private float _bloomScatter = 0.7f;
        [SerializeField] private Color _bloomTint = new Color(1f, 0.93f, 0.8f, 1f);
        [Tooltip("Keyframe-matched grading LUT (URP 32³ strip, 1024x32, made by tools/art/match_lut.py), applied after "
            + "the hand grade. Empty = off.")]
        [SerializeField] private string _gradeLutPath = "Assets/_Game/Art/HeroBasin/Grade/HeroBasin_F4e_Lut.png";
        [SerializeField] private float _gradeLutContribution = 1f;
        [Tooltip("Distance (m) at which the sky takes the project fog: lower = clearer sky above the horizon band.")]
        [SerializeField] private float _skyFogDistanceM = 2400f;

        [Header("Sun glow (painted sky layer, toward the sun): x core gain, y core power, z halo gain, w halo power")]
        [SerializeField] private Vector4 _sunGlow = new Vector4(0f, 400f, 0f, 12f);
        [SerializeField] private Color _sunGlowColor = new Color(1f, 0.86f, 0.62f, 1f);

        [Header("Pista rim (Rim Overlay material slot: backlit Fresnel edge toward the sun; 0 intensity = off)")]
        [SerializeField] private Color _rimLightColor = new Color(1f, 0.82f, 0.58f, 1f);
        [SerializeField] private float _rimLightIntensity = 0f;
        [SerializeField] private float _rimPower = 3f;
        [SerializeField] private float _rimSunFacing = 0.8f;

        [Header("Water")]
        [SerializeField] private Color _waterShallowColor = new Color(0.08f, 0.78f, 0.74f, 1f);
        [SerializeField] private Color _waterDeepColor = new Color(0.01f, 0.40f, 0.47f, 1f);
        [SerializeField] private float _basinWaterY = -14f;
        [Tooltip("Pool water: sky reflection strength, opacity deep / shallow (lower reflection keeps grazing water turquoise).")]
        [SerializeField] private float _waterReflection = 0.3f;
        [SerializeField] private float _waterOpacity = 0.93f;
        [SerializeField] private float _waterShallowOpacity = 0.65f;
        [Tooltip("Basin water disc: centre (x, z) and radius.")]
        [SerializeField] private Vector3 _basinWater = new Vector3(15f, 120f, 160f);
        [Tooltip("Terraces: (x, z, top y, radius). Mossy rock islands with a pool on top; falls spill from the front rim to the basin.")]
        [SerializeField] private Vector4[] _terraces =
        {
            new Vector4(22f, 168f, -4.5f, 30f), new Vector4(8f, 128f, -8f, 20f), new Vector4(38f, 124f, -8.5f, 18f),
            new Vector4(-8f, 104f, -10.5f, 14f), new Vector4(22f, 92f, -11f, 15f), new Vector4(50f, 98f, -10.5f, 12f),
            new Vector4(6f, 70f, -12.3f, 10f), new Vector4(34f, 66f, -12.5f, 9f),
        };

        [Tooltip("Travertine tier pieces (RS_TravertineTiers, used instead of the procedural terraces when it has landed): "
            + "(x, z, extra yaw deg, uniform scale); the piece turns its cascades toward the camera and its apron sits on the "
            + "basin water.")]
        [SerializeField] private Vector4[] _travertines = { new Vector4(16f, 76f, 0f, 1.3f) };
        [Tooltip("Vertical scale of the travertine pieces relative to their uniform scale (flatter keeps the stair under eye level).")]
        [SerializeField] private float _travertineHeightScale = 0.85f;
        [Tooltip("Travertine albedo tint and moss on upward faces (keyframe: grey-green limestone, mossy crests).")]
        [SerializeField] private Color _travertineTint = Color.white;
        [SerializeField] private float _travertineMoss = 0.4f;
        [Tooltip("Framing plant clumps (FP_FrameLeft / FP_FrameRight): (x, z, yaw deg, scale) each.")]
        [SerializeField] private Vector4 _frameLeft = new Vector4(-3.4f, -0.6f, 20f, 1f);
        [SerializeField] private Vector4 _frameRight = new Vector4(4.6f, 0.4f, -25f, 1f);
        [Tooltip("Canopy wall segments (FP_Canopy_Clump) on the banks: (x, z, extra yaw deg, scale).")]
        [SerializeField] private Vector4[] _canopyWalls =
        {
            new Vector4(-62f, 70f, 0f, 1.6f), new Vector4(-48f, 120f, 0f, 1.8f), new Vector4(78f, 80f, 0f, 1.6f), new Vector4(70f, 130f, 0f, 1.8f),
        };

        [Header("Arch and falls")]
        [SerializeField] private Vector3 _archFootA = new Vector3(-95f, -22f, 250f);
        [SerializeField] private Vector3 _archFootB = new Vector3(122f, -22f, 236f);
        [SerializeField] private float _archTopY = 128f;
        [Tooltip("Depth (local Z) of the hero arch relative to the mean of its span and height scales (below 1 = slimmer, "
            + "keeps a wide arch from reading as a dome).")]
        [SerializeField] private float _archDepthScale = 1f;
        [Tooltip("Second hero-arch instance braided into the first (turned 180 degrees): feet A/B and top height. Thickens "
            + "the legs and crown (keyframe F4_f: massive braided legs around a tall sky window). Top 0 = none.")]
        [SerializeField] private Vector3 _archBraidFootA = Vector3.zero;
        [SerializeField] private Vector3 _archBraidFootB = Vector3.zero;
        [SerializeField] private float _archBraidTopY;
        [SerializeField] private float _archBraidDepthScale = 0.4f;
        [Tooltip("Vine curtains cleared from the arch opening: x = half width of the cleared band (fraction of the arch "
            + "width), y = curtains reaching below this height (fraction of the arch height) inside the band are dropped.")]
        [SerializeField] private Vector2 _archVineClearing = new Vector2(0.2f, 0.62f);
        [Tooltip("Albedo tint of the hero arch (keyframe: pale grey stone, warm in the light).")]
        [SerializeField] private Color _archTint = new Color(0.96f, 0.94f, 0.88f, 1f);
        [SerializeField] private float _archMoss = 0.55f;
        [SerializeField] private int _archCrowns = 60;
        [Tooltip("Waterfall sheets: water color between the streaks, and backlit glow (keyframe: white aerated water).")]
        [SerializeField] private Color _fallWaterColor = new Color(0.52f, 0.74f, 0.74f, 1f);
        [SerializeField] private float _fallTranslucency = 0.45f;
        [SerializeField] private float _fallEdgeBreakup = 0.65f;
        [Tooltip("Waterfall white water: bias toward aerated white in the sheet core, and glow of the white water out of the sun.")]
        [SerializeField] private float _fallWhiteBias = 0.3f;
        [SerializeField] private float _fallFoamGlow = 0f;
        [Tooltip("Waterfall clumping (0..1: broad dense and thin columns) and streak tiling (x across, y down, per m).")]
        [SerializeField] private float _fallClumping = 0f;
        [SerializeField] private Vector2 _fallStreakTiling = new Vector2(0.16f, 0.035f);
        [Tooltip("Opacity gain of the tall fall's spray burst (>= 1) and mist plume (>= 0, 0 = normal mist).")]
        [SerializeField] private float _fallSprayDensity = 1f;
        [SerializeField] private float _fallPlumeDensity = 0f;
        [Tooltip("Offset (m) of the tall fall from the arch's WaterfallMouth anchor (keyframe: right of the crown's centre).")]
        [SerializeField] private Vector3 _tallFallOffset = Vector3.zero;
        [Tooltip("Leaf cards: tint and backlight translucency in the hero scene (deeper green than the look test).")]
        [SerializeField] private Color _leafTint = new Color(0.62f, 0.78f, 0.52f, 1f);
        [SerializeField] private float _leafTranslucency = 0.35f;
        [Tooltip("Multiplier on the plant atlas tints (foreground framing plants: keyframe keeps them dark against the light).")]
        [SerializeField] private Color _plantTint = Color.white;
        [Tooltip("No forest tree closer to the camera than this (keeps crowns out of Pista's space), m.")]
        [SerializeField] private float _forestMinDistanceM = 32f;
        [Tooltip("Multiplier on the minimum forest distance on the left (keyframe: the sunlit valley stays open and hazy there).")]
        [SerializeField] private float _forestLeftDistanceScale = 2.2f;
        [Tooltip("Height multiplier for forest trees in front of the arch (keeps its legs clear).")]
        [SerializeField] private float _forestUnderArchScale = 1f;
        [SerializeField] private HeroFall[] _falls =
        {
            new HeroFall(new Vector3(60f, 112f, 330f), -4f, 22f, 4, false),
            new HeroFall(new Vector3(22f, 4.5f, 196f), -4.1f, 52f, 4, true),
        };

        [Header("Kit pieces")]
        [Tooltip("Rootstone pillars for parallax in front of the painted range: (x, z, height m, yaw deg).")]
        [SerializeField] private Vector4[] _kitPillars =
        {
            new Vector4(-150f, 330f, 120f, 30f), new Vector4(185f, 310f, 105f, 200f), new Vector4(-95f, 420f, 150f, 110f),
        };
        [Tooltip("Lookout ledge under Pista: position of its pivot (ground level, centre) and yaw.")]
        [SerializeField] private Vector4 _ledgePiece = new Vector4(0.1f, -1.72f, 0.4f, 15f);
        [Tooltip("Where Pista stands on the ledge relative to its Stand anchor (piece space, m): + x = toward its right edge.")]
        [SerializeField] private Vector3 _ledgeStandOffset = Vector3.zero;

        [Header("Terrain")]
        [SerializeField] private float _basinFloorY = -18f;
        [Tooltip("Left hills: inner x, outer x, height.")]
        [SerializeField] private Vector3 _leftHills = new Vector3(-50f, -150f, 10f);
        [Tooltip("Right hills: inner x, outer x, height.")]
        [SerializeField] private Vector3 _rightHills = new Vector3(68f, 150f, 26f);
        [Tooltip("Ledge promontory under Pista: half width x, front z (drop), height.")]
        [SerializeField] private Vector3 _ledge = new Vector3(6f, 2.6f, 0f);
        [Tooltip("Near right bank mound: (x, z, radius, top y); radius 0 = none.")]
        [SerializeField] private Vector4 _rightBank = Vector4.zero;
        [SerializeField] private int _forestTrees = 420;
        [Tooltip("Framing trees on the left: (x, z, height m, leaf card size m). Keep their crowns off the sun.")]
        [SerializeField] private Vector4[] _heroTrees = { new Vector4(-19f, 4f, 26f, 5f), new Vector4(-30f, 24f, 30f, 9f) };
        [Tooltip("Palm crowns hanging into the top-left corner: (x, y, z of the crown top, frond size m).")]
        [SerializeField] private Vector4[] _cornerPalms = new Vector4[0];

        [Header("Backdrop layers (far to near)")]
        [SerializeField] private HeroBackdropLayer[] _backdrops =
        {
            new HeroBackdropLayer("BD_F4e_Sky", 3600f, 37f, 180f, -75f, false, 0f, 1f),
            new HeroBackdropLayer("BD_F4e_FarRange", 2800f, -38f, 62f, -4f, false, 0.25f, 1f),
            new HeroBackdropLayer("BD_F4e_FarRange", 2700f, 30f, 58f, -4f, true, 0.25f, 1f),
            new HeroBackdropLayer("BD_MistBand", 2000f, 0f, 150f, -8f, false, 0f, 1f),
            new HeroBackdropLayer("BD_F4e_MidPillars", 1500f, 6f, 42f, -7f, false, 0.2f, 1f),
            new HeroBackdropLayer("BD_F4e_FarFalls", 900f, 11f, 15f, -9f, false, 0.15f, 1f),
            new HeroBackdropLayer("BD_MistBand", 800f, 5f, 120f, -9f, false, 0f, 1f),
            new HeroBackdropLayer("BD_F4e_JungleWall", 640f, 8f, 56f, -13f, false, 0.1f, 1f),
            new HeroBackdropLayer("BD_F4e_JungleWall", 520f, -48f, 66f, -11f, false, 0.1f, 1f),
            new HeroBackdropLayer("BD_F4e_JungleWall", 480f, 48f, 60f, -11f, true, 0.1f, 1f),
        };

        public HeroBasinStyle Style => _style;
        public HeroPainterlyLook Painterly => _painterly;
        public HeroDressing Dressing => _dressing;
        public HeroCards Cards => _style == HeroBasinStyle.Painterly ? _cards : null;
        public Vector3 LandscapePosition => _landscapePosition;
        public Vector3 LandscapeEuler => _landscapeEuler;
        public float LandscapeFovDeg => _landscapeFovDeg;
        public Vector3 PortraitPosition => _portraitPosition;
        public Vector3 PortraitEuler => _portraitEuler;
        public float PortraitFovDeg => _portraitFovDeg;
        public float FarClipM => _farClipM;
        public Vector3 PistaPosition => _pistaPosition;
        public float PistaYawDeg => _pistaYawDeg;
        public float PistaIdleTimeS => _pistaIdleTimeS;
        public float SunElevationDeg => _sunElevationDeg;
        public float SunAzimuthDeg => _sunAzimuthDeg;
        public Color SunColor => _sunColor;
        public float SunIntensity => _sunIntensity;
        public float KeyElevationDeg => _keyElevationDeg;
        public float KeyAzimuthDeg => _keyAzimuthDeg;

        /// <summary>Direction the key light travels (the directional light; see <see cref="SunLightDirection"/> for the visible sun).</summary>
        public Vector3 KeyLightDirection
        {
            get
            {
                float el = _keyElevationDeg * Mathf.Deg2Rad;
                float az = _keyAzimuthDeg * Mathf.Deg2Rad;
                return -new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            }
        }
        public float SkyExposure => _skyExposure;
        public float AmbientIntensity => _ambientIntensity;
        public Color FogColor => _fogColor;
        public Color FogSunColor => _fogSunColor;
        public float FogDensity => _fogDensity;
        public float FogHeightFalloff => _fogHeightFalloff;
        public float FogBaseHeightM => _fogBaseHeightM;
        public float FogStartM => _fogStartM;
        public float FogMaxOpacity => _fogMaxOpacity;
        public float FogSunPower => _fogSunPower;
        public Color MistColor => _mistColor;
        public float MistOpacity => _mistOpacity;
        public float MistSunScatter => _mistSunScatter;
        public float MistScatterNeutral => _mistScatterNeutral;
        public Color ShaftColor => _shaftColor;
        public float ShaftIntensity => _shaftIntensity;
        public Vector4[] Shafts => _shafts;
        public Vector2 ShaftWidthM => _shaftWidthM;
        public float PostExposure => _postExposure;
        public float Contrast => _contrast;
        public float Saturation => _saturation;
        public float WhiteBalanceTemperature => _whiteBalanceTemperature;
        public float WhiteBalanceTint => _whiteBalanceTint;
        public Color SplitShadows => _splitShadows;
        public Color SplitHighlights => _splitHighlights;
        public float BloomThreshold => _bloomThreshold;
        public float BloomIntensity => _bloomIntensity;
        public float VignetteIntensity => _vignetteIntensity;
        public bool AcesTonemapping => _acesTonemapping;
        public float SplitBalance => _splitBalance;
        public Vector4 GradeShadows => _gradeShadows;
        public Vector4 GradeMidtones => _gradeMidtones;
        public Vector4 GradeHighlights => _gradeHighlights;
        public float BloomScatter => _bloomScatter;
        public Color BloomTint => _bloomTint;
        public string GradeLutPath => _gradeLutPath;
        public float GradeLutContribution => _gradeLutContribution;
        public float SkyFogDistanceM => _skyFogDistanceM;
        public Vector4 SunGlow => _sunGlow;
        public Color SunGlowColor => _sunGlowColor;
        public Color RimLightColor => _rimLightColor;
        public float RimLightIntensity => _rimLightIntensity;
        public float RimPower => _rimPower;
        public float RimSunFacing => _rimSunFacing;
        public Color WaterShallowColor => _waterShallowColor;
        public Color WaterDeepColor => _waterDeepColor;
        public float BasinWaterY => _basinWaterY;
        public float WaterReflection => _waterReflection;
        public float WaterOpacity => _waterOpacity;
        public float WaterShallowOpacity => _waterShallowOpacity;
        public Color ArchTint => _archTint;
        public int ArchCrowns => _archCrowns;
        public float ArchMoss => _archMoss;
        public Color FallWaterColor => _fallWaterColor;
        public float FallTranslucency => _fallTranslucency;
        public float FallEdgeBreakup => _fallEdgeBreakup;
        public float FallWhiteBias => _fallWhiteBias;
        public float FallFoamGlow => _fallFoamGlow;
        public float FallClumping => _fallClumping;
        public Vector2 FallStreakTiling => _fallStreakTiling;
        public float FallSprayDensity => _fallSprayDensity;
        public float FallPlumeDensity => _fallPlumeDensity;
        public Vector3 TallFallOffset => _tallFallOffset;
        public Color LeafTint => _leafTint;
        public float LeafTranslucency => _leafTranslucency;
        public Color PlantTint => _plantTint;
        public float ForestMinDistanceM => _forestMinDistanceM;
        public float ForestLeftDistanceScale => _forestLeftDistanceScale;
        public float ForestUnderArchScale => _forestUnderArchScale;
        public Vector3 BasinWater => _basinWater;
        public Vector4[] Terraces => _terraces;
        public Vector4[] Travertines => _travertines;
        public float TravertineHeightScale => _travertineHeightScale;
        public Color TravertineTint => _travertineTint;
        public float TravertineMoss => _travertineMoss;
        public Vector4 FrameLeft => _frameLeft;
        public Vector4 FrameRight => _frameRight;
        public Vector4[] CanopyWalls => _canopyWalls;
        public Vector3 ArchFootA => _archFootA;
        public Vector3 ArchFootB => _archFootB;
        public float ArchTopY => _archTopY;
        public float ArchDepthScale => _archDepthScale;
        public Vector3 ArchBraidFootA => _archBraidFootA;
        public Vector3 ArchBraidFootB => _archBraidFootB;
        public float ArchBraidTopY => _archBraidTopY;
        public float ArchBraidDepthScale => _archBraidDepthScale;
        public Vector2 ArchVineClearing => _archVineClearing;
        public HeroFall[] Falls => _falls;
        public float BasinFloorY => _basinFloorY;
        public Vector3 LeftHills => _leftHills;
        public Vector3 RightHills => _rightHills;
        public Vector3 Ledge => _ledge;
        public Vector4 RightBank => _rightBank;
        public int ForestTrees => _forestTrees;
        public Vector4[] HeroTrees => _heroTrees;
        public Vector4[] CornerPalms => _cornerPalms;
        public HeroBackdropLayer[] Backdrops => _backdrops;
        public Vector4[] KitPillars => _kitPillars;
        public Vector4 LedgePiece => _ledgePiece;
        public Vector3 LedgeStandOffset => _ledgeStandOffset;

        /// <summary>Direction the sunlight travels (from the sun into the scene).</summary>
        public Vector3 SunLightDirection
        {
            get
            {
                float el = _sunElevationDeg * Mathf.Deg2Rad;
                float az = _sunAzimuthDeg * Mathf.Deg2Rad;
                return -new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            }
        }
    }
}
