using System;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Views;

namespace JungleBooze.Gameplay.Animation
{
    /// <summary>
    /// Maps the interpolated simulation state and simulation events to Animator commands (presentation only, plain
    /// C#, EditMode-tested). One-shot states (jump, slide, stumble, hard landing, death) start on the frame their
    /// event arrives, so the visible response is never later than the simulation's (same tick). The run cycle's
    /// playback rate follows the ground speed with a contact-phase warp (see <see cref="StrideWarp"/>), and the
    /// body yaws and banks into steering. Never writes simulation state.
    /// </summary>
    public sealed class RunnerAnimationModel
    {
        private const float FramesPerSecond = 30f;

        private readonly RunnerAnimationConfig _c;
        private readonly float _jumpAirtime;
        private RunnerAnimationOutput _out;
        private RunnerAnimState _state;
        private float _clipFrame;
        private float _timeInState;
        private float _airNoJump;
        private float _dt;

        private bool _evJump;
        private bool _evSlide;
        private bool _evStumble;
        private bool _evLand;
        private bool _evLandHard;
        private bool _evLedge;
        private bool _evLeap;
        private bool _evSplash;
        private float _bodyPitch;
        private float _swimWeight;
        private float _sinceDodge = 99f;
        private int _dodgeDir;
        private float _sinceEdge = 99f;
        private int _edgeSide;

        private float _yaw;
        private float _roll;
        private float _anchor;
        private float _forwardLean;
        private bool _canopy;
        private float _wade;

        /// <param name="config">Presentation tuning.</param>
        /// <param name="jumpAirtime">The simulation's flat-ground jump airtime (s), from <see cref="JumpArc"/>.</param>
        public RunnerAnimationModel(RunnerAnimationConfig config, float jumpAirtime)
        {
            _c = config ?? throw new ArgumentNullException(nameof(config));
            _jumpAirtime = Math.Max(0.1f, jumpAirtime);
            Reset();
        }

        public RunnerAnimationConfig Config => _c;

        public ref readonly RunnerAnimationOutput Output => ref _out;

        public RunnerAnimState State => _state;

        /// <summary>
        /// The controller has the traversal clips (Swim_Surface, Swim_Dive, Swim_Underwater, Swim_Leap, Vine_Release,
        /// Balance_Run, Water_Wade): water poses come from the clips (no procedural body pitch) and the run switches to
        /// Balance on beams and Wade in shallow water. Off = the procedural fallbacks.
        /// </summary>
        public bool TraversalClips { get; set; }

        /// <summary>Seconds in the current state.</summary>
        public float TimeInState => _timeInState;

        /// <summary>New run: idle pose, all transient state cleared.</summary>
        public void Reset()
        {
            _state = RunnerAnimState.Idle;
            _clipFrame = 0f;
            _timeInState = 0f;
            _airNoJump = 0f;
            ClearEvents();
            _sinceDodge = 99f;
            _sinceEdge = 99f;
            _yaw = 0f;
            _roll = 0f;
            _anchor = 0f;
            _forwardLean = 0f;
            _bodyPitch = 0f;
            _swimWeight = 0f;
            _out = new RunnerAnimationOutput
            {
                State = RunnerAnimState.Idle,
                Changed = true,
                Fade = 0f,
                StartNormalized = 0f,
                RunRate = 1f,
                StateRate = 1f,
            };
        }

        public void OnRunEvent(in RunEvent e)
        {
            switch (e.Type)
            {
                case RunEventType.Jump:
                    _evJump = true;
                    break;
                case RunEventType.SlideStart:
                    _evSlide = true;
                    break;
                case RunEventType.Land:
                    _evLand = true;
                    _evLandHard |= e.Reason == (byte)LandingKind.Hard;
                    break;
                case RunEventType.LedgeAssist:
                    _evLedge = true;
                    break;
                case RunEventType.Leap:
                    _evLeap = true;
                    break;
                case RunEventType.Splash:
                    _evSplash = true;
                    break;
                case RunEventType.Hit:
                    if (e.Reason != (byte)HitKind.Crash)
                    {
                        _evStumble = true;
                    }

                    break;
                case RunEventType.Dodge:
                    _sinceDodge = 0f;
                    _dodgeDir = e.Reason == 1 ? 1 : -1;
                    break;
                case RunEventType.EdgeBrush:
                    _sinceEdge = 0f;
                    _edgeSide = e.Reason == 1 ? 1 : -1;
                    break;
            }
        }

        /// <summary>
        /// Advances one rendered frame.
        /// </summary>
        /// <param name="s">Interpolated runner state.</param>
        /// <param name="dt">Frame time (s).</param>
        /// <param name="locoPhase">Current normalized phase (0…1) of the locomotion cycle as the Animator reports it;
        /// negative if unknown (the warp then uses the uniform rate).</param>
        public void Update(in RunnerVisualState s, float dt, float locoPhase)
        {
            dt = Math.Max(0f, dt);
            _dt = dt;
            _out.Changed = false;
            _canopy = s.Canopy;
            _wade = s.WadeDepth;
            _timeInState += dt;
            _clipFrame += _out.StateRate * dt * FramesPerSecond;
            _sinceDodge += dt;
            _sinceEdge += dt;

            if (!ResolveTraversal(s))
            {
                ResolveState(s);
            }

            UpdateRates(s, locoPhase);
            UpdateLean(s, dt);
            UpdateBody(s, dt);
            ClearEvents();
        }

        private void ResolveState(in RunnerVisualState s)
        {
            if (s.Dead)
            {
                if (s.Cause == DeathCause.Fall)
                {
                    Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
                }
                else
                {
                    Enter(RunnerAnimState.Death, _c.FadeToDeath, 0f, _c.DeathRate, false);
                }

                return;
            }

            if (_state == RunnerAnimState.Death)
            {
                // Revive: back to the run (restart goes through Reset()).
                Enter(s.Speed < _c.IdleSpeed ? RunnerAnimState.Idle : RunnerAnimState.Locomotion, _c.FadeLandHardToRun, 0f, 1f, true);
            }

            // Discrete events first: they are the same-tick response to input.
            if (_evJump)
            {
                float start = _c.JumpTakeoffFrame / FramesPerSecond / _c.JumpClipLength;
                float rate = (_c.JumpTouchdownFrame - _c.JumpTakeoffFrame) / FramesPerSecond / _jumpAirtime;
                Enter(RunnerAnimState.Jump, _c.FadeToJump, start, rate, true);
                _airNoJump = 0f;
                return;
            }

            if (_evSlide || (s.Sliding && _state != RunnerAnimState.Slide))
            {
                Enter(RunnerAnimState.Slide, _c.FadeToSlide, _c.SlideStartFrame / FramesPerSecond / _c.SlideClipLength, SlideDropRate(), true);
                return;
            }

            if (_evLedge || (_evLand && _evLandHard))
            {
                Enter(RunnerAnimState.LandHard, _c.FadeToLandHard, _c.LandHardStartFrame / FramesPerSecond / _c.LandHardClipLength, _c.LandHardRate, true);
                return;
            }

            if (_evLand && s.Grounded)
            {
                Enter(RunnerAnimState.Locomotion, _c.FadeLandToRun, _c.LandingRunPhase, 1f, true);
                _airNoJump = 0f;
                return;
            }

            if (_evStumble && s.Grounded && !s.Sliding)
            {
                Enter(RunnerAnimState.Stumble, _c.FadeToStumble, _c.StumbleStartFrame / FramesPerSecond / _c.StumbleClipLength, _c.StumbleRate, true);
                return;
            }

            // Continuous conditions.
            switch (_state)
            {
                case RunnerAnimState.Slide:
                    if (!s.Sliding)
                    {
                        if (s.Grounded)
                        {
                            Enter(RunnerAnimState.Locomotion, _c.FadeSlideToRun, 0f, 1f, false);
                        }
                        else
                        {
                            Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
                        }
                    }

                    return;
                case RunnerAnimState.Jump:
                    if (s.Grounded)
                    {
                        Enter(RunnerAnimState.Locomotion, _c.FadeLandToRun, _c.LandingRunPhase, 1f, false);
                    }
                    else if (_clipFrame >= _c.JumpTouchdownFrame || s.FastFalling)
                    {
                        Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
                    }

                    return;
                case RunnerAnimState.Fall:
                    if (s.Grounded)
                    {
                        Enter(RunnerAnimState.Locomotion, _c.FadeLandToRun, _c.LandingRunPhase, 1f, false);
                    }

                    return;
                case RunnerAnimState.VineRelease:
                    if (s.Grounded)
                    {
                        Enter(RunnerAnimState.Locomotion, _c.FadeLandToRun, _c.LandingRunPhase, 1f, false);
                    }
                    else if (_timeInState >= _c.VineReleaseTime)
                    {
                        Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
                    }

                    return;
                case RunnerAnimState.Stumble:
                    if (_timeInState >= _c.StumbleTime)
                    {
                        EnterGroundOrAir(s, _c.FadeStumbleToRun);
                    }

                    return;
                case RunnerAnimState.LandHard:
                    if (_timeInState >= _c.LandHardTime)
                    {
                        EnterGroundOrAir(s, _c.FadeLandHardToRun);
                    }

                    return;
            }

            // Idle / Locomotion / walk-off (small step-downs keep running).
            if (!s.Grounded)
            {
                _airNoJump += _dt;
                if (_airNoJump >= _c.WalkOffFallDelay)
                {
                    Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
                }

                return;
            }

            _airNoJump = 0f;
            if (_state == RunnerAnimState.Idle && s.Speed >= _c.IdleSpeed)
            {
                Enter(RunnerAnimState.Locomotion, _c.FadeIdleToRun, 0f, 1f, false);
            }
            else if (IsGroundLoop(_state) && s.Speed < _c.IdleSpeed)
            {
                Enter(RunnerAnimState.Idle, _c.FadeToIdle, 0f, 1f, false);
            }
            else if (IsGroundLoop(_state) && GroundLoop() != _state)
            {
                // Onto or off the beams, into or out of shallow water.
                Enter(RunnerAnimState.Locomotion, _c.FadeTraversalLoop, 0f, 1f, false);
            }
        }

        /// <summary>
        /// Swim, dive, leap and vine poses (spec 103). Uses the available clips until asset-pipeline delivers swim
        /// clips: Swim = Run at a slow rate on a pitched body, Dive = Fall pitched head-down, leap = Jump, the swing =
        /// Vine_Grab then Vine_Hang. Returns false when the land rules apply.
        /// </summary>
        private bool ResolveTraversal(in RunnerVisualState s)
        {
            if (s.Dead)
            {
                return false;
            }

            if (s.Mode == MoveMode.Swing)
            {
                if (_state != RunnerAnimState.Grab && _state != RunnerAnimState.Hang)
                {
                    Enter(RunnerAnimState.Grab, _c.FadeToJump, 0.3f, 1.6f, true);
                }
                else if (_state == RunnerAnimState.Grab && _timeInState >= 0.22f)
                {
                    Enter(RunnerAnimState.Hang, 0.15f, 0f, 1f, false);
                }

                return true;
            }

            bool water = s.Mode == MoveMode.Swim || s.Mode == MoveMode.DeepDive;
            if (!water)
            {
                if (_state == RunnerAnimState.Swim || _state == RunnerAnimState.Dive || _state == RunnerAnimState.Grab || _state == RunnerAnimState.Hang ||
                    _state == RunnerAnimState.Leap || _state == RunnerAnimState.Underwater)
                {
                    // Out of the water or off the vine: back to the land states.
                    if (s.Grounded)
                    {
                        Enter(RunnerAnimState.Locomotion, _c.FadeLandToRun, _c.LandingRunPhase, 1f, true);
                    }
                    else if (TraversalClips && s.VineAir && (_state == RunnerAnimState.Grab || _state == RunnerAnimState.Hang))
                    {
                        Enter(RunnerAnimState.VineRelease, _c.FadeToJump, 0f, _c.VineReleaseRate, true);
                    }
                    else
                    {
                        Enter(_evJump ? RunnerAnimState.Jump : RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, true);
                    }

                    return true;
                }

                return false;
            }

            if (TraversalClips)
            {
                ResolveWaterClips(s);
                return true;
            }

            if (_evLeap)
            {
                float start = _c.JumpTakeoffFrame / FramesPerSecond / _c.JumpClipLength;
                Enter(RunnerAnimState.Jump, _c.FadeToJump, start, 1f, true);
                return true;
            }

            if (s.Leaping)
            {
                if (_state != RunnerAnimState.Jump && _state != RunnerAnimState.Fall)
                {
                    Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
                }

                return true;
            }

            bool under = s.Mode == MoveMode.DeepDive || s.Dive != DivePhase.None;
            if (under)
            {
                Enter(RunnerAnimState.Dive, 0.12f, 0f, 0.8f, false);
            }
            else
            {
                Enter(RunnerAnimState.Swim, _evSplash ? 0.08f : 0.18f, 0f, 0.65f, false);
            }

            return true;
        }

        /// <summary>Water with the swim clips: Swim_Surface, Swim_Dive, Swim_Underwater, Swim_Leap.</summary>
        private void ResolveWaterClips(in RunnerVisualState s)
        {
            float leapStart = _c.SwimLeapStartFrame / FramesPerSecond / Math.Max(0.05f, _c.SwimLeapClipLength);
            if (_evLeap)
            {
                Enter(RunnerAnimState.Leap, _c.FadeToJump, leapStart, _c.SwimLeapRate, true);
                return;
            }

            if (s.Leaping)
            {
                Enter(RunnerAnimState.Leap, _c.FadeToJump, leapStart, _c.SwimLeapRate, false);
                return;
            }

            if (s.Mode == MoveMode.DeepDive)
            {
                Enter(RunnerAnimState.Underwater, 0.2f, 0f, _c.SwimUnderwaterRate, false);
            }
            else if (s.Dive != DivePhase.None)
            {
                Enter(RunnerAnimState.Dive, 0.1f, 0f, _c.SwimDiveRate, false);
            }
            else
            {
                Enter(RunnerAnimState.Swim, _evSplash ? 0.08f : _c.FadeToSwim, 0f, _c.SwimSurfaceRate, false);
            }
        }

        private static bool IsGroundLoop(RunnerAnimState state)
        {
            return state == RunnerAnimState.Locomotion || state == RunnerAnimState.Balance || state == RunnerAnimState.Wade;
        }

        /// <summary>The ground loop for the current surface (Locomotion without the traversal clips).</summary>
        private RunnerAnimState GroundLoop()
        {
            if (!TraversalClips)
            {
                return RunnerAnimState.Locomotion;
            }

            if (_canopy)
            {
                return RunnerAnimState.Balance;
            }

            return _wade >= _c.WadeDepth ? RunnerAnimState.Wade : RunnerAnimState.Locomotion;
        }

        private void UpdateBody(in RunnerVisualState s, float dt)
        {
            float pitch = 0f;
            float swim = 0f;
            if (!s.Dead)
            {
                switch (s.Mode)
                {
                    case MoveMode.Swim:
                        swim = 1f;
                        pitch = TraversalClips ? 0f : s.Leaping ? 20f : s.Dive == DivePhase.Down ? 115f : s.Dive == DivePhase.Under ? 90f : s.Dive == DivePhase.Up ? 55f : 72f;
                        break;
                    case MoveMode.DeepDive:
                        swim = 1f;
                        pitch = TraversalClips ? 0f : 95f;
                        break;
                    case MoveMode.Swing:
                        // The body trails the vine a little (the rope carries the full angle).
                        pitch = -0.35f * s.SwingDeg;
                        break;
                }
            }

            _bodyPitch = Damp(_bodyPitch, pitch, 0.08f, dt);
            _swimWeight = Damp(_swimWeight, swim, 0.08f, dt);
            _out.BodyPitchDeg = _bodyPitch;
            _out.SwimWeight = _swimWeight;
        }

        private void EnterGroundOrAir(in RunnerVisualState s, float fade)
        {
            if (s.Grounded)
            {
                Enter(s.Speed < _c.IdleSpeed ? RunnerAnimState.Idle : RunnerAnimState.Locomotion, fade, 0f, 1f, false);
            }
            else
            {
                Enter(RunnerAnimState.Fall, _c.FadeToFall, 0f, _c.FallRate, false);
            }
        }

        private void Enter(RunnerAnimState state, float fade, float startNormalized, float rate, bool restart)
        {
            if (state == RunnerAnimState.Locomotion)
            {
                state = GroundLoop();
                if (state == RunnerAnimState.Balance)
                {
                    rate = _c.BalanceRunRate;
                }
                else if (state == RunnerAnimState.Wade)
                {
                    rate = _c.WadeRate;
                }
            }

            if (state == _state && !restart)
            {
                return;
            }

            _state = state;
            _timeInState = 0f;
            _clipFrame = startNormalized * ClipFrames(state);
            _out.State = state;
            _out.Changed = true;
            _out.Fade = fade;
            _out.StartNormalized = startNormalized;
            _out.StateRate = rate;
        }

        private float ClipFrames(RunnerAnimState state)
        {
            switch (state)
            {
                case RunnerAnimState.Jump:
                    return _c.JumpClipLength * FramesPerSecond;
                case RunnerAnimState.Slide:
                    return _c.SlideClipLength * FramesPerSecond;
                case RunnerAnimState.Stumble:
                    return _c.StumbleClipLength * FramesPerSecond;
                case RunnerAnimState.LandHard:
                    return _c.LandHardClipLength * FramesPerSecond;
                case RunnerAnimState.Leap:
                    return _c.SwimLeapClipLength * FramesPerSecond;
                default:
                    return 0f;
            }
        }

        private float SlideDropRate()
        {
            float frames = Math.Max(0f, _c.SlideLowFrame - _c.SlideStartFrame);
            return frames / FramesPerSecond / Math.Max(0.02f, _c.SlideDropTime);
        }

        private void UpdateRates(in RunnerVisualState s, float locoPhase)
        {
            float blend = Smoothstep(_c.SprintBlendStartSpeed, _c.SprintBlendEndSpeed, s.Speed) * Clamp(_c.SprintBlendMax, 0f, 1f);
            _out.LocoBlend = blend;
            _out.RunRate = StrideWarp.FrameRate(_c, s.Speed, blend, locoPhase, _dt);

            if (_state == RunnerAnimState.Slide)
            {
                if (_clipFrame < _c.SlideLowFrame)
                {
                    _out.StateRate = SlideDropRate();
                }
                else if (_clipFrame < _c.SlideHoldEndFrame - 0.5f)
                {
                    float hold = Math.Max(0.05f, _c.SlideDuration - _c.SlideDropTime);
                    _out.StateRate = (_c.SlideHoldEndFrame - _c.SlideLowFrame) / FramesPerSecond / hold;
                }
                else
                {
                    // The simulation extended the slide (ceiling guard): hold the low pose.
                    _out.StateRate = 0f;
                }
            }
        }

        private void UpdateLean(in RunnerVisualState s, float dt)
        {
            float yawTarget = 0f;
            float rollTarget = 0f;
            bool upright = !s.Dead && _state != RunnerAnimState.Idle;
            if (upright)
            {
                float forward = Math.Max(1f, s.Speed);
                float travelDeg = (float)(Math.Atan2(s.VLat, forward) * (180.0 / Math.PI));
                yawTarget = Clamp(travelDeg * _c.YawFactor, -_c.MaxYawDeg, _c.MaxYawDeg);
                float lateral = s.VLatMax > 0f ? Clamp(s.VLat / s.VLatMax, -1f, 1f) : 0f;
                rollTarget = lateral * _c.RollAtMaxLateral;
                if (_sinceDodge < _c.DodgeRollTime)
                {
                    rollTarget += _dodgeDir * _c.DodgeRollDeg * (1f - (_sinceDodge / _c.DodgeRollTime));
                }

                if (_sinceEdge < 0.12f)
                {
                    rollTarget -= _edgeSide * _c.EdgeBrushRollDeg;
                }
            }

            _yaw = Damp(_yaw, yawTarget, _c.LeanHalfLife, dt);
            _roll = Damp(_roll, rollTarget, _c.LeanHalfLife, dt);
            _out.YawDeg = _yaw;
            _out.RollDeg = _roll;

            float leanTarget = 0f;
            if (_state == RunnerAnimState.Locomotion && !s.Dead)
            {
                float t = Clamp((s.Speed - _c.ForwardLeanSpeedA) / Math.Max(0.01f, _c.ForwardLeanSpeedB - _c.ForwardLeanSpeedA), 0f, 1f);
                leanTarget = s.Speed < _c.IdleSpeed ? 0f : _c.ForwardLeanDegA + ((_c.ForwardLeanDegB - _c.ForwardLeanDegA) * t);
            }

            _forwardLean = Damp(_forwardLean, leanTarget, 0.12f, dt);
            _out.ForwardLeanDeg = _forwardLean;

            float anchorTarget = _c.AirFootAnchor && !s.Grounded ? 1f : 0f;
            _anchor = Damp(_anchor, anchorTarget, _c.AnchorHalfLife, dt);
            _out.AnchorWeight = _anchor;
        }

        private void ClearEvents()
        {
            _evJump = false;
            _evSlide = false;
            _evStumble = false;
            _evLand = false;
            _evLandHard = false;
            _evLedge = false;
            _evLeap = false;
            _evSplash = false;
        }

        internal static float Smoothstep(float a, float b, float x)
        {
            if (b <= a)
            {
                return x >= b ? 1f : 0f;
            }

            float t = Clamp((x - a) / (b - a), 0f, 1f);
            return t * t * (3f - (2f * t));
        }

        internal static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        internal static float Damp(float current, float target, float halfLife, float dt)
        {
            if (halfLife <= 0f || dt <= 0f)
            {
                return dt <= 0f ? current : target;
            }

            float keep = (float)Math.Pow(0.5, dt / halfLife);
            return target + ((current - target) * keep);
        }
    }
}
