namespace JungleBooze.Gameplay.Path
{
    /// <summary>Which layer of the route the centerline is on (spec 003 section 7). Presentation only.</summary>
    public enum PathLayer : byte
    {
        /// <summary>The trail on the forest floor.</summary>
        Floor = 0,

        /// <summary>The canopy, cliff ledge or wall-top.</summary>
        High = 1,
    }
}
