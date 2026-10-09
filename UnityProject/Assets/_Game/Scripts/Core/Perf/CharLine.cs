using System;

namespace JungleBooze.Core.Perf
{
    /// <summary>
    /// Allocation-free text line builder for per-second benchmark logs: appends text, integers and fixed-point numbers
    /// into a reusable char buffer (no boxing, no number-to-string allocations). Text that does not fit is cut.
    /// </summary>
    public sealed class CharLine
    {
        private static readonly long[] Pow10 = { 1, 10, 100, 1000, 10000, 100000 };
        private readonly char[] _buffer;
        private readonly char[] _digits = new char[20];

        public CharLine(int capacity = 512)
        {
            _buffer = new char[capacity];
        }

        public char[] Buffer => _buffer;

        public int Length { get; private set; }

        public void Clear()
        {
            Length = 0;
        }

        public CharLine Append(char c)
        {
            if (Length < _buffer.Length)
            {
                _buffer[Length++] = c;
            }

            return this;
        }

        public CharLine Append(string text)
        {
            if (text == null)
            {
                return this;
            }

            for (int i = 0; i < text.Length; i++)
            {
                Append(text[i]);
            }

            return this;
        }

        public CharLine Append(long value)
        {
            if (value < 0)
            {
                Append('-');
                if (value == long.MinValue)
                {
                    return Append("9223372036854775808");
                }

                value = -value;
            }

            int n = 0;
            do
            {
                _digits[n++] = (char)('0' + (int)(value % 10));
                value /= 10;
            }
            while (value > 0 && n < _digits.Length);

            while (n > 0)
            {
                Append(_digits[--n]);
            }

            return this;
        }

        /// <summary>Appends <paramref name="value"/> rounded to <paramref name="decimals"/> (0–5) places.</summary>
        public CharLine Append(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return Append("nan");
            }

            decimals = Math.Max(0, Math.Min(5, decimals));
            long scale = Pow10[decimals];
            long scaled = (long)Math.Round(Math.Abs(value) * scale, MidpointRounding.AwayFromZero);
            if (value < 0.0 && scaled != 0)
            {
                Append('-');
            }

            Append(scaled / scale);
            if (decimals > 0)
            {
                Append('.');
                long frac = scaled % scale;
                for (long div = scale / 10; div > 0; div /= 10)
                {
                    Append((char)('0' + (int)(frac / div % 10)));
                }
            }

            return this;
        }

        /// <summary>The line as a string (allocates; for tests and once-per-phase summaries only).</summary>
        public override string ToString()
        {
            return new string(_buffer, 0, Length);
        }
    }
}
