using JungleBooze.Gameplay.Animation;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The rigged Pista (prefab <c>Assets/_Game/Prefabs/Characters/Pista.prefab</c>). A thin adapter: the plain-C#
    /// <see cref="RunnerAnimationModel"/> decides states, rates and lean; this component cross-fades the Animator,
    /// steps it manually (the Animator component stays disabled so evaluation order is fixed: state → animation →
    /// procedural), then applies the in-air foot anchor and the ponytail spring. Root motion is off: the simulation
    /// owns the position. No allocation per frame.
    /// </summary>
    public sealed class AnimatedRunnerAvatar : RunnerAvatar
    {
        private const float FixedStep = 1f / 60f;

        private static readonly int LocoBlendId = Animator.StringToHash("LocoBlend");
        private static readonly int RunRateId = Animator.StringToHash("RunRate");
        private static readonly int StateRateId = Animator.StringToHash("StateRate");
        private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

        private static readonly string[] StateNames =
        {
            "Idle", "Locomotion", "Jump", "Fall", "Slide", "Stumble", "LandHard", "Death", "Swim", "Dive", "Grab", "Hang",
            "Leap", "Underwater", "VineRelease", "Balance", "Wade",
        };

        /// <summary>
        /// Clip per state ('|' = alternatives, newest first: the 2026-10-09 clips replace Slide and Death_Backward;
        /// Swim/Dive without swim clips reuse Run/Fall and are detected by Vine_Hang). Used for lengths and detection.
        /// </summary>
        private static readonly string[] StateClips =
        {
            "Idle", "Run_Alt", "Jump", "Fall", "Slide_Clean|Slide", "Stumble", "Land_Run", "Death_Stumble|Death_Backward",
            "Swim_Surface|Vine_Hang", "Swim_Dive|Vine_Hang", "Vine_Grab", "Vine_Hang",
            "Swim_Leap", "Swim_Underwater", "Vine_Release", "Balance_Run", "Water_Wade",
        };

        /// <summary>
        /// Fallback when a controller lacks a state (followed until a state exists): Swim → Locomotion, Dive → Fall,
        /// Grab → Jump, Hang → Fall, Leap → Jump, Underwater → Dive, VineRelease → Fall, Balance/Wade → Locomotion.
        /// </summary>
        private static readonly int[] Fallback = { 0, 1, 2, 3, 4, 5, 6, 7, 1, 3, 2, 3, 2, 9, 3, 1, 1 };

        /// <summary>Hip height above the feet (pivot of the procedural body pitch), m.</summary>
        private const float HipHeight = 0.95f;
        private static readonly string[] HairBones = { "Ponytail01", "Ponytail02", "Ponytail03" };

        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _modelRoot;
        [SerializeField] private RunnerAnimationConfigAsset _config;

        private readonly int[] _stateHashes = new int[StateNames.Length];
        private readonly bool[] _hasState = new bool[StateNames.Length];
        private readonly float[] _stateLengths = new float[StateNames.Length];
        private readonly Vector3[] _hairTargets = new Vector3[3];
        private readonly Transform[] _hair = new Transform[3];
        private readonly Quaternion[] _hairRest = new Quaternion[3];

        private RunnerAnimationConfig _values;
        private RunnerAnimationModel _model;
        private SpringChain _spring;
        private Renderer[] _renderers;
        private Transform _spine;
        private Transform _chest;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private float _hairTipLength;
        private float _anchorOffset;
        private Vector3 _lastPosition;
        private Vector3 _lastVelocity;
        private bool _hasLast;
        private bool _ready;
        private float _swimLineHeight = 0.35f;

        public RunnerAnimationModel Model => _model;

        public Animator Animator => _animator;

        /// <summary>Current in-air foot anchor offset (m, ≤ 0).</summary>
        public float AnchorOffset => _anchorOffset;

        /// <summary>Editor setup: wires the prefab's references.</summary>
        public void Configure(Animator animator, Transform modelRoot, RunnerAnimationConfigAsset config)
        {
            _animator = animator;
            _modelRoot = modelRoot;
            _config = config;
        }

        public override void Bind(MovementConfig config)
        {
            Init();
            if (config == null)
            {
                return;
            }

            _values.SlideDuration = config.JumpSlide.SlideDuration;
            _swimLineHeight = config.Swim.SwimLineHeight;
            JumpArc arc = JumpArc.Measure(config.JumpSlide, FixedStep);
            _model = new RunnerAnimationModel(_values, arc.Airtime) { TraversalClips = TraversalClips };
        }

        public override void Apply(in RunnerVisualState state, float frameSeconds)
        {
            Init();
            float dt = Mathf.Max(0f, frameSeconds);
            if (_animator == null)
            {
                transform.SetPositionAndRotation(state.Position, state.Facing);
                return;
            }

            _model.Update(state, dt, LocomotionPhase());
            ref readonly RunnerAnimationOutput o = ref _model.Output;
            if (o.Changed)
            {
                int index = Resolve((int)o.State);
                float offset = o.StartNormalized * _stateLengths[index];
                if (o.Fade <= 0f)
                {
                    _animator.PlayInFixedTime(_stateHashes[index], 0, offset);
                }
                else
                {
                    _animator.CrossFadeInFixedTime(_stateHashes[index], o.Fade, 0, offset);
                }
            }

            _animator.SetFloat(LocoBlendId, o.LocoBlend);
            _animator.SetFloat(RunRateId, o.RunRate);
            _animator.SetFloat(StateRateId, o.StateRate);

            Quaternion rotation = state.Facing * Quaternion.Euler(0f, o.YawDeg, -o.RollDeg);
            Vector3 position = state.Position;
            if (_model.TraversalClips)
            {
                // Swim clips use the water surface as their origin; the simulation's body line is swimLineHeight
                // above it (IMPORT_NOTES 2026-10-09). The clips carry the body pitch; only the swing pitches here.
                position -= Vector3.up * (_swimLineHeight * o.SwimWeight);
                if (Mathf.Abs(o.BodyPitchDeg) > 0.01f)
                {
                    Vector3 pivot = position + (Vector3.up * HipHeight);
                    rotation = Quaternion.AngleAxis(o.BodyPitchDeg, rotation * Vector3.right) * rotation;
                    position = pivot - (rotation * new Vector3(0f, HipHeight, 0f));
                }
            }
            else if (Mathf.Abs(o.BodyPitchDeg) > 0.01f || o.SwimWeight > 0.001f)
            {
                // Pitch about the hips; in water the position is the body line, so the hips sit on it (spec 103 §4).
                Vector3 pivot = position + (Vector3.up * (HipHeight * (1f - o.SwimWeight)));
                rotation = Quaternion.AngleAxis(o.BodyPitchDeg, rotation * Vector3.right) * rotation;
                position = pivot - (rotation * new Vector3(0f, HipHeight, 0f));
            }

            transform.SetPositionAndRotation(position, rotation);
            RestoreHair();
            _animator.Update(dt);

            ApplyForwardLean(o.ForwardLeanDeg);
            ApplyFootAnchor(o.AnchorWeight, dt);
            ApplyHair(state.Position, dt);
        }

        public override void OnRunEvent(in RunEvent runEvent)
        {
            Init();
            _model.OnRunEvent(runEvent);
        }

        public override void SetVisible(bool visible)
        {
            Init();
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }

        public override void ResetPose()
        {
            Init();
            SetVisible(true);
            _model.Reset();
            _anchorOffset = 0f;
            _hasLast = false;
            _spring.Reset();
            if (_modelRoot != null)
            {
                _modelRoot.localPosition = Vector3.zero;
            }

            if (_animator != null)
            {
                _animator.Rebind();
                RestoreHair();
                _animator.PlayInFixedTime(_stateHashes[(int)RunnerAnimState.Idle], 0, 0f);
                _animator.Update(0f);
            }
        }

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_modelRoot == null && _animator != null)
            {
                _modelRoot = _animator.transform;
            }

            _values = _config != null ? _config.Values.Clone() : new RunnerAnimationConfig();
            _model = new RunnerAnimationModel(_values, 0.6f);
            _spring = new SpringChain(3);
            _renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < StateNames.Length; i++)
            {
                _stateHashes[i] = Animator.StringToHash(StateNames[i]);
                _stateLengths[i] = 1f;
                _hasState[i] = i < 8;
            }

            if (_animator == null)
            {
                return;
            }

            // Manual stepping: Apply() evaluates the Animator, then the procedural layers run on top.
            _animator.enabled = false;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (_animator.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
                for (int s = 0; s < StateClips.Length; s++)
                {
                    AnimationClip clip = FindClip(clips, StateClips[s]);
                    if (clip != null)
                    {
                        _stateLengths[s] = clip.length;
                    }
                }
            }

            if (_animator.isHuman)
            {
                _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
                _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            }

            for (int i = 0; i < HairBones.Length; i++)
            {
                _hair[i] = FindDeep(_animator.transform, HairBones[i]);
                if (_hair[i] != null)
                {
                    _hairRest[i] = _hair[i].localRotation;
                }
            }

            _hairTipLength = _hair[1] != null && _hair[2] != null ? Vector3.Distance(_hair[1].position, _hair[2].position) : 0.1f;
            for (int i = 8; i < StateNames.Length; i++)
            {
                _hasState[i] = _animator.runtimeAnimatorController != null && FindClip(_animator.runtimeAnimatorController.animationClips, StateClips[i]) != null;
            }

            _model.TraversalClips = TraversalClips;
            _spring.Stiffness = _values.HairStiffness;
            _spring.Damping = _values.HairDamping;
            _spring.Gravity = _values.HairGravity;
            _spring.MaxAngleDeg = _values.HairMaxAngleDeg;
            _animator.Rebind();
        }

        /// <summary>The controller has the 2026-10-09 traversal clips (Swim_Leap stands for the set).</summary>
        public bool TraversalClips => _hasState[12];

        private int Resolve(int index)
        {
            for (int guard = 0; guard < 4 && index < _hasState.Length && !_hasState[index]; guard++)
            {
                index = Fallback[index];
            }

            return index;
        }

        private static AnimationClip FindClip(AnimationClip[] clips, string names)
        {
            // Setup time only (allocates on '|').
            string[] options = names.Split('|');
            for (int o = 0; o < options.Length; o++)
            {
                for (int c = 0; c < clips.Length; c++)
                {
                    if (clips[c].name == options[o])
                    {
                        return clips[c];
                    }
                }
            }

            return null;
        }

        private float LocomotionPhase()
        {
            if (!_animator.isInitialized)
            {
                return -1f;
            }

            AnimatorStateInfo info = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != LocomotionHash)
            {
                return -1f;
            }

            float t = info.normalizedTime;
            return t - Mathf.Floor(t);
        }

        private void ApplyForwardLean(float degrees)
        {
            if (_spine == null || _chest == null || Mathf.Abs(degrees) < 0.01f)
            {
                return;
            }

            // Split over two spine joints so the back curves instead of hinging.
            Quaternion half = Quaternion.AngleAxis(degrees * 0.5f, transform.right);
            _spine.rotation = half * _spine.rotation;
            _chest.rotation = half * _chest.rotation;
        }

        private void ApplyFootAnchor(float weight, float dt)
        {
            if (_modelRoot == null || _leftFoot == null || _rightFoot == null)
            {
                return;
            }

            float target = 0f;
            if (weight > 0.001f)
            {
                float left = _modelRoot.InverseTransformPoint(_leftFoot.position).y;
                float right = _modelRoot.InverseTransformPoint(_rightFoot.position).y;
                float lift = Mathf.Clamp(Mathf.Min(left, right) - _values.AnkleRestHeight, 0f, _values.MaxAnchorDrop);
                target = -lift * weight;
            }

            _anchorOffset = RunnerAnimationModel.Damp(_anchorOffset, target, _values.AnchorHalfLife, dt);
            _modelRoot.localPosition = new Vector3(0f, _anchorOffset, 0f);
        }

        private void RestoreHair()
        {
            for (int i = 0; i < _hair.Length; i++)
            {
                if (_hair[i] != null)
                {
                    _hair[i].localRotation = _hairRest[i];
                }
            }
        }

        private void ApplyHair(Vector3 worldPosition, float dt)
        {
            if (_hair[0] == null || _hair[1] == null || _hair[2] == null)
            {
                return;
            }

            // Inertia from the body's motion along the path (jump arcs, landings, steering, speed changes).
            Vector3 inertial = Vector3.zero;
            if (dt > 0f)
            {
                Vector3 velocity = _hasLast ? (worldPosition - _lastPosition) / dt : Vector3.zero;
                Vector3 accel = _hasLast ? (velocity - _lastVelocity) / dt : Vector3.zero;
                accel = Vector3.ClampMagnitude(accel, _values.HairMaxAcceleration);
                inertial = transform.InverseTransformDirection(-accel * _values.HairInertia);
                _lastVelocity = velocity;
                _lastPosition = worldPosition;
                _hasLast = true;
            }

            Transform space = transform;
            Vector3 root = space.InverseTransformPoint(_hair[0].position);
            _hairTargets[0] = space.InverseTransformPoint(_hair[1].position);
            _hairTargets[1] = space.InverseTransformPoint(_hair[2].position);
            _hairTargets[2] = space.InverseTransformPoint(_hair[2].TransformPoint(new Vector3(0f, _hairTipLength, 0f)));
            _spring.Step(root, _hairTargets, space.InverseTransformDirection(Vector3.down), inertial, dt);

            for (int i = 0; i < 3; i++)
            {
                Transform bone = _hair[i];
                Vector3 child = i < 2 ? _hair[i + 1].position : bone.TransformPoint(new Vector3(0f, _hairTipLength, 0f));
                Vector3 from = child - bone.position;
                Vector3 to = space.TransformPoint(_spring[i]) - bone.position;
                if (from.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f)
                {
                    bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
                }
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
