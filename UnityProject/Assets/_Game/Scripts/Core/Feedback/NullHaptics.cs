namespace JungleBooze.Core.Feedback
{
    /// <summary>No-op haptics (editor, tests, unsupported devices). Counts calls for tests and the debug overlay.</summary>
    public sealed class NullHaptics : IHaptics
    {
        public bool Supported => false;

        public int LightCount { get; private set; }

        public int MediumCount { get; private set; }

        public int HeavyCount { get; private set; }

        public void Impact(HapticImpact strength)
        {
            switch (strength)
            {
                case HapticImpact.Light:
                    LightCount++;
                    break;
                case HapticImpact.Medium:
                    MediumCount++;
                    break;
                default:
                    HeavyCount++;
                    break;
            }
        }
    }
}
