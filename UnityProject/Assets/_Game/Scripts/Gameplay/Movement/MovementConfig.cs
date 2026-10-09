using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// All simulation tuning for one run. Built from the ScriptableObjects at run start (deep copy), so the
    /// simulation never sees authoring changes mid-run. Tests and bots construct it directly.
    /// </summary>
    public sealed class MovementConfig
    {
        public MovementConfig(
            RunSpeedConfig speed,
            LateralMovementConfig lateral,
            JumpSlideConfig jumpSlide,
            HitboxConfig hitbox,
            HealthConfig health,
            RunFlowConfig flow,
            SwimConfig swim = null,
            VineConfig vine = null,
            CanopyConfig canopy = null)
        {
            Speed = (speed ?? throw new ArgumentNullException(nameof(speed))).Clone();
            Lateral = (lateral ?? throw new ArgumentNullException(nameof(lateral))).Clone();
            JumpSlide = (jumpSlide ?? throw new ArgumentNullException(nameof(jumpSlide))).Clone();
            Hitbox = (hitbox ?? throw new ArgumentNullException(nameof(hitbox))).Clone();
            Health = (health ?? throw new ArgumentNullException(nameof(health))).Clone();
            Flow = (flow ?? throw new ArgumentNullException(nameof(flow))).Clone();
            Swim = (swim ?? new SwimConfig()).Clone();
            Vine = (vine ?? new VineConfig()).Clone();
            Canopy = (canopy ?? new CanopyConfig()).Clone();
        }

        public RunSpeedConfig Speed { get; }

        public LateralMovementConfig Lateral { get; }

        public JumpSlideConfig JumpSlide { get; }

        public HitboxConfig Hitbox { get; }

        public HealthConfig Health { get; }

        public RunFlowConfig Flow { get; }

        /// <summary>Swimming (spec 103 §4).</summary>
        public SwimConfig Swim { get; }

        /// <summary>Vine swing (spec 103 §5).</summary>
        public VineConfig Vine { get; }

        /// <summary>Canopy beams (spec 103 §6).</summary>
        public CanopyConfig Canopy { get; }

        /// <summary>Range checks. Returns problems (empty = valid). Setup time only (allocates).</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            Positive(problems, "Speed.V0", Speed.V0);
            if (Speed.VMax < Speed.V0)
            {
                problems.Add("Speed.VMax must be ≥ V0.");
            }

            Positive(problems, "Speed.DScale", Speed.DScale);
            Positive(problems, "Lateral.Tau", Lateral.Tau);
            Positive(problems, "Lateral.VLatMax", Lateral.VLatMax);
            Positive(problems, "Lateral.AccelLat", Lateral.AccelLat);
            Positive(problems, "Lateral.DecelLat", Lateral.DecelLat);
            Positive(problems, "Lateral.SoftZone", Lateral.SoftZone);
            Positive(problems, "JumpSlide.JumpVelocity", JumpSlide.JumpVelocity);
            Positive(problems, "JumpSlide.GUp", JumpSlide.GUp);
            Positive(problems, "JumpSlide.GDown", JumpSlide.GDown);
            Positive(problems, "JumpSlide.SlideDuration", JumpSlide.SlideDuration);
            Positive(problems, "Hitbox.Width", Hitbox.Width);
            Positive(problems, "Hitbox.RunDepth", Hitbox.RunDepth);
            if (Health.MaxHealth < 1)
            {
                problems.Add("Health.MaxHealth must be ≥ 1.");
            }

            JumpArc arc = JumpArc.Measure(JumpSlide, 1f / 60f);
            if (arc.Apex < 1.35f || arc.Apex > 1.50f)
            {
                problems.Add("Jump apex " + arc.Apex.ToString("0.000") + " m is outside 1.35–1.50 m (spec 101 §7).");
            }

            if (arc.Airtime < 0.55f || arc.Airtime > 0.65f)
            {
                problems.Add("Jump airtime " + arc.Airtime.ToString("0.000") + " s is outside 0.55–0.65 s (spec 101 §7).");
            }

            Positive(problems, "Swim.SpeedFactor", Swim.SpeedFactor);
            Positive(problems, "Swim.Tau", Swim.Tau);
            if (Swim.ExitDepth >= Swim.EnterDepth)
            {
                problems.Add("Swim.ExitDepth must be < EnterDepth (hysteresis).");
            }

            Positive(problems, "Vine.SwingTime", Vine.SwingTime);
            if (!(Vine.ReleaseOpen < Vine.PerfectStart && Vine.PerfectStart <= Vine.PerfectEnd && Vine.PerfectEnd <= 1f))
            {
                problems.Add("Vine: need ReleaseOpen < PerfectStart ≤ PerfectEnd ≤ 1.");
            }

            return problems;
        }

        private static void Positive(List<string> problems, string name, float value)
        {
            if (!(value > 0f))
            {
                problems.Add(name + " must be > 0.");
            }
        }
    }
}
