using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Hosts the <see cref="ContinuePanel"/> on its own overlay canvas above the HUD (so the main HUD needs no
    /// change): a full-screen dim plus the panel while the session is in <see cref="SessionPhase.ContinueOffer"/>
    /// (GDD 14.4, 19). Uses the HUD's event system. No allocations per frame (the panel's texts are set when the
    /// offer opens).
    /// </summary>
    public sealed class ContinueView : MonoBehaviour, IRunView
    {
        private const SessionPhase NoPhase = (SessionPhase)255;

        private Canvas _canvas;
        private Image _dim;
        private ContinuePanel _panel;
        private IContinueCommands _commands;
        private PlayerSave _save;
        private SessionPhase _shownPhase = NoPhase;
        private bool _shownLock = true;

        public ContinuePanel Panel => _panel;

        public bool Visible => _panel != null && _panel.Visible;

        /// <summary>Builds the canvas and panel. Call once, right after AddComponent on a RectTransform object.</summary>
        public void Build(IContinueCommands commands, Font font, PlayerSave save)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _save = save;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 12;
            _canvas.pixelPerfect = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudView.ReferenceResolutionPt;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            gameObject.AddComponent<GraphicRaycaster>();

            // The dim takes touches, so nothing under the panel reacts while the offer is up.
            _dim = HudFactory.CreateImage(transform, "Dim", StylePalette.Dim, true);
            HudFactory.Stretch(_dim.rectTransform, 0f);

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(safe, 0f);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            _panel = new ContinuePanel(safe, font, commands);
            _dim.gameObject.SetActive(false);
        }

        public void BeginRun(GameSession session)
        {
            _shownPhase = NoPhase;
            _shownLock = true;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_panel == null)
            {
                return;
            }

            SessionPhase phase = session.Phase;
            bool offer = phase == SessionPhase.ContinueOffer;
            if (phase != _shownPhase)
            {
                _shownPhase = phase;
                if (offer)
                {
                    _panel.Show(session, _commands, _save);
                    _shownLock = session.ContinueInputLocked;
                }
                else
                {
                    _panel.Hide();
                }

                if (_dim.gameObject.activeSelf != offer)
                {
                    _dim.gameObject.SetActive(offer);
                }
            }

            if (!offer)
            {
                return;
            }

            _panel.UpdateTimer(session);
            bool locked = session.ContinueInputLocked;
            if (locked != _shownLock)
            {
                _shownLock = locked;
                _panel.SetLocked(locked);
            }
        }
    }
}
