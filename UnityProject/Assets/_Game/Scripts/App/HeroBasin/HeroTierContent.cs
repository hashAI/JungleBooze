using JungleBooze.App.Perf;
using JungleBooze.Core.Perf;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Content that only the High quality tier draws (ADR 0011): the hero basin builder adds it to the renderers named
    /// in <see cref="HeroCards.LowTierDrops"/> (extra blended layers such as god rays and the mist band). On Low the
    /// renderer is switched off once, when the scene wakes; on High nothing changes, so the reviewed look is untouched.
    /// No per-frame work.
    /// </summary>
    public sealed class HeroTierContent : MonoBehaviour
    {
        [SerializeField] private DeviceTier _minimumTier = DeviceTier.High;

        public DeviceTier MinimumTier
        {
            get => _minimumTier;
            set => _minimumTier = value;
        }

        /// <summary>True when content needing <paramref name="minimum"/> is drawn on <paramref name="current"/>.</summary>
        public static bool IsShown(DeviceTier current, DeviceTier minimum)
        {
            return current >= minimum;
        }

        private void Awake()
        {
            if (!IsShown(QualityTiers.Current, _minimumTier) && TryGetComponent(out Renderer target))
            {
                target.enabled = false;
            }
        }
    }
}
