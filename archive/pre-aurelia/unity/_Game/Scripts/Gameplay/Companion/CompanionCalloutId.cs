namespace JungleBooze.Gameplay.Companion
{
    /// <summary>
    /// Language-neutral companion call-outs (GDD 15.1). Stored in <c>RunnerEvent.Value</c> of
    /// <c>CompanionCallout</c> events; append-only. Audio and text map these through
    /// <see cref="CompanionCalloutKeys"/> (<c>call.vine</c>, <c>call.danger</c>, <c>call.cheer.1..3</c>).
    /// </summary>
    public enum CompanionCalloutId : byte
    {
        None = 0,

        /// <summary>"Vine!" (rising), 2.0 s before a vine section; the macaw swoops over the vine lane.</summary>
        Vine = 1,

        /// <summary>"Look out!" (urgent), 1.5 s before a mover or signature hazard; swoop over that lane.</summary>
        Danger = 2,

        /// <summary>Cheer variant 1 (Perfect release, new record).</summary>
        Cheer1 = 3,

        Cheer2 = 4,

        Cheer3 = 5,
    }
}
