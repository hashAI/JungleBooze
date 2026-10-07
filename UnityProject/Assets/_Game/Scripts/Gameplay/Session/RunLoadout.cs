namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// What the player brings into one run from outside it (GDD 13): power-up upgrade levels, an optional Shield
    /// start and Head Start, and the permanent score multiplier. Built by the composition root from the save and
    /// applied once, before the run's first tick. A value of 0 for a level or the multiplier means "default".
    /// </summary>
    public readonly struct RunLoadout
    {
        public RunLoadout(int magnetLevel, int shieldLevel, int speedBoostLevel, bool startShield, int headStartMeters, int scoreMultiplier)
        {
            MagnetLevel = magnetLevel;
            ShieldLevel = shieldLevel;
            SpeedBoostLevel = speedBoostLevel;
            StartShield = startShield;
            HeadStartMeters = headStartMeters;
            ScoreMultiplier = scoreMultiplier;
        }

        public int MagnetLevel { get; }

        public int ShieldLevel { get; }

        public int SpeedBoostLevel { get; }

        /// <summary>The run starts with a Shield already active.</summary>
        public bool StartShield { get; }

        /// <summary>The run starts with a Speed Boost dash of about this many meters (0 = none).</summary>
        public int HeadStartMeters { get; }

        public int ScoreMultiplier { get; }
    }
}
