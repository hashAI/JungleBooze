using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Common
{
    /// <summary>A built button: root rect, the uGUI button, optional label and icon.</summary>
    public sealed class UiButton
    {
        public RectTransform Root;
        public Button Button;
        public Image Background;
        public Text Label;
        public Image Icon;

        public bool Interactable
        {
            get => Button.interactable;
            set
            {
                if (Button.interactable != value)
                {
                    Button.interactable = value;
                    Color c = Label != null ? Label.color : Color.white;
                    if (Label != null)
                    {
                        c.a = value ? 1f : 0.55f;
                        Label.color = c;
                    }

                    if (Icon != null)
                    {
                        Icon.color = value ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    }
                }
            }
        }

        public void SetLabel(string text)
        {
            if (Label != null && !ReferenceEquals(Label.text, text))
            {
                Label.text = text;
            }
        }

        public void SetActive(bool active)
        {
            if (Root.gameObject.activeSelf != active)
            {
                Root.gameObject.SetActive(active);
            }
        }
    }
}
