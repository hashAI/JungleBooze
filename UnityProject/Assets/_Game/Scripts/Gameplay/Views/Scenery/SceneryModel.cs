namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Everything the scenery system can draw (spec 003 section 11). Values index the per-model tables in
    /// <see cref="ScenerySettings"/>: append only.
    /// </summary>
    public enum SceneryModel : byte
    {
        /// <summary>Big tree, art slot <c>Foliage_TreeA</c> (mid ring).</summary>
        TreeA = 0,

        /// <summary>Big tree, art slot <c>Foliage_TreeB</c> (mid ring).</summary>
        TreeB = 1,

        /// <summary>Giant trunk, art slot <c>Tree_Trunk</c>: tall columns in the mid ring, scaled thin (dia 0.6 m or less) in the near ring.</summary>
        GiantTrunk = 2,

        /// <summary>Bush, art slot <c>Foliage_Bush</c>.</summary>
        Bush = 3,

        /// <summary>Fern cluster, art slot <c>Foliage_FernCluster</c>.</summary>
        Fern = 4,

        /// <summary>Rock, art slot <c>Prop_Rock</c> (gray-box blob until the art exists).</summary>
        Rock = 5,

        /// <summary>Root arch on the ground, art slot <c>Prop_Root</c> (gray-box until the art exists).</summary>
        Root = 6,

        /// <summary>Hanging non-gameplay vine, art slot <c>Vine_Liana</c>, kept thin, short and dim so it never reads as the grab vine.</summary>
        HangingVine = 7,

        /// <summary>Soft light-shaft card (no art slot; a generated quad).</summary>
        LightShaft = 8,

        /// <summary>Wall-ring trunk (generated, flared dark foot), 30 m tall at scale 1.</summary>
        WallTrunk = 9,

        /// <summary>Opaque leaf mass, variant A (generated clump, wall ring, canopy, stub tufts).</summary>
        LeafMassA = 10,

        /// <summary>Leaf mass variant B (darker teal-green).</summary>
        LeafMassB = 11,

        /// <summary>Leaf mass variant C (yellower green).</summary>
        LeafMassC = 12,

        /// <summary>Branch stub growing from a wall trunk over the corridor edge (generated, along +Y, rolled by the placer).</summary>
        BranchStub = 13,

        /// <summary>Far-ring silhouette tree (generated, one material, one mesh for every distance).</summary>
        FarTree = 14,

        /// <summary>Leaf litter decal on the ground (generated flat fan).</summary>
        LeafLitter = 15,
    }
}
