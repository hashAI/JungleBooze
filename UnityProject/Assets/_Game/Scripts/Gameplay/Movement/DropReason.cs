namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Why a command was dropped (spec 101 §2.5, §2.7).</summary>
    public enum DropReason : byte
    {
        None = 0,
        Buffer,
        Ceiling,

        /// <summary>Swipe down or flick during a vine swing (spec 103 §5.4).</summary>
        Swing,
    }
}
