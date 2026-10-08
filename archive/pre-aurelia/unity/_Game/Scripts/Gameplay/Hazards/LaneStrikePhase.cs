namespace JungleBooze.Gameplay.Hazards
{
    /// <summary>
    /// Cycle of a telegraphed lane strike (GDD 8.3): dormant until HERO comes within the trigger lead, then
    /// Warning → Active → Rest → Warning … on a fixed rhythm. Only <see cref="Active"/> has a hitbox.
    /// </summary>
    public enum LaneStrikePhase : byte
    {
        Dormant = 0,

        /// <summary>The lane marker pulses; no hitbox yet.</summary>
        Warning = 1,

        /// <summary>The strike is in the lane (falling rocks, water spout, darts): lethal from the front.</summary>
        Active = 2,

        /// <summary>The strike is over until the next warning.</summary>
        Rest = 3,
    }
}
