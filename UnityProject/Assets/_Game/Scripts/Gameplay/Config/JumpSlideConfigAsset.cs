using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Jump, gravity, coyote, buffer, ledges, slide and fast-fall (spec 101 §2.4–2.5).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/JumpSlideConfig", fileName = "JumpSlideConfig")]
    public sealed class JumpSlideConfigAsset : ScriptableObject
    {
        [SerializeField] private JumpSlideConfig _values = new JumpSlideConfig();

        public JumpSlideConfig Values => _values;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Spec 101 §7: apex 1.35–1.50 m and airtime 0.55–0.65 s, else a warning.
            JumpArc arc = JumpArc.Measure(_values, 1f / 60f);
            if (arc.Apex < 1.35f || arc.Apex > 1.50f || arc.Airtime < 0.55f || arc.Airtime > 0.65f)
            {
                Debug.LogWarning(
                    "[JungleBooze] " + name + ": jump apex " + arc.Apex.ToString("0.000") + " m, airtime " +
                    arc.Airtime.ToString("0.000") + " s (spec 101 wants 1.35–1.50 m and 0.55–0.65 s).",
                    this);
            }
        }
#endif
    }
}
