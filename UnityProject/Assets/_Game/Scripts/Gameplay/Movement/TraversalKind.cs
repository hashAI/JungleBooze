namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Traversal outcomes for analytics (GDD §22 <c>traversal_result.type</c>) and the DDA traversal score.</summary>
    public enum TraversalKind : byte
    {
        Jump = 0,
        Slide,
        Dodge,
        SwimDive,
        SwimLeap,
        VineGood,
        VinePerfect,
        BeamGap,
    }
}
