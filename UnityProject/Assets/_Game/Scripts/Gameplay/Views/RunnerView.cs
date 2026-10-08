using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Places the avatar from the simulation's previous/current states interpolated by the fixed-step alpha
    /// (ADR 0002 rule 5), forwards events, and blinks at 8 Hz during i-frames. Straight path: world = (x, y, s).
    /// No allocation per frame.
    /// </summary>
    public sealed class RunnerView
    {
        private const float BlinkHz = 8f;

        private readonly RunnerSimulation _sim;
        private RunnerAvatar _avatar;
        private float _blinkClock;
        private float _sinceStumble = 99f;
        private float _sinceDodge = 99f;
        private int _dodgeDir;

        public RunnerView(RunnerSimulation sim, RunnerAvatar avatar)
        {
            _sim = sim;
            _avatar = avatar;
        }

        public RunnerAvatar Avatar => _avatar;

        /// <summary>Last interpolated state (camera input, debug).</summary>
        public RunnerState Interpolated { get; private set; }

        public void SetAvatar(RunnerAvatar avatar)
        {
            _avatar = avatar;
        }

        public void ResetRun()
        {
            _sinceStumble = 99f;
            _sinceDodge = 99f;
            _blinkClock = 0f;
            if (_avatar != null)
            {
                _avatar.ResetPose();
            }
        }

        public void OnRunEvent(in RunEvent e)
        {
            if (e.Type == RunEventType.Hit && e.Reason != (byte)HitKind.Crash)
            {
                _sinceStumble = 0f;
            }
            else if (e.Type == RunEventType.Dodge)
            {
                _sinceDodge = 0f;
                _dodgeDir = e.Reason == 1 ? 1 : -1;
            }

            if (_avatar != null)
            {
                _avatar.OnRunEvent(e);
            }
        }

        public void Sync(float alpha, float frameSeconds)
        {
            ref readonly RunnerState a = ref _sim.Previous;
            ref readonly RunnerState b = ref _sim.State;
            RunnerState s = b;
            s.S = Mathf.LerpUnclamped(a.S, b.S, alpha);
            s.X = Mathf.LerpUnclamped(a.X, b.X, alpha);
            s.Y = Mathf.LerpUnclamped(a.Y, b.Y, alpha);
            s.GroundY = Mathf.LerpUnclamped(a.GroundY, b.GroundY, alpha);
            s.XTarget = Mathf.LerpUnclamped(a.XTarget, b.XTarget, alpha);
            s.VLat = Mathf.LerpUnclamped(a.VLat, b.VLat, alpha);
            s.Speed = Mathf.LerpUnclamped(a.Speed, b.Speed, alpha);
            Interpolated = s;

            _sinceStumble += frameSeconds;
            _sinceDodge += frameSeconds;
            if (_avatar == null)
            {
                return;
            }

            var visual = new RunnerVisualState
            {
                Position = new Vector3(s.X, s.Y, s.S),
                Facing = Quaternion.identity,
                Speed = s.Speed,
                VLat = s.VLat,
                VLatMax = _sim.Config.Lateral.VLatMax,
                Vy = s.Vy,
                HeightAboveGround = Mathf.Max(0f, s.Y - s.GroundY),
                Grounded = s.Grounded,
                Sliding = s.Sliding,
                FastFalling = s.FastFalling,
                Dead = s.Dead,
                Cause = s.Cause,
                Invulnerable = s.IsInvulnerable && !s.Dead,
                SinceStumble = _sinceStumble,
                SinceDodge = _sinceDodge,
                DodgeDirection = _dodgeDir,
            };
            _avatar.Apply(visual, frameSeconds);

            if (visual.Invulnerable)
            {
                _blinkClock += frameSeconds;
                _avatar.SetVisible(Mathf.Repeat(_blinkClock * BlinkHz, 1f) < 0.5f);
            }
            else if (_blinkClock != 0f)
            {
                _blinkClock = 0f;
                _avatar.SetVisible(true);
            }
        }
    }
}
