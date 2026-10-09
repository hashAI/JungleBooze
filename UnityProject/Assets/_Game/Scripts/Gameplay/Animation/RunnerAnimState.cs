namespace JungleBooze.Gameplay.Animation
{
    /// <summary>Base-layer states of the runner's Animator Controller (one state per value, same names).</summary>
    public enum RunnerAnimState : byte
    {
        Idle = 0,
        Locomotion,
        Jump,
        Fall,
        Slide,
        Stumble,
        LandHard,
        Death,
    }
}
