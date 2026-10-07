using JungleBooze.Gameplay.Tutorial;

namespace JungleBooze.UI.Tutorial
{
    /// <summary>
    /// English tutorial strings (GDD 12). [ASSUMED] Placeholder string table until the localization system exists,
    /// like <see cref="Hud.HudStrings"/>: the tutorial reads every word from here.
    /// </summary>
    public static class TutorialStrings
    {
        public const string Lateral = "Swipe left or right!";
        public const string Jump = "Swipe up to jump!";
        public const string Slide = "Swipe down to slide!";
        public const string Coins = "Grab coins!";
        // Fixed-pivot swing (spec 004): the rope sets the catch speed (13 to 16 m/s), so the lesson is only "jump to catch it";
        // the Perfect window is 183 ms around the gold part of the ring, so the lesson is "let go when the ring is gold".
        public const string VineGrab = "Jump to catch the rope!";
        public const string VineRelease = "Swipe up when the ring is gold!";
        public const string Outro = "You're on your own!";
        public const string Skip = "Skip";

#if UNITY_IOS || UNITY_ANDROID
        public const string Assist = "Double tap: Duko lifts you!";
#else
        public const string Assist = "Double-click or E: Duko lifts you!";
#endif

        // Gentle hints after a rescue (GDD 12: "Swipe up to jump!").
        public const string RescueJump = "Swipe up to jump!";
        public const string RescueSlide = "Swipe down to slide!";
        public const string RescueLateral = "Swipe left or right to dodge!";
        public const string RescueVine = "Jump to catch the rope!";

        // Ghost-hand symbols (arrows are in the UI font).
        public const string GlyphSides = "↔";
        public const string GlyphUp = "↑";
        public const string GlyphDown = "↓";
        public const string GlyphDoubleTap = "x2";

        /// <summary>Text for a hint. Never allocates.</summary>
        public static string For(TutorialHint hint)
        {
            switch (hint)
            {
                case TutorialHint.Lateral:
                    return Lateral;
                case TutorialHint.Jump:
                    return Jump;
                case TutorialHint.Slide:
                    return Slide;
                case TutorialHint.Coins:
                    return Coins;
                case TutorialHint.VineGrab:
                    return VineGrab;
                case TutorialHint.VineRelease:
                    return VineRelease;
                case TutorialHint.Assist:
                    return Assist;
                case TutorialHint.Outro:
                    return Outro;
                case TutorialHint.RescueJump:
                    return RescueJump;
                case TutorialHint.RescueSlide:
                    return RescueSlide;
                case TutorialHint.RescueLateral:
                    return RescueLateral;
                case TutorialHint.RescueVine:
                    return RescueVine;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Symbol shown on the ghost hand for a gesture. Never allocates.</summary>
        public static string GlyphFor(TutorialGesture gesture)
        {
            switch (gesture)
            {
                case TutorialGesture.SwipeSides:
                    return GlyphSides;
                case TutorialGesture.SwipeUp:
                    return GlyphUp;
                case TutorialGesture.SwipeDown:
                    return GlyphDown;
                case TutorialGesture.DoubleTap:
                    return GlyphDoubleTap;
                default:
                    return string.Empty;
            }
        }
    }
}
