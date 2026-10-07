using JungleBooze.Services.Audio;
using JungleBooze.UI.Hud;
using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// Plays the UI tap sound whenever a button built by <see cref="HudFactory"/> is pressed. Subscribes while
    /// enabled so the static event never keeps a destroyed scene's audio alive.
    /// </summary>
    public sealed class UiTapAudioBinder : MonoBehaviour
    {
        private AudioPlayback _audio;

        public void Init(AudioPlayback audio)
        {
            _audio = audio;
        }

        private void OnEnable()
        {
            HudFactory.ButtonPressed += OnButtonPressed;
        }

        private void OnDisable()
        {
            HudFactory.ButtonPressed -= OnButtonPressed;
        }

        private void OnButtonPressed()
        {
            if (_audio != null)
            {
                _audio.PlayUiTap();
            }
        }
    }
}
