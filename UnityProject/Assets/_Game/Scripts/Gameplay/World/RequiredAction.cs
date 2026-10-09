namespace JungleBooze.Gameplay.World
{
    /// <summary>A jump or slide a route can't steer around (validator V2/V5).</summary>
    public readonly struct RequiredAction
    {
        public RequiredAction(float s, float x, bool jump)
        {
            S = s;
            X = x;
            Jump = jump;
        }

        public float S { get; }

        public float X { get; }

        /// <summary>True = jump (low, thorns, gap), false = slide (high).</summary>
        public bool Jump { get; }
    }
}
