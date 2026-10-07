namespace JungleBooze.Gameplay.Views
{
    /// <summary>Silhouette of a generated stand-in or far-LOD mesh (see <see cref="SceneryMeshes"/>).</summary>
    public enum SceneryShape : byte
    {
        /// <summary>Short trunk with one canopy blob.</summary>
        Tree = 0,

        /// <summary>Tall tapered column with a small crown blob at the top.</summary>
        Trunk = 1,

        /// <summary>One ellipsoid (bushes, ferns, rocks).</summary>
        Blob = 2,

        /// <summary>A low horizontal arch lying on the ground.</summary>
        Root = 3,

        /// <summary>A thin hanging strand from y = 0 down to the bottom of the bounds.</summary>
        Vine = 4,

        /// <summary>A one-metre quad standing on y = 0 with a soft vertical gradient.</summary>
        Shaft = 5,

        /// <summary>Tall wall-ring trunk with a flared, dark-based foot (no crown).</summary>
        WallTrunk = 6,

        /// <summary>Opaque clump of three overlapping blobs (leaf mass, bottom at y = 0). Three variants.</summary>
        LeafClump = 7,

        /// <summary>A unit-long tapered branch along +Y (the placer rolls it to horizontal).</summary>
        Stub = 8,

        /// <summary>Single-material silhouette: thin trunk with two crown blobs (far ring, sits in the fog).</summary>
        FarTree = 9,

        /// <summary>A flat irregular fan lying on the ground (leaf litter decal).</summary>
        Litter = 10,
    }
}
