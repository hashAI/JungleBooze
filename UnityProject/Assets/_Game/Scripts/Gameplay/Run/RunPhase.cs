namespace JungleBooze.Gameplay.Run
{
    /// <summary>Run lifecycle (spec 101 §6, GDD §17): Ready → Running → Dying/Finishing → Results → (restart) Ready.</summary>
    public enum RunPhase : byte
    {
        Ready = 0,
        Running = 1,
        Dying = 2,
        Finishing = 3,
        Results = 4,
    }
}
