namespace JungleBooze.Core
{
    /// <summary>Kinds of <see cref="ReplayMarker"/>. Values are stored in replay files: append only.</summary>
    public enum ReplayMarkerKind : byte
    {
        None = 0,

        /// <summary>The player accepted a revive ("Continue?") while dead.</summary>
        Revive = 1,
    }
}
