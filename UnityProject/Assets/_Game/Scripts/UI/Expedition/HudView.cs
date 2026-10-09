using JungleBooze.Gameplay.World;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// In-run HUD (GDD §15–16, spec 103 §7.3, §9.3): distance, coins, crystals, 3 health leaves (full = filled leaf,
    /// empty = outline: shape, not only colour), Shield with a timer ring, pause, the NEW DISCOVERY toast, the
    /// first-run gesture hint and the centre message. Placement comes from <see cref="HudLayout"/>. Every setter
    /// touches the UI only when a value changes and never allocates.
    /// </summary>
    public sealed class HudView
    {
        private const float ToastIn = 0.2f;

        private readonly UiFactory _f;
        private readonly RectTransform _root;
        private readonly RectTransform _distanceRoot;
        private readonly RectTransform _coinsRoot;
        private readonly RectTransform _crystalsRoot;
        private readonly RectTransform _healthRoot;
        private readonly RectTransform _toastRoot;
        private readonly RectTransform _hintRoot;
        private readonly CanvasGroup _toastGroup;
        private readonly Text _distance;
        private readonly Text _coins;
        private readonly Text _crystals;
        private readonly Image[] _leaves;
        private readonly RectTransform _shieldRoot;
        private readonly Image _shieldRing;
        private readonly Image _shieldIcon;
        private readonly UiButton _pause;
        private readonly Text _center;
        private readonly Text _toastTitle;
        private readonly Text _toastLine;
        private readonly Image _arrowA;
        private readonly Image _arrowB;
        private readonly Text _hintCaption;
        private readonly Image _scrim;
        private readonly string[] _hintCaptions;
        private readonly NumberText _metres;
        private readonly NumberText _numbers;
        private int _shownDistance = -1;
        private int _shownCoins = -1;
        private int _shownCrystals = -1;
        private int _shownHealth = -1;
        private bool _shownShield;
        private float _shownShieldFill = -1f;
        private int _shownHelp = -1;
        private float _toastT = 1f;
        private float _hintClock;
        private bool _visible = true;
        private HudRects _rects;
        private float _scale = 1f;

        public HudView(UiFactory f, RectTransform safe, int maxHealth, UnityAction onPause)
        {
            _f = f;
            _metres = new NumberText(5001, 100000, f.S("unit.metres"));
            _numbers = new NumberText(3001, 100000, null);
            _root = UiFactory.Rect(safe, "Hud");
            UiFactory.Stretch(_root);

            // Top scrim: keeps cream numbers readable over a bright sky (no extra draw when fully transparent).
            _scrim = f.Image(_root, "TopScrim", f.Theme.Scrim, new Color(1f, 1f, 1f, 0.38f));
            _scrim.preserveAspect = false;
            _scrim.rectTransform.anchorMin = new Vector2(0f, 1f);
            _scrim.rectTransform.anchorMax = new Vector2(1f, 1f);
            _scrim.rectTransform.pivot = new Vector2(0.5f, 1f);
            _scrim.rectTransform.offsetMin = new Vector2(-80f, -120f);
            _scrim.rectTransform.offsetMax = new Vector2(80f, 80f);

            _distance = f.Chip(_root, "Distance", f.Theme.Distance, out _distanceRoot, out _);
            _coins = f.Chip(_root, "Coins", f.Theme.Coin, out _coinsRoot, out _);
            _crystals = f.Chip(_root, "Crystals", f.Theme.Crystal, out _crystalsRoot, out _);
            _distance.text = _metres.Get(0);
            _coins.text = _numbers.Get(0);
            _crystals.text = _numbers.Get(0);

            _healthRoot = UiFactory.Rect(_root, "Health");
            _leaves = new Image[maxHealth];
            for (int i = 0; i < maxHealth; i++)
            {
                _leaves[i] = f.Image(_healthRoot, "Leaf" + i, f.Theme.Health, Color.white);
            }

            _shieldRoot = UiFactory.Rect(_healthRoot, "Shield");
            _shieldRing = f.Image(_shieldRoot, "Ring", f.Theme.Circle, UiColors.Turquoise);
            _shieldRing.type = Image.Type.Filled;
            _shieldRing.fillMethod = Image.FillMethod.Radial360;
            _shieldRing.fillOrigin = (int)Image.Origin360.Top;
            _shieldRing.fillClockwise = false;
            UiFactory.Stretch(_shieldRing.rectTransform);
            _shieldIcon = f.Image(_shieldRoot, "Icon", f.Theme.ShieldIcon, Color.white);
            UiFactory.Stretch(_shieldIcon.rectTransform, 3f, 3f, 3f, 3f);
            _shieldRoot.gameObject.SetActive(false);

            _pause = f.Button(_root, "Pause", ButtonStyle.Round, null, f.Theme.Pause, 0, new Vector2(HudLayout.PauseSize, HudLayout.PauseSize), onPause);

            _center = f.Label(_root, "Center", string.Empty, FontKind.Display, 54, UiColors.Cream, TextAnchor.MiddleCenter, true, false, true);
            UiFactory.Anchor(_center.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(600f, 90f), Vector2.zero);
            _center.horizontalOverflow = HorizontalWrapMode.Overflow;
            _center.gameObject.SetActive(false);

            // Toast: parchment card, discovery icon, gold-on-ink title, ink line.
            _toastRoot = UiFactory.Rect(_root, "Toast");
            _toastGroup = _toastRoot.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;
            Image card = f.Panel(_toastRoot, "Card", f.Theme.Card);
            UiFactory.Stretch(UiFactory.PanelRoot(card));
            Image icon = f.Icon(card.transform, "Icon", f.Theme.Discovery, 44f);
            UiFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 44f), new Vector2(12f, 0f));
            _toastTitle = f.Label(card.transform, "Title", f.S("hud.toastTitle"), FontKind.Heavy, 17, UiColors.Emerald, TextAnchor.LowerLeft, true);
            UiFactory.Stretch(_toastTitle.rectTransform, 66f, 12f, 8f, 34f);
            _toastTitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _toastTitle.rectTransform.offsetMin = new Vector2(66f, 0f);
            _toastLine = f.Label(card.transform, "Line", string.Empty, FontKind.Body, 16, UiColors.Ink, TextAnchor.UpperLeft, true);
            _toastLine.rectTransform.anchorMin = Vector2.zero;
            _toastLine.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _toastLine.rectTransform.offsetMin = new Vector2(66f, 6f);
            _toastLine.rectTransform.offsetMax = new Vector2(-12f, 0f);
            _toastRoot.gameObject.SetActive(false);

            // First-run gesture hint: two chevrons that nudge in the gesture's direction + one short caption.
            _hintRoot = UiFactory.Rect(_root, "Hint");
            _arrowA = f.Image(_hintRoot, "ArrowA", f.Theme.Next, Color.white);
            _arrowB = f.Image(_hintRoot, "ArrowB", f.Theme.Next, Color.white);
            _arrowA.rectTransform.sizeDelta = new Vector2(64f, 64f);
            _arrowB.rectTransform.sizeDelta = new Vector2(64f, 64f);
            _hintCaption = f.Label(_hintRoot, "Caption", string.Empty, FontKind.Heavy, 20, UiColors.Cream, TextAnchor.UpperCenter, true, true, true);
            _hintCaption.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hintCaption.rectTransform.anchorMax = new Vector2(1f, 0f);
            _hintCaption.rectTransform.pivot = new Vector2(0.5f, 0f);
            _hintCaption.rectTransform.sizeDelta = new Vector2(0f, 34f);
            _hintCaption.rectTransform.anchoredPosition = Vector2.zero;
            _hintRoot.gameObject.SetActive(false);
            _hintCaptions = new string[8];
            string[] keys = { "steer", "jump", "slide", "dodge", "gap", "dive", "leap", "release" };
            for (int i = 0; i < keys.Length; i++)
            {
                _hintCaptions[i] = f.S("hud.hint." + keys[i]);
            }
        }

        public RectTransform Root => _root;

        public RectTransform PauseRect => _pause.Root;

        public bool ToastVisible => _toastRoot.gameObject.activeSelf;

        public string ToastLine => _toastLine.text;

        public bool HelpVisible => _hintRoot.gameObject.activeSelf;

        public string CenterText => _center.gameObject.activeSelf ? _center.text : string.Empty;

        public HudRects Rects => _rects;

        public string DistanceText => _distance.text;

        public string CoinsText => _coins.text;

        public void SetVisible(bool visible)
        {
            if (visible != _visible)
            {
                _visible = visible;
                _root.gameObject.SetActive(visible);
            }
        }

        public void Layout(in ScreenFrame frame, bool leftHanded, float scale)
        {
            _scale = scale;
            _rects = HudLayout.Compute(frame, _leaves.Length, leftHanded, scale);
            UiFactory.Place(_distanceRoot, _rects.Distance);
            UiFactory.Place(_coinsRoot, _rects.Coins);
            UiFactory.Place(_crystalsRoot, _rects.Crystals);
            UiFactory.Place(_pause.Root, _rects.Pause);
            UiFactory.Place(_healthRoot, _rects.Health);
            UiFactory.Place(_toastRoot, _rects.Toast);
            UiFactory.Place(_hintRoot, _rects.Hint);
            float leaf = HudLayout.LeafSize * scale;
            float gap = HudLayout.LeafGap * scale;
            // The Shield slot sits on the inner side (right when right-handed, left when mirrored).
            float offset = leftHanded ? leaf * 0.9f : 0f;
            for (int i = 0; i < _leaves.Length; i++)
            {
                UiFactory.Place(_leaves[i].rectTransform, new UiRect(offset + (i * (leaf + gap)), 0f, leaf, leaf));
            }

            float shield = leaf * 0.8f;
            float sx = leftHanded ? 0f : _rects.Health.W - shield;
            UiFactory.Place(_shieldRoot, new UiRect(sx, (leaf - shield) * 0.5f, shield, shield));
            float icon = HudLayout.IconSize * scale;
            ScaleChipIcon(_distanceRoot, icon);
            ScaleChipIcon(_coinsRoot, icon);
            ScaleChipIcon(_crystalsRoot, icon);
        }

        public void SetDistance(int metres)
        {
            if (metres != _shownDistance)
            {
                _shownDistance = metres;
                _distance.text = _metres.Get(metres);
            }
        }

        public void SetWallet(int coins, int crystals)
        {
            if (coins != _shownCoins)
            {
                _shownCoins = coins;
                _coins.text = _numbers.Get(coins);
            }

            if (crystals != _shownCrystals)
            {
                _shownCrystals = crystals;
                _crystals.text = _numbers.Get(crystals);
            }
        }

        public void SetHealth(int health, bool shield, float fraction)
        {
            if (health != _shownHealth)
            {
                _shownHealth = health;
                for (int i = 0; i < _leaves.Length; i++)
                {
                    _leaves[i].sprite = i < health ? _f.Theme.Health : _f.Theme.HealthEmpty;
                }
            }

            if (shield != _shownShield)
            {
                _shownShield = shield;
                _shieldRoot.gameObject.SetActive(shield);
            }

            float fill = Mathf.Round(Mathf.Clamp01(fraction) * 48f) / 48f;
            if (shield && fill != _shownShieldFill)
            {
                _shownShieldFill = fill;
                _shieldRing.fillAmount = fill;
                _shieldIcon.color = fill < 0.1f ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            }
        }

        public void SetCenter(string message)
        {
            bool show = !string.IsNullOrEmpty(message);
            if (_center.gameObject.activeSelf != show)
            {
                _center.gameObject.SetActive(show);
            }

            if (show && !ReferenceEquals(_center.text, message))
            {
                _center.text = message;
            }
        }

        public void ShowToast(string title, string line)
        {
            if (!ReferenceEquals(_toastTitle.text, title))
            {
                _toastTitle.text = title;
            }

            if (!ReferenceEquals(_toastLine.text, line))
            {
                _toastLine.text = line;
            }

            if (!_toastRoot.gameObject.activeSelf)
            {
                _toastRoot.gameObject.SetActive(true);
                _toastT = 0f;
            }
        }

        public void HideToast()
        {
            if (_toastRoot.gameObject.activeSelf)
            {
                _toastRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>First-run gesture hint for a move; −1 hides it.</summary>
        public void SetHelp(int move)
        {
            if (move == _shownHelp)
            {
                return;
            }

            _shownHelp = move;
            bool show = move >= 0;
            _hintRoot.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            _hintClock = 0f;
            _hintCaption.text = _hintCaptions[Mathf.Clamp(move, 0, _hintCaptions.Length - 1)];
            var m = (HelpMove)move;
            bool twoWay = m == HelpMove.Steer || m == HelpMove.Dodge;
            _arrowB.gameObject.SetActive(twoWay);
            float rotation = m == HelpMove.Slide || m == HelpMove.Dive ? -90f : twoWay ? 0f : 90f;
            _arrowA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, twoWay ? 180f : rotation);
            _arrowB.rectTransform.localRotation = Quaternion.identity;
            PositionArrows(0f);
        }

        public void Tick(float seconds, bool reducedMotion)
        {
            if (_toastT < 1f)
            {
                _toastT = reducedMotion ? 1f : Mathf.Min(1f, _toastT + (seconds / ToastIn));
                float e = 1f - ((1f - _toastT) * (1f - _toastT));
                _toastGroup.alpha = e;
                _toastRoot.anchoredPosition = new Vector2(_rects.Toast.X + (_rects.Toast.W * 0.5f), -(_rects.Toast.Y + (_rects.Toast.H * 0.5f)) + (18f * (1f - e)));
            }

            if (_shownHelp >= 0)
            {
                _hintClock += seconds;
                PositionArrows(reducedMotion ? 0f : Mathf.Sin(_hintClock * 7f) * 10f);
            }
        }

        private void PositionArrows(float nudge)
        {
            var m = (HelpMove)_shownHelp;
            bool twoWay = m == HelpMove.Steer || m == HelpMove.Dodge;
            float size = 64f * _scale;
            _arrowA.rectTransform.sizeDelta = new Vector2(size, size);
            _arrowB.rectTransform.sizeDelta = new Vector2(size, size);
            float cy = (_rects.Hint.H * 0.5f) + 10f;
            if (twoWay)
            {
                float spread = (m == HelpMove.Dodge ? 70f : 50f) + Mathf.Abs(nudge);
                UiFactory.Anchor(_arrowA.rectTransform, new Vector2(0.5f, 0f), _arrowA.rectTransform.sizeDelta, new Vector2(-spread, cy - (size * 0.5f)));
                UiFactory.Anchor(_arrowB.rectTransform, new Vector2(0.5f, 0f), _arrowB.rectTransform.sizeDelta, new Vector2(spread, cy - (size * 0.5f)));
                _arrowA.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _arrowB.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                float dir = m == HelpMove.Slide || m == HelpMove.Dive ? -1f : 1f;
                UiFactory.Anchor(_arrowA.rectTransform, new Vector2(0.5f, 0f), _arrowA.rectTransform.sizeDelta, new Vector2(0f, cy + (dir * Mathf.Abs(nudge))));
                _arrowA.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private static void ScaleChipIcon(RectTransform chip, float size)
        {
            var icon = (RectTransform)chip.Find("Icon");
            var value = (RectTransform)chip.Find("Value");
            icon.sizeDelta = new Vector2(size, size);
            value.offsetMin = new Vector2(12f + size + 6f, value.offsetMin.y);
        }
    }
}
