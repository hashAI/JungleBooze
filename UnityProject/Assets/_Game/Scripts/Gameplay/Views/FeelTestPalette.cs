using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>Gray-box materials for the feel course (spec 101 §6 colours). Created by the FeelTest scene builder.</summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/FeelTestPalette", fileName = "FeelTestPalette")]
    public sealed class FeelTestPalette : ScriptableObject
    {
        public Material Path;
        public Material PathSafe;
        public Material PathRisky;
        public Material PathSide;
        public Material Ground;
        public Material Hedge;
        public Material Low;
        public Material High;
        public Material Blocker;
        public Material Thorns;
        public Material Coin;
        public Material Divider;
        public Material Finish;
        public Material Runner;
        public Material RunnerAccent;
        public Material DebugHitbox;
        public Material DebugRunner;
        public Material DebugTarget;
        public Material Marker;

        /// <summary>Edge-brush leaf particles (URP Particles/Simple Lit, vertex colour).</summary>
        public Material Leaf;
    }
}
