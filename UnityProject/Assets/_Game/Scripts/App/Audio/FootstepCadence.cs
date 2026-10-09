using System;
using JungleBooze.Gameplay.World;

namespace JungleBooze.App.Audio
{
    /// <summary>
    /// Footsteps synced to the run cycle (GDD §19), presentation only: the step rate follows the ground speed
    /// (a runner's cadence rises slowly while the stride lengthens), a step fires each time the phase wraps, and
    /// landings restart the cycle so the first step after a jump doesn't double the landing sound.
    /// </summary>
    public sealed class FootstepCadence
    {
        /// <summary>Steps per second at 0 m/s and per m/s (≈ 2.6 at 8 m/s, 3.3 at 16 m/s).</summary>
        public const float BaseRate = 1.9f;
        public const float RatePerSpeed = 0.088f;
        public const float MinRate = 2.2f;
        public const float MaxRate = 3.6f;

        /// <summary>Below this speed nobody runs (no steps).</summary>
        public const float MinSpeed = 1f;

        private float _phase;

        public static float StepRate(float speed)
        {
            float r = BaseRate + (RatePerSpeed * speed);
            return r < MinRate ? MinRate : r > MaxRate ? MaxRate : r;
        }

        /// <summary>Advances the cycle. True when a foot lands this frame.</summary>
        public bool Advance(float dt, float speed, bool running)
        {
            if (!running || speed < MinSpeed || dt <= 0f)
            {
                return false;
            }

            _phase += dt * StepRate(speed);
            if (_phase >= 1f)
            {
                _phase -= (float)Math.Floor(_phase);
                return true;
            }

            return false;
        }

        /// <summary>After a landing: the next step comes half a cycle later.</summary>
        public void OnLanded()
        {
            _phase = 0.5f;
        }

        public void Reset()
        {
            _phase = 0f;
        }

        /// <summary>Surface under the feet: beams/canopy wood, wading shallow water, river and falls banks moss, else soil.</summary>
        public static FootstepSurface SurfaceFor(EnvironmentSet set, bool canopy, bool wading)
        {
            if (wading)
            {
                return FootstepSurface.Shallow;
            }

            if (canopy || set == EnvironmentSet.Canopy)
            {
                return FootstepSurface.Wood;
            }

            return set == EnvironmentSet.River || set == EnvironmentSet.Waterfall ? FootstepSurface.Moss : FootstepSurface.Dirt;
        }

        public static string CueFor(FootstepSurface surface)
        {
            switch (surface)
            {
                case FootstepSurface.Moss:
                    return ExpeditionAudioMap.StepMoss;
                case FootstepSurface.Wood:
                    return ExpeditionAudioMap.StepWood;
                case FootstepSurface.Shallow:
                    return ExpeditionAudioMap.StepShallow;
                default:
                    return ExpeditionAudioMap.StepDirt;
            }
        }
    }
}
