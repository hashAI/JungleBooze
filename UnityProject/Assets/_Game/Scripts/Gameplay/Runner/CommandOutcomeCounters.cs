using System;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Per-outcome counters for spec 001 rule I9. Allocation-free after construction.
    /// Invariant: <see cref="Total"/> == <see cref="CommandsReceived"/> after every step.
    /// </summary>
    public sealed class CommandOutcomeCounters
    {
        private const int OutcomeCount = (int)CommandOutcome.Ignored + 1;
        private readonly int[] _counts = new int[OutcomeCount];

        /// <summary>Movement command flags received (MoveLeft, MoveRight, Jump, Slide).</summary>
        public int CommandsReceived { get; private set; }

        /// <summary>Jumps that ever entered the buffer (for the "Expired ≤ 5% of buffered jumps" sim target).</summary>
        public int JumpsBufferedTotal { get; private set; }

        /// <summary>Sum over all outcomes, pending ones included.</summary>
        public int Total
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _counts.Length; i++)
                {
                    sum += _counts[i];
                }

                return sum;
            }
        }

        public int Get(CommandOutcome outcome)
        {
            return _counts[(int)outcome];
        }

        internal void Receive(CommandOutcome outcome)
        {
            if (outcome == CommandOutcome.None)
            {
                throw new ArgumentException("A received command needs an outcome.", nameof(outcome));
            }

            CommandsReceived++;
            _counts[(int)outcome]++;
            if (outcome == CommandOutcome.Buffered)
            {
                JumpsBufferedTotal++;
            }
        }

        /// <summary>Moves one pending command (Buffered or Queued) to its final outcome.</summary>
        internal void Resolve(CommandOutcome pending, CommandOutcome final)
        {
            _counts[(int)pending]--;
            _counts[(int)final]++;
        }

        internal void CopyFrom(CommandOutcomeCounters source)
        {
            Array.Copy(source._counts, _counts, _counts.Length);
            CommandsReceived = source.CommandsReceived;
            JumpsBufferedTotal = source.JumpsBufferedTotal;
        }

        internal void Reset()
        {
            Array.Clear(_counts, 0, _counts.Length);
            CommandsReceived = 0;
            JumpsBufferedTotal = 0;
        }
    }
}
