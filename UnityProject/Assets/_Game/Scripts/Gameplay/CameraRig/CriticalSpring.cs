using System;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>
    /// Critically damped spring with a half-life parameter (a step target halves its error after exactly
    /// <c>halfLife</c>), integrated exactly for a target that moves linearly during the frame. Because simulation
    /// poses are interpolated linearly between ticks, the result is frame-rate independent (spec 101 §5,
    /// AC-101-42). No overshoot for step targets.
    /// </summary>
    public struct CriticalSpring
    {
        /// <summary>Solves (1 + u)·e^(−u) = 0.5.</summary>
        private const double HalfLifeDecay = 1.6783469900166608;

        public float Value;
        public float Velocity;
        public float LastTarget;
        public bool Initialized;

        public void Snap(float target)
        {
            Value = target;
            Velocity = 0f;
            LastTarget = target;
            Initialized = true;
        }

        public float Update(float target, float halfLife, float dt)
        {
            if (!Initialized || halfLife <= 0f)
            {
                Snap(target);
                return Value;
            }

            if (dt <= 0f)
            {
                return Value;
            }

            double y = HalfLifeDecay / halfLife;
            double q = (target - LastTarget) / (double)dt;
            double ep = -2.0 * q / y;
            double e0 = Value - LastTarget;
            double de0 = Velocity - q;
            double j0 = e0 - ep;
            double j1 = de0 + (j0 * y);
            double decay = Math.Exp(-y * dt);
            double e1 = (decay * (j0 + (j1 * dt))) + ep;
            double de1 = decay * (de0 - (j1 * y * dt));
            Value = (float)(target + e1);
            Velocity = (float)(q + de1);
            LastTarget = target;
            return Value;
        }
    }
}
