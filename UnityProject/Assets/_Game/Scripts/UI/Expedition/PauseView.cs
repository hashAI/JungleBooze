using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>Pause menu: RESUME (primary), Settings, Return to Camp (with an honest note about this run's coins).</summary>
    public sealed class PauseView
    {
        public PauseView(UiFactory f, Transform canvas, UnityAction onResume, UnityAction onSettings, UnityAction onCamp)
        {
            Shell = new ScreenShell(f, canvas, "Pause", new Vector2(340f, 380f), new Vector2(380f, 312f));
            Shell.AddHeader(f.S("pause.title"), null);
            Transform body = Shell.Body.transform;
            RectTransform column = UiFactory.Rect(body, "Column");
            UiFactory.Stretch(column, 24f, 24f, ScreenShell.HeaderHeight + 16f, 18f);
            VerticalLayoutGroup v = column.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            Resume = Add(f.Button(column, "Resume", ButtonStyle.Primary, f.S("pause.resume"), f.Theme.Play, 24, new Vector2(260f, 62f), onResume), 62f);
            Settings = Add(f.Button(column, "Settings", ButtonStyle.Secondary, f.S("pause.settings"), f.Theme.Settings, 20, new Vector2(260f, 52f), onSettings), 52f);
            Camp = Add(f.Button(column, "Camp", ButtonStyle.Ghost, f.S("pause.home"), f.Theme.Camp, 18, new Vector2(260f, 48f), onCamp), 48f);
            Text note = f.Label(column, "CampNote", f.S("pause.homeNote"), FontKind.Body, 14, UiColors.Mist, TextAnchor.UpperCenter);
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
        }

        public ScreenShell Shell { get; }

        public UiButton Resume { get; }

        public UiButton Settings { get; }

        public UiButton Camp { get; }

        private static UiButton Add(UiButton b, float h)
        {
            LayoutElement le = b.Root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            return b;
        }
    }
}
