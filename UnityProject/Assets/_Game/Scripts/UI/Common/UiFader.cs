using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// Screen transitions: fades a CanvasGroup (and a slight scale-in) over ≤ 0.2 s, instant with Reduced Motion.
    /// Driven from the HUD's Tick with unscaled time; no allocations.
    /// </summary>
    public sealed class UiFader
    {
        public const float Duration = 0.18f;

        private readonly CanvasGroup _group;
        private readonly RectTransform _scaled;
        private float _t = 1f;
        private bool _visible;

        public UiFader(CanvasGroup group, RectTransform scaled)
        {
            _group = group;
            _scaled = scaled;
            _visible = group.gameObject.activeSelf;
        }

        public bool Visible => _visible;

        public bool Animating => _t < 1f;

        public CanvasGroup Group => _group;

        public void Show(bool visible, bool instant)
        {
            if (visible == _visible && !(visible && !_group.gameObject.activeSelf))
            {
                return;
            }

            _visible = visible;
            if (visible)
            {
                _group.gameObject.SetActive(true);
                _group.interactable = true;
                _group.blocksRaycasts = true;
                _t = instant ? 1f : 0f;
                Apply();
            }
            else
            {
                _group.interactable = false;
                _group.blocksRaycasts = false;
                _group.gameObject.SetActive(false);
                _t = 1f;
            }
        }

        public void Tick(float seconds)
        {
            if (_t >= 1f || !_visible)
            {
                return;
            }

            _t += seconds / Duration;
            if (_t > 1f)
            {
                _t = 1f;
            }

            Apply();
        }

        private void Apply()
        {
            float e = 1f - ((1f - _t) * (1f - _t));
            _group.alpha = e;
            if (_scaled != null)
            {
                float s = 0.96f + (0.04f * e);
                _scaled.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
