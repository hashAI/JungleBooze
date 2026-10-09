using JungleBooze.Gameplay.Animation;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>Presentation tuning of the animated runner (clip timing, rates, fades, lean, ponytail).</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/RunnerAnimationConfig", fileName = "RunnerAnimationConfig")]
    public sealed class RunnerAnimationConfigAsset : ScriptableObject
    {
        [SerializeField] private RunnerAnimationConfig _values = new RunnerAnimationConfig();

        public RunnerAnimationConfig Values => _values;

        public void SetValues(RunnerAnimationConfig values)
        {
            _values = values;
        }
    }
}
