using JungleBooze.Core.Perf;
using UnityEngine;

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// Quality tiers per device class (ADR 0010). Loaded from Resources at start-up by <see cref="QualityTiers"/>.
    /// High = the owner-reviewed look (Unity quality level "High", pipeline URP-Realistic) on iPhone 12 / A14 and newer;
    /// Low = fallback level "Low" (pipeline URP-Realistic-Low) for older or low-memory devices. Each tier holds the
    /// internal render resolution at a fixed pixel count so GPU load is the same on every screen size.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/Perf/Quality Tiers", fileName = "QualityTiers")]
    public sealed class QualityTierConfigAsset : ScriptableObject
    {
        [Tooltip("Auto = pick from the device model and memory. High/Low force a tier (testing only).")]
        [SerializeField] private QualityTierOverride _override = QualityTierOverride.Auto;

        [SerializeField] private string _highQualityLevel = "High";
        [SerializeField] private string _lowQualityLevel = "Low";

        [Tooltip("Internal render pixels for High, in megapixels (ADR 0004: about 1.7 MP; iPhone 12 render scale ≈ 0.76).")]
        [SerializeField] private float _highTargetMegapixels = 1.7f;

        [Tooltip("Internal render pixels for Low, in megapixels.")]
        [SerializeField] private float _lowTargetMegapixels = 1.2f;

        [SerializeField] private float _minRenderScale = 0.5f;
        [SerializeField] private float _maxRenderScale = 1f;

        [Tooltip("Frame-rate cap. 60 everywhere: ProMotion phones are capped too (thermals and battery).")]
        [SerializeField] private int _targetFrameRate = 60;

        [Tooltip("Lowest iPhone model generation for High: iPhone13,x = iPhone 12 (A14).")]
        [SerializeField] private int _minHighIphoneGeneration = 13;

        [Tooltip("Lowest iPad model generation for High: iPad13,x = iPad Air 4 / iPad 10 (A14) and M1 iPads.")]
        [SerializeField] private int _minHighIpadGeneration = 13;

        [Tooltip("Below this system memory (MB) a device is Low even when its chip qualifies.")]
        [SerializeField] private int _minHighMemoryMb = 3500;

        public QualityTierOverride Override => _override;
        public int TargetFrameRate => _targetFrameRate;
        public float MinRenderScale => _minRenderScale;
        public float MaxRenderScale => _maxRenderScale;

        public string QualityLevelName(DeviceTier tier)
        {
            return tier == DeviceTier.High ? _highQualityLevel : _lowQualityLevel;
        }

        public float TargetMegapixels(DeviceTier tier)
        {
            return tier == DeviceTier.High ? _highTargetMegapixels : _lowTargetMegapixels;
        }

        public DeviceTier Classify(string deviceModel, int systemMemoryMb)
        {
            switch (_override)
            {
                case QualityTierOverride.High:
                    return DeviceTier.High;
                case QualityTierOverride.Low:
                    return DeviceTier.Low;
                default:
                    return DeviceTierRules.Classify(deviceModel, systemMemoryMb, _minHighIphoneGeneration, _minHighIpadGeneration, _minHighMemoryMb);
            }
        }

        private void OnValidate()
        {
            _highTargetMegapixels = Mathf.Clamp(_highTargetMegapixels, 0.3f, 8f);
            _lowTargetMegapixels = Mathf.Clamp(_lowTargetMegapixels, 0.3f, 8f);
            _minRenderScale = Mathf.Clamp(_minRenderScale, 0.25f, 1f);
            _maxRenderScale = Mathf.Clamp(_maxRenderScale, _minRenderScale, 2f);
            _targetFrameRate = Mathf.Clamp(_targetFrameRate, 30, 120);
            _minHighMemoryMb = Mathf.Max(0, _minHighMemoryMb);
        }
    }
}
