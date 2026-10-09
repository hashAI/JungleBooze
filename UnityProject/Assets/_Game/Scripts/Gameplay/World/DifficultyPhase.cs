namespace JungleBooze.Gameplay.World
{
    /// <summary>Difficulty phases by distance (spec 102 §5).</summary>
    public enum DifficultyPhase : byte
    {
        Learning = 0,
        Rhythm,
        Decision,
        Challenge,
        Danger,
        Mastery,
    }
}
