namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>The three power-ups of GDD 10. Stored in events and replays: append only.</summary>
    public enum PowerUpType : byte
    {
        None = 0,

        /// <summary>Pulls coins from all 3 lanes within 10 m.</summary>
        Magnet = 1,

        /// <summary>Absorbs one hit; the obstacle shatters; 1.0 s invulnerability after.</summary>
        Shield = 2,

        /// <summary>1.6× speed, invulnerable, auto-collects coins in the lane, smashes obstacles, auto-jumps gaps.</summary>
        SpeedBoost = 3,
    }
}
