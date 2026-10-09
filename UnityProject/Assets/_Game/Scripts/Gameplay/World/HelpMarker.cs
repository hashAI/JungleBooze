using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// First-run slow-time help marker ([ST:move], GDD §16), chunk-local: the obstacle's front <see cref="S"/> and,
    /// for steering, the x range the runner must leave.
    /// </summary>
    [Serializable]
    public struct HelpMarker
    {
        public HelpMove Move;
        public float S;
        public float XMin;
        public float XMax;
    }
}
