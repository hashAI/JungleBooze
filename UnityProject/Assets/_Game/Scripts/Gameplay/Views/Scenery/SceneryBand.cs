namespace JungleBooze.Gameplay.Views
{
    /// <summary>The lateral bands beside the path (spec 003 section 11.1). Values feed the placement hash.</summary>
    public enum SceneryBand : byte
    {
        /// <summary>From the sight corridor edge to 9 m: ferns, bushes, small rocks, roots, thin trunks.</summary>
        Near = 0,

        /// <summary>9 to 25 m: big trees, boulders, columns.</summary>
        Mid = 1,

        /// <summary>Overhead: branch stubs with their tufts and hanging vines, light shafts.</summary>
        Overhead = 2,

        /// <summary>Wall ring (dense corridor): thick trunks, big opaque leaf masses, branch stubs hugging the corridor edge.</summary>
        Wall = 3,

        /// <summary>Far ring: 26 m and beyond, silhouettes that sit in the fog.</summary>
        Far = 4,

        /// <summary>Overhead leaf masses (12 m and higher above the path) and the vines hanging from them.</summary>
        Canopy = 5,

        /// <summary>Ground cover: ferns, small rocks, roots and leaf litter decals.</summary>
        Cover = 6,
    }
}
