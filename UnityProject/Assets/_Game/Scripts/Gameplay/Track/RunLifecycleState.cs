namespace JungleBooze.Gameplay.Track
{
    /// <summary>FP1 run lifecycle states (spec 002 section 12.1).</summary>
    public enum RunLifecycleState : byte
    {
        /// <summary>Run set up with a seed, track generated, HERO idle; waiting for the first input.</summary>
        Ready = 0,

        /// <summary>The simulation steps.</summary>
        Running = 1,

        /// <summary>After <c>Died</c>: simulation frozen, hit-pause then camera hold; all input ignored.</summary>
        Dying = 2,

        /// <summary>Game Over panel; Run again / Same track after the input lock.</summary>
        GameOver = 3,
    }
}
