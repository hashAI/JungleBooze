namespace JungleBooze.Editor.Build
{
    /// <summary>Kind of iOS player build produced by <see cref="BuildScript"/>.</summary>
    public enum IosBuildType
    {
        /// <summary>Optimized player without the development console or profiler hooks. Default.</summary>
        Release = 0,

        /// <summary>Development player (Debug.isDebugBuild is true; input recording stays on).</summary>
        Development = 1,
    }
}
