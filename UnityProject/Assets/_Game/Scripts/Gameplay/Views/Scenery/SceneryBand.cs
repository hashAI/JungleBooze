namespace JungleBooze.Gameplay.Views
{
    /// <summary>The lateral bands beside the path (spec 003 section 11.1). Values feed the placement hash.</summary>
    public enum SceneryBand : byte
    {
        /// <summary>From the sight corridor edge to 9 m: ferns, bushes, small rocks, roots, thin trunks.</summary>
        Near = 0,

        /// <summary>9 to 25 m: big trees, boulders, columns.</summary>
        Mid = 1,

        /// <summary>Overhead: hanging vines and light shafts (outside the corridor).</summary>
        Overhead = 2,
    }
}
