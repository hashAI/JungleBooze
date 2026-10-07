using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The gray-box look of one world (style guide section 5): light, fog, ground and obstacle tints. A plain value,
    /// so blending two themes during a gateway never allocates. Built from <see cref="StylePalette"/> by
    /// <see cref="WorldThemes"/>.
    /// </summary>
    public struct WorldTheme
    {
        public Color KeyLight;

        /// <summary>Key light yaw and pitch of section 5 in degrees (the Jungle is -25 / 25).</summary>
        public float KeyYawDeg;

        public float KeyPitchDeg;

        public Color ShadowTint;
        public Color SkyHorizon;
        public Color Fog;
        public Color Path;
        public Color PathAlternate;
        public Color Verge;

        /// <summary>Body of low, high and full obstacles.</summary>
        public Color ObstacleBody;

        /// <summary>Body of movers (boulder, raft, snowball, stone disc).</summary>
        public Color MoverBody;

        /// <summary>Gateway frame color.</summary>
        public Color Accent;

        public static WorldTheme Lerp(in WorldTheme a, in WorldTheme b, float t)
        {
            return new WorldTheme
            {
                KeyLight = Color.Lerp(a.KeyLight, b.KeyLight, t),
                KeyYawDeg = Mathf.Lerp(a.KeyYawDeg, b.KeyYawDeg, t),
                KeyPitchDeg = Mathf.Lerp(a.KeyPitchDeg, b.KeyPitchDeg, t),
                ShadowTint = Color.Lerp(a.ShadowTint, b.ShadowTint, t),
                SkyHorizon = Color.Lerp(a.SkyHorizon, b.SkyHorizon, t),
                Fog = Color.Lerp(a.Fog, b.Fog, t),
                Path = Color.Lerp(a.Path, b.Path, t),
                PathAlternate = Color.Lerp(a.PathAlternate, b.PathAlternate, t),
                Verge = Color.Lerp(a.Verge, b.Verge, t),
                ObstacleBody = Color.Lerp(a.ObstacleBody, b.ObstacleBody, t),
                MoverBody = Color.Lerp(a.MoverBody, b.MoverBody, t),
                Accent = Color.Lerp(a.Accent, b.Accent, t),
            };
        }
    }
}
