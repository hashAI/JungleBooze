using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// Fixed-step clock with a real-time accumulator.
    /// The frame driver calls <see cref="Accumulate"/> once per rendered frame with the real frame time,
    /// then runs the simulation once per returned step and calls <see cref="Step"/> after each one.
    /// Headless simulations skip <see cref="Accumulate"/> and just call <see cref="Step"/> in a loop.
    /// </summary>
    public sealed class FixedStepTimeSource : ITimeSource
    {
        private readonly double _stepSeconds;
        private readonly int _maxStepsPerFrame;
        private double _accumulator;

        /// <param name="stepsPerSecond">Simulation rate, for example 60.</param>
        /// <param name="maxStepsPerFrame">
        /// Upper bound of steps per rendered frame. Protects against a "spiral of death" after a hitch
        /// (for example returning from background). Time beyond the cap is dropped, not simulated.
        /// </param>
        public FixedStepTimeSource(int stepsPerSecond, int maxStepsPerFrame = 5)
        {
            if (stepsPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepsPerSecond), "Must be positive.");
            }

            if (maxStepsPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame), "Must be positive.");
            }

            StepsPerSecond = stepsPerSecond;
            _stepSeconds = 1.0 / stepsPerSecond;
            _maxStepsPerFrame = maxStepsPerFrame;
            DeltaTime = (float)_stepSeconds;
        }

        public int StepsPerSecond { get; }

        public long Tick { get; private set; }

        public float DeltaTime { get; }

        public double ElapsedSeconds => Tick * _stepSeconds;

        /// <summary>
        /// Fraction (0..1) of a step left in the accumulator. Presentation code uses it to
        /// interpolate visuals between the last two simulation states. Never read by simulation code.
        /// </summary>
        public float InterpolationAlpha => (float)(_accumulator / _stepSeconds);

        /// <summary>Total real seconds discarded because of the per-frame step cap.</summary>
        public double DroppedSeconds { get; private set; }

        /// <summary>
        /// Adds real elapsed time and returns how many simulation steps are now due (0.._maxStepsPerFrame).
        /// Negative, NaN or infinite input is treated as zero.
        /// </summary>
        public int Accumulate(double realDeltaSeconds)
        {
            if (double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds) || realDeltaSeconds < 0.0)
            {
                realDeltaSeconds = 0.0;
            }

            _accumulator += realDeltaSeconds;

            int steps = 0;
            while (_accumulator >= _stepSeconds && steps < _maxStepsPerFrame)
            {
                _accumulator -= _stepSeconds;
                steps++;
            }

            if (_accumulator >= _stepSeconds)
            {
                // Cap reached: drop whole steps but keep the fractional part for smooth interpolation.
                double whole = Math.Floor(_accumulator / _stepSeconds) * _stepSeconds;
                DroppedSeconds += whole;
                _accumulator -= whole;
            }

            return steps;
        }

        /// <summary>Marks one simulation step as complete.</summary>
        public void Step()
        {
            Tick++;
        }

        /// <summary>Resets the clock for a new run.</summary>
        public void Reset()
        {
            Tick = 0;
            _accumulator = 0.0;
            DroppedSeconds = 0.0;
        }
    }
}
