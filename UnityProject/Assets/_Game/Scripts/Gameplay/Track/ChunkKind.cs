namespace JungleBooze.Gameplay.Track
{
    /// <summary>Kind of a chunk (spec 002 section 4.1). Values are stored in chunk assets: append only.</summary>
    public enum ChunkKind : byte
    {
        /// <summary>First chunk of every run; no obstacles.</summary>
        Start = 0,

        /// <summary>Selected by tier weights.</summary>
        Normal = 1,

        /// <summary>Coins only; inserted by the breather schedule and as the seam fallback.</summary>
        Breather = 2,

        /// <summary>Reserved (vine sections, spec 003).</summary>
        Vine = 3,

        /// <summary>Reserved (world gateways).</summary>
        Gateway = 4,

        /// <summary>Reserved (signature hazards).</summary>
        Signature = 5,
    }
}
