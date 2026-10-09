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

        [Header("Arch and falls")]
        [SerializeField] private Vector3 _archFootA = new Vector3(-95f, -22f, 250f);
        [SerializeField] private Vector3 _archFootB = new Vector3(122f, -22f, 236f);
        [SerializeField] private float _archTopY = 128f;
        [Tooltip("Albedo tint of the hero arch (keyframe: pale grey stone, warm in the light).")]
        [SerializeField] private Color _archTint = new Color(0.96f, 0.94f, 0.88f, 1f);
        [SerializeField] private int _archCrowns = 60;
        [Tooltip("Leaf cards: tint and backlight translucency in the hero scene (deeper green than the look test).")]
        [SerializeField] private Color _leafTint = new Color(0.62f, 0.78f, 0.52f, 1f);
        [SerializeField] private float _leafTranslucency = 0.35f;
        [Tooltip("No forest tree closer to the camera than this (keeps crowns out of Pista's space), m.")]
        [SerializeField] private float _forestMinDistanceM = 32f;
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

        [Header("Terrain")]
        [SerializeField] private float _basinFloorY = -18f;
        [Tooltip("Left hills: inner x, outer x, height.")]
        [SerializeField] private Vector3 _leftHills = new Vector3(-50f, -150f, 10f);
        [Tooltip("Right hills: inner x, outer x, height.")]
        [SerializeField] private Vector3 _rightHills = new Vector3(68f, 150f, 26f);
        [Tooltip("Ledge promontory under Pista: half width x, front z (drop), height.")]
        [SerializeField] private Vector3 _ledge = new Vector3(6f, 2.6f, 0f);
        [SerializeField] private int _forestTrees = 420;

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
        public Color WaterShallowColor => _waterShallowColor;
        public Color WaterDeepColor => _waterDeepColor;
        public float BasinWaterY => _basinWaterY;
        public float WaterReflection => _waterReflection;
        public float WaterOpacity => _waterOpacity;
        public float WaterShallowOpacity => _waterShallowOpacity;
        public Color ArchTint => _archTint;
        public int ArchCrowns => _archCrowns;
        public Color LeafTint => _leafTint;
        public float LeafTranslucency => _leafTranslucency;
        public float ForestMinDistanceM => _forestMinDistanceM;
        public Vector3 BasinWater => _basinWater;
        public Vector4[] Terraces => _terraces;
        public Vector3 ArchFootA => _archFootA;
        public Vector3 ArchFootB => _archFootB;
        public float ArchTopY => _archTopY;
        public HeroFall[] Falls => _falls;
        public float BasinFloorY => _basinFloorY;
        public Vector3 LeftHills => _leftHills;
        public Vector3 RightHills => _rightHills;
        public Vector3 Ledge => _ledge;
        public int ForestTrees => _forestTrees;
        public HeroBackdropLayer[] Backdrops => _backdrops;
        public Vector4[] KitPillars => _kitPillars;
        public Vector4 LedgePiece => _ledgePiece;

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
