namespace JungleBooze.Core
{
    /// <summary>
    /// A change to the run that is not player input, recorded so a replay can apply it on the same session tick
    /// (replay format 3, ADR 0006 amendment). Applied before the input of <see cref="Tick"/> is read.
    /// </summary>
    public readonly struct ReplayMarker
    {
        public ReplayMarker(long tick, ReplayMarkerKind kind)
        {
            Tick = tick;
            Kind = kind;
        }

        /// <summary>Session tick (the replay clock) the marker applies on.</summary>
        public long Tick { get; }

        public ReplayMarkerKind Kind { get; }
    }
}
