namespace JungleBooze.App.Perf
{
    /// <summary>What the device benchmark does in a phase's scene.</summary>
    public enum DeviceBenchMode
    {
        /// <summary>Hero basin: fixed keyframe camera (landscape or portrait pose from the hero config).</summary>
        HeroView = 0,

        /// <summary>Expedition: the Perfect bot plays run after run with an in-memory save.</summary>
        ExpeditionBot = 1,
    }
}
