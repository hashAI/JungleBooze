using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Hook for the rigged Pista model (asset-pipeline delivers it in Assets/_Game/Art/Characters/Pista/). Put this
    /// component on the root of a prefab that contains the model and an <see cref="Animator"/>, then assign that
    /// prefab as the FeelTest root's avatar. It drives these Animator parameters when they exist (missing ones are
    /// skipped): floats <c>Speed</c> (m/s), <c>Lateral</c> (−1…1), <c>VerticalSpeed</c> (m/s), <c>HeightAboveGround</c>
    /// (m); bools <c>Grounded</c>, <c>Sliding</c>, <c>Dead</c>; triggers <c>Jump</c>, <c>Land</c>, <c>HardLand</c>,
    /// <c>Dodge</c>, <c>Stumble</c>, <c>Crash</c>, <c>Fall</c>, <c>LedgePull</c>. The model's feet must be at the
    /// prefab origin, facing +Z, 1.65 m tall. Root motion must be off: the simulation owns the position.
    /// </summary>
    public sealed class AnimatedRunnerAvatar : RunnerAvatar
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int LateralId = Animator.StringToHash("Lateral");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        private static readonly int HeightId = Animator.StringToHash("HeightAboveGround");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int SlidingId = Animator.StringToHash("Sliding");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int LandId = Animator.StringToHash("Land");
        private static readonly int HardLandId = Animator.StringToHash("HardLand");
        private static readonly int DodgeId = Animator.StringToHash("Dodge");
        private static readonly int StumbleId = Animator.StringToHash("Stumble");
        private static readonly int CrashId = Animator.StringToHash("Crash");
        private static readonly int FallId = Animator.StringToHash("Fall");
        private static readonly int LedgePullId = Animator.StringToHash("LedgePull");

        [SerializeField] private Animator _animator;

        private Renderer[] _renderers;
        private int _present;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                AnimatorControllerParameter[] parameters = _animator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    _present |= Bit(parameters[i].nameHash);
                }
            }

            _renderers = GetComponentsInChildren<Renderer>();
        }

        public override void Apply(in RunnerVisualState state, float frameSeconds)
        {
            transform.SetPositionAndRotation(state.Position, state.Facing);
            if (_animator == null)
            {
                return;
            }

            SetFloat(SpeedId, state.Dead ? 0f : state.Speed);
            SetFloat(LateralId, state.VLatMax > 0f ? Mathf.Clamp(state.VLat / state.VLatMax, -1f, 1f) : 0f);
            SetFloat(VerticalSpeedId, state.Vy);
            SetFloat(HeightId, state.HeightAboveGround);
            SetBool(GroundedId, state.Grounded);
            SetBool(SlidingId, state.Sliding);
            SetBool(DeadId, state.Dead);
        }

        public override void OnRunEvent(in RunEvent runEvent)
        {
            if (_animator == null)
            {
                return;
            }

            switch (runEvent.Type)
            {
                case RunEventType.Jump:
                    Trigger(JumpId);
                    break;
                case RunEventType.Land:
                    Trigger(runEvent.Reason == (byte)LandingKind.Hard ? HardLandId : LandId);
                    break;
                case RunEventType.Dodge:
                    Trigger(DodgeId);
                    break;
                case RunEventType.Hit:
                    Trigger(runEvent.Reason == (byte)HitKind.Crash ? CrashId : StumbleId);
                    break;
                case RunEventType.Died:
                    if (runEvent.Reason == (byte)DeathCause.Fall)
                    {
                        Trigger(FallId);
                    }

                    break;
                case RunEventType.LedgeAssist:
                    Trigger(LedgePullId);
                    break;
            }
        }

        public override void SetVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }

        public override void ResetPose()
        {
            SetVisible(true);
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }
        }

        private static int Bit(int hash)
        {
            return hash == SpeedId ? 1 << 0 : hash == LateralId ? 1 << 1 : hash == VerticalSpeedId ? 1 << 2 : hash == HeightId ? 1 << 3 :
                hash == GroundedId ? 1 << 4 : hash == SlidingId ? 1 << 5 : hash == DeadId ? 1 << 6 : hash == JumpId ? 1 << 7 :
                hash == LandId ? 1 << 8 : hash == HardLandId ? 1 << 9 : hash == DodgeId ? 1 << 10 : hash == StumbleId ? 1 << 11 :
                hash == CrashId ? 1 << 12 : hash == FallId ? 1 << 13 : hash == LedgePullId ? 1 << 14 : 0;
        }

        private void SetFloat(int id, float value)
        {
            if ((_present & Bit(id)) != 0)
            {
                _animator.SetFloat(id, value);
            }
        }

        private void SetBool(int id, bool value)
        {
            if ((_present & Bit(id)) != 0)
            {
                _animator.SetBool(id, value);
            }
        }

        private void Trigger(int id)
        {
            if ((_present & Bit(id)) != 0)
            {
                _animator.SetTrigger(id);
            }
        }
    }
}
