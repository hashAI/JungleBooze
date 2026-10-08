using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>Interpolates HERO's position between two simulation snapshots (spec 001 section 10.4).</summary>
    public static class RunnerInterpolation
    {
        public static void Evaluate(in RunnerState previous, in RunnerState current, float alpha, out float x, out float y, out double z)
        {
            if (alpha <= 0f)
            {
                alpha = 0f;
            }
            else if (alpha >= 1f)
            {
                alpha = 1f;
            }

            x = previous.X + (current.X - previous.X) * alpha;
            y = previous.Y + (current.Y - previous.Y) * alpha;
            z = previous.Z + (current.Z - previous.Z) * alpha;
        }
    }
}
