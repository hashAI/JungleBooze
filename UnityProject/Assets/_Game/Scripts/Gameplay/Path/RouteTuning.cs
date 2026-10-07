using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Route tuning (spec 003 sections 4.3 and 14.1). Presentation only, never part of the simulation. In T1 only the
    /// storage window and the floating-origin fields are read; the rest is data for the route generator (T2).
    /// All values are [ASSUMED] first guesses from the spec. One value set for all worlds for now; per-world tables
    /// come with T2.
    /// </summary>
    [CreateAssetMenu(fileName = "RouteTuning", menuName = "JungleBooze/Config/Route Tuning")]
    public sealed class RouteTuning : ScriptableObject
    {
        [Header("Storage (spec 003 section 3.1)")]
        [SerializeField] private float _sampleSpacingM = 1f;
        [SerializeField] private float _behindM = 40f;
        [SerializeField] private float _aheadM = 200f;

        [Header("Floating origin (section 3.1)")]
        [SerializeField] private bool _floatingOriginEnabled = false;
        [SerializeField] private float _floatingOriginThresholdM = 800f;
        [SerializeField] private float _floatingOriginSnapM = 100f;

        [Header("Limits (section 4.3, used by the generator in T2)")]
        [SerializeField] private float _rMinM = 75f;
        [SerializeField] private float _gradeMaxPct = 10f;
        [SerializeField] private float _bankMaxDeg = 6f;
        [SerializeField] private float _kappaJerk = 0.0006f;
        [SerializeField] private float _turnWindowM = 120f;
        [SerializeField] private float _turnWindowMaxDeg = 75f;
        [SerializeField] private float _restoringDeg = 50f;

        [Header("Schedule (sections 4.4, 7.2, used in T2/T5)")]
        [SerializeField] private float _clearingEveryM = 350f;
        [SerializeField] private float _canopyEveryM = 650f;
        [SerializeField] private float _canopyLengthM = 300f;
        [SerializeField] private float _ascentLengthM = 110f;
        [SerializeField] private float _ascentGradePct = 12f;
        [SerializeField] private float _calmStartS = 25f;

        [Header("Debug (used in T3)")]
        [SerializeField] private bool _debugRoute = false;

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

        public float RMinM => _rMinM;
        public float GradeMaxPct => _gradeMaxPct;
        public float BankMaxDeg => _bankMaxDeg;
        public float KappaJerk => _kappaJerk;
        public float TurnWindowM => _turnWindowM;
        public float TurnWindowMaxDeg => _turnWindowMaxDeg;
        public float RestoringDeg => _restoringDeg;
        public float ClearingEveryM => _clearingEveryM;
        public float CanopyEveryM => _canopyEveryM;
        public float CanopyLengthM => _canopyLengthM;
        public float AscentLengthM => _ascentLengthM;
        public float AscentGradePct => _ascentGradePct;
        public float CalmStartS => _calmStartS;

        /// <summary>When true (T3) the views use the curved debug route instead of the straight one.</summary>
        public bool DebugRoute => _debugRoute;

        /// <summary>A runtime instance with the default values (no asset file needed).</summary>
        public static RouteTuning CreateDefault()
        {
            return CreateInstance<RouteTuning>();
        }
    }
}
