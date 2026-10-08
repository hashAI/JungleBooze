namespace JungleBooze.Services.Audio
{
    /// <summary>Clips under <c>Assets/_Game/Audio</c>. Append only; playback and the catalog switch on these values.</summary>
    public enum AudioClipId : byte
    {
        None = 0,

        Jump = 1,
        Slide = 2,
        LaneSwitch = 3,
        Coin = 4,
        Stumble = 5,
        Death = 6,
        NearMiss = 7,
        VineGrab = 8,
        PowerUp = 9,
        ShieldPop = 10,
        SpeedBoost = 11,
        UiTap = 12,

        CallVine = 20,
        CallDanger = 21,
        CallCheerShiny = 22,
        CallCheerWow = 23,
        CallCheerWoohoo = 24,
        SquawkVine = 25,
        SquawkDanger = 26,
        SquawkCheer = 27,
        ChatterPretty = 28,
        ChatterMine = 29,
        ChatterGiggle = 30,

        JungleThemeA = 40,
        JungleThemeB = 41,
        JungleThemeC = 42,
        MenuLoop = 43,
        GameOverSting = 44,
    }
}
