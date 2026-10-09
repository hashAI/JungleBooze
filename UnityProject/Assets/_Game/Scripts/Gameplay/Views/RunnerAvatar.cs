using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The visual body of the runner. <see cref="RunnerView"/> places this object at Pista's feet every frame and
    /// calls <see cref="Apply"/>; simulation events arrive through <see cref="OnRunEvent"/>. Swap the gray-box
    /// <see cref="CapsuleRunnerAvatar"/> for <see cref="AnimatedRunnerAvatar"/> (rigged Pista + Animator) by giving the
    /// FeelTest root an avatar prefab. Avatars never touch simulation state.
    /// </summary>
    public abstract class RunnerAvatar : MonoBehaviour
    {
        public abstract void Apply(in RunnerVisualState state, float frameSeconds);

        public virtual void OnRunEvent(in RunEvent runEvent)
        {
        }

        /// <summary>Blink during i-frames.</summary>
        public abstract void SetVisible(bool visible);

        /// <summary>Gives the avatar the run's movement config (jump airtime, slide length) once, before the first frame.</summary>
        public virtual void Bind(MovementConfig config)
        {
        }

        /// <summary>New run: clear transient animation state.</summary>
        public virtual void ResetPose()
        {
        }
    }
}
