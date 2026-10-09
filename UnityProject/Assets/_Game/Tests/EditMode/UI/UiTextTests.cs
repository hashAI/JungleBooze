using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JungleBooze.Core.Settings;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.UI.Common;
using JungleBooze.UI.Expedition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.EditMode.UI
{
    /// <summary>Localization table, number formatting, objective lines, preferences, navigation, contrast.</summary>
    public sealed class UiTextTests
    {
        private const string English = "Assets/_Game/Config/UI/Strings/en.txt";

        private static StringTable En()
        {
            return StringTable.Parse("en", File.ReadAllText(English));
        }

        [Test]
        public void StringTable_ParsesKeysCommentsAndEscapes()
        {
            StringTable t = StringTable.Parse("en", "# comment\na.b = Hello {0}\n\nc = one\\ntwo\nbad line\n");
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual("Hello 5", t.Format("a.b", 5));
            Assert.AreEqual("one\ntwo", t.Get("c"));
            Assert.AreEqual("#missing", t.Get("missing"));
        }

        [Test]
        public void EveryUiKeyInCode_ExistsInTheEnglishTable()
        {
            StringTable en = En();
            var keys = new HashSet<string>();
            var pattern = new Regex("\"((?:app|home|hud|revive|pause|settings|credits|results|objective|upgrade|journal|abilities|common|unit)\\.[A-Za-z.]+)\"");
            foreach (string folder in new[] { "Assets/_Game/Scripts/UI", "Assets/_Game/Scripts/App/Expedition" })
            {
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (Match m in pattern.Matches(File.ReadAllText(file)))
                    {
                        if (!m.Groups[1].Value.EndsWith("."))
                        {
                            keys.Add(m.Groups[1].Value);
                        }
                    }
                }
            }

            foreach (string move in new[] { "steer", "jump", "slide", "dodge", "gap", "dive", "leap", "release" })
            {
                keys.Add("hud.hint." + move);
            }

            Assert.Greater(keys.Count, 60);
            var missing = new List<string>();
            foreach (string k in keys)
            {
                if (!en.Has(k))
                {
                    missing.Add(k);
                }
            }

            CollectionAssert.IsEmpty(missing, "missing in en.txt: " + string.Join(", ", missing));
        }

        [Test]
        public void EnglishTable_NeverMentionsTheOldName()
        {
            StringAssert.DoesNotContain("booze", File.ReadAllText(English).ToLowerInvariant());
        }

        [Test]
        public void NumberText_GroupsThousands_AndIsCachedWithoutAllocation()
        {
            var metres = new NumberText(5001, 100000, "{0} m");
            Assert.AreEqual("0 m", metres.Get(0));
            Assert.AreEqual("2,480 m", metres.Get(2480));
            Assert.AreEqual("12,345 m", metres.Get(12345));
            Assert.AreEqual("0 m", metres.Get(-5));
            Assert.AreEqual("100,000 m", metres.Get(100000));
            Assert.That(() =>
            {
                for (int i = 0; i < 5000; i++)
                {
                    metres.Get(i);
                }

                metres.Get(12345);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ObjectiveText_FormatsByKind_AndFallsBackToTheObjectiveTitle()
        {
            StringTable en = En();
            var near = new NextObjective { Kind = ObjectiveKind.NearBest, Progress = 4870, Target = 5000 };
            Assert.AreEqual("4,870 m / 5,000 m", ObjectiveText.Title(en, near, null));
            Assert.AreEqual("So close to your best", ObjectiveText.Detail(en, near, null));
            var best = new NextObjective { Kind = ObjectiveKind.BeatBest, Target = 2410 };
            Assert.AreEqual("Beat your best: 2,410 m", ObjectiveText.Title(en, best, null));
            var secrets = new NextObjective { Kind = ObjectiveKind.Secrets, Progress = 1, Target = 5 };
            Assert.AreEqual("Secrets 1/5", ObjectiveText.Title(en, secrets, null));
            var hint = new NextObjective { Kind = ObjectiveKind.Secrets, Entry = 2, Title = "Something glinted behind Veil Falls" };
            Assert.AreEqual("Something glinted behind Veil Falls", ObjectiveText.Line(en, hint, null));
            Assert.AreEqual(string.Empty, ObjectiveText.Line(en, new NextObjective(), null));
        }

        [Test]
        public void ResultsBonusLine_ListsOnlyEarnedBonuses()
        {
            StringTable en = En();
            Assert.AreEqual(string.Empty, ResultsView.BonusLine(en, new RunResults()));
            Assert.AreEqual("+36 Clean Line   +50 Discovery", ResultsView.BonusLine(en, new RunResults { CleanLineCoins = 36, DiscoveryCoins = 50 }));
        }

        [Test]
        public void UiPreferences_DefaultsClampPersistAndNotify()
        {
            var store = new MemorySettingsStore();
            var p = new UiPreferences(store);
            Assert.AreEqual(0.8f, p.MusicVolume, 1e-4f);
            Assert.AreEqual(1f, p.SfxVolume, 1e-4f);
            Assert.IsFalse(p.LeftHanded);
            Assert.AreEqual(1f, p.TextScale);
            int changes = 0;
            p.Changed += () => changes++;
            p.SetMusicVolume(1.7f);
            p.SetSfxVolume(-1f);
            p.SetLeftHanded(true);
            p.SetTextSizeIndex(99);
            Assert.AreEqual(4, changes);
            var reloaded = new UiPreferences(store);
            Assert.AreEqual(1f, reloaded.MusicVolume);
            Assert.AreEqual(0f, reloaded.SfxVolume);
            Assert.IsTrue(reloaded.LeftHanded);
            Assert.AreEqual(2f, reloaded.TextScale, 1e-4f);
            Assert.AreEqual(UiPreferences.HudTextScaleMax, reloaded.HudTextScale, 1e-4f);
            Assert.AreEqual(200, UiPreferences.Percent(reloaded.TextSizeIndex));
        }

        [Test]
        public void Navigation_BackAlwaysReturnsOneLevel_AndNeverLeavesTheBase()
        {
            var nav = new NavigationStack(UiScreen.Home);
            Assert.IsFalse(nav.Back());
            nav.Push(UiScreen.Settings);
            nav.Push(UiScreen.Credits);
            nav.Push(UiScreen.Credits);
            Assert.AreEqual(3, nav.Depth);
            Assert.IsTrue(nav.Back());
            Assert.AreEqual(UiScreen.Settings, nav.Current);
            Assert.IsTrue(nav.Back());
            Assert.AreEqual(UiScreen.Home, nav.Current);
            Assert.IsFalse(nav.Back());
            nav.Reset(UiScreen.Hud);
            Assert.AreEqual(UiScreen.Hud, nav.Root);
        }

        [Test]
        public void Palette_TextPairsMeetWcagAA()
        {
            AssertContrast(UiColors.Cream, UiColors.PanelDeep, 4.5f, "cream on panel");
            AssertContrast(UiColors.Mist, UiColors.PanelDeep, 4.5f, "mist on panel");
            AssertContrast(UiColors.Gold, UiColors.PanelDeep, 4.5f, "gold on panel");
            AssertContrast(UiColors.Turquoise, UiColors.PanelDeep, 4.5f, "turquoise on panel");
            AssertContrast(UiColors.Ink, UiColors.Parchment, 4.5f, "ink on parchment");
            AssertContrast(UiColors.InkSoft, UiColors.Parchment, 4.5f, "soft ink on parchment");
            AssertContrast(UiColors.Emerald, UiColors.Parchment, 4.5f, "emerald on parchment");
            AssertContrast(UiColors.Ink, UiColors.ButtonGold, 4.5f, "ink on gold button");
            AssertContrast(UiColors.Cream, UiColors.ButtonTeal, 4.5f, "cream on teal button");
        }

        [Test]
        public void CoverUv_CropsWithoutStretching()
        {
            var tex = new Texture2D(1536, 1024);
            Rect portrait = HomeView.CoverUv(tex, 390f, 844f, 0.5f);
            Assert.AreEqual(1f, portrait.height, 1e-4f);
            Assert.AreEqual((390f / 844f) / 1.5f, portrait.width, 1e-4f);
            Assert.AreEqual(0.5f, portrait.center.x, 1e-3f);
            Rect wide = HomeView.CoverUv(tex, 844f, 390f, 0.5f);
            Assert.AreEqual(1f, wide.width, 1e-4f);
            Assert.Less(wide.height, 1f);
            Object.DestroyImmediate(tex);
        }

        private static void AssertContrast(Color text, Color bg, float min, string what)
        {
            float c = UiColors.Contrast(text, bg);
            Assert.GreaterOrEqual(c, min, what + " contrast " + c.ToString("0.00"));
        }
    }
}
