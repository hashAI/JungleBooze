using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>Range-check helpers shared by the spec 002 config validators. Setup time only (allocates messages).</summary>
    internal static class ConfigChecks
    {
        public static void Range(List<string> errors, string name, double value, double min, double max)
        {
            if (!(value >= min && value <= max))
            {
                errors.Add(name + " = " + value + " is outside " + min + "–" + max + ".");
            }
        }

        public static void Positive(List<string> errors, string name, double value)
        {
            if (!(value > 0.0))
            {
                errors.Add(name + " must be positive.");
            }
        }
    }
}
