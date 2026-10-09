using UnityEngine;

namespace JungleBooze.Gameplay.Views.World
{
    /// <summary>
    /// Gray-box materials of the Expedition scene (spec 101 §6 colours plus route tints, Part B placeholder tints,
    /// crystals, the Shield and discovery markers). Created by the Expedition scene builder. Submesh order of the
    /// baked chunk meshes is <see cref="ChunkMeshBuilder"/>'s <c>Sub*</c> constants.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/World/WorldPalette", fileName = "WorldPalette")]
    public sealed class WorldPalette : ScriptableObject
    {
        public Material Path;
        public Material PathSafe;
        public Material PathRisky;
        public Material PathSecret;
        public Material PathSide;
        public Material Hedge;
        public Material Low;
        public Material High;
        public Material Blocker;
        public Material Thorns;
        public Material Divider;
        public Material WaterPlaceholder;
        public Material CanopyPlaceholder;
        public Material ShallowWater;
        public Material Curtain;
        public Material Marker;
        public Material Ground;
        public Material Coin;
        public Material Crystal;
        public Material Shield;
        public Material Discovery;
        public Material Runner;
        public Material RunnerAccent;
        public Material Leaf;

        /// <summary>Materials in submesh order for the baked chunk meshes.</summary>
        public Material[] ChunkMaterials()
        {
            return new[] { Path, PathSafe, PathRisky, PathSecret, PathSide, Hedge, Low, High, Blocker, Thorns, Divider, WaterPlaceholder, CanopyPlaceholder, ShallowWater, Curtain, Marker };
        }
    }
}
