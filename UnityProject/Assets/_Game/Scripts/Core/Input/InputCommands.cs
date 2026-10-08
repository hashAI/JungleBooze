namespace JungleBooze.Core
{
    /// <summary>Helpers for <see cref="InputCommand"/> masks. Allocation-free.</summary>
    public static class InputCommands
    {
        /// <summary>The mutually exclusive discrete actions (one per tick).</summary>
        public const InputCommand DiscreteMask =
            InputCommand.Jump | InputCommand.Slide | InputCommand.DodgeLeft | InputCommand.DodgeRight;

        /// <summary>The discrete part of <paramref name="commands"/>; if several are set (malformed), the lowest bit wins.</summary>
        public static InputCommand Discrete(InputCommand commands)
        {
            int bits = (int)(commands & DiscreteMask);
            return (InputCommand)(bits & -bits);
        }

        public static bool Has(InputCommand commands, InputCommand flag)
        {
            return (commands & flag) != 0;
        }
    }
}
