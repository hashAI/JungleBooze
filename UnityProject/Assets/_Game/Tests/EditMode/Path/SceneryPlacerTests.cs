using JungleBooze.Core;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>
    /// Spec 003 T8: AC-308 (nothing inside the sight corridor), AC-309 (stateless, identical rebuilds, seeds differ)
    /// and the pure-math part of AC-327 (count and triangle caps) for the stateless scenery placer, plus the dense
    /// corridor rings (wall, ground cover, far, canopy), tree-anchored vines and the vine-section framing.
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

        private static int PlaceFramed(
            ulong seed, long cell, RouteBeatKind beat, float curvature, PathLayer layer, int world, bool swingFrame, SceneryPiece[] buffer)
        {
            var context = new SceneryCellContext { Curvature = curvature, Beat = beat, Layer = layer, WorldIndex = world, SwingFrame = swingFrame };
            return SceneryPlacer.PlaceCell(seed, Stream, cell, in context, Settings, buffer);
        }

        private static SceneryPiece[] NewBuffer()
        {
            return new SceneryPiece[128];
        }

        private static bool IsInnerBend(int side, float curvature)
        {
            return System.Math.Abs(curvature) >= Settings.BendCurvature && side == (curvature > 0f ? 1 : -1);
        }

        /// <summary>Pieces that may enter the corridor above 7 m: stub family (Overhead band, not shafts) and the canopy.</summary>
        private static bool IsOverheadFamily(in SceneryPiece p)
        {
            return p.Band == SceneryBand.Canopy || (p.Band == SceneryBand.Overhead && p.Model != SceneryModel.LightShaft);
        }

        /// <summary>The lowest point of a hanging vine (the art is 6 m long at scale 1).</summary>
        private static float VineTipM(in SceneryPiece p)
        {
            return p.HeightM - (SceneryPlacer.VineNativeLengthM * p.ScaleY);
        }

        private static void AssertPieceRespectsTheCorridor(
            in SceneryPiece p, ulong seed, long cell, RouteBeatKind beat, float curvature, bool swingFrame)
        {
            string label = "seed " + seed + " cell " + cell + " " + p.Model + "/" + p.Band + " side " + p.Side + " inner " + p.InnerEdgeM;
            float edge = SceneryPlacer.CorridorEdgeM(p.Side, curvature, beat, Settings);
            bool swing = swingFrame || beat == RouteBeatKind.SwingZone;
            Assert.Greater(p.LateralM, 0f, label);
            Assert.IsTrue(p.Side == -1 || p.Side == 1, label);
            Assert.GreaterOrEqual(p.S, cell * 10.0 - 1e-6, label);
            Assert.LessOrEqual(p.S, (cell + 1) * 10.0 + 1e-6, label);

            if (p.Model == SceneryModel.LeafLitter)
            {
                // Flat decal: outside the lane band and shoulder, lying under 0.1 m.
                Assert.GreaterOrEqual(p.InnerEdgeM, Settings.LitterInnerM - Epsilon, label);
                Assert.Less(p.HeightM, 0.1f, label);
                return;
            }

            if (p.Band == SceneryBand.Canopy)
            {
                if (p.Model == SceneryModel.HangingVine)
                {
                    Assert.GreaterOrEqual(VineTipM(p), Settings.VineTipMinM - Epsilon, label + " canopy vine tip");
                    if (swing)
                    {
                        Assert.GreaterOrEqual(p.LateralM, Settings.CorridorSwingM - Epsilon, label + " canopy vine over the span");
                    }
                }
                else
                {
                    Assert.GreaterOrEqual(p.HeightM, Settings.CanopyMinHeightM - Epsilon, label + " canopy underside");
                    if (swing)
                    {
                        Assert.GreaterOrEqual(p.InnerEdgeM, Settings.CorridorSwingM - Epsilon, label + " canopy over the span");
                    }
                }

                return;
            }

            if (IsOverheadFamily(p))
            {
                // Stub, tuft and vine: at most 1 m inside the edge (0 m in a vine section or on the inner side of a bend), 7 m up or more.
                float overhang = swing || IsInnerBend(p.Side, curvature) ? 0f : Settings.StubOverhangM;
                if (p.Model == SceneryModel.HangingVine)
                {
                    Assert.GreaterOrEqual(VineTipM(p), Settings.VineTipMinM - Epsilon, label + " stub vine tip");
                    Assert.GreaterOrEqual(p.LateralM - p.FootprintM, edge - overhang - Epsilon, label);
                }
                else
                {
                    Assert.GreaterOrEqual(p.HeightM, 7f, label + " stub family height");
                    Assert.GreaterOrEqual(p.InnerEdgeM, edge - overhang - Epsilon, label);
                }

                return;
            }

            Assert.GreaterOrEqual(p.InnerEdgeM, edge - Epsilon, label);
            if (swingFrame)
            {
                Assert.GreaterOrEqual(p.InnerEdgeM, Settings.SwingFrameInnerM - Epsilon, label + " framed vine section");
            }
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
                    bool swingFrame = seed % 5UL == 0UL;
                    int n = PlaceFramed(seed, cell, beat, curvature, layer, world, swingFrame, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        AssertPieceRespectsTheCorridor(in buffer[i], seed, cell, beat, curvature, swingFrame);
                        checkedPieces++;
                    }
                }
            }

            Assert.Greater(checkedPieces, 500000, "the check must see a real amount of scenery");
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
                            // The Vine_Creeper model is 6 m long: the tip is Height - 6 * ScaleY and stays at 7 m or more (spec: nothing lower inside the corridor).
                            Assert.GreaterOrEqual(VineTipM(p), 7f - Epsilon);
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
        public void RingDensitiesAreThickNearRingOnePerTwoToThreeAndAHalfMetresMidOnePerFourAndAHalfToEightWallTrunkEveryThreeToFiveAndAHalf()
        {
            SceneryPiece[] buffer = NewBuffer();
            int near = 0;
            int mid = 0;
            int wallTrunks = 0;
            int far = 0;
            const int cells = 2000;
            for (long cell = 0; cell < cells; cell++)
            {
                int n = Place(5UL, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                for (int i = 0; i < n; i++)
                {
                    SceneryPiece p = buffer[i];
                    if (p.Band == SceneryBand.Near)
                    {
                        near++;
                    }
                    else if (p.Band == SceneryBand.Mid)
                    {
                        mid++;
                    }
                    else if (p.Band == SceneryBand.Far)
                    {
                        far++;
                    }
                    else if (p.Band == SceneryBand.Wall && p.Model == SceneryModel.WallTrunk)
                    {
                        wallTrunks++;
                    }
                }
            }

            double meters = cells * 10.0 * 2.0;
            Assert.GreaterOrEqual(meters / near, 2.0);
            Assert.LessOrEqual(meters / near, 3.5);
            Assert.GreaterOrEqual(meters / mid, 4.5);
            Assert.LessOrEqual(meters / mid, 8.0);
            Assert.GreaterOrEqual(meters / wallTrunks, 3.0);
            Assert.LessOrEqual(meters / wallTrunks, 5.5);
            Assert.GreaterOrEqual(meters / far, 5.0, "the far ring has silhouettes along the whole route");
            Assert.LessOrEqual(meters / far, 9.0);
        }

        [Test]
        public void WallRingHugsTheCorridorEdgeWithTrunksCrownsAndStubs()
        {
            SceneryPiece[] buffer = NewBuffer();
            int trunks = 0;
            int crowns = 0;
            int stubs = 0;
            int nearEdge = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Model == SceneryModel.WallTrunk)
                        {
                            Assert.AreEqual(SceneryBand.Wall, p.Band);
                            trunks++;
                            if (p.InnerEdgeM <= Settings.CorridorEdgeM + 2.5f)
                            {
                                nearEdge++;
                            }
                        }
                        else if (p.Band == SceneryBand.Wall && p.HeightM >= 3.4f)
                        {
                            crowns++;
                        }
                        else if (p.Model == SceneryModel.BranchStub)
                        {
                            Assert.GreaterOrEqual(p.HeightM, Settings.StubMinHeightM - Epsilon);
                            stubs++;
                        }
                    }
                }
            }

            Assert.Greater(trunks, 4000);
            Assert.Greater(crowns, trunks / 2, "most trunks carry a leaf mass");
            Assert.Greater(stubs, trunks / 4, "a good share of trunks grow a branch stub");
            Assert.Greater(nearEdge, trunks / 3, "the wall hugs the corridor edge");
        }

        [Test]
        public void EveryHangingVineHangsFromAStubTuftOrACanopyMass()
        {
            SceneryPiece[] buffer = NewBuffer();
            int vines = 0;
            int stubVines = 0;
            int canopyVines = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    RouteBeatKind beat = Beats[(int)((seed + (ulong)cell) % (ulong)Beats.Length)];
                    int n = PlaceFramed(seed, cell, beat, 0f, PathLayer.Floor, (int)(seed % 4UL), seed % 7UL == 0UL, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece vine = buffer[i];
                        if (vine.Model != SceneryModel.HangingVine)
                        {
                            continue;
                        }

                        vines++;
                        bool supported = false;
                        for (int k = 0; k < n && !supported; k++)
                        {
                            SceneryPiece support = buffer[k];
                            bool isMass = support.Model == SceneryModel.LeafMassA || support.Model == SceneryModel.LeafMassB || support.Model == SceneryModel.LeafMassC;
                            if (!isMass)
                            {
                                continue;
                            }

                            if (vine.Band == SceneryBand.Overhead
                                && support.Band == SceneryBand.Overhead
                                && support.Side == vine.Side
                                && System.Math.Abs(support.LateralM - vine.LateralM) < 1e-3f
                                && System.Math.Abs(support.S - vine.S) < 1e-6
                                && System.Math.Abs((support.HeightM + 0.9f) - (vine.HeightM + 0.3f)) < 1e-3f)
                            {
                                supported = true;
                                stubVines++;
                            }
                            else if (vine.Band == SceneryBand.Canopy
                                && support.Band == SceneryBand.Canopy
                                && System.Math.Abs(support.S - vine.S) < 1e-6
                                && System.Math.Abs((support.HeightM + 0.8f) - vine.HeightM) < 1e-3f
                                && System.Math.Abs((support.Side * support.LateralM) - (vine.Side * vine.LateralM)) <= (0.55f * support.FootprintM) + 0.1f)
                            {
                                supported = true;
                                canopyVines++;
                            }
                        }

                        Assert.IsTrue(supported, "seed " + seed + " cell " + cell + ": a hanging vine with nothing above it");
                    }
                }
            }

            Assert.Greater(stubVines, 200, "stub vines must appear");
            Assert.Greater(canopyVines, 200, "canopy vines must appear");
            Assert.AreEqual(vines, stubVines + canopyVines);
        }

        [Test]
        public void BranchStubsPointAtThePathAndStayInTheirCell()
        {
            SceneryPiece[] buffer = NewBuffer();
            int stubs = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Model != SceneryModel.BranchStub)
                        {
                            continue;
                        }

                        stubs++;

                        // A positive roll tips +Y toward -x: the right side (+1) rolls positive, toward the path, rising 12 degrees.
                        Assert.AreEqual(p.Side * (90f - SceneryPlacer.StubTiltDeg), p.RollDeg, Epsilon);
                        Assert.GreaterOrEqual(p.FootprintM, SceneryPlacer.MinStubReachM - Epsilon);
                        Assert.Less(p.FootprintM, p.LateralM);
                    }
                }
            }

            Assert.Greater(stubs, 500);
        }

        [Test]
        public void CanopyHangsAtLeastTwelveMetresUpAndExistsAboveTheLanes()
        {
            SceneryPiece[] buffer = NewBuffer();
            int masses = 0;
            int overLanes = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        SceneryPiece p = buffer[i];
                        if (p.Band != SceneryBand.Canopy || p.Model == SceneryModel.HangingVine)
                        {
                            continue;
                        }

                        masses++;
                        Assert.GreaterOrEqual(p.HeightM, Settings.CanopyMinHeightM - Epsilon, "the camera never looks through the canopy at anything low");
                        Assert.LessOrEqual(p.HeightM, Settings.CanopyMaxHeightM + Epsilon);
                        if (p.InnerEdgeM < Settings.CorridorEdgeM)
                        {
                            overLanes++;
                        }
                    }
                }
            }

            Assert.Greater(masses, 1000);
            Assert.Greater(overLanes, masses / 4, "a dense stretch closes in overhead");
        }

        [Test]
        public void GroundCoverFillsTheFloorButNotTheHighLayer()
        {
            SceneryPiece[] buffer = NewBuffer();
            int cover = 0;
            int litter = 0;
            int highCover = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    int n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        if (buffer[i].Band == SceneryBand.Cover)
                        {
                            cover++;
                            if (buffer[i].Model == SceneryModel.LeafLitter)
                            {
                                litter++;
                            }
                        }
                    }

                    n = Place(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.High, 0, buffer);
                    for (int i = 0; i < n; i++)
                    {
                        if (buffer[i].Band == SceneryBand.Cover)
                        {
                            highCover++;
                        }
                    }
                }
            }

            Assert.Greater(cover, 100 * 20 * 2 * 4, "ground cover appears several times per side per cell");
            Assert.Greater(litter, cover / 3);
            Assert.AreEqual(0, highCover, "the forest floor is far below the canopy bough");
        }

        [Test]
        public void VineSectionFramingKeepsEverySolidPieceBehindTheAnchorTreeAndTheCanopyOffTheSpan()
        {
            SceneryPiece[] buffer = NewBuffer();
            int solid = 0;
            int overheadMasses = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                for (long cell = 0; cell < 20; cell++)
                {
                    // Once through the swing beat, once through the lookahead flag on a normal beat.
                    for (int mode = 0; mode < 2; mode++)
                    {
                        int n = mode == 0
                            ? Place(seed, cell, RouteBeatKind.SwingZone, 0f, PathLayer.Floor, 0, buffer)
                            : PlaceFramed(seed, cell, RouteBeatKind.Straight, 0f, PathLayer.Floor, 0, true, buffer);
                        for (int i = 0; i < n; i++)
                        {
                            SceneryPiece p = buffer[i];
                            if (p.Band == SceneryBand.Canopy)
                            {
                                if (p.Model != SceneryModel.HangingVine)
                                {
                                    overheadMasses++;
                                }

                                // Nothing hangs over the lanes of a vine section: the span (8.64 m) and the rope stay in clear view.
                                Assert.GreaterOrEqual(p.Model == SceneryModel.HangingVine ? p.LateralM : p.InnerEdgeM, Settings.CorridorSwingM - Epsilon);
                                continue;
                            }

                            if (p.Model == SceneryModel.LeafLitter || IsOverheadFamily(p))
                            {
                                continue;
                            }

                            // Anchor trees stand 6 to 9 m out with a trunk radius of up to 1.75 m, so solid growth starts at 11 m.
                            Assert.GreaterOrEqual(p.InnerEdgeM, Settings.SwingFrameInnerM - Epsilon, p.Model + " near the anchor tree");
                            solid++;
                        }
                    }
                }
            }

            Assert.Greater(solid, 10000, "the sides are still densely grown, they just start behind the anchor tree");
            Assert.Greater(overheadMasses, 100, "growth overhead still frames the section from the sides");
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

                        if (p.Band == SceneryBand.Canopy)
                        {
                            Assert.AreNotEqual(SceneryModel.HangingVine, p.Model, "no canopy vines in a clearing");
                            Assert.GreaterOrEqual(p.InnerEdgeM, 9f - Epsilon, "the sky stays open over the lanes of a clearing");
                        }
                    }
                }
            }

            Assert.Less(clearing, dense * 0.5, "a clearing has about 40 percent of the near ring");
            Assert.Greater(clearing, 0, "a clearing is thinner, not empty");
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
            long triangleSum = 0;
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
                        bool near = System.Math.Abs(p.S - reference) <= Settings.LodNearOf(p.Model);
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
                Assert.LessOrEqual(shafts, 6, "the every-second-cell gate keeps shafts to 6 in 12 cells even before the cap");
                triangleSum += budget.Triangles;
                if (budget.Dropped > 0)
                {
                    windowsWithDrops++;
                }
            }

            Assert.Less(worstCell, Settings.MaxPiecesPerCell, "a cell must fit its buffer with room to spare");
            Assert.AreEqual(90000, Settings.MaxTriangles, "the dense-corridor scenery cap");
            Assert.Greater(triangleSum / windows, 35000, "the dense corridor really uses the budget (nominal triangle counts)");
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
