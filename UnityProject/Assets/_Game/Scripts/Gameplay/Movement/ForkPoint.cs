namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// A route split (spec 102 §3.3): a divider from <see cref="SFront"/> to <see cref="SMerge"/>. The branch is the
    /// side of the divider centre the runner's x is on (tie → <see cref="SafeSide"/>).
    /// </summary>
    public readonly struct ForkPoint
    {
        public ForkPoint(float sFront, float sMerge, float dividerCenterX, float dividerHalfWidth, int safeSide)
        {
            SFront = sFront;
            SMerge = sMerge;
            DividerCenterX = dividerCenterX;
            DividerHalfWidth = dividerHalfWidth;
            SafeSide = safeSide < 0 ? -1 : 1;
        }

        public float SFront { get; }

        public float SMerge { get; }

        public float DividerCenterX { get; }

        public float DividerHalfWidth { get; }

        /// <summary>−1 = left branch is the safe one, +1 = right.</summary>
        public int SafeSide { get; }
    }
}
