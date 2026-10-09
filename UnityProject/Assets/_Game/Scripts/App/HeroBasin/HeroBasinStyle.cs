namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Rendering style of the hero basin (ADR 0009). Each style has its own config asset, scene, materials and meshes,
    /// so both can be built, compared and rolled back independently. Presentation only.
    /// </summary>
    public enum HeroBasinStyle
    {
        /// <summary>Iteration 3 look (keyframe F4_e): URP PBR in Nature Lit, realistic water and falls.</summary>
        Realistic = 0,

        /// <summary>Painterly / stylized-realistic trial (keyframe F4_f): the _PAINTERLY shader variants.</summary>
        Painterly = 1,
    }
}
