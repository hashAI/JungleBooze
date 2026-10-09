namespace JungleBooze.Core.Feedback
{
    /// <summary>Device haptics. Implementations must not allocate per call.</summary>
    public interface IHaptics
    {
        /// <summary>True when the device can play impacts (false in the editor and on devices without a Taptic Engine).</summary>
        bool Supported { get; }

        void Impact(HapticImpact strength);
    }
}
