namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Swim dive phases (spec 103 §4.3). Only <see cref="Under"/> is Submerged.</summary>
    public enum DivePhase : byte
    {
        None = 0,
        Down,
        Under,
        Up,
    }
}
