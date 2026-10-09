using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A vine (spec 103 §5.2), chunk-local: anchor sA / xVine, anchor height above the takeoff floor, length, takeoff
    /// lip, landing platform start and the perfect column s range (items there only Perfect arcs reach; validator
    /// V13). <see cref="ReleaseHelp"/> marks the vine that gets first-run release slow-time (V1 only).
    /// </summary>
    [Serializable]
    public struct VineAnchor
    {
        public float AnchorS;
        public float X;
        public float AnchorHeight;
        public float Length;
        public float LipS;
        public float LandingS;
        public float ColumnS0;
        public float ColumnS1;
        public bool ReleaseHelp;
    }
}
