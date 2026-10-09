using System;
using JungleBooze.UI.Common;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.UI
{
    /// <summary>
    /// Creates/updates the painterly UI theme asset from the files in Assets/_Game/Art/UI and the string tables.
    ///   Menu: JungleBooze > UI > Build Theme
    ///   Batch: tools/ci/unity.sh method JungleBooze.Editor.UI.UiThemeBuilder.BuildBatch
    /// </summary>
    public static class UiThemeBuilder
    {
        public const string ThemePath = "Assets/_Game/Config/UI/Resources/AureliaUiTheme.asset";
        public const string StringsPath = "Assets/_Game/Config/UI/Strings/en.txt";
        private const string Art = "Assets/_Game/Art/UI/";

        [MenuItem("JungleBooze/UI/Build Theme")]
        public static UiTheme Build()
        {
            AssetDatabase.Refresh();
            foreach (string folder in UiArtImporter.Folders())
            {
                AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            }

            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            bool created = theme == null;
            if (created)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
            }

            theme.Body = Load<Font>("Fonts/Nunito-Bold.ttf");
            theme.Heavy = Load<Font>("Fonts/Nunito-Black.ttf");
            theme.Display = Load<Font>("Fonts/Cinzel-Bold.ttf");
            theme.Panel = Sprite("Sprites/panel");
            theme.Card = Sprite("Sprites/card");
            theme.ButtonPrimary = Sprite("Sprites/button_primary");
            theme.ButtonSecondary = Sprite("Sprites/button_secondary");
            theme.Chip = Sprite("Sprites/chip");
            theme.Circle = Sprite("Sprites/circle");
            theme.Track = Sprite("Sprites/track");
            theme.TrackFill = Sprite("Sprites/track_fill");
            theme.Knob = Sprite("Sprites/knob");
            theme.Glow = Sprite("Sprites/glow");
            theme.Shadow = Sprite("Sprites/shadow");
            theme.Scrim = Sprite("Sprites/scrim");
            theme.Divider = Sprite("Sprites/divider");
            theme.White = Sprite("Sprites/white");
            theme.Coin = Sprite("Icons/coin");
            theme.Crystal = Sprite("Icons/crystal");
            theme.Health = Sprite("Icons/health");
            theme.HealthEmpty = Sprite("Icons/health_empty");
            theme.ShieldIcon = Sprite("Icons/shield");
            theme.Settings = Sprite("Icons/settings");
            theme.Journal = Sprite("Icons/journal");
            theme.Abilities = Sprite("Icons/abilities");
            theme.Discovery = Sprite("Icons/discovery");
            theme.Record = Sprite("Icons/record");
            theme.Camp = Sprite("Icons/camp");
            theme.Distance = Sprite("Icons/distance");
            theme.Lock = Sprite("Icons/lock");
            theme.Pause = Sprite("Icons/pause");
            theme.Back = Sprite("Icons/back");
            theme.Next = Sprite("Icons/next");
            theme.Check = Sprite("Icons/check");
            theme.Close = Sprite("Icons/close");
            theme.Play = Sprite("Icons/play");
            theme.HomeLandscape = Load<Texture2D>("Backdrops/HomeLandscape.jpg");
            theme.HomePortrait = Load<Texture2D>("Backdrops/HomePortrait.jpg");
            theme.StringsEnglish = AssetDatabase.LoadAssetAtPath<TextAsset>(StringsPath);
            if (created)
            {
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[JungleBooze] UI theme built: " + ThemePath + Missing(theme));
            return theme;
        }

        public static void BuildBatch()
        {
            int code = 0;
            try
            {
                UiTheme theme = Build();
                code = Missing(theme).Length > 0 ? 3 : 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[JungleBooze] UI theme build failed: " + e);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        /// <summary>" (missing: a, b)" for unassigned theme slots, else empty.</summary>
        public static string Missing(UiTheme theme)
        {
            var missing = new System.Text.StringBuilder();
            foreach (System.Reflection.FieldInfo field in typeof(UiTheme).GetFields())
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && (UnityEngine.Object)field.GetValue(theme) == null)
                {
                    missing.Append(missing.Length > 0 ? ", " : string.Empty).Append(field.Name);
                }
            }

            return missing.Length > 0 ? " (missing: " + missing + ")" : string.Empty;
        }

        private static T Load<T>(string relative) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(Art + relative);
        }

        private static Sprite Sprite(string relative)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(Art + relative + ".png");
        }
    }
}
