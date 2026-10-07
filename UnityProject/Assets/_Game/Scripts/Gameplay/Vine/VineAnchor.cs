namespace JungleBooze.Gameplay.Vine
{
    /// <summary>
    /// One live vine as the runner sees it (GDD 7.2): a grab point over one lane. Written by
    /// <see cref="IVineTrackQuery.GetVines"/>; plain value, no allocation.
    /// </summary>
    public struct VineAnchor
    {
        /// <summary>Per-run vine id starting at 1.</summary>
        public int Id;

        /// <summary>Vines of one vine section share a group (the chunk serial); chains only run inside a group.</summary>
        public int Group;

        /// <summary>0 = first vine of the section, 1 = second (chain), 2 = third.</summary>
        public int Row;

        public int Lane;

        /// <summary>World z of the grab point (centre of the grab zone).</summary>
        public double Z;

        /// <summary>A chasm lies below: missing this vine is a death ("Missed vine").</summary>
        public bool OverChasm;
    }
}
