namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Haptic paired with a cue (spec 103 §11: L light, M medium, H heavy, S success). Success currently plays a
    /// medium impact (the native plugin only has impact generators; a notification generator is a later hook).
    /// </summary>
    public enum CueHaptic : byte
    {
        None = 0,
        Light,
        Medium,
        Heavy,
        Success,
    }
}
