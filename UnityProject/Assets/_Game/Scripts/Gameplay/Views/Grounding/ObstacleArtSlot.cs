namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Art slots of the grounded obstacle rigs (spec 005 15.2). Each slot is one model in
    /// <c>Resources/EnvironmentArt</c>; a missing model leaves the procedural gray-box piece. See
    /// <see cref="ObstacleArtSlots"/> for the names and the authoring convention.
    /// </summary>
    public enum ObstacleArtSlot
    {
        LogTrunk = 0,
        LogTrunkB = 1,
        LogTrunkC = 2,
        RootPlate = 3,
        RootPlateB = 4,
        Stump = 5,
        RockHump = 6,
        HangMat = 7,
        HangMatB = 8,
        HangMatC = 9,
        Limb = 10,
        LianaTie = 11,
        ButtressFin = 12,
        StandingStone = 13,
        WedgedSlab = 14,
        RootWeb = 15,
        BarrelBoulder = 16,
        Wallow = 17,
        Chock = 18,
        Furrow = 19,
        SandBar = 20,
        ThornCage = 21,
        CaneWall = 22,
        RootCrown = 23,
        ThornLitter = 24,

        // Old unit-cube models, used only when ObstacleGroundingTuning.UseLegacyArt is on.
        LegacyLow = 25,
        LegacyHigh = 26,
        LegacyFull = 27,
        LegacyThorn = 28,
    }
}
