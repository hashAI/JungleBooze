using System;
using System.Collections.Generic;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Bots
{
    /// <summary>
    /// Scripted input: a list of (tick, commands) entries set up front. Used by tests and as the output stage of
    /// later bots. Several entries for the same tick are combined. Lookups do not allocate.
    /// </summary>
    public sealed class BotInputProvider : IInputProvider
    {
        private readonly Dictionary<long, InputCommand> _script = new Dictionary<long, InputCommand>();

        /// <summary>Number of ticks that carry at least one command.</summary>
        public int ScriptedTickCount => _script.Count;

        /// <summary>Adds <paramref name="commands"/> on <paramref name="tick"/> (combined with anything already there).</summary>
        public BotInputProvider At(long tick, InputCommand commands)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Must not be negative.");
            }

            if (commands == InputCommand.None)
            {
                return this;
            }

            _script.TryGetValue(tick, out InputCommand existing);
            _script[tick] = existing | commands;
            return this;
        }

        /// <summary>Commands scripted for <paramref name="tick"/>, or none.</summary>
        public InputCommand ReadCommands(long tick)
        {
            return _script.TryGetValue(tick, out InputCommand commands) ? commands : InputCommand.None;
        }

        public void Clear()
        {
            _script.Clear();
        }
    }
}
