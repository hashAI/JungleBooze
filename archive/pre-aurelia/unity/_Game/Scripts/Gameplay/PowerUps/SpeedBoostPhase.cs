namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>Phases of an active Speed Boost (GDD 10).</summary>
    public enum SpeedBoostPhase : byte
    {
        /// <summary>No boost.</summary>
        None = 0,

        /// <summary>Full speed multiplier, invulnerable, coin auto-collect, gap auto-jump.</summary>
        Dash = 1,

        /// <summary>The dash time ran out in the air: the dash goes on until HERO lands (GDD 10 "never ends in the air").</summary>
        DashUntilLanding = 2,

        /// <summary>Speed eases back to normal, still invulnerable; the clear stretch ahead was cleared when it started.</summary>
        Slowdown = 3,
    }
}
