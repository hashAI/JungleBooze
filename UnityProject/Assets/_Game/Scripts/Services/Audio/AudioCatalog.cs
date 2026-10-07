using UnityEngine;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Clip references for <see cref="AudioPlayback"/>. Assigned in <c>Assets/_Game/Audio/Resources/RunAudioCatalog.asset</c> (loaded at run start by the bootstrap).
    /// </summary>
    [CreateAssetMenu(fileName = "RunAudioCatalog", menuName = "JungleBooze/Audio/Run Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioClip _jump;
        [SerializeField] private AudioClip _slide;
        [SerializeField] private AudioClip _laneSwitch;
        [SerializeField] private AudioClip _coin;
        [SerializeField] private AudioClip _stumble;
        [SerializeField] private AudioClip _death;
        [SerializeField] private AudioClip _nearMiss;
        [SerializeField] private AudioClip _vineGrab;
        [SerializeField] private AudioClip _powerUp;
        [SerializeField] private AudioClip _shieldPop;
        [SerializeField] private AudioClip _speedBoost;
        [SerializeField] private AudioClip _uiTap;

        [SerializeField] private AudioClip _callVine;
        [SerializeField] private AudioClip _callDanger;
        [SerializeField] private AudioClip _callCheerShiny;
        [SerializeField] private AudioClip _callCheerWow;
        [SerializeField] private AudioClip _callCheerWoohoo;
        [SerializeField] private AudioClip _squawkVine;
        [SerializeField] private AudioClip _squawkDanger;
        [SerializeField] private AudioClip _squawkCheer;
        [SerializeField] private AudioClip _chatterPretty;
        [SerializeField] private AudioClip _chatterMine;
        [SerializeField] private AudioClip _chatterGiggle;

        [SerializeField] private AudioClip _jungleThemeA;
        [SerializeField] private AudioClip _jungleThemeB;
        [SerializeField] private AudioClip _jungleThemeC;
        [SerializeField] private AudioClip _menuLoop;
        [SerializeField] private AudioClip _gameOverSting;

        public AudioClip Get(AudioClipId id)
        {
            switch (id)
            {
                case AudioClipId.Jump: return _jump;
                case AudioClipId.Slide: return _slide;
                case AudioClipId.LaneSwitch: return _laneSwitch;
                case AudioClipId.Coin: return _coin;
                case AudioClipId.Stumble: return _stumble;
                case AudioClipId.Death: return _death;
                case AudioClipId.NearMiss: return _nearMiss;
                case AudioClipId.VineGrab: return _vineGrab;
                case AudioClipId.PowerUp: return _powerUp;
                case AudioClipId.ShieldPop: return _shieldPop;
                case AudioClipId.SpeedBoost: return _speedBoost;
                case AudioClipId.UiTap: return _uiTap;
                case AudioClipId.CallVine: return _callVine;
                case AudioClipId.CallDanger: return _callDanger;
                case AudioClipId.CallCheerShiny: return _callCheerShiny;
                case AudioClipId.CallCheerWow: return _callCheerWow;
                case AudioClipId.CallCheerWoohoo: return _callCheerWoohoo;
                case AudioClipId.SquawkVine: return _squawkVine;
                case AudioClipId.SquawkDanger: return _squawkDanger;
                case AudioClipId.SquawkCheer: return _squawkCheer;
                case AudioClipId.ChatterPretty: return _chatterPretty;
                case AudioClipId.ChatterMine: return _chatterMine;
                case AudioClipId.ChatterGiggle: return _chatterGiggle;
                case AudioClipId.JungleThemeA: return _jungleThemeA;
                case AudioClipId.JungleThemeB: return _jungleThemeB;
                case AudioClipId.JungleThemeC: return _jungleThemeC;
                case AudioClipId.MenuLoop: return _menuLoop;
                case AudioClipId.GameOverSting: return _gameOverSting;
                default: return null;
            }
        }
    }
}
