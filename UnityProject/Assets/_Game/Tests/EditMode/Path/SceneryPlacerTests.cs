using JungleBooze.Core;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>
    /// Spec 003 T8: AC-308 (nothing inside the sight corridor), AC-309 (stateless, identical rebuilds, seeds differ)
    /// and the pure-math part of AC-327 (count and triangle caps) for the stateless scenery placer.
    /// </summary>
    public sealed class SceneryPlacerTests
    {
        private const ulong Stream = RandomStreamIds.Scenery;
        private const float Epsilon = 1e-3f;

        private static readonly ScenerySettings Settings = new ScenerySettings();

        private static readonly RouteBeatKind[] Beats =
        {
            RouteBeatKind.Straight, RouteBeatKind.GentleBend, RouteBeatKind.Bend, RouteBeatKind.SBend, RouteBeatKind.Roll,
            RouteBeatKind.RiseFall, RouteBeatKind.Clearing, RouteBeatKind.Crossing, RouteBeatKind.Bridge, RouteBeatKind.Ascent,
            RouteBeatKind.Descent, RouteBeatKind.SwingZone, RouteBeatKind.Gateway,
        };

        private static readonly float[] Curvatures = { 0f, 0.002f, 0.008f, -0.008f, 0.0133f, -0.0133f };

        private static int Place(
            ulong seed, long cell, RouteBeatKind beat, float curvature, PathLayer layer, int world, SceneryPiece[] buffer)
        {
            var context = new SceneryCellContext { Curvature = curvature, Beat = beat, Layer = layer, WorldIndex = world };
            return SceneryPlacer.PlaceCell(seed, Stream, cell, in context, Settings, buffer);
        }

        private static SceneryPiece[] NewBuffer()
        {
            return new SceneryPiece[64];
        }

        [Test]
        public void AC308_NoPieceReachesIntoTheSightCorridorFor1000Seeds()
        {
            SceneryPiece[] buffer = NewBuffer();
            int checkedPieces = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                for (long cell = -3; cell < 31; cell++)
                {
                    // Every seed and cell takes a different beat, bend and layer, so all combinations are covered.
                    RouteBeatKind beat = Beats[(int)((seed + (ulong)(cell + 3)) % (ulong)Beats.Length)];
                    float curvature = Curvatures[(int)((seed * 7UL + (ulong)(cell + 3)) % (ulong)Curvatures.Length)];
                    PathLayer layer = seed % 11UL == 0UL ? PathLayer.High : PathLayer.Floor;
                    int world = (int)(seed % 4UL);
                    int n = Place(seed, cell, beat, curvature, layer, world, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        float edge = SceneryPlacer.CorridorEdgeM(p.Side, curvature, beat, Settings);
                        Assert.GreaterOrEqual(
                            p.InnerEdgeM, edge - Epsilon,
                            "seed " + seed + " cell " + cell + " " + p.Model + " side " + p.Side + " reaches " + p.InnerEdgeM + " m, corridor " + edge + " m");
                        Assert.Greater(p.LateralM, 0f);
                        Assert.IsTrue(p.Side == -1 || p.Side == 1);
                        Assert.GreaterOrEqual(p.S, cell * 10.0 - 1e-6);
                        Assert.LessOrEqual(p.S, (cell + 1) * 10.0 + 1e-6);
                        checkedPieces++;
                    }
                }
            }

            Assert.Greater(checkedPieces, 100000, "the check must see a real amount of scenery");
        }

        [Test]
        public void AC308_CorridorIs5Elsewhere7OnTheInnerSideOfABendAnd9InASwingZone()
        {
            Assert.AreEqual(5f, SceneryPlacer.CorridorEdgeM(1, 0f, RouteBeatKind.Straight, Settings), Epsilon);
            Assert.AreEqual(5f, SceneryPlacer.CorridorEdgeM(-1, 0f, RouteBeatKind.Straight, Settings), Epsilon);

            // Positive curvature turns right, so the right side is the inside.
            Assert.AreEqual(7f, SceneryPlacer.CorridorEdgeM(1, 0.01f, RouteBeatKind.Bend, Settings), Epsilon);
            Assert.AreEqual(5f, SceneryPlacer.CorridorEdgeM(-1, 0.01f, RouteBeatKind.Bend, Settings), Epsilon);
            Assert.AreEqual(7f, SceneryPlacer.CorridorEdgeM(-1, -0.01f, RouteBeatKind.Bend, Settings), Epsilon);
            Assert.AreEqual(5f, SceneryPlacer.CorridorEdgeM(1, -0.01f, RouteBeatKind.Bend, Settings), Epsilon);

            // A gentle bend (R 500 m) is not a bend for the corridor.
            Assert.AreEqual(5f, SceneryPlacer.CorridorEdgeM(1, 0.002f, RouteBeatKind.GentleBend, Settings), Epsilon);

            Assert.AreEqual(9f, SceneryPlacer.CorridorEdgeM(1, 0f, RouteBeatKind.SwingZone, Settings), Epsilon);
            Assert.AreEqual(9f, SceneryPlacer.CorridorEdgeM(-1, 0.01f, RouteBeatKind.SwingZone, Settings), Epsilon);
        }

        [Test]
        public void AC308_GroundPropsStayOutOfTheLaneBandAndHangingVinesStayHigh()
        {
            SceneryPiece[] buffer = NewBuffer();
            for (ulong seed = 1; seed <= 200; seed++)
            {
                for (long cell = 0; cell < 30; cell++)
                {
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Model == SceneryModel.Root || p.Model == SceneryModel.Rock)
                        {
                            Assert.Greater(p.InnerEdgeM, 4.2f, "roots and rocks stay out of the lane band (spec 9.6)");
                        }

                        if (p.Model == SceneryModel.HangingVine)
                        {
                            // The Vine_Liana model is 6 m long: the tip is Height - 6 * ScaleY and stays well above a runner's reach.
                            Assert.GreaterOrEqual(p.HeightM - (6f * p.ScaleY), 3.99f);
                        }
                    }
                }
            }
        }

        [Test]
        public void ThinTrunksAreNarrowAndAtLeastFourMetresApart()
        {
            SceneryPiece[] buffer = NewBuffer();
            int trunks = 0;
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                double lastS = double.NegativeInfinity;
                for (long cell = 0; cell < 600; cell++)
                {
                    int n = Place(77UL, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Band != SceneryBand.Near || p.Model != SceneryModel.GiantTrunk || p.Side != (sideIndex == 0 ? -1 : 1))
                        {
                            continue;
                        }

                        // Native trunk radius 1.5 m: diameter 0.6 m or less.
                        Assert.LessOrEqual(2f * 1.5f * p.ScaleXz, 0.6f + Epsilon);
                        Assert.GreaterOrEqual(p.S - lastS, 4.0);
                        lastS = p.S;
                        trunks++;
                    }
                }
            }

            Assert.Greater(trunks, 20, "thin trunks must actually appear");
        }

        [Test]
        public void NearRingDensityIsOnePropPerTwoAndAHalfToFourMetresAndMidRingOnePerTenToFourteen()
        {
            SceneryPiece[] buffer = NewBuffer();
            int near = 0;
            int mid = 0;
            const int cells = 2000;
            for (long cell = 0; cell < cells; cell++)
            {
                int n = Place(5UL, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                for (int i = 0; i < n; i++)
                {
                    if (buffer[i].Band == SceneryBand.Near)
                    {
                        near++;
                    }
                    else if (buffer[i].Band == SceneryBand.Mid)
                    {
                        mid++;
                    }
                }
            }

            double nearSpacing = (cells * 10.0 * 2.0) / near;
            double midSpacing = (cells * 10.0 * 2.0) / mid;
            Assert.GreaterOrEqual(nearSpacing, 2.5);
            Assert.LessOrEqual(nearSpacing, 4.0);
            Assert.GreaterOrEqual(midSpacing, 10.0);
            Assert.LessOrEqual(midSpacing, 14.0);
        }

        [Test]
        public void ClearingKeepsTheMidRingFarOutAndThinsTheNearRing()
        {
            SceneryPiece[] buffer = NewBuffer();
            int dense = 0;
            int clearing = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    dense += CountBand(Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer), buffer, SceneryBand.Near);
                    int n = Place(seed, cell, RouteBeatKind.Clearing, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Band == SceneryBand.Near)
                        {
                            clearing++;
                        }

                        if (p.Band == SceneryBand.Mid)
                        {
                            Assert.GreaterOrEqual(p.InnerEdgeM, Settings.ClearingMidMinM - Epsilon);
                        }

                        Assert.AreNotEqual(SceneryModel.HangingVine, p.Model, "the overhead is open in a clearing");
                    }
                }
            }

            Assert.Less(clearing, dense * 0.5, "a clearing has about 40 percent of the near ring");
        }

        [Test]
        public void HighLayerHasOnlyTuftsNearAndTrunkColumnsInTheMidRing()
        {
            SceneryPiece[] buffer = NewBuffer();
            for (ulong seed = 1; seed <= 200; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.High, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Band == SceneryBand.Near)
                        {
                            Assert.AreEqual(SceneryModel.Fern, p.Model);
                        }
                        else if (p.Band == SceneryBand.Mid)
                        {
                            Assert.AreEqual(SceneryModel.GiantTrunk, p.Model);
                        }
                    }
                }
            }
        }

        [Test]
        public void SparserWorldsGetFewerPieces()
        {
            SceneryPiece[] buffer = NewBuffer();
            int jungle = 0;
            int mountains = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    jungle += Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    mountains += Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 2, buffer);
                }
            }

            Assert.Less(mountains, jungle * 0.75);
        }

        [Test]
        public void AC309_ARebuiltCellIsIdenticalInAnyCallOrder()
        {
            SceneryPiece[] first = NewBuffer();
            SceneryPiece[] second = NewBuffer();
            SceneryPiece[] noise = NewBuffer();
            for (ulong seed = 1; seed <= 50; seed++)
            {
                for (long cell = -5; cell < 40; cell++)
                {
                    RouteBeatKind beat = Beats[(int)((ulong)(cell + 5) % (ulong)Beats.Length)];
                    int a = Place(seed, cell, beat, 0.005f, PathLayer.Floor, 1, first);

                    // Place other cells (streaming away and back) in between: placement keeps no state.
                    Place(seed + 1, cell + 17, RouteBeatKind.Bend, -0.01f, PathLayer.Floor, 0, noise);
                    Place(seed, cell - 1, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, noise);

                    int b = Place(seed, cell, beat, 0.005f, PathLayer.Floor, 1, second);
                    Assert.AreEqual(a, b, "seed " + seed + " cell " + cell);
                    for (int i = 0; i < a; i++)
                    {
                        Assert.IsTrue(Same(first[i], second[i]), "seed " + seed + " cell " + cell + " piece " + i);
                    }
                }
            }
        }

        [Test]
        public void AC309_TwoSeedsDressAtLeast95PercentOfCellsDifferently()
        {
            SceneryPiece[] a = NewBuffer();
            SceneryPiece[] b = NewBuffer();
            int different = 0;
            const int cells = 1000;
            for (long cell = 0; cell < cells; cell++)
            {
                int na = Place(1001UL, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, a);
                int nb = Place(1002UL, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, b);
                bool same = na == nb;
                for (int i = 0; same && i < na; i++)
                {
                    same = Same(a[i], b[i]);
                }

                if (!same)
                {
                    different++;
                }
            }

            Assert.GreaterOrEqual(different, cells * 95 / 100);
        }

        [Test]
        public void AC327_ACellNeverOverflowsItsBufferAndTheWindowStaysUnderTheCaps()
        {
            SceneryPiece[] buffer = NewBuffer();
            int worstCell = 0;
            int windowsWithDrops = 0;
            const int windows = 300;
            const int firstOffset = -1;
            const int lastOffset = 10;
            for (int w = 0; w < windows; w++)
            {
                ulong seed = 9000UL + (ulong)w;
                long hero = 3 + (w * 7);
                double reference = (hero + 0.5) * 10.0;
                var budget = new SceneryBudget();
                int shafts = 0;

                // Nearest first, as the view feeds them: ahead cells, then the cell behind.
                for (int step = 0; step <= lastOffset - firstOffset; step++)
                {
                    long cell = step <= lastOffset ? hero + step : hero - 1;
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    worstCell = System.Math.Max(worstCell, n);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        bool near = System.Math.Abs(p.S - reference) <= Settings.LodNearM;
                        int tris = near ? Settings.NominalNearTriangles[(int)p.Model] : Settings.NominalFarTriangles[(int)p.Model];
                        budget.TryTake(p.Model, tris, Settings);
                        if (p.Model == SceneryModel.LightShaft)
                        {
                            shafts++;
                        }
                    }
                }

                Assert.LessOrEqual(budget.Triangles, Settings.MaxTriangles);
                Assert.LessOrEqual(budget.Pieces, Settings.MaxActivePieces);
                Assert.LessOrEqual(budget.Shafts, Settings.MaxLightShaftsInView);
                Assert.LessOrEqual(shafts, 4, "the every-third-cell gate keeps shafts to 4 in 12 cells even before the cap");
                if (budget.Dropped > 0)
                {
                    windowsWithDrops++;
                }
            }

            Assert.Less(worstCell, Settings.MaxPiecesPerCell, "a cell must fit its buffer with room to spare");
            Assert.LessOrEqual(windowsWithDrops, windows / 20, "with nominal triangle counts the caps should rarely engage");
        }

        [Test]
        public void AC327_BudgetRefusesPiecesPastEachCap()
        {
            var settings = new ScenerySettings { MaxTriangles = 1000, MaxActivePieces = 3, MaxLightShaftsInView = 1 };
            var budget = new SceneryBudget();
            Assert.IsTrue(budget.TryTake(SceneryModel.TreeA, 900, settings));
            Assert.IsFalse(budget.TryTake(SceneryModel.TreeA, 200, settings), "triangle cap");
            Assert.IsTrue(budget.TryTake(SceneryModel.LightShaft, 2, settings));
            Assert.IsFalse(budget.TryTake(SceneryModel.LightShaft, 2, settings), "shaft cap");
            Assert.IsTrue(budget.TryTake(SceneryModel.Fern, 10, settings));
            Assert.IsFalse(budget.TryTake(SceneryModel.Fern, 10, settings), "piece cap");
            Assert.AreEqual(3, budget.Pieces);
            Assert.AreEqual(3, budget.Dropped);
        }

        [Test]
        public void HashIsStatelessAndSensitiveToEveryInput()
        {
            ulong baseline = SceneryHash.Mix(1UL, Stream, 5L, 0U, 0U, 0U);
            Assert.AreEqual(baseline, SceneryHash.Mix(1UL, Stream, 5L, 0U, 0U, 0U));
            Assert.AreNotEqual(baseline, SceneryHash.Mix(2UL, Stream, 5L, 0U, 0U, 0U));
            Assert.AreNotEqual(baseline, SceneryHash.Mix(1UL, Stream + 1UL, 5L, 0U, 0U, 0U));
            Assert.AreNotEqual(baseline, SceneryHash.Mix(1UL, Stream, 6L, 0U, 0U, 0U));
            Assert.AreNotEqual(baseline, SceneryHash.Mix(1UL, Stream, 5L, 1U, 0U, 0U));
            Assert.AreNotEqual(baseline, SceneryHash.Mix(1UL, Stream, 5L, 0U, 1U, 0U));
            Assert.AreNotEqual(baseline, SceneryHash.Mix(1UL, Stream, 5L, 0U, 0U, 1U));
            for (long cell = -50; cell < 50; cell++)
            {
                float u = SceneryHash.Unit(SceneryHash.Mix(3UL, Stream, cell, 0U, 0U, 0U));
                Assert.GreaterOrEqual(u, 0f);
                Assert.Less(u, 1f);
            }
        }

        private static int CountBand(int n, SceneryPiece[] pieces, SceneryBand band)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (pieces[i].Band == band)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Same(in SceneryPiece a, in SceneryPiece b)
        {
            return a.Model == b.Model && a.Band == b.Band && a.Side == b.Side && a.LateralM == b.LateralM && a.S == b.S
                && a.ScaleXz == b.ScaleXz && a.ScaleY == b.ScaleY && a.YawDeg == b.YawDeg && a.RollDeg == b.RollDeg
                && a.FootprintM == b.FootprintM && a.HeightM == b.HeightM;
        }
    }
}
