namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// A route split (spec 102 §3.3): a divider from <see cref="SFront"/> to <see cref="SMerge"/>. The branch is the
    /// side of the divider centre the runner's x is on (tie → <see cref="SafeSide"/>).
    /// </summary>
    public readonly struct ForkPoint
    {
        public ForkPoint(float sFront, float sMerge, float dividerCenterX, float dividerHalfWidth, int safeSide)
            : this(-1, sFront, sMerge, dividerCenterX, dividerHalfWidth, safeSide)
        {
        }

        public ForkPoint(int id, float sFront, float sMerge, float dividerCenterX, float dividerHalfWidth, int safeSide)
        {
            Id = id;
            SFront = sFront;
            SMerge = sMerge;
            DividerCenterX = dividerCenterX;
            DividerHalfWidth = dividerHalfWidth;
            SafeSide = safeSide < 0 ? -1 : 1;
        }

        /// <summary>Stable id for the run (events, nudge memory); −1 = use the index.</summary>
        public int Id { get; }

        public float SFront { get; }

        public float SMerge { get; }

        public float DividerCenterX { get; }

        public float DividerHalfWidth { get; }

        /// <summary>−1 = left branch is the safe one, +1 = right.</summary>
        public int SafeSide { get; }
    }
}
