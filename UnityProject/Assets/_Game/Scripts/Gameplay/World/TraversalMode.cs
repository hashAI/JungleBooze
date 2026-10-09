namespace JungleBooze.Gameplay.World
{
    /// <summary>Traversal set pieces (spec 103 §4–7). Part A authors them as data only; the gray-box stand-ins are run on foot.</summary>
    public enum TraversalMode : byte
    {
        Swim = 0,
        Vine,
        Canopy,
        DeepDive,
        Creature,

        /// <summary>Ankle-deep ford: ground with splash footsteps, no speed change (spec 103 §3.5). Implemented.</summary>
        ShallowWater,

        /// <summary>A water curtain the runner passes through (no collision; VFX/SFX only).</summary>
        Curtain,
    }
}
