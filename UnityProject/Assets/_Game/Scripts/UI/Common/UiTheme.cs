using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Everything the game UI draws with: fonts (OFL, docs/LICENSES.md), painted 9-slice sprites, icons, home
    /// backdrops and the string tables. One asset at <c>Assets/_Game/Config/UI/Resources/AureliaUiTheme.asset</c>,
    /// built by <c>JungleBooze.Editor.UI.UiThemeBuilder</c>; loaded with <see cref="Load"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/UI Theme", fileName = "AureliaUiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        public const string ResourcePath = "AureliaUiTheme";

        [Header("Fonts")]
        public Font Body;
        public Font Heavy;
        public Font Display;

        [Header("Painted sprites (9-slice)")]
        public Sprite Panel;
        public Sprite Card;
        public Sprite ButtonPrimary;
        public Sprite ButtonSecondary;
        public Sprite Chip;
        public Sprite Circle;
        public Sprite Track;
        public Sprite TrackFill;
        public Sprite Knob;
        public Sprite Glow;
        public Sprite Shadow;
        public Sprite Scrim;
        public Sprite Divider;
        public Sprite White;

        [Header("Icons")]
        public Sprite Coin;
        public Sprite Crystal;
        public Sprite Health;
        public Sprite HealthEmpty;
        public Sprite ShieldIcon;
        public Sprite Settings;
        public Sprite Journal;
        public Sprite Abilities;
        public Sprite Discovery;
        public Sprite Record;
        public Sprite Camp;
        public Sprite Distance;
        public Sprite Lock;
        public Sprite Pause;
        public Sprite Back;
        public Sprite Next;
        public Sprite Check;
        public Sprite Close;
        public Sprite Play;

        [Header("Home backdrops (painted cards)")]
        public Texture2D HomeLandscape;
        public Texture2D HomePortrait;

        [Header("Strings")]
        [Tooltip("English base table (key = value).")]
        public TextAsset StringsEnglish;

        [Tooltip("Live privacy policy URL (App Store 5.1.1). Empty = the row shows as not yet available.")]
        public string PrivacyPolicyUrl = string.Empty;

        public static UiTheme Load()
        {
            return Resources.Load<UiTheme>(ResourcePath);
        }

        public StringTable LoadStrings()
        {
            return StringTable.Parse("en", StringsEnglish != null ? StringsEnglish.text : string.Empty);
        }
    }
}
