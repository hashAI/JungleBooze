using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Immutable runtime Continue rules (GDD 14.4) built from <see cref="EconomyDesignValues"/>. Plain C#; the
    /// session and the app layer only read it.
    /// </summary>
    public sealed class ContinueRules
    {
        private readonly int[] _costs;

        private ContinueRules(EconomyDesignValues v)
        {
            _costs = (int[])v.ContinueCosts.Clone();
            MaxContinuesPerRun = v.MaxContinuesPerRun;
            OfferSeconds = v.ContinueOfferSeconds;
            FreeContinueInFirstSession = v.FreeContinueInFirstSession;
            InvulnerableTicks = RunnerConfig.MsToTicksAllowZero(v.ContinueInvulnerableMs);
            ClearStretchSeconds = v.ContinueClearStretchSeconds;
            InputLockSeconds = v.ContinueInputLockMs / 1000.0;
        }

        public int MaxContinuesPerRun { get; }

        public double OfferSeconds { get; }

        public bool FreeContinueInFirstSession { get; }

        public int InvulnerableTicks { get; }

        public double ClearStretchSeconds { get; }

        public double InputLockSeconds { get; }

        /// <summary>Coin price of the next continue after <paramref name="continuesUsed"/> (the last price repeats).</summary>
        public int CostFor(int continuesUsed)
        {
            if (continuesUsed < 0)
            {
                continuesUsed = 0;
            }

            return _costs[continuesUsed < _costs.Length ? continuesUsed : _costs.Length - 1];
        }

        public static ContinueRules FromDesignValues(EconomyDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid economy tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new ContinueRules(values);
        }

        public static ContinueRules CreateDefault()
        {
            return FromDesignValues(EconomyDesignValues.CreateDefault());
        }
    }
}
