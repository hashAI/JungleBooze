using UnityEngine;

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// Device benchmark script (ADR 0010, docs/perf): the phases run in order, then the run stops on a summary screen.
    /// Default: 60 s hero basin landscape, 60 s portrait, 90 s + 60 s Expedition bot, then the 10-minute soak on the
    /// heaviest view (hero basin landscape). Every second goes to a CSV in the app's Documents/bench folder.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/Perf/Device Bench Config", fileName = "DeviceBenchConfig")]
    public sealed class DeviceBenchConfigAsset : ScriptableObject
    {
        public const string HeroScene = "HeroBasin_Painterly";
        public const string ExpeditionScene = "Expedition";

        [SerializeField] private DeviceBenchPhase[] _phases =
        {
            new DeviceBenchPhase("hero-landscape", HeroScene, DeviceBenchMode.HeroView, false, 60f, 5f),
            new DeviceBenchPhase("hero-portrait", HeroScene, DeviceBenchMode.HeroView, true, 60f, 5f),
            new DeviceBenchPhase("expedition-landscape", ExpeditionScene, DeviceBenchMode.ExpeditionBot, false, 90f, 8f),
            new DeviceBenchPhase("expedition-portrait", ExpeditionScene, DeviceBenchMode.ExpeditionBot, true, 60f, 8f),
            new DeviceBenchPhase("soak-hero-landscape", HeroScene, DeviceBenchMode.HeroView, false, 600f, 5f),
        };

        [Tooltip("Seconds between CSV rows and overlay refreshes.")]
        [SerializeField] private float _logIntervalSeconds = 1f;

        [Tooltip("Seconds between log flushes to storage.")]
        [SerializeField] private float _flushSeconds = 10f;

        [Tooltip("A frame slower than this multiple of the target frame time counts as a missed frame (1.5 = took two refreshes).")]
        [SerializeField] private float _missFactor = 1.5f;

        [Tooltip("Expedition: seconds the results screen shows before the bot runs again.")]
        [SerializeField] private float _runAgainSeconds = 2f;

        [Tooltip("Folder under Application.persistentDataPath (Documents on iOS).")]
        [SerializeField] private string _logFolder = "bench";

        public DeviceBenchPhase[] Phases => _phases;
        public float LogIntervalSeconds => Mathf.Max(0.25f, _logIntervalSeconds);
        public float FlushSeconds => Mathf.Max(1f, _flushSeconds);
        public float MissFactor => Mathf.Max(1.05f, _missFactor);
        public float RunAgainSeconds => Mathf.Max(0f, _runAgainSeconds);
        public string LogFolder => string.IsNullOrEmpty(_logFolder) ? "bench" : _logFolder;

        /// <summary>Total scripted seconds including warm-ups.</summary>
        public float TotalSeconds
        {
            get
            {
                float total = 0f;
                foreach (DeviceBenchPhase phase in _phases)
                {
                    total += phase.Seconds + phase.WarmUpSeconds;
                }

                return total;
            }
        }
    }
}
