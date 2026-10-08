using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// Everything the player did during one simulation tick (spec 101 §3.2, ADR 0006): discrete commands plus the
    /// steering drag in integer millimetres (after sensitivity). Integer millimetres keep replays exact; input
    /// sources carry the sub-millimetre remainder to the next tick so nothing drifts.
    /// </summary>
    public readonly struct InputFrame : IEquatable<InputFrame>
    {
        public static readonly InputFrame Empty = default;

        public InputFrame(InputCommand commands, short lateralDeltaMm)
        {
            Commands = commands;
            LateralDeltaMm = lateralDeltaMm;
        }

        public InputCommand Commands { get; }

        /// <summary>Lateral target change for this tick, mm, + = right.</summary>
        public short LateralDeltaMm { get; }

        public bool IsEmpty => Commands == InputCommand.None && LateralDeltaMm == 0;

        /// <summary>Lateral delta in metres.</summary>
        public float LateralDeltaM => LateralDeltaMm * 0.001f;

        public static InputFrame FromCommands(InputCommand commands)
        {
            return new InputFrame(commands, 0);
        }

        /// <summary>Clamps a millimetre value into the <see cref="short"/> range.</summary>
        public static short ClampMm(long mm)
        {
            if (mm > short.MaxValue)
            {
                return short.MaxValue;
            }

            if (mm < short.MinValue)
            {
                return short.MinValue;
            }

            return (short)mm;
        }

        public bool Equals(InputFrame other)
        {
            return Commands == other.Commands && LateralDeltaMm == other.LateralDeltaMm;
        }

        public override bool Equals(object obj)
        {
            return obj is InputFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Commands << 16) ^ (ushort)LateralDeltaMm;
        }

        public override string ToString()
        {
            return Commands + " " + LateralDeltaMm + "mm";
        }
    }
}
