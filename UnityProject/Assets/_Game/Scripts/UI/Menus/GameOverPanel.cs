using System.Globalization;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Game Over / Results panel (GDD 19, spec 002 12.1; placeholder look): title, cause line, the run's score in
    /// big digits with a "New best!" stamp, rows for distance, coins this run and best score, the seed in
    /// development builds, then Play again (primary), Same track (secondary) and Home (neutral). All buttons ignore
    /// input during the Game Over input lock. Texts are formatted once when the panel opens.
    /// </summary>
    public sealed class GameOverPanel
    {
        private const float WidthPt = 320f;
        private const float HeightPt = 520f;
        private const float RowWidthPt = 260f;
        private const float FirstRowYPt = -176f;
        private const float RowStepPt = 30f;
        private const int TitleFontSize = 34;
        private const int ScoreFontSize = 44;
        private const int RowFontSize = 20;
        private const int SmallFontSize = 15;
        private const int PrimaryFontSize = 26;
        private const int ButtonFontSize = 20;
        private const int MaxScoreDigits = 8;

        private readonly GameObject _root;
        private readonly Text _cause;
        private readonly DigitCounter _score;
        private readonly Text _newBest;
        private readonly Text _distanceValue;
        private readonly Text _coinsValue;
        private readonly Text _bestValue;
        private readonly Text _seed;
        private readonly Text _metaLine;

        public GameOverPanel(Transform safeArea, Font font, IRunCommands commands)
        {
            Image fill = HudFactory.CreatePanel(
                safeArea, "GameOverPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(WidthPt, HeightPt), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);

            MenuFactory.Label(panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 50f), new Vector2(0f, -10f), HudStrings.GameOver);
            _cause = MenuFactory.Label(panel, "Cause", font, 18, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 26f), new Vector2(0f, -58f), string.Empty);
            MenuFactory.Label(panel, "ScoreLabel", font, 17, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 22f), new Vector2(0f, -88f), MenuStrings.Score);

            _score = MenuFactory.Counter(panel, "FinalScore", font, ScoreFontSize, MaxScoreDigits, null, true);
            HudFactory.Place((RectTransform)_score.transform, top, new Vector2(RowWidthPt, 56f), new Vector2(0f, -108f));

            // "New best!" stamp on the panel's top-right corner (style guide 8.1: pulp orange, ink outline, rotated).
            _newBest = HudFactory.CreateText(panel, "NewBestStamp", font, 22, StylePalette.PulpOrange, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_newBest.rectTransform, new Vector2(1f, 1f), new Vector2(130f, 30f), new Vector2(16f, 22f));
            _newBest.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            _newBest.text = HudStrings.NewBest;

            _distanceValue = MenuFactory.Row(panel, "DistanceRow", font, RowFontSize, MenuStrings.Distance, RowWidthPt, FirstRowYPt, out _);
            _coinsValue = MenuFactory.Row(panel, "CoinsRow", font, RowFontSize, MenuStrings.CoinsThisRun, RowWidthPt, FirstRowYPt - RowStepPt, out _);
            _bestValue = MenuFactory.Row(panel, "BestRow", font, RowFontSize, MenuStrings.BestScore, RowWidthPt, FirstRowYPt - 2f * RowStepPt, out _);
            _seed = MenuFactory.Label(
                panel, "Seed", font, SmallFontSize, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 22f), new Vector2(0f, FirstRowYPt - 3f * RowStepPt), string.Empty);

            // What the run earned from missions and the daily challenge (GDD 13); empty most runs.
            _metaLine = MenuFactory.Label(
                panel, "MetaLine", font, SmallFontSize, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 40f), new Vector2(0f, FirstRowYPt - 3f * RowStepPt - 24f), string.Empty);

            PlayAgainButton = MenuFactory.PrimaryButton(
                panel, "RunAgainButton", font, HudStrings.RunAgain, PrimaryFontSize, bottom, new Vector2(RowWidthPt, 80f), new Vector2(0f, 104f), commands.Restart);
            SameTrackButton = MenuFactory.SecondaryButton(
                panel, "SameTrackButton", font, HudStrings.SameTrack, ButtonFontSize, bottom, new Vector2(126f, 56f), new Vector2(-67f, 38f), commands.RestartSameTrack);
            HomeButton = MenuFactory.NeutralButton(
                panel, "HomeButton", font, MenuStrings.Home, ButtonFontSize, bottom, new Vector2(126f, 56f), new Vector2(67f, 38f), commands.GoHome);
#if UNITY_EDITOR || UNITY_STANDALONE
            MenuFactory.Label(panel, "KeyHint", font, SmallFontSize, TextAnchor.MiddleCenter, bottom, new Vector2(RowWidthPt, 24f), new Vector2(0f, 8f), HudStrings.GameOverKeyHint);
#endif

            _root.SetActive(false);
        }

        public Button PlayAgainButton { get; }

        public Button SameTrackButton { get; }

        public Button HomeButton { get; }

        public bool Visible => _root.activeSelf;

        public string CauseText => _cause.text;

        public bool NewBestVisible => _newBest.gameObject.activeSelf;

        /// <summary>Fills and shows the panel (allocates a few strings; once per Game Over).</summary>
        public void Show(GameSession session, PlayerSave save)
        {
            Show(session, save, null);
        }

        /// <summary>
        /// Like <see cref="Show(GameSession, PlayerSave)"/>; <paramref name="metaLine"/> (one or two lines on missions
        /// and the daily challenge) is shown under the rows when not empty.
        /// </summary>
        public void Show(GameSession session, PlayerSave save, string metaLine)
        {
            bool hasMeta = !string.IsNullOrEmpty(metaLine);
            _metaLine.gameObject.SetActive(hasMeta);
            _metaLine.text = hasMeta ? metaLine : string.Empty;

            long score = session.Score;
            _cause.text = HudView.CauseTextFor(session);
            _score.SetValue(score > int.MaxValue ? int.MaxValue : (int)score);
            _distanceValue.text = HudView.MetersOf(session.DistanceM).ToString(CultureInfo.InvariantCulture) + " " + HudStrings.DistanceUnit;
            _coinsValue.text = session.Coins.ToString(CultureInfo.InvariantCulture);

            // The run driver records the run (and its coins) before the panel opens; fall back to the session
            // values if it did not (no save attached).
            long best = save != null ? save.BestScore : score;
            if (best < score)
            {
                best = score;
            }

            _bestValue.text = best.ToString(CultureInfo.InvariantCulture);

            // [ASSUMED] No stamp when there was no previous best (the very first run is not a "record").
            RunRecord run = save != null ? save.LastRun : default;
            bool newBest = run.IsNewBestScore && run.PreviousBestScore > 0L && run.Score == score;
            _newBest.gameObject.SetActive(newBest);

            // Seed: development builds only (spec 002 12.1).
            bool showSeed = Debug.isDebugBuild;
            _seed.gameObject.SetActive(showSeed);
            if (showSeed)
            {
                _seed.text = HudStrings.Seed + " " + session.RunSeed.ToString(CultureInfo.InvariantCulture);
            }

            SetLocked(session.GameOverInputLocked);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>Buttons stay disabled during the Game Over input lock (spec 002 12.1).</summary>
        public void SetLocked(bool locked)
        {
            PlayAgainButton.interactable = !locked;
            SameTrackButton.interactable = !locked;
            HomeButton.interactable = !locked;
        }
    }
}
