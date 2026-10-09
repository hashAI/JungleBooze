using System.Globalization;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Allocation-free number strings for the HUD: values below <c>prefill</c> are built up front, larger ones once
    /// on first use (then cached up to <c>capacity</c>). Thousands are grouped ("2,480 m"). Values at or above the
    /// capacity allocate (beyond any realistic run).
    /// </summary>
    public sealed class NumberText
    {
        private readonly string[] _cache;
        private readonly string _format;

        /// <param name="format">Composite format with {0} for the grouped number, e.g. "{0} m"; null = the number.</param>
        public NumberText(int prefill, int capacity, string format)
        {
            _format = string.IsNullOrEmpty(format) ? "{0}" : format;
            _cache = new string[capacity < 1 ? 1 : capacity];
            int n = prefill < _cache.Length ? prefill : _cache.Length;
            for (int i = 0; i < n; i++)
            {
                _cache[i] = Build(i);
            }
        }

        public int Capacity => _cache.Length;

        public string Get(int value)
        {
            if (value < 0)
            {
                value = 0;
            }

            if (value >= _cache.Length)
            {
                return Build(value);
            }

            return _cache[value] ?? (_cache[value] = Build(value));
        }

        /// <summary>"2,480" (invariant grouping; locale-aware grouping arrives with translations).</summary>
        public static string Group(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private string Build(int value)
        {
            return string.Format(CultureInfo.InvariantCulture, _format, Group(value));
        }
    }
}
