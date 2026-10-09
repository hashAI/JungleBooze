using System;
using System.Text;

namespace JungleBooze.App.FeelTest
{
    /// <summary>
    /// Allocation-free text helpers for the debug overlay: numbers are appended digit by digit (StringBuilder's
    /// numeric overloads allocate on Mono) and enum names come from arrays built once.
    /// </summary>
    internal static class DebugFormat
    {
        /// <summary>Names indexed by the enum's integer value (built once, at type initialization).</summary>
        public static string[] Names(Type enumType)
        {
            Array values = Enum.GetValues(enumType);
            int max = 0;
            foreach (object value in values)
            {
                max = Math.Max(max, Convert.ToInt32(value));
            }

            var names = new string[max + 1];
            foreach (object value in values)
            {
                int index = Convert.ToInt32(value);
                if (index >= 0)
                {
                    names[index] = Enum.GetName(enumType, value);
                }
            }

            return names;
        }

        public static string Name(string[] names, int value)
        {
            return value >= 0 && value < names.Length && names[value] != null ? names[value] : "?";
        }

        public static void Int(StringBuilder b, long value)
        {
            if (value < 0)
            {
                b.Append('-');
                value = -value;
            }

            long power = 1;
            while (value / power >= 10)
            {
                power *= 10;
            }

            for (; power >= 1; power /= 10)
            {
                b.Append((char)('0' + ((value / power) % 10)));
            }
        }

        public static void Fixed(StringBuilder b, double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                b.Append("nan");
                return;
            }

            long scale = 1;
            for (int i = 0; i < decimals; i++)
            {
                scale *= 10;
            }

            long n = (long)Math.Round(Math.Abs(value) * scale, MidpointRounding.AwayFromZero);
            if (value < 0.0 && n != 0)
            {
                b.Append('-');
            }

            Int(b, n / scale);
            if (decimals <= 0)
            {
                return;
            }

            b.Append('.');
            long fraction = n % scale;
            for (long digit = scale / 10; digit >= 1; digit /= 10)
            {
                b.Append((char)('0' + ((fraction / digit) % 10)));
            }
        }
    }
}
