using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box stand-in for the companion macaw (GDD 15.1, style guide 2.4 and 6.2): violet teardrop body, orange
    /// head and chest wrapping round the nape (reads orange from behind), cream face patch, dark beak, violet wings
    /// with the lighter underwing lining, long violet tail with the turquoise tip. Flies at its home position
    /// (2.0 m ahead of HERO, 4.2 m above the track, on HERO's lane with the camera's smoothing), swoops over the
    /// lane that matters on a "Vine!" / "Look out!" call-out (staying at or above the swoop height), loops on a
    /// cheer, dives to HERO's wrists and carries her during Lift and a Continue rescue, and perches on her head
    /// after a death. Wings beat in real time. Pivot at the body center. No allocations per frame.
    /// </summary>
    public sealed class CompanionView : MonoBehaviour, IRunView
    {
        private const float CarryAboveFeetM = RunnerView.HandAboveFeetM + 0.35f;
        private const float PerchAboveFeetM = 2.0f;
        private const float MinPerchHeightM = 0.8f;
        private const float MoveSmoothSeconds = 0.08f;
        private const float RescueSeconds = 0.9f;
        private const float LoopSeconds = 0.6f;
        private const float FlapAmplitudeDeg = 40f;
        private const float CarryFlapFactor = 1.6f;
        private const float CarryWingScale = 1.2f;
        private const float FoldedWingDeg = -70f;

        // Real model (optional): a static mesh without a wing rig, so flight is procedural (pitch, roll, bob).
        private const string ModelResourcePath = "Characters/Duko/Duko";
        private const float ModelHeightM = 0.55f;
        private const float ModelFlightPitchDeg = 70f;
        private const float ModelFlapRollDeg = 14f;
        private const float ModelFlapBobM = 0.04f;
        private const float ModelPoseSmoothSeconds = 0.1f;

        private RunnerConfig _runnerConfig;
        private CompanionConfig _config;
        private Transform _model;
        private Transform _leftWing;
        private Transform _rightWing;
        private Vector3 _position;
        private Vector3 _velocity;
        private float _followX;
        private float _followVelX;
        private float _flapClock;
        private float _swoopLeft;
        private float _swoopLaneX;
        private float _loopLeft;
        private float _rescueLeft;
        private bool _snap;
        private Transform _visualHolder;
        private float _visualPitch;

        /// <summary>World position of the bird (where its speech bubble is anchored).</summary>
        public Vector3 BubbleAnchor => _position + new Vector3(0f, 0.55f, 0f);

        /// <summary>The bird is carrying HERO (Lift or a Continue rescue).</summary>
        public bool IsCarrying { get; private set; }

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, CompanionConfig config)
        {
            _runnerConfig = runnerConfig;
            _config = config ?? CompanionConfig.CreateDefault();

            _model = new GameObject("MacawModel").transform;
            _model.SetParent(transform, false);

            GameObject prefab = Resources.Load<GameObject>(ModelResourcePath);
            if (prefab != null)
            {
                GameObject visual = Instantiate(prefab, _model);
                visual.name = "DukoVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                RunnerView.FitToHeight(visual, ModelHeightM);
                // Pivot at the body center: the fitted model stands on y = 0, so lift it into a holder.
                Transform holder = new GameObject("DukoHolder").transform;
                holder.SetParent(_model, false);
                visual.transform.SetParent(holder, true);
                visual.transform.localPosition -= new Vector3(0f, ModelHeightM * 0.5f, 0f);
                _visualHolder = holder;
                return;
            }

            // Body: violet teardrop (longer than wide), chest and head orange, wrapping round the back of the head.
            kit.Create(PrimitiveType.Sphere, "Body", _model, StylePalette.MacawViolet, new Vector3(0f, 0f, 0f), new Vector3(0.26f, 0.24f, 0.44f));
            kit.Create(PrimitiveType.Sphere, "Chest", _model, StylePalette.MacawOrange, new Vector3(0f, -0.02f, 0.12f), new Vector3(0.22f, 0.2f, 0.22f));
            kit.Create(PrimitiveType.Sphere, "Head", _model, StylePalette.MacawOrange, new Vector3(0f, 0.1f, 0.24f), new Vector3(0.2f, 0.2f, 0.2f));
            kit.Create(PrimitiveType.Sphere, "FacePatch", _model, StylePalette.MacawFace, new Vector3(0f, 0.11f, 0.32f), new Vector3(0.12f, 0.1f, 0.06f));
            kit.Create(PrimitiveType.Cube, "Beak", _model, StylePalette.MacawBeak, new Vector3(0f, 0.07f, 0.37f), new Vector3(0.06f, 0.07f, 0.08f));

            // Tail: long and tapered, turquoise tip band (the treasure thief's mark).
            kit.Create(PrimitiveType.Cube, "Tail", _model, StylePalette.MacawViolet, new Vector3(0f, -0.02f, -0.38f), new Vector3(0.1f, 0.03f, 0.4f));
            kit.Create(PrimitiveType.Cube, "TailTip", _model, StylePalette.MacawTailTip, new Vector3(0f, -0.02f, -0.62f), new Vector3(0.1f, 0.031f, 0.1f));

            _leftWing = CreateWing(kit, "LeftWing", -1f);
            _rightWing = CreateWing(kit, "RightWing", 1f);
        }

        public void BeginRun(GameSession session)
        {
            _swoopLeft = 0f;
            _loopLeft = 0f;
            _rescueLeft = 0f;
            _velocity = Vector3.zero;
            _followVelX = 0f;
            _snap = true;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (_model == null)
            {
                return;
            }

            switch (e.Type)
            {
                case RunnerEventType.CompanionCallout:
                    var id = (CompanionCalloutId)e.Value;
                    if (CompanionCalloutKeys.IsCheer(id))
                    {
                        _loopLeft = LoopSeconds;
                    }
                    else if (e.Lane < _runnerConfig.LaneCount)
                    {
                        _swoopLeft = _config.SwoopSeconds;
                        _swoopLaneX = _runnerConfig.LaneCenterX(e.Lane);
                    }

                    break;

                case RunnerEventType.Revived:
                    _rescueLeft = RescueSeconds;
                    _snap = true;
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
            RunnerInterpolation.Evaluate(runner.Previous, current, alpha, out float x, out float y, out double zd);
            float z = (float)zd;
            bool frozen = session.Phase == SessionPhase.Paused || session.InHitPause;
            float dt = frozen ? 0f : realDeltaSeconds;

            // Home lane follow with the camera's smoothing.
            if (_snap || dt <= 0f)
            {
                if (_snap)
                {
                    _followX = x;
                }
            }
            else
            {
                _followX = Mathf.SmoothDamp(_followX, x, ref _followVelX, _config.FollowSmoothSeconds, Mathf.Infinity, dt);
            }

            bool lifted = current.Locomotion == Locomotion.Lifted;
            IsCarrying = lifted || _rescueLeft > 0f;
            Vector3 target;
            if (IsCarrying)
            {
                // Holding HERO's wrists above her raised hands.
                target = new Vector3(x, y + CarryAboveFeetM, z + 0.1f);
            }
            else if (current.IsDead)
            {
                // Perched on HERO's head with a sympathetic look (never into the pit).
                target = new Vector3(x, Mathf.Max(y + PerchAboveFeetM, MinPerchHeightM), z);
            }
            else
            {
                target = new Vector3(_followX, _config.HomeHeightM, z + _config.HomeAheadM);
                if (_swoopLeft > 0f && _config.SwoopSeconds > 0f)
                {
                    float u = 1f - _swoopLeft / _config.SwoopSeconds;
                    float s = Mathf.Sin(u * Mathf.PI);
                    target.x = Mathf.Lerp(_followX, _swoopLaneX, s);
                    target.y = Mathf.Lerp(_config.HomeHeightM, Mathf.Max(_config.SwoopHeightM, 0f), s);
                }
            }

            if (_snap)
            {
                _position = target;
                _velocity = Vector3.zero;
                _snap = false;
            }
            else if (dt > 0f)
            {
                // Lateral and vertical moves are smoothed (the dive reads as a dive); z follows exactly so the bird
                // never drifts behind the camera.
                float px = Mathf.SmoothDamp(_position.x, target.x, ref _velocity.x, MoveSmoothSeconds, Mathf.Infinity, dt);
                float py = Mathf.SmoothDamp(_position.y, target.y, ref _velocity.y, MoveSmoothSeconds, Mathf.Infinity, dt);
                _position = new Vector3(px, py, target.z);
            }
            else
            {
                _position.z = target.z;
            }

            transform.localPosition = _position;

            // Wing beat and the cheer loop-the-loop.
            float flapHz = IsCarrying ? _config.FlapHz * CarryFlapFactor : _config.FlapHz;
            _flapClock += dt * flapHz;
            if (_flapClock > 1000f)
            {
                _flapClock -= 1000f;
            }

            bool perched = current.IsDead && !IsCarrying;
            float wingAngle = perched
                ? FoldedWingDeg
                : FlapAmplitudeDeg * Mathf.Sin(_flapClock * 2f * Mathf.PI);
            if (_visualHolder != null)
            {
                // Static model: body pitches into flight, rolls and bobs with the beat; upright when perched.
                float targetPitch = perched ? 0f : ModelFlightPitchDeg;
                float pk = dt <= 0f ? 0f : 1f - Mathf.Exp(-dt / ModelPoseSmoothSeconds);
                _visualPitch += (targetPitch - _visualPitch) * pk;
                float beat = perched ? 0f : Mathf.Sin(_flapClock * 2f * Mathf.PI);
                _visualHolder.localRotation = Quaternion.Euler(_visualPitch, 0f, ModelFlapRollDeg * beat);
                _visualHolder.localPosition = new Vector3(0f, ModelFlapBobM * beat, 0f);
            }
            else
            {
                _leftWing.localRotation = Quaternion.Euler(0f, 0f, -wingAngle);
                _rightWing.localRotation = Quaternion.Euler(0f, 0f, wingAngle);
                float wingScale = IsCarrying ? CarryWingScale : 1f;
                _leftWing.localScale = new Vector3(wingScale, 1f, wingScale);
                _rightWing.localScale = new Vector3(wingScale, 1f, wingScale);
            }

            float pitch = 0f;
            if (_loopLeft > 0f)
            {
                pitch = -360f * (1f - _loopLeft / LoopSeconds);
                _loopLeft -= dt;
            }

            _model.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (_swoopLeft > 0f)
            {
                _swoopLeft -= dt;
            }

            if (_rescueLeft > 0f)
            {
                _rescueLeft -= dt;
            }
        }

        private Transform CreateWing(GrayBoxKit kit, string name, float side)
        {
            float half = _config.WingspanM * 0.5f;
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(_model, false);
            pivot.localPosition = new Vector3(side * 0.1f, 0.06f, 0.04f);
            float length = half - 0.1f;
            kit.Create(PrimitiveType.Cube, "Feathers", pivot, StylePalette.MacawViolet, new Vector3(side * length * 0.5f, 0f, 0f), new Vector3(length, 0.03f, 0.24f));
            kit.Create(PrimitiveType.Cube, "Lining", pivot, StylePalette.MacawUnderwing, new Vector3(side * length * 0.5f, -0.02f, -0.01f), new Vector3(length * 0.9f, 0.012f, 0.2f));
            return pivot;
        }
    }
}
