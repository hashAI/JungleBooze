using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>Credits (font licenses, engine). Scrolls at any text size.</summary>
    public sealed class CreditsView
    {
        public CreditsView(UiFactory f, Transform canvas, UnityAction onBack)
        {
            Shell = new ScreenShell(f, canvas, "Credits", new Vector2(400f, 620f), new Vector2(620f, 360f));
            Shell.AddHeader(f.S("credits.title"), onBack);
            RectTransform content = Shell.AddList(8f);
            Text body = f.Label(content, "Body", f.S("credits.body"), FontKind.Body, 16, UiColors.Cream, TextAnchor.UpperLeft, false, false);
            body.verticalOverflow = VerticalWrapMode.Overflow;
        }

        public ScreenShell Shell { get; }
    }
}
