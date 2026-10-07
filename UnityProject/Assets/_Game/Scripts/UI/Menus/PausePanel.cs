using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Pause menu (placeholder look): "Paused" title, Resume (primary, starts the 3-2-1 countdown), Restart
    /// (secondary, new run) and Home (neutral, back to the main menu). Centered in the safe area.
    /// </summary>
    public sealed class PausePanel
    {
        private const int TitleFontSize = 34;
        private const int PrimaryFontSize = 26;
        private const int ButtonFontSize = 22;

        private readonly GameObject _root;

        public PausePanel(Transform safeArea, Font font, IRunCommands commands)
        {
            Image fill = HudFactory.CreatePanel(
                safeArea, "PauseMenu", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(280f, 330f), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;

            MenuFactory.Label(
                panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(240f, 60f), new Vector2(0f, -12f), HudStrings.Paused);

            ResumeButton = MenuFactory.PrimaryButton(
                panel, "ResumeButton", font, HudStrings.Resume, PrimaryFontSize, new Vector2(0.5f, 1f), new Vector2(220f, 80f), new Vector2(0f, -80f), commands.Resume);
            RestartButton = MenuFactory.SecondaryButton(
                panel, "RestartButton", font, MenuStrings.Restart, ButtonFontSize, new Vector2(0.5f, 1f), new Vector2(220f, 56f), new Vector2(0f, -178f), commands.Restart);
            HomeButton = MenuFactory.NeutralButton(
                panel, "HomeButton", font, MenuStrings.Home, ButtonFontSize, new Vector2(0.5f, 1f), new Vector2(220f, 56f), new Vector2(0f, -248f), commands.GoHome);

            _root.SetActive(false);
        }

        public Button ResumeButton { get; }

        public Button RestartButton { get; }

        public Button HomeButton { get; }

        public bool Visible => _root.activeSelf;

        public void SetVisible(bool visible)
        {
            if (_root.activeSelf != visible)
            {
                _root.SetActive(visible);
            }
        }
    }
}
