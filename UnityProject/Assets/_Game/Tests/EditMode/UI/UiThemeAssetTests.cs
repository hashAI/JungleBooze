using JungleBooze.Editor.UI;
using JungleBooze.UI.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.UI
{
    /// <summary>The theme asset exists, every slot is assigned, 9-slice sprites have borders, fonts are the OFL ones.</summary>
    public sealed class UiThemeAssetTests
    {
        [Test]
        public void Theme_IsComplete_AndLoadsFromResources()
        {
            UiTheme theme = UiTheme.Load();
            Assert.IsNotNull(theme, "run JungleBooze > UI > Build Theme");
            Assert.AreEqual(string.Empty, UiThemeBuilder.Missing(theme));
            Assert.Greater(theme.LoadStrings().Count, 80);
            Assert.Greater(theme.Panel.border.x, 0f, "panel is 9-sliced");
            Assert.Greater(theme.ButtonPrimary.border.x, 0f);
            Assert.Greater(theme.Chip.border.x, 0f);
            StringAssert.Contains("Nunito", AssetDatabase.GetAssetPath(theme.Body));
            StringAssert.Contains("Cinzel", AssetDatabase.GetAssetPath(theme.Display));
            Assert.IsTrue(System.IO.File.Exists("Assets/_Game/Art/UI/Fonts/Nunito-OFL.txt"));
            Assert.IsTrue(System.IO.File.Exists("Assets/_Game/Art/UI/Fonts/Cinzel-OFL.txt"));
        }

        [Test]
        public void UiSprites_AreUncompressedEnoughAndHaveNoMips()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/_Game/Art/UI/Sprites/panel.png");
            Assert.IsNotNull(importer);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterCompression.CompressedHQ, importer.textureCompression);
        }
    }
}
