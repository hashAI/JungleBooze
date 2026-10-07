using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box stand-in for Pista: a map-cloth capsule body with skin head and dark hair, the brown "X" on her back
    /// (two thin crossed cubes, facing the camera), the teal sash band below it, a satchel at the hip, and a flat
    /// blob shadow that shrinks with height. Squashes while sliding, wobbles toward the blocked side on a lane bump
    /// (spec 001 10.8), bobs while running and tips over on death. On a vine (GDD 7.3) the arms reach up to the
    /// hand hold and the body hangs from the hand along the pendulum angle; in the launch after a release she
    /// leans forward. Pivot at the feet. No allocations per frame.
    /// </summary>
    public sealed class RunnerView : MonoBehaviour, IRunView
    {
        // Gray-box proportions (not tuning; replaced by the real model).
        private const float BodyHeightM = 1.4f;
        private const float BodyRadiusM = 0.4f;
        private const float HeadDiameterM = 0.42f;
        private const float SquashSmoothSeconds = 0.05f;
        private const float SlideWidenFactor = 1.2f;
        private const float BobHeightM = 0.05f;
        private const float BobStrideM = 1.6f;
        private const float ShadowWidthM = 0.9f;
        private const float ShadowDepthM = 0.6f;
        private const float ShadowLiftM = 0.012f;
        private const float DeathTiltDeg = 75f;
        private const float StumbleSeconds = 0.35f;
        private const float StumbleHopM = 0.2f;
        private const float StumbleTiltDeg = 20f;

        /// <summary>Height of the vine hand hold above the feet (gray-box proportion; the vine view uses the same).</summary>
        public const float HandAboveFeetM = 2.0f;

        private const float ShoulderHeightM = 1.3f;
        private const float FlightLeanDeg = 18f;

        // Real model (optional). Loaded from Resources; when absent the gray-box shapes are built instead.
        private const string ModelResourcePath = "Characters/Pista/Pista";
        private const string ClipResourcePrefix = "Characters/Pista/Pista_";
        private const float ClipBlendSeconds = 0.08f;

        private static readonly string[] ClipNames = { "run", "jump", "slide", "stumble", "idle" };

        private enum Pose
        {
            Run = 0,
            Jump = 1,
            Slide = 2,
            Stumble = 3,
            Idle = 4,
        }

        private RunnerConfig _runnerConfig;
        private RunnerPresentationConfig _presentation;
        private GameObject _visual;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly AnimationClipPlayable[] _clipPlayables = new AnimationClipPlayable[5];
        private readonly bool[] _clipLoaded = new bool[5];
        private readonly float[] _clipWeights = new float[5];
        private bool _graphReady;
        private Pose _pose = Pose.Idle;
        private Transform _model;
        private Transform _shadow;
        private GameObject _arms;
        private float _squash = 1f;
        private float _wobbleLeft;
        private float _wobbleDir;
        private bool _dead;
        private float _stumbleLeft;

        /// <summary>Rendered position of HERO's feet (after interpolation, before wobble and bob).</summary>
        public Vector3 RenderedFeetPosition { get; private set; }

        /// <summary>Current vertical scale of the model (1 standing, &lt; 1 sliding).</summary>
        public float CurrentSquash => _squash;

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, RunnerPresentationConfig presentation)
        {
            _runnerConfig = runnerConfig;
            _presentation = presentation;

            _model = new GameObject("PistaModel").transform;
            _model.SetParent(transform, false);

            if (!TryBuildModel())
            {
                BuildGrayBox(kit);
            }

            _shadow = kit.Create(
                PrimitiveType.Cylinder,
                "BlobShadow",
                transform,
                StylePalette.BlobShadow,
                new Vector3(0f, ShadowLiftM, 0f),
                new Vector3(ShadowWidthM, 0.002f, ShadowDepthM));
        }

        /// <summary>True when the real Pista model was found and is in use (false: gray-box fallback).</summary>
        public bool UsesModel => _visual != null;

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        /// <summary>
        /// Instantiates the Pista prefab from Resources and fits it to the standing hitbox height with its pivot at the
        /// feet. Returns false (and builds nothing) when the asset is missing. Setup-time only.
        /// </summary>
        private bool TryBuildModel()
        {
            GameObject prefab = Resources.Load<GameObject>(ModelResourcePath);
            if (prefab == null)
            {
                return false;
            }

            _visual = Instantiate(prefab, _model);
            _visual.name = "PistaVisual";
            _visual.transform.localPosition = Vector3.zero;
            _visual.transform.localRotation = Quaternion.identity;
            FitToHeight(_visual, _runnerConfig.StandingHeightM);
            BuildAnimationGraph();
            return true;
        }

        /// <summary>
        /// Scales <paramref name="visual"/> so its renderers are <paramref name="heightM"/> tall, with the lowest point
        /// at y = 0 and the horizontal center at x = z = 0 of the parent. Shared with the companion view.
        /// </summary>
        internal static void FitToHeight(GameObject visual, float heightM)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y <= 0.0001f)
            {
                return;
            }

            Transform t = visual.transform;
            float scale = heightM / bounds.size.y;
            t.localScale = t.localScale * scale;
            Vector3 offset = new Vector3(bounds.center.x - t.position.x, bounds.min.y - t.position.y, bounds.center.z - t.position.z);
            t.localPosition = -offset * scale;
        }

        private void BuildAnimationGraph()
        {
            Animator animator = _visual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = _visual.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("PistaAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, ClipNames.Length);
            bool any = false;
            for (int i = 0; i < ClipNames.Length; i++)
            {
                AnimationClip clip = Resources.Load<AnimationClip>(ClipResourcePrefix + ClipNames[i]);
                if (clip == null)
                {
                    continue;
                }

                _clipPlayables[i] = AnimationClipPlayable.Create(_graph, clip);
                _graph.Connect(_clipPlayables[i], 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _clipLoaded[i] = true;
                any = true;
            }

            if (!any)
            {
                _graph.Destroy();
                return;
            }

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Pista", animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
            _graphReady = true;
        }

        private Pose ChoosePose(GameSession session, RunnerState current)
        {
            if (current.IsDead || _dead || _stumbleLeft > 0f)
            {
                return Pose.Stumble;
            }

            if (session.Phase == SessionPhase.Ready || session.Phase == SessionPhase.Menu)
            {
                return Pose.Idle;
            }

            switch (current.Locomotion)
            {
                case Locomotion.Sliding:
                    return Pose.Slide;
                case Locomotion.Running:
                    return Pose.Run;
                default:
                    return Pose.Jump;
            }
        }

        private int ResolveClip(Pose pose)
        {
            int index = (int)pose;
            if (_clipLoaded[index])
            {
                return index;
            }

            // Fall back to the run clip, then to any clip that exists.
            if (_clipLoaded[(int)Pose.Run])
            {
                return (int)Pose.Run;
            }

            for (int i = 0; i < _clipLoaded.Length; i++)
            {
                if (_clipLoaded[i])
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateAnimation(GameSession session, RunnerState current, float dt)
        {
            if (!_graphReady)
            {
                return;
            }

            Pose wanted = ChoosePose(session, current);
            if (wanted != _pose)
            {
                _pose = wanted;
                int entered = ResolveClip(wanted);
                if (entered >= 0 && wanted != Pose.Run && wanted != Pose.Idle)
                {
                    // One-shot clips restart on entry.
                    _clipPlayables[entered].SetTime(0.0);
                }
            }

            int active = ResolveClip(_pose);
            float k = dt <= 0f ? 0f : 1f - Mathf.Exp(-dt / ClipBlendSeconds);
            for (int i = 0; i < _clipWeights.Length; i++)
            {
                if (!_clipLoaded[i])
                {
                    continue;
                }

                _clipWeights[i] += ((i == active ? 1f : 0f) - _clipWeights[i]) * k;
                _mixer.SetInputWeight(i, _clipWeights[i]);
            }

            _graph.Evaluate(dt);
        }

        private void BuildGrayBox(GrayBoxKit kit)
        {
            float bodyDiameter = BodyRadiusM * 2f;
            kit.Create(
                PrimitiveType.Capsule,
                "Body",
                _model,
                StylePalette.PistaMapCloth,
                new Vector3(0f, BodyHeightM * 0.5f, 0f),
                new Vector3(bodyDiameter, BodyHeightM * 0.5f, bodyDiameter));

            kit.Create(
                PrimitiveType.Sphere,
                "Head",
                _model,
                StylePalette.PistaSkin,
                new Vector3(0f, BodyHeightM + HeadDiameterM * 0.35f, 0f),
                new Vector3(HeadDiameterM, HeadDiameterM, HeadDiameterM));

            kit.Create(
                PrimitiveType.Sphere,
                "Hair",
                _model,
                StylePalette.PistaHair,
                new Vector3(0f, BodyHeightM + HeadDiameterM * 0.5f, -0.04f),
                new Vector3(HeadDiameterM * 1.06f, HeadDiameterM * 0.7f, HeadDiameterM * 1.06f));

            // The back "X" (camera looks along +z, so the back faces -z).
            float backZ = -(BodyRadiusM + 0.005f);
            float xCenterY = BodyHeightM * 0.68f;
            Vector3 barScale = new Vector3(0.06f, 0.42f, 0.02f);
            Transform barA = kit.Create(PrimitiveType.Cube, "BackX_A", _model, StylePalette.PistaMapLines, new Vector3(0f, xCenterY, backZ), barScale);
            barA.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Transform barB = kit.Create(PrimitiveType.Cube, "BackX_B", _model, StylePalette.PistaMapLines, new Vector3(0f, xCenterY, backZ), barScale);
            barB.localRotation = Quaternion.Euler(0f, 0f, -45f);

            // Teal sash band below the X (owner decision: sash below the X).
            kit.Create(
                PrimitiveType.Cylinder,
                "Sash",
                _model,
                StylePalette.PistaTealSash,
                new Vector3(0f, BodyHeightM * 0.42f, 0f),
                new Vector3(bodyDiameter * 1.05f, 0.05f, bodyDiameter * 1.05f));

            // Satchel on the right hip with its saffron strap tab.
            kit.Create(
                PrimitiveType.Cube,
                "Satchel",
                _model,
                StylePalette.PistaSatchel,
                new Vector3(BodyRadiusM + 0.06f, BodyHeightM * 0.33f, 0f),
                new Vector3(0.12f, 0.24f, 0.3f));
            kit.Create(
                PrimitiveType.Cube,
                "SatchelStrap",
                _model,
                StylePalette.PistaSatchelStrap,
                new Vector3(BodyRadiusM + 0.04f, BodyHeightM * 0.5f, 0f),
                new Vector3(0.05f, 0.3f, 0.08f));

            // Arms raised to the hand hold, shown only while on a vine.
            float armLength = HandAboveFeetM - ShoulderHeightM;
            _arms = kit.Create(
                PrimitiveType.Cube,
                "ArmsUp",
                _model,
                StylePalette.PistaSkin,
                new Vector3(0f, ShoulderHeightM + armLength * 0.5f, 0f),
                new Vector3(0.42f, armLength, 0.12f)).gameObject;
            _arms.SetActive(false);
        }

        public void BeginRun(GameSession session)
        {
            _squash = 1f;
            _wobbleLeft = 0f;
            _wobbleDir = 0f;
            _dead = false;
            _stumbleLeft = 0f;
            if (_model != null)
            {
                _model.localRotation = Quaternion.identity;
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            switch (e.Type)
            {
                case RunnerEventType.LaneBlocked:
                    _wobbleLeft = _presentation.LaneBumpWobbleMs / 1000f;
                    _wobbleDir = e.Dir;
                    break;

                case RunnerEventType.Stumbled:
                    _stumbleLeft = StumbleSeconds;
                    break;

                case RunnerEventType.Died:
                    _dead = true;
                    break;

                case RunnerEventType.Revived:
                    // GDD 14.4: back on her feet after a Continue.
                    _dead = false;
                    _stumbleLeft = 0f;
                    break;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_model == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerState current = runner.Current;
            RunnerInterpolation.Evaluate(runner.Previous, current, alpha, out float x, out float y, out double z);
            RenderedFeetPosition = new Vector3(x, y, (float)z);

            // Slide squash: hitbox ratio, smoothed a little in real time.
            float targetSquash = current.Locomotion == Locomotion.Sliding
                ? _runnerConfig.SlidingHeightM / _runnerConfig.StandingHeightM
                : 1f;
            if (realDeltaSeconds <= 0f)
            {
                _squash = targetSquash;
            }
            else
            {
                float k = 1f - Mathf.Exp(-realDeltaSeconds / SquashSmoothSeconds);
                _squash += (targetSquash - _squash) * k;
            }

            float widen = 1f + (SlideWidenFactor - 1f) * (1f - _squash) / (1f - _runnerConfig.SlidingHeightM / _runnerConfig.StandingHeightM);
            if (_visual == null)
            {
                _model.localScale = new Vector3(widen, _squash, widen);
            }

            // Lane bump wobble: a half-sine push toward the blocked side; the camera does not move.
            float wobble = 0f;
            float wobbleSeconds = _presentation.LaneBumpWobbleMs / 1000f;
            if (_wobbleLeft > 0f && wobbleSeconds > 0f)
            {
                float u = 1f - _wobbleLeft / wobbleSeconds;
                wobble = _wobbleDir * _presentation.LaneBumpWobbleM * Mathf.Sin(u * Mathf.PI);
                if (!session.InHitPause)
                {
                    _wobbleLeft -= realDeltaSeconds;
                }
            }

            // Run bob from distance (no clock needed, freezes with the simulation).
            float bob = 0f;
            if (_visual == null && current.Locomotion == Locomotion.Running)
            {
                bob = BobHeightM * Mathf.Abs(Mathf.Sin((float)(z % (BobStrideM * 2.0)) * Mathf.PI / BobStrideM));
            }

            // Stumble: a short hop and forward lurch (no tint: hazard red is banned on the hero).
            float stumble = 0f;
            if (_stumbleLeft > 0f)
            {
                stumble = Mathf.Sin((1f - _stumbleLeft / StumbleSeconds) * Mathf.PI);
                if (!session.InHitPause)
                {
                    _stumbleLeft -= realDeltaSeconds;
                }
            }

            transform.localPosition = new Vector3(x, 0f, (float)z);
            // On a vine, or held by the wrists during the companion's Lift (no swing angle then).
            bool carried = current.Locomotion == Locomotion.Carried || current.Locomotion == Locomotion.Lifted;
            if (_arms != null && _arms.activeSelf != carried)
            {
                _arms.SetActive(carried);
            }

            if (carried)
            {
                // Hang from the hand: rotate about the hand hold by the pendulum angle (feet swing forward).
                RunnerState previous = runner.Previous;
                float angle = previous.Locomotion == Locomotion.Carried
                    ? Mathf.Lerp(previous.SwingAngleRad, current.SwingAngleRad, alpha)
                    : current.SwingAngleRad;
                Quaternion hang = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
                Vector3 hand = new Vector3(0f, HandAboveFeetM, 0f);
                _model.localRotation = hang;
                _model.localPosition = new Vector3(0f, y, 0f) + hand - hang * hand;
            }
            else
            {
                _model.localPosition = new Vector3(wobble, y + bob + StumbleHopM * stumble, 0f);
                float lean = current.InVineFlight ? FlightLeanDeg : 0f;
                _model.localRotation = _dead
                    ? Quaternion.Euler(DeathTiltDeg, 0f, 0f)
                    : Quaternion.Euler(StumbleTiltDeg * stumble + lean, 0f, 0f);
            }

            float apex = _runnerConfig.JumpApexHeightM > 0f ? _runnerConfig.JumpApexHeightM : 1f;
            float shadowScale = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(y / apex));
            _shadow.localScale = new Vector3(ShadowWidthM * shadowScale, 0.002f, ShadowDepthM * shadowScale);
            _shadow.gameObject.SetActive(y > -0.05f);

            bool frozen = session.Phase == SessionPhase.Paused || session.InHitPause;
            UpdateAnimation(session, current, frozen ? 0f : realDeltaSeconds);
        }
    }
}
