namespace JungleBooze.UI.FeelTest
{
    /// <summary>Pre-built strings for small non-negative integers with a suffix, so HUD updates never allocate.</summary>
    public sealed class IntStringCache
    {
        private readonly string[] _values;
        private readonly string _suffix;

        public IntStringCache(int count, string suffix)
        {
            _suffix = suffix ?? string.Empty;
            _values = new string[count];
            for (int i = 0; i < count; i++)
            {
                _values[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture) + _suffix;
            }
        }

        /// <summary>Cached string for <paramref name="value"/>; values outside the cache allocate.</summary>
        public string Get(int value)
        {
            if (value >= 0 && value < _values.Length)
            {
                return _values[value];
            }

            return value.ToString(System.Globalization.CultureInfo.InvariantCulture) + _suffix;
        }
    }
}
