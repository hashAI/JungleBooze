using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Route tuning (spec 003 sections 4.3, 4.4 and 14.1). Presentation only, never part of the simulation.
    /// The storage window and the floating-origin fields are read by <see cref="PathFrame"/>; the limits, the beat
    /// length table and the per-world tables are read by <see cref="RouteGenerator"/> and <see cref="RouteValidator"/>.
    /// All values are [ASSUMED] first guesses from the spec.
    /// </summary>
    [CreateAssetMenu(fileName = "RouteTuning", menuName = "JungleBooze/Config/Route Tuning")]
    public sealed class RouteTuning : ScriptableObject
    {
        /// <summary>Number of <see cref="RouteBeatKind"/> values; the per-beat tables have this length.</summary>
        public const int BeatKindCount = 13;

        /// <summary>Resources name of the optional tuning asset (see <see cref="LoadOrDefault"/>).</summary>
        public const string ResourceName = "RouteTuning";

        /// <summary>PlayerPrefs key (editor and development builds only) that switches the generated route on.</summary>
        public const string UseGeneratedRoutePrefKey = "JungleBooze.UseGeneratedRoute";

        [Header("Storage (spec 003 section 3.1)")]
        [SerializeField] private float _sampleSpacingM = 1f;
        [SerializeField] private float _behindM = 40f;

        [Tooltip("[ASSUMED] 115, not the spec's 200: the track commits 190 m and the route needs ReadAheadM (70) of notice before it builds (5 m slack).")]
        [SerializeField] private float _aheadM = 115f;

        [Header("Floating origin (section 3.1)")]
        [SerializeField] private bool _floatingOriginEnabled = false;
        [SerializeField] private float _floatingOriginThresholdM = 800f;
        [SerializeField] private float _floatingOriginSnapM = 100f;

        [Header("Limits (section 4.3)")]
        [SerializeField] private float _kappaJerk = 0.0006f;
        [SerializeField] private float _emergencyKappaJerk = 0.0015f;
        [SerializeField] private float _turnWindowM = 120f;
        [SerializeField] private float _turnWindowMaxDeg = 75f;
        [SerializeField] private float _restoringDeg = 50f;
        [SerializeField] private float _crestRadiusMinM = 250f;
        [SerializeField] private float _sagRadiusMinM = 120f;
        [SerializeField] private float _maxYawRateDegS = 28f;
        [SerializeField] private float _maxBoostSpeedMps = 33.6f;

        [Tooltip("Curvature (1/R) that gets the full bank; below it the bank scales linearly.")]
        [SerializeField] private float _fullBankRadiusM = 75f;

        [Header("Calm start and zones (sections 4.3, 8)")]
        [Tooltip("[ASSUMED] About 25 s of the speed curve: R >= 200 m and |grade| <= 4 % until this distance.")]
        [SerializeField] private float _calmStartM = 250f;
        [SerializeField] private float _calmRMinM = 200f;
        [SerializeField] private float _calmGradeMaxPct = 4f;

        [Tooltip("The route looks this far ahead of the sample it builds for vine and gateway chunks.")]
        [SerializeField] private float _readAheadM = 70f;

        [Tooltip("Curvature must be at most 1/this at this many metres before a vine or gateway chunk.")]
        [SerializeField] private float _zoneRadiusM = 250f;
        [SerializeField] private float _zoneNoticeM = 30f;
        [SerializeField] private float _zoneGradeMaxPct = 4f;

        [Header("Schedule (sections 4.4, 7.2, used in T2/T5)")]
        [SerializeField] private float _clearingEveryM = 350f;
        [SerializeField] private float _canopyEveryM = 650f;
        [SerializeField] private float _canopyLengthM = 300f;
        [SerializeField] private float _ascentLengthM = 110f;
        [SerializeField] private float _ascentGradePct = 12f;

        [Header("Beat lengths in m, indexed by RouteBeatKind (section 4.2)")]
        [SerializeField] private float[] _beatMinLengthM = CreateMinLengths();
        [SerializeField] private float[] _beatMaxLengthM = CreateMaxLengths();

        [Header("Worlds, indexed by WorldKind: Jungle, River, Mountains, Ruins (section 4.4)")]
        [SerializeField] private RouteWorldTuning[] _worlds = RouteWorldTuning.CreateDefaults();

        [Header("Route selection")]
        [SerializeField] private bool _debugRoute = false;

        [Tooltip("Off until the owner has played the debug route: the straight route stays the default.")]
        [SerializeField] private bool _useGeneratedRoute = false;

        /// <summary>Distance between stored samples in m.</summary>
        public float SampleSpacingM => _sampleSpacingM;

        /// <summary>Route kept behind the hero in m.</summary>
        public float BehindM => _behindM;

        /// <summary>Route built ahead of the hero in m.</summary>
        public float AheadM => _aheadM;

        /// <summary>Off by default: nothing is ever translated, so T1 changes no position.</summary>
        public bool FloatingOriginEnabled => _floatingOriginEnabled;

        public float FloatingOriginThresholdM => _floatingOriginThresholdM;

        public float FloatingOriginSnapM => _floatingOriginSnapM;

        /// <summary>Largest change of curvature per metre (rad/m^2).</summary>
        public float KappaJerk => _kappaJerk;

        /// <summary>Change of curvature per metre allowed by the emergency ease (rad/m^2).</summary>
        public float EmergencyKappaJerk => _emergencyKappaJerk;

        public float TurnWindowM => _turnWindowM;
        public float TurnWindowMaxDeg => _turnWindowMaxDeg;
        public float RestoringDeg => _restoringDeg;

        /// <summary>Smallest vertical radius over a crest in m.</summary>
        public float CrestRadiusMinM => _crestRadiusMinM;

        /// <summary>Smallest vertical radius in a sag in m.</summary>
        public float SagRadiusMinM => _sagRadiusMinM;

        /// <summary>Hard cap of the yaw rate in degrees per second at <see cref="MaxBoostSpeedMps"/>.</summary>
        public float MaxYawRateDegS => _maxYawRateDegS;

        public float MaxBoostSpeedMps => _maxBoostSpeedMps;

        public float FullBankRadiusM => _fullBankRadiusM;

        /// <summary>Distance up to which the route is calm (wide bends, gentle grade).</summary>
        public float CalmStartM => _calmStartM;

        public float CalmRMinM => _calmRMinM;
        public float CalmGradeMaxPct => _calmGradeMaxPct;
        public float ReadAheadM => _readAheadM;
        public float ZoneRadiusM => _zoneRadiusM;
        public float ZoneNoticeM => _zoneNoticeM;
        public float ZoneGradeMaxPct => _zoneGradeMaxPct;
        public float ClearingEveryM => _clearingEveryM;
        public float CanopyEveryM => _canopyEveryM;
        public float CanopyLengthM => _canopyLengthM;
        public float AscentLengthM => _ascentLengthM;
        public float AscentGradePct => _ascentGradePct;

        /// <summary>When true (T3) the views use the curved debug route instead of the straight one.</summary>
        public bool DebugRoute => _debugRoute;

        /// <summary>When true the generated route (<see cref="RouteGenerator"/>) is used. Default false.</summary>
        public bool UseGeneratedRoute => _useGeneratedRoute;

        /// <summary>Shortest length of <paramref name="kind"/> in m.</summary>
        public float GetMinLengthM(RouteBeatKind kind)
        {
            return Element(_beatMinLengthM, (int)kind);
        }

        /// <summary>Longest length of <paramref name="kind"/> in m.</summary>
        public float GetMaxLengthM(RouteBeatKind kind)
        {
            return Element(_beatMaxLengthM, (int)kind);
        }

        /// <summary>The table of <paramref name="world"/> (the Jungle table if the asset has fewer worlds).</summary>
        public RouteWorldTuning GetWorld(WorldKind world)
        {
            int i = (int)world;
            if (_worlds == null || _worlds.Length == 0)
            {
                _worlds = RouteWorldTuning.CreateDefaults();
            }

            return _worlds[i < _worlds.Length ? i : 0];
        }

        /// <summary>Sets <see cref="UseGeneratedRoute"/> on this instance (tests, tools).</summary>
        public void SetUseGeneratedRoute(bool value)
        {
            _useGeneratedRoute = value;
        }

        /// <summary>A runtime instance with the default values (no asset file needed).</summary>
        public static RouteTuning CreateDefault()
        {
            return CreateInstance<RouteTuning>();
        }

        /// <summary>
        /// The tuning asset <c>Resources/RouteTuning</c> if there is one, else the defaults. In the editor and in
        /// development builds the PlayerPrefs key <see cref="UseGeneratedRoutePrefKey"/> = 1 (menu
        /// <c>JungleBooze > Route > Use generated route</c>) switches the generated route on without an asset.
        /// Works on a copy, so the asset is never modified. Setup time only.
        /// </summary>
        public static RouteTuning LoadOrDefault()
        {
            RouteTuning asset = Resources.Load<RouteTuning>(ResourceName);
            RouteTuning tuning = asset != null ? Instantiate(asset) : CreateDefault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PlayerPrefs.GetInt(UseGeneratedRoutePrefKey, 0) == 1)
            {
                tuning._useGeneratedRoute = true;
            }
#endif
            return tuning;
        }

        private static float Element(float[] table, int index)
        {
            return table != null && index >= 0 && index < table.Length ? table[index] : 0f;
        }

        private static float[] CreateMinLengths()
        {
            var t = new float[BeatKindCount];
            t[(int)RouteBeatKind.Straight] = 40f;
            t[(int)RouteBeatKind.GentleBend] = 60f;
            t[(int)RouteBeatKind.Bend] = 60f;
            t[(int)RouteBeatKind.SBend] = 120f;
            t[(int)RouteBeatKind.Roll] = 80f;
            t[(int)RouteBeatKind.RiseFall] = 60f;
            t[(int)RouteBeatKind.Clearing] = 40f;
            t[(int)RouteBeatKind.Crossing] = 20f;
            t[(int)RouteBeatKind.Bridge] = 60f;
            t[(int)RouteBeatKind.Ascent] = 90f;
            t[(int)RouteBeatKind.Descent] = 90f;
            t[(int)RouteBeatKind.Gateway] = 54f;
            return t;
        }

        private static float[] CreateMaxLengths()
        {
            var t = new float[BeatKindCount];
            t[(int)RouteBeatKind.Straight] = 100f;
            t[(int)RouteBeatKind.GentleBend] = 140f;
            t[(int)RouteBeatKind.Bend] = 120f;
            t[(int)RouteBeatKind.SBend] = 200f;
            t[(int)RouteBeatKind.Roll] = 140f;
            t[(int)RouteBeatKind.RiseFall] = 140f;
            t[(int)RouteBeatKind.Clearing] = 60f;
            t[(int)RouteBeatKind.Crossing] = 40f;
            t[(int)RouteBeatKind.Bridge] = 90f;
            t[(int)RouteBeatKind.Ascent] = 130f;
            t[(int)RouteBeatKind.Descent] = 130f;
            t[(int)RouteBeatKind.Gateway] = 54f;
            return t;
        }
    }
}
