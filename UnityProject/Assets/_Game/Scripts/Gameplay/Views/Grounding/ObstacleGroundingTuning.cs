namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Presentation-only tuning of the grounded obstacle rigs (spec 005 section 16), plain C# like
    /// <see cref="ScenerySettings"/>; never part of the simulation hash. Field defaults are the [ASSUMED] start values.
    /// </summary>
    public sealed class ObstacleGroundingTuning
    {
        // Contact (G1, G2).

        /// <summary>Lowest embed of a model below the ground surface (m).</summary>
        public float EmbedMinM = 0.03f;

        /// <summary>Deepest embed on flat ground (m); slopes add to it.</summary>
        public float EmbedMaxM = 0.12f;

        /// <summary>A ground-level model whose lowest point is higher than this above the ground fails the audit (m).</summary>
        public float MaxFloatM = 0.02f;

        /// <summary>Contact decal size relative to the footprint (1.1 to 1.4).</summary>
        public float ContactDecalScale = 1.2f;

        /// <summary>Pebbles, bark flakes and leaves per object (4 to 10), each 0.15 m or lower.</summary>
        public int DebrisPerObject = 6;

        // Fill (G6).

        public float GhostMaxStaticM = 0.15f;
        public float GhostMaxMoverM = 0.25f;

        // Context pieces (3.3).

        public float CpZoneInnerM = 4.2f;
        public float CpZoneOuterM = 7f;

        /// <summary>Underside of the support limb over the path (m).</summary>
        public float LimbHeightM = 4.6f;

        /// <summary>Curvature (rad/m) from which a bend has an outer side for context pieces (R of 400 m or less).</summary>
        public float BendCurvature = 1f / 400f;

        // Sway.

        public float MatSwayDeg = 3f;
        public float MatSwayNearDeg = 0.5f;
        public float MatSwayHz = 0.4f;
        public float SwayFadeDistM = 12f;
        public float CaneSwayDeg = 1f;
        public float CaneSwayHz = 1.2f;

        // Mover (8.4).

        public float RollRadiusM = 0.95f;
        public float RockIdleDeg = 1.5f;
        public float RockAnticipationDeg = 5f;
        public float RockHz = 0.7f;
        public float AnticipationS = 0.3f;
        public float SinkOnSettleM = 0.10f;
        public int SinkTicks = 6;
        public float ChockPopS = 0.4f;
        public float DustPuffsPerS = 20f;
        public float DustPuffsPerSReduced = 6f;
        public float DustLifeS = 0.5f;
        public int DustBurstCount = 6;

        // Spawn and pop-in (11).

        /// <summary>A rig is built when its front is this close (m); the nearest point of its decals is at least 95 m out.</summary>
        public float SpawnAheadM = 100f;

        /// <summary>No state change that adds geometry happens closer than this (m, the fog start).</summary>
        public float PopInMinDistM = 45f;

        /// <summary>
        /// When true and the old unit-cube art (Obstacle_LowBarrier, Obstacle_HighBarrier, Obstacle_FullBlock,
        /// Hazard_ThornPatch) exists, it is used as the body in place of the grounded gray-box body. Off: the old
        /// models are stretched to the hitbox and never touch the ground, which spec 005 retires.
        /// </summary>
        public bool UseLegacyArt;

        /// <summary>Wedged slab lean (degrees); 1.5 keeps the top within 0.08 m of the box.</summary>
        public float WedgedSlabLeanDeg = 1.5f;
    }
}
