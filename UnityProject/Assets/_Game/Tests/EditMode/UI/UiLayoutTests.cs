using JungleBooze.UI.Common;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.UI
{
    /// <summary>
    /// HUD and results layouts on every supported iPhone, both orientations, every HUD text scale and both hands:
    /// everything inside the safe area, no overlaps (the old portrait distance/health overlap), results never clip
    /// (the old landscape top clip), touch targets ≥ 44 pt.
    /// </summary>
    public sealed class UiLayoutTests
    {
        private static readonly float[] HudScales = { 1f, 1.15f, 1.3f };

        [Test]
        public void Hud_FitsSafeArea_NoOverlaps_AllDevices([Values(true, false)] bool landscape, [Values(true, false)] bool leftHanded)
        {
            foreach (DeviceProfile d in DeviceProfile.All)
            {
                foreach (float scale in HudScales)
                {
                    ScreenFrame f = d.Frame(landscape);
                    HudRects r = HudLayout.Compute(f, 3, leftHanded, scale);
                    string where = d.Name + (landscape ? " L " : " P ") + scale + (leftHanded ? " left" : " right");
                    UiRect[] all = { r.Distance, r.Health, r.Coins, r.Crystals, r.Pause, r.Toast };
                    string[] names = { "distance", "health", "coins", "crystals", "pause", "toast" };
                    for (int i = 0; i < all.Length; i++)
                    {
                        Assert.IsTrue(all[i].Inside(f.SafeWidth, f.SafeHeight), where + ": " + names[i] + " " + all[i] + " outside " + f.SafeWidth + "x" + f.SafeHeight);
                        for (int j = i + 1; j < all.Length; j++)
                        {
                            Assert.IsFalse(all[i].Overlaps(all[j]), where + ": " + names[i] + " " + all[i] + " overlaps " + names[j] + " " + all[j]);
                        }
                    }

                    Assert.GreaterOrEqual(r.Pause.W, 44f, where);
                    Assert.GreaterOrEqual(r.Pause.H, 44f, where);
                    if (!landscape)
                    {
                        Assert.IsFalse(r.OneRow, "portrait uses two rows: " + where);
                    }

                    // The toast stays above Pista (drawn at ~60–75 % of the screen height) and the thumb zone.
                    Assert.Less(r.Toast.Bottom, f.SafeHeight * 0.58f, where);
                    Assert.Less(r.Hint.Bottom, f.SafeHeight, where);
                }
            }
        }

        [Test]
        public void Hud_RightHanded_PauseTopRight_LeftHandedMirrors()
        {
            ScreenFrame f = DeviceProfile.All[2].Frame(false);
            HudRects right = HudLayout.Compute(f, 3, false, 1f);
            HudRects left = HudLayout.Compute(f, 3, true, 1f);
            Assert.Greater(right.Pause.X, f.SafeWidth * 0.5f);
            Assert.Less(left.Pause.X, f.SafeWidth * 0.5f);
            Assert.AreEqual(f.SafeWidth - right.Pause.Right, left.Pause.X, 0.01f);
        }

        [Test]
        public void Hud_LandscapeIsOneRowOnWideScreens()
        {
            HudRects r = HudLayout.Compute(DeviceProfile.All[2].Frame(true), 3, false, 1f);
            Assert.IsTrue(r.OneRow);
        }

        [Test]
        public void Hud_ChipsFitTheLargestNumbers()
        {
            // "99,999 m" in Nunito Black at 22 pt: 8 glyphs ≤ 0.62 em each plus icon and padding.
            float needed = 14f + HudLayout.IconSize + 6f + (8 * HudLayout.NumberFont * 0.6f) + 10f;
            Assert.GreaterOrEqual(HudLayout.ChipWidth(8, 1f), needed);
        }

        [Test]
        public void Results_FitWithoutClipping_AllDevices([Values(true, false)] bool landscape)
        {
            foreach (DeviceProfile d in DeviceProfile.All)
            {
                foreach (float scale in HudScales)
                {
                    ScreenFrame f = d.Frame(landscape);
                    ResultsLayout l = ResultsLayout.Compute(f, scale);
                    string where = d.Name + (landscape ? " L " : " P ") + scale;
                    Assert.IsTrue(l.Fits, where + ": left " + l.LeftColumnHeight + " right " + l.RightColumnHeight + " panel " + l.PanelHeight);
                    Assert.LessOrEqual(l.PanelHeight, f.SafeHeight, where);
                    Assert.LessOrEqual(l.PanelWidth, f.SafeWidth, where);
                    Assert.AreEqual(landscape, l.TwoColumns, where);
                    Assert.GreaterOrEqual(l.ButtonHeight, 44f, where);
                    Assert.GreaterOrEqual(l.SmallButtonHeight, 44f, where);
                }
            }
        }

        [Test]
        public void ScreenFrame_ShortSideIs390Units_AndInsetsMatchTheDevice()
        {
            ScreenFrame p = DeviceProfile.All[2].Frame(false);
            Assert.AreEqual(390f, p.Width, 0.01f);
            Assert.AreEqual(844f, p.Height, 0.5f);
            Assert.AreEqual(47f, p.Top, 0.5f);
            Assert.AreEqual(34f, p.Bottom, 0.5f);
            ScreenFrame l = DeviceProfile.All[2].Frame(true);
            Assert.AreEqual(390f, l.Height, 0.01f);
            Assert.AreEqual(47f, l.Left, 0.5f);
            Assert.AreEqual(47f, l.Right, 0.5f);
            Assert.AreEqual(21f, l.Bottom, 0.5f);
            Assert.IsTrue(l.Landscape);
        }

        [Test]
        public void UiRect_MirrorAndOverlap()
        {
            var a = new UiRect(10f, 0f, 20f, 10f);
            Assert.AreEqual(70f, a.Mirror(100f).X, 0.001f);
            Assert.IsTrue(a.Overlaps(new UiRect(25f, 5f, 10f, 10f)));
            Assert.IsFalse(a.Overlaps(new UiRect(30f, 0f, 10f, 10f)));
        }
    }
}
