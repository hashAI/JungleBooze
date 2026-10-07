namespace JungleBooze.Gameplay.Tutorial
{
    /// <summary>
    /// What the tutorial is telling the player right now (GDD 12). Language-neutral: the UI maps each value to a
    /// string. Values are only used at runtime, never saved.
    /// </summary>
    public enum TutorialHint : byte
    {
        None = 0,

        /// <summary>Obstacle across the lane: swipe left or right.</summary>
        Lateral = 1,

        /// <summary>Low log or gap: swipe up.</summary>
        Jump = 2,

        /// <summary>Low branch: swipe down.</summary>
        Slide = 3,

        /// <summary>Coin trail: "Grab coins!".</summary>
        Coins = 4,

        /// <summary>First vine: swipe up to grab.</summary>
        VineGrab = 5,

        /// <summary>Hanging on the vine: swipe up when it glows.</summary>
        VineRelease = 6,

        /// <summary>Meter full: double tap, Duko lifts HERO.</summary>
        Assist = 7,

        /// <summary>"You're on your own!"</summary>
        Outro = 8,

        /// <summary>After a rescue: gentle reminder to jump.</summary>
        RescueJump = 9,

        /// <summary>After a rescue: gentle reminder to slide.</summary>
        RescueSlide = 10,

        /// <summary>After a rescue: gentle reminder to change lane.</summary>
        RescueLateral = 11,

        /// <summary>After a rescue: gentle reminder to grab the vine.</summary>
        RescueVine = 12,
    }
}
