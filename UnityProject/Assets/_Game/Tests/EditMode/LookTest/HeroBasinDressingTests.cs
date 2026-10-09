using System.Collections.Generic;
using JungleBooze.App.HeroBasin;
using JungleBooze.Core;
using JungleBooze.Editor.HeroBasin;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Pure helpers of the painterly set-dressing pass and its frame-budget measurement (HERO_BASIN_DRESSING.md):
    /// screen-size conversion, seeded stratified sampling, atlas island detection, open-water share, colour rules.
    /// </summary>
    public sealed class HeroBasinDressingTests
    {
        [Test]
        public void FrameWidthGrowsLinearlyWithDepth()
        {
            float w10 = HeroBasinDressing.FrameWidthM(50f, 2f, 10f);
            Assert.That(w10, Is.EqualTo(2f * Mathf.Tan(25f * Mathf.Deg2Rad) * 2f * 10f).Within(1e-4f));
            Assert.That(HeroBasinDressing.FrameWidthM(50f, 2f, 20f), Is.EqualTo(w10 * 2f).Within(1e-4f));
        }

        [Test]
        public void ScreenDistanceScalesVerticalByAspect()
        {
            // 0.2 of the frame height on a 2:1 frame is 0.1 of its width.
            Assert.That(HeroBasinDressing.ScreenDistanceU(new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.4f), 2f), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(HeroBasinDressing.ScreenDistanceU(new Vector2(0.1f, 0.5f), new Vector2(0.4f, 0.5f), 2f), Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void StratifiedSamplesStayInRegionAndRepeatWithTheSeed()
        {
            var region = new Vector4(0.4f, 0.6f, 0.9f, 0.95f);
            List<Vector2> a = HeroBasinDressing.Stratified(new Pcg32Random(7UL, 99UL), region, 40, 2.16f);
            List<Vector2> b = HeroBasinDressing.Stratified(new Pcg32Random(7UL, 99UL), region, 40, 2.16f);
            Assert.That(a.Count, Is.GreaterThanOrEqualTo(40));
            Assert.That(a, Is.EqualTo(b));
            foreach (Vector2 p in a)
            {
                Assert.That(p.x, Is.InRange(region.x, region.z));
                Assert.That(p.y, Is.InRange(region.y, region.w));
            }
        }

        [Test]
        public void IslandsFindsSeparateOpaqueCells()
        {
            const int n = 16;
            var mask = new bool[n, n];
            for (int y = 2; y < 6; y++)
            {
                for (int x = 1; x < 4; x++)
                {
                    mask[x, y] = true;
                }
            }

            for (int y = 8; y < 15; y++)
            {
                mask[10, y] = true;
            }

            mask[14, 0] = true; // a speck below the area threshold
            List<Rect> cells = HeroBasinDressing.Islands(mask, n, 0.01f);
            Assert.That(cells.Count, Is.EqualTo(2));
            Assert.That(cells[0], Is.EqualTo(Rect.MinMaxRect(1f / n, 2f / n, 4f / n, 6f / n)));
            Assert.That(cells[1], Is.EqualTo(Rect.MinMaxRect(10f / n, 8f / n, 11f / n, 15f / n)));
        }

        [Test]
        public void OpenShareCountsOnlyWaterFarFromBreaks()
        {
            const int w = 40;
            const int h = 20;
            var open = new bool[w * h];
            for (int i = 0; i < open.Length; i++)
            {
                open[i] = true;
            }

            Assert.That(HeroBasinSegmentation.OpenShare(open, w, h, 3f, 1), Is.EqualTo(1f));
            // A break column in the middle: everything within 3 px of it is not open.
            for (int y = 0; y < h; y++)
            {
                open[y * w + 20] = false;
            }

            float share = HeroBasinSegmentation.OpenShare(open, w, h, 3f, 1);
            Assert.That(share, Is.EqualTo((w - 7f) / w).Within(1e-4f));
        }

        [Test]
        public void OrangeIsBellcapHueOnly()
        {
            Color.RGBToHSV(new Color(0.898f, 0.643f, 0.353f), out float hue, out float s, out float v); // #E5A45A bellcap lit
            Assert.That(HeroBasinSegmentation.IsOrange(hue, s, v), Is.True);
            Color.RGBToHSV(new Color(0.941f, 0.541f, 0.173f), out hue, out s, out v); // #F08A2C Pista's shoulder
            Assert.That(HeroBasinSegmentation.IsOrange(hue, s, v), Is.True);
            Color.RGBToHSV(new Color(0.839f, 0.698f, 0.451f), out hue, out s, out v); // #D6B273 lit sandstone
            Assert.That(HeroBasinSegmentation.IsOrange(hue, s, v), Is.False);
            Color.RGBToHSV(new Color(0.741f, 0.678f, 0.278f), out hue, out s, out v); // #BDAD47 leaf tip
            Assert.That(HeroBasinSegmentation.IsOrange(hue, s, v), Is.False);
        }

        [Test]
        public void ClassifyMapsEveryPaletteColourToItself()
        {
            for (int k = 0; k < HeroBasinSegmentation.Palette.Length; k++)
            {
                Assert.That(HeroBasinSegmentation.Classify(HeroBasinSegmentation.Palette[k].linear), Is.EqualTo((HeroBasinSegmentation.Category)k));
            }
        }

        [Test]
        public void LabelOfReadsTheMergedRendererName()
        {
            Assert.That(HeroBasinSegmentation.LabelOf("L2 Arch (Env_RS_HeroArch_P_A)"), Is.EqualTo("Arch"));
            Assert.That(HeroBasinSegmentation.LabelOf("L0 Dressing rocks (Boulder)"), Is.EqualTo("Dressing rocks"));
        }

        [Test]
        public void DressingDefaultsFollowTheMap()
        {
            var d = new HeroDressing();
            Assert.That(d.ArchClumps, Is.InRange(35, 45));
            Assert.That(d.ArchCurtains, Is.InRange(20, 30));
            Assert.That(d.Islands, Is.InRange(14, 18));
            Assert.That(d.GodRayEnds.Length, Is.InRange(3, 4));
        }

        [Test]
        public void LandscapePassHasHeroFramingAndAClearCentreBand()
        {
            HeroDressingPass p = HeroDressingPass.LandscapeDefault();
            Assert.That(System.Array.Exists(p.Frames, f => f.Piece == "FrameLeft_P" && f.Uv.x < 0.25f), "broadleaf cluster lower left");
            Assert.That(System.Array.Exists(p.Frames, f => f.Piece == "Bellflower_P" && f.Uv.x > 0.75f), "bell-flower clump lower right");
            Assert.That(p.ClearBandU.x, Is.LessThan(p.ClearBandU.y));
            Assert.That(p.ClearBandU.x, Is.InRange(0.2f, 0.4f), "centre band starts right of the left framing");
            foreach (HeroFramePlant f in p.Frames)
            {
                Assert.That(f.DepthM, Is.EqualTo(0f), "landscape framing stands on ground: " + f.Piece);
            }
        }

        [Test]
        public void PortraitPassStaysOnTheNearGroundAndCarriesNoBellClump()
        {
            HeroDressingPass p = HeroDressingPass.PortraitDefault();
            Assert.That(p.MaxDistanceM, Is.LessThan(30f));
            Assert.That(System.Array.Exists(p.Frames, f => f.Piece == "Bellflower_P"), Is.False);
            foreach (HeroFramePlant f in p.Frames)
            {
                Assert.That(f.DepthM, Is.LessThan(6f), "lens-near plants stay behind the landscape camera's view: " + f.Piece);
                Assert.That(f.Uv.x, Is.InRange(0f, 1f));
                Assert.That(f.Uv.y, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SpillwaysAreRealDrops()
        {
            var d = new HeroDressing();
            Assert.That(d.Spillways, Is.InRange(4, 8));
            Assert.That(d.SpillwayDropShare, Is.GreaterThan(0.2f), "a visible step, not a foam band");
        }
    }
}
