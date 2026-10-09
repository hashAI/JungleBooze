using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Places the avatar from the simulation's previous/current states interpolated by the fixed-step alpha
    /// (ADR 0002 rule 5), forwards events, and blinks during i-frames (<see cref="HealthConfig.InvulnerableBlinkHz"/>). Straight path: world = (x, y, s).
    /// No allocation per frame.
    /// </summary>
    public sealed class RunnerView
    {
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

        /// <summary>View-side centreline (curved worlds); null = straight path (world = (x, y, s)).</summary>
        public WorldPath Frames { get; set; }

        /// <summary>World position of a path-space point through <see cref="Frames"/>.</summary>
        public Vector3 WorldPoint(float s, float x, float y)
        {
            if (Frames == null)
            {
                return new Vector3(x, y, s);
            }

            PathFrame f = Frames.GetFrame(s);
            f.Offset(x, out float wx, out float wz);
            return new Vector3(wx, y, wz);
        }

        /// <summary>Path heading at s, degrees.</summary>
        public float HeadingDeg(float s)
        {
            return Frames == null ? 0f : Frames.GetFrame(s).HeadingDeg;
        }

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

            Vector3 hand = Vector3.zero;
            float swingDeg = 0f;
            if (_sim.TryGetSwing(out VinePoint vine, out float theta))
            {
                swingDeg = theta;
                float rad = theta * Mathf.Deg2Rad;
                hand = WorldPoint(vine.AnchorS + (vine.Length * Mathf.Sin(rad)), vine.X, vine.AnchorY - (vine.Length * Mathf.Cos(rad)));
            }

            float wade = 0f;
            if (b.Mode == MoveMode.Run && s.Grounded && _sim.Traversal != null && _sim.Traversal.TryGetWater(s.S, s.X, out float surface))
            {
                wade = Mathf.Max(0f, surface - s.Y);
            }

            var visual = new RunnerVisualState
            {
                VineAir = b.VineAir,
                Canopy = Frames != null && Frames.IsCanopy(s.S),
                WadeDepth = wade,
                Position = WorldPoint(s.S, s.X, s.Y),
                Facing = Quaternion.Euler(0f, HeadingDeg(s.S), 0f),
                Mode = b.Mode,
                Dive = b.Dive,
                Leaping = b.Leaping,
                Submerged = b.Submerged,
                SwingDeg = swingDeg,
                Hand = hand,
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
                _avatar.SetVisible(Mathf.Repeat(_blinkClock * _sim.Config.Health.InvulnerableBlinkHz, 1f) < 0.5f);
            }
            else if (_blinkClock != 0f)
            {
                _blinkClock = 0f;
                _avatar.SetVisible(true);
            }
        }
    }
}
