using System;

namespace JungleBooze.Gameplay.Animation
{
    /// <summary>
    /// Run-cycle playback rate with contact-phase time warping. An in-place run clip has a "natural" ground speed at
    /// which the planted foot stays still. At game speeds (10–16 m/s) playing the whole cycle that fast gives a
    /// frantic cadence. Instead the stance part of the cycle plays at the no-skate rate (the planted foot moves
    /// backwards exactly at ground speed) and the flight part, where both feet are off the ground, plays slower.
    /// The result: no foot skating, a believable cadence, and longer bounding strides at higher speed.
    /// </summary>
    public static class StrideWarp
    {
        /// <summary>Rate that keeps the planted foot still at <paramref name="speed"/> for the given blend.</summary>
        public static float StanceRate(RunnerAnimationConfig c, float speed, float blend)
        {
            float natural = c.RunNaturalSpeed + ((c.SprintNaturalSpeed - c.RunNaturalSpeed) * blend);
            return natural > 0f ? speed * c.StanceMatch / natural : 1f;
        }

        /// <summary>0…1 weight: how much a foot is planted at this cycle phase (smooth edges).</summary>
        public static float ContactWeight(RunnerAnimationConfig c, float phase)
        {
            if (phase < 0f)
            {
                return 1f;
            }

            phase -= (float)Math.Floor(phase);
            float a = Window(phase, c.ContactAStart, c.ContactAEnd, c.ContactEdge);
            float b = Window(phase, c.ContactBStart, c.ContactBEnd, c.ContactEdge);
            return Math.Max(a, b);
        }

        /// <summary>Playback rate of the locomotion state this frame.</summary>
        /// <param name="phase">Normalized cycle phase, negative if unknown (uniform rate).</param>
        public static float Rate(RunnerAnimationConfig c, float speed, float blend, float phase)
        {
            float stance = StanceRate(c, speed, blend);
            float rate = stance;
            if (c.ContactWarp && phase >= 0f && c.StanceSpeedTable != null && c.StanceSpeedTable.Length > 1)
            {
                float footSpeed = SampleTable(c.StanceSpeedTable, phase);
                float flight = Math.Min(stance, c.FlightRateMax);
                if (footSpeed < 0f)
                {
                    rate = flight; // both feet in the air
                }
                else if (footSpeed < c.MinStanceSpeed)
                {
                    rate = c.TouchdownRate; // touch-down / lift-off frames where the source foot barely moves: pass quickly
                }
                else
                {
                    rate = speed * c.StanceMatch / footSpeed;
                }
            }
            else if (c.ContactWarp && phase >= 0f)
            {
                float flight = Math.Min(stance, c.FlightRateMax);
                float w = ContactWeight(c, phase);
                rate = flight + ((stance - flight) * w);
            }

            return RunnerAnimationModel.Clamp(rate, c.MinRunRate, Math.Max(c.MaxRunRate, c.TouchdownRate));
        }

        /// <summary>
        /// Playback rate to use for the next frame of length <paramref name="dt"/>: the warp is integrated over the
        /// frame in sub-steps, so a short contact phase that starts mid-frame still plays at the no-skate rate (a
        /// single rate sampled at the frame start would overshoot into or out of the contact at 13–16 m/s, where a
        /// contact lasts only ~2 frames).
        /// </summary>
        public static float FrameRate(RunnerAnimationConfig c, float speed, float blend, float phase, float dt)
        {
            if (phase < 0f || dt <= 0f || !c.ContactWarp)
            {
                return Rate(c, speed, blend, phase);
            }

            const int SubSteps = 16;
            float cycle = c.RunCycleLength + ((c.SprintCycleLength - c.RunCycleLength) * blend);
            if (cycle <= 0f)
            {
                return Rate(c, speed, blend, phase);
            }

            float h = dt / SubSteps;
            float p = phase;
            for (int i = 0; i < SubSteps; i++)
            {
                p += Rate(c, speed, blend, p) * h / cycle;
            }

            return (p - phase) * cycle / dt;
        }

        /// <summary>
        /// Average cadence (steps per second, two steps per cycle) that <see cref="Rate"/> produces, by integrating
        /// the warped rate over one cycle. Used by tests and the tuning readout.
        /// </summary>
        public static float StepsPerSecond(RunnerAnimationConfig c, float speed, float blend)
        {
            const int Samples = 400;
            double seconds = 0;
            for (int i = 0; i < Samples; i++)
            {
                float phase = (i + 0.5f) / Samples;
                seconds += 1.0 / Samples / Rate(c, speed, blend, phase);
            }

            float cycle = c.RunCycleLength + ((c.SprintCycleLength - c.RunCycleLength) * blend);
            return (float)(2.0 / (seconds * cycle));
        }

        /// <summary>Linear, wrapping lookup of a per-phase table.</summary>
        public static float SampleTable(float[] table, float phase)
        {
            phase -= (float)Math.Floor(phase);
            float x = phase * table.Length;
            int i = (int)x;
            float t = x - i;
            float a = table[i % table.Length];
            float b = table[(i + 1) % table.Length];
            return a + ((b - a) * t);
        }

        // Wrap-aware window [start, end] with smooth edges.
        private static float Window(float phase, float start, float end, float edge)
        {
            float best = 0f;
            for (int k = -1; k <= 1; k++)
            {
                float p = phase + k;
                float w;
                if (p >= start && p <= end)
                {
                    w = 1f;
                }
                else if (edge > 0f && p < start && p > start - edge)
                {
                    w = RunnerAnimationModel.Smoothstep(start - edge, start, p);
                }
                else if (edge > 0f && p > end && p < end + edge)
                {
                    w = 1f - RunnerAnimationModel.Smoothstep(end, end + edge, p);
                }
                else
                {
                    w = 0f;
                }

                best = Math.Max(best, w);
            }

            return best;
        }
    }
}
