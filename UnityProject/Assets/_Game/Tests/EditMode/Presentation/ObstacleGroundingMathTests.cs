using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Presentation
{
    /// <summary>
    /// Spec 005 wave 1, pure math of the grounded obstacle rigs: AC-506 (stateless variants), embed (G1, 3.4), the
    /// grounding audit math (AC-501 / AC-504), the no-slip roll (AC-510), settle (AC-511), sway (AC-514), the context
    /// layout (AC-507 / AC-508 / AC-509) and the strike pose contract.
    /// </summary>
    public sealed class ObstacleGroundingMathTests
    {
        private const float Epsilon = 1e-3f;
        private const float LaneWidthM = 2.4f;
        private const float BendCurvature = 1f / 400f;

        // ------------------------------------------------------------ AC-506 variants

        [Test]
        public void Variant_IsAPureFunctionOfSeedAndId_AnyOrder()
        {
            const ulong seed = 0xC0FFEEUL;
            var forward = new ObstacleVariant[200];
            for (int id = 1; id <= 200; id++)
            {
                forward[id - 1] = GroundingMath.ResolveVariant(seed, id, 3, 0f, 0, BendCurvature);
            }

            for (int id = 200; id >= 1; id--)
            {
                ObstacleVariant again = GroundingMath.ResolveVariant(seed, id, 3, 0f, 0, BendCurvature);
                Assert.AreEqual(forward[id - 1].Skin, again.Skin, "skin of " + id);
                Assert.AreEqual(forward[id - 1].Mirror, again.Mirror, "mirror of " + id);
                Assert.AreEqual(forward[id - 1].ContextSide, again.ContextSide, "side of " + id);
                Assert.AreEqual(forward[id - 1].Detail0, again.Detail0, 0f, "detail of " + id);
            }
        }

        [Test]
        public void Variant_RepeatedThousandTimes_IsIdentical()
        {
            ObstacleVariant first = GroundingMath.ResolveVariant(77UL, 42, 3, 0.001f, 1, BendCurvature);
            for (int i = 0; i < 1000; i++)
            {
                ObstacleVariant v = GroundingMath.ResolveVariant(77UL, 42, 3, 0.001f, 1, BendCurvature);
                Assert.AreEqual(first.Skin, v.Skin);
                Assert.AreEqual(first.Mirror, v.Mirror);
                Assert.AreEqual(first.ContextSide, v.ContextSide);
            }
        }

        [Test]
        public void Variant_SkinStaysInRange_AndAllSkinsAppear()
        {
            var counts = new int[3];
            for (int id = 1; id <= 3000; id++)
            {
                ObstacleVariant v = GroundingMath.ResolveVariant(12345UL, id, 3, 0f, 0, BendCurvature);
                Assert.That(v.Skin, Is.InRange(0, 2));
                counts[v.Skin]++;
            }

            for (int s = 0; s < 3; s++)
            {
                Assert.That(counts[s], Is.InRange(800, 1200), "skin " + s);
            }
        }

        [Test]
        public void Variant_DifferentSeedsGiveDifferentJungles()
        {
            int differing = 0;
            for (int id = 1; id <= 200; id++)
            {
                ObstacleVariant a = GroundingMath.ResolveVariant(1UL, id, 3, 0f, 0, BendCurvature);
                ObstacleVariant b = GroundingMath.ResolveVariant(2UL, id, 3, 0f, 0, BendCurvature);
                if (a.Skin != b.Skin || a.Mirror != b.Mirror)
                {
                    differing++;
                }
            }

            Assert.Greater(differing, 100);
        }

        [Test]
        public void Variant_SingleSkinArchetype_IsAlwaysSkinZero()
        {
            for (int id = 1; id <= 100; id++)
            {
                Assert.AreEqual(0, GroundingMath.ResolveVariant(9UL, id, 1, 0f, 0, BendCurvature).Skin);
            }
        }

        [Test]
        public void ContextSide_IsTheOuterSideOfABend_AndThePreferredSideOnAStraight()
        {
            // Positive curvature is a right turn, so the outer side is left.
            Assert.AreEqual(-1, GroundingMath.ContextSide(0.01f, 0.9f, 1, BendCurvature));
            Assert.AreEqual(1, GroundingMath.ContextSide(-0.01f, 0.1f, -1, BendCurvature));
            Assert.AreEqual(1, GroundingMath.ContextSide(0f, 0.1f, 1, BendCurvature));
            Assert.AreEqual(-1, GroundingMath.ContextSide(0.0001f, 0.9f, -1, BendCurvature));
            Assert.AreEqual(-1, GroundingMath.ContextSide(0f, 0.2f, 0, BendCurvature));
            Assert.AreEqual(1, GroundingMath.ContextSide(0f, 0.8f, 0, BendCurvature));
        }

        // ------------------------------------------------------------ G1 embed

        [Test]
        public void Embed_FlatGround_StaysInTheSpecRangeOfTheSurface()
        {
            for (int k = 0; k <= 10; k++)
            {
                float u = k / 10f;
                Assert.That(GroundingMath.EmbedDepth(PathSurface.Trail, u, 0f, 0.6f), Is.InRange(0.06f - Epsilon, 0.12f + Epsilon));
                Assert.That(GroundingMath.EmbedDepth(PathSurface.Ford, u, 0f, 0.6f), Is.InRange(0.06f - Epsilon, 0.12f + Epsilon));
                Assert.That(GroundingMath.EmbedDepth(PathSurface.Mud, u, 0f, 0.6f), Is.InRange(0.10f - Epsilon, 0.12f + Epsilon));
                Assert.That(GroundingMath.EmbedDepth(PathSurface.Bough, u, 0f, 0.6f), Is.InRange(0.04f - Epsilon, 0.08f + Epsilon));
                Assert.That(GroundingMath.EmbedDepth(PathSurface.Ledge, u, 0f, 0.6f), Is.InRange(0.03f - Epsilon, 0.06f + Epsilon));
                Assert.That(GroundingMath.EmbedDepth(PathSurface.Planks, u, 0f, 0.6f), Is.InRange(0.03f - Epsilon, 0.06f + Epsilon));
            }
        }

        [Test]
        public void Embed_OnASlope_AddsTanSlopeTimesHalfTheDepth()
        {
            float flat = GroundingMath.EmbedDepth(PathSurface.Trail, 0.5f, 0f, 4f);
            float up = GroundingMath.EmbedDepth(PathSurface.Trail, 0.5f, 12f, 4f);
            float down = GroundingMath.EmbedDepth(PathSurface.Trail, 0.5f, -12f, 4f);
            Assert.AreEqual(0.12f * 4f * 0.5f, up - flat, Epsilon);
            Assert.AreEqual(up, down, 0f);
        }

        [Test]
        public void Embed_ClampsTheHashUnit()
        {
            Assert.AreEqual(
                GroundingMath.EmbedDepth(PathSurface.Trail, 1f, 0f, 1f),
                GroundingMath.EmbedDepth(PathSurface.Trail, 5f, 0f, 1f),
                0f);
            Assert.AreEqual(
                GroundingMath.EmbedDepth(PathSurface.Trail, 0f, 0f, 1f),
                GroundingMath.EmbedDepth(PathSurface.Trail, -3f, 0f, 1f),
                0f);
        }

        // ------------------------------------------------------------ AC-501 / AC-504 audit math

        [Test]
        public void Audit_ModelThatFillsTheBoxAndIsEmbedded_Passes()
        {
            var hitMin = new Vector3(-1.02f, 0f, -0.3f);
            var hitMax = new Vector3(1.02f, 0.8f, 0.3f);
            var visMin = new Vector3(-1.17f, -0.08f, -0.3f);
            var visMax = new Vector3(1.17f, 0.84f, 0.3f);
            GroundingAuditResult r = GroundingAuditMath.Evaluate(hitMin, hitMax, visMin, visMax, true, 0.15f, 0.03f, 0.02f);
            Assert.IsTrue(r.Pass, r.Failures.ToString());
            Assert.AreEqual(0.08f, r.EmbedM, Epsilon);
            Assert.AreEqual(0f, r.GhostFrontM, Epsilon);
        }

        [Test]
        public void Audit_ModelStartingBehindTheLethalFace_IsAGhostFront()
        {
            var hitMin = new Vector3(-1.02f, 0f, -0.3f);
            var hitMax = new Vector3(1.02f, 0.8f, 0.3f);
            var visMin = new Vector3(-1.02f, -0.08f, -0.05f);
            var visMax = new Vector3(1.02f, 0.8f, 0.3f);
            GroundingAuditResult r = GroundingAuditMath.Evaluate(hitMin, hitMax, visMin, visMax, true, 0.15f, 0.03f, 0.02f);
            Assert.AreEqual(0.25f, r.GhostFrontM, Epsilon);
            Assert.AreNotEqual(GroundingFailures.None, r.Failures & GroundingFailures.GhostFront);
            Assert.IsFalse(r.Pass);
        }

        [Test]
        public void Audit_LowTopAndNarrowSides_AreGhosts()
        {
            var hitMin = new Vector3(-1f, 0f, -0.3f);
            var hitMax = new Vector3(1f, 0.8f, 0.3f);
            var visMin = new Vector3(-0.7f, -0.08f, -0.3f);
            var visMax = new Vector3(0.9f, 0.5f, 0.3f);
            GroundingAuditResult r = GroundingAuditMath.Evaluate(hitMin, hitMax, visMin, visMax, true, 0.15f, 0.03f, 0.02f);
            Assert.AreEqual(0.3f, r.GhostTopM, Epsilon);
            Assert.AreEqual(0.3f, r.GhostLeftM, Epsilon);
            Assert.AreEqual(0.1f, r.GhostRightM, Epsilon);
            Assert.AreNotEqual(GroundingFailures.None, r.Failures & GroundingFailures.GhostTop);
            Assert.AreNotEqual(GroundingFailures.None, r.Failures & GroundingFailures.GhostSide);
        }

        [Test]
        public void Audit_FloatingAndShallowEmbed_AreCaught()
        {
            var hitMin = new Vector3(-1f, 0f, -0.3f);
            var hitMax = new Vector3(1f, 0.8f, 0.3f);
            GroundingAuditResult floating = GroundingAuditMath.Evaluate(
                hitMin, hitMax, new Vector3(-1f, 0.05f, -0.3f), new Vector3(1f, 0.8f, 0.3f), true, 0.15f, 0.03f, 0.02f);
            Assert.AreEqual(0.05f, floating.FloatM, Epsilon);
            Assert.AreNotEqual(GroundingFailures.None, floating.Failures & GroundingFailures.Floating);

            GroundingAuditResult shallow = GroundingAuditMath.Evaluate(
                hitMin, hitMax, new Vector3(-1f, -0.005f, -0.3f), new Vector3(1f, 0.8f, 0.3f), true, 0.15f, 0.03f, 0.02f);
            Assert.AreNotEqual(GroundingFailures.None, shallow.Failures & GroundingFailures.EmbedTooShallow);
            Assert.AreEqual(GroundingFailures.None, shallow.Failures & GroundingFailures.Floating);
        }

        [Test]
        public void Audit_ElevatedBox_ChecksTheBottomNotTheGround()
        {
            var hitMin = new Vector3(-1f, 1.1f, -0.25f);
            var hitMax = new Vector3(1f, 3f, 0.25f);
            GroundingAuditResult ok = GroundingAuditMath.Evaluate(
                hitMin, hitMax, new Vector3(-1f, 1.1f, -0.29f), new Vector3(1f, 3.05f, 0.25f), true, 0.15f, 0.03f, 0.02f);
            Assert.IsTrue(ok.Pass, ok.Failures.ToString());
            Assert.AreEqual(0f, ok.EmbedM, 0f);

            GroundingAuditResult high = GroundingAuditMath.Evaluate(
                hitMin, hitMax, new Vector3(-1f, 1.5f, -0.25f), new Vector3(1f, 3.05f, 0.25f), true, 0.15f, 0.03f, 0.02f);
            Assert.AreEqual(0.4f, high.GhostBottomM, Epsilon);
            Assert.AreNotEqual(GroundingFailures.None, high.Failures & GroundingFailures.GhostBottom);
        }

        [Test]
        public void Audit_NothingDrawn_Fails()
        {
            GroundingAuditResult r = GroundingAuditMath.Evaluate(
                Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, false, 0.15f, 0.03f, 0.02f);
            Assert.AreEqual(GroundingFailures.NoVisible, r.Failures);
        }

        // ------------------------------------------------------------ AC-510 roll

        [Test]
        public void Roll_OmegaIsSpeedOverRadius_AndTheContactPointIsAtRest()
        {
            float omega = GroundingMath.RollOmega(4.8f, 0.95f);
            Assert.AreEqual(5.0526f, omega, 1e-3f);
            Assert.AreEqual(0f, GroundingMath.ContactPointSpeed(4.8f, omega, 0.95f), 1e-4f);
            Assert.Less(GroundingMath.ContactPointSpeed(4.8f, omega, 0.95f), 0.1f);
        }

        [Test]
        public void Roll_AngleIsMinusXOverR_SoTheTopMovesWithTheTravel()
        {
            Assert.AreEqual(0f, GroundingMath.RollAngleDeg(0f, 0.95f), 0f);
            Assert.AreEqual(-57.2958f, GroundingMath.RollAngleDeg(0.95f, 0.95f), 1e-2f);
            Assert.AreEqual(57.2958f, GroundingMath.RollAngleDeg(-0.95f, 0.95f), 1e-2f);
        }

        [Test]
        public void Roll_AngleChangeBetweenTicks_MatchesOmegaTimesDt()
        {
            // The sim moves 4.8 m/s: 0.08 m per tick. The angle step must equal omega * dt (no-slip), tick after tick.
            float dt = 1f / 60f;
            float expectedStepDeg = GroundingMath.RollOmega(4.8f, 0.95f) * dt * Mathf.Rad2Deg;
            for (int tick = 0; tick < 30; tick++)
            {
                float a = GroundingMath.RollAngleDeg(4.8f * dt * tick, 0.95f);
                float b = GroundingMath.RollAngleDeg(4.8f * dt * (tick + 1), 0.95f);
                Assert.AreEqual(expectedStepDeg, a - b, 1e-3f);
            }
        }

        [Test]
        public void IdleRock_StaysWithinTheIdleAmplitude_ThenGrowsAndLeansToTheEndLane()
        {
            float max = 0f;
            for (int i = 0; i < 600; i++)
            {
                float angle = GroundingMath.IdleRockDeg(i / 60f, 0.5f, 0.7f, 1.5f, 5f, 0f, 1f);
                max = Mathf.Max(max, Mathf.Abs(angle));
            }

            Assert.LessOrEqual(max, 1.5f + Epsilon);
            Assert.Greater(max, 1.4f);

            float lean = GroundingMath.IdleRockDeg(0f, 0f, 0.7f, 1.5f, 5f, 1f, 1f);
            Assert.AreEqual(2.5f, lean, Epsilon);
            Assert.AreEqual(-2.5f, GroundingMath.IdleRockDeg(0f, 0f, 0.7f, 1.5f, 5f, 1f, -1f), Epsilon);
        }

        // ------------------------------------------------------------ AC-511 settle, chock, dust

        [Test]
        public void Settle_SinksOverSixTicks_ThenRocksAndIsStillAfter04Seconds()
        {
            GroundingMath.SettlePose(0f, 0.10f, 6, out float sink0, out float squash0, out float rock0);
            Assert.AreEqual(0f, sink0, Epsilon);
            Assert.AreEqual(0f, squash0, Epsilon);
            Assert.AreEqual(0f, rock0, Epsilon);

            GroundingMath.SettlePose(6f / 60f, 0.10f, 6, out float sinkEnd, out _, out _);
            Assert.AreEqual(0.10f, sinkEnd, Epsilon);

            GroundingMath.SettlePose(0.5f, 0.10f, 6, out float sinkRest, out float squashRest, out float rockRest);
            Assert.AreEqual(0.07f, sinkRest, Epsilon);
            Assert.AreEqual(0f, rockRest, 0f);
            Assert.AreEqual(0f, squashRest, Epsilon);

            float maxRock = 0f;
            float maxSquash = 0f;
            for (int i = 0; i <= 60; i++)
            {
                GroundingMath.SettlePose(i / 100f, 0.10f, 6, out _, out float squash, out float rock);
                maxRock = Mathf.Max(maxRock, Mathf.Abs(rock));
                maxSquash = Mathf.Max(maxSquash, squash);
            }

            Assert.LessOrEqual(maxRock, 3f + Epsilon);
            Assert.Greater(maxRock, 0.5f);
            Assert.LessOrEqual(maxSquash, 0.03f + Epsilon);
        }

        [Test]
        public void ChockPop_FliesAwayInTheTravelDirection_AndEndsAfterTheDuration()
        {
            Assert.IsTrue(GroundingMath.ChockPopPose(0.2f, 0.4f, 1f, out float dx, out float dy, out _));
            Assert.Greater(dx, 0f);
            Assert.Greater(dy, 0f);
            Assert.IsTrue(GroundingMath.ChockPopPose(0.2f, 0.4f, -1f, out float dxLeft, out _, out _));
            Assert.Less(dxLeft, 0f);
            Assert.IsFalse(GroundingMath.ChockPopPose(0.4f, 0.4f, 1f, out _, out _, out _));
            Assert.IsFalse(GroundingMath.ChockPopPose(-0.1f, 0.4f, 1f, out _, out _, out _));
        }

        [Test]
        public void Puffs_EmitAtTheConfiguredRate_AndShrinkToNothing()
        {
            float acc = 0f;
            int total = 0;
            for (int i = 0; i < 60; i++)
            {
                total += GroundingMath.PuffsDue(ref acc, 20f, 1f / 60f);
            }

            Assert.That(total, Is.InRange(19, 20));

            acc = 0f;
            total = 0;
            for (int i = 0; i < 60; i++)
            {
                total += GroundingMath.PuffsDue(ref acc, 6f, 1f / 60f);
            }

            Assert.That(total, Is.InRange(5, 6));
            Assert.AreEqual(0f, GroundingMath.PuffScale(1f, 0.7f), Epsilon);
            Assert.Greater(GroundingMath.PuffScale(0.5f, 0.7f), 0.2f);
            Assert.Greater(GroundingMath.PuffScale(0f, 0.7f), 0f);
        }

        // ------------------------------------------------------------ AC-514 sway

        [Test]
        public void SwayAmplitude_IsQuietInsideTheFadeDistance_AndFullFarAway()
        {
            Assert.AreEqual(0.5f, GroundingMath.SwayAmplitudeDeg(5f, 3f, 0.5f, 12f), Epsilon);
            Assert.AreEqual(0.5f, GroundingMath.SwayAmplitudeDeg(12f, 3f, 0.5f, 12f), Epsilon);
            Assert.AreEqual(3f, GroundingMath.SwayAmplitudeDeg(24f, 3f, 0.5f, 12f), Epsilon);
            Assert.AreEqual(3f, GroundingMath.SwayAmplitudeDeg(80f, 3f, 0.5f, 12f), Epsilon);
            float previous = 0f;
            for (int d = 0; d <= 30; d++)
            {
                float a = GroundingMath.SwayAmplitudeDeg(d, 3f, 0.5f, 12f);
                Assert.GreaterOrEqual(a, previous - Epsilon);
                previous = a;
            }
        }

        [Test]
        public void MatSway_NeverLowersTheLowestTipBelowTheClearance()
        {
            // The mat hangs 1.95 m from its hinge: the drop of the tips is L * (1 - cos(angle)).
            float worstDrop = 1.95f * (1f - Mathf.Cos(3f * Mathf.Deg2Rad));
            Assert.Less(worstDrop, 0.05f);
            for (int i = 0; i < 600; i++)
            {
                float angle = GroundingMath.SwayDeg(i / 60f, 1f, 0.4f, 3f);
                Assert.LessOrEqual(Mathf.Abs(angle), 3f + Epsilon);
            }
        }

        // ------------------------------------------------------------ AC-507 / 508 / 509 context layout

        [Test]
        public void ContextLayout_PiecesStandInTheContextZone_AndRespectTheHeightCaps()
        {
            var anchors = new ContextAnchor[ContextLayout.MaxAnchors];
            ObstacleArchetype[] kinds =
            {
                ObstacleArchetype.LowBarrier, ObstacleArchetype.HighBarrier, ObstacleArchetype.FullBlock,
                ObstacleArchetype.Mover, ObstacleArchetype.LaneDenial,
            };
            byte[] masks = { LaneMasks.Lane0, LaneMasks.Lane1, LaneMasks.Lane2, (byte)(LaneMasks.Lane0 | LaneMasks.Lane1), (byte)(LaneMasks.Lane1 | LaneMasks.Lane2), LaneMasks.All, (byte)(LaneMasks.Lane0 | LaneMasks.Lane2) };
            float[] curvatures = { 0f, 0.005f, -0.005f };
            for (ulong seed = 1; seed <= 40; seed++)
            {
                for (int id = 1; id <= 25; id++)
                {
                    for (int k = 0; k < kinds.Length; k++)
                    {
                        for (int m = 0; m < masks.Length; m++)
                        {
                            for (int c = 0; c < curvatures.Length; c++)
                            {
                                int from = LaneMasks.Lowest(masks[m]);
                                int to = LaneMasks.Highest(masks[m]) == from ? (from == 0 ? 1 : from - 1) : LaneMasks.Highest(masks[m]);
                                int n = ContextLayout.ForObstacle(
                                    seed, id, kinds[k], masks[m], from, to, curvatures[c], LaneWidthM, BendCurvature, out ObstacleVariant v, out int skin, anchors);
                                Assert.That(skin, Is.InRange(0, GroundingMath.SkinCount(kinds[k]) - 1));
                                for (int a = 0; a < n; a++)
                                {
                                    ContextAnchor anchor = anchors[a];
                                    float x = Mathf.Abs(anchor.X);
                                    Assert.GreaterOrEqual(x, 4.2f - Epsilon, kinds[k] + " " + anchor.Kind + " too close");
                                    Assert.LessOrEqual(x + (anchor.Kind == ContextKind.Stump ? 0.55f : 0f), 7f + Epsilon, anchor.Kind + " too far");
                                    Assert.IsTrue(
                                        x - anchor.RadiusM >= 5f - Epsilon || anchor.HeightM <= 2.5f + Epsilon,
                                        anchor.Kind + " is taller than 2.5 m inside 4.2 to 5.0 m");
                                    Assert.GreaterOrEqual(anchor.RadiusM, 0.1f);
                                }

                                if (kinds[k] == ObstacleArchetype.LowBarrier || kinds[k] == ObstacleArchetype.HighBarrier)
                                {
                                    // On a bend the support stands on the outer side (left for a right turn).
                                    if (curvatures[c] > 0f && n > 0 && kinds[k] == ObstacleArchetype.HighBarrier)
                                    {
                                        Assert.Less(anchors[0].X, 0f);
                                    }
                                }

                                Assert.That(v.ContextSide, Is.InRange(-1, 1));
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void ContextLayout_IsDeterministic()
        {
            var a = new ContextAnchor[ContextLayout.MaxAnchors];
            var b = new ContextAnchor[ContextLayout.MaxAnchors];
            for (int id = 1; id <= 50; id++)
            {
                int na = ContextLayout.ForObstacle(5UL, id, ObstacleArchetype.LowBarrier, LaneMasks.All, 0, 2, 0f, LaneWidthM, BendCurvature, out ObstacleVariant va, out int sa, a);
                int nb = ContextLayout.ForObstacle(5UL, id, ObstacleArchetype.LowBarrier, LaneMasks.All, 0, 2, 0f, LaneWidthM, BendCurvature, out ObstacleVariant vb, out int sb, b);
                Assert.AreEqual(na, nb);
                Assert.AreEqual(sa, sb);
                Assert.AreEqual(va.Skin, vb.Skin);
                for (int i = 0; i < na; i++)
                {
                    Assert.AreEqual(a[i].Kind, b[i].Kind);
                    Assert.AreEqual(a[i].X, b[i].X, 0f);
                    Assert.AreEqual(a[i].ZOffsetM, b[i].ZOffsetM, 0f);
                }
            }
        }

        [Test]
        public void ContextLayout_LogThatTouchesNoEdgeLane_UsesTheSingleLogSkin_WithoutARootPlate()
        {
            var anchors = new ContextAnchor[ContextLayout.MaxAnchors];
            for (int id = 1; id <= 60; id++)
            {
                int n = ContextLayout.ForObstacle(
                    3UL, id, ObstacleArchetype.LowBarrier, LaneMasks.Lane1, 1, 1, 0f, LaneWidthM, BendCurvature, out _, out int skin, anchors);
                Assert.AreEqual(2, skin);
                Assert.AreEqual(0, n);
            }
        }

        [Test]
        public void ContextLayout_RootPlateStandsOnTheEdgeSideTheLogReaches()
        {
            var anchors = new ContextAnchor[ContextLayout.MaxAnchors];
            for (int id = 1; id <= 60; id++)
            {
                int n = ContextLayout.ForObstacle(
                    3UL, id, ObstacleArchetype.LowBarrier, LaneMasks.Lane0, 0, 0, 0.01f, LaneWidthM, BendCurvature, out _, out int skin, anchors);
                if (skin < 2)
                {
                    Assert.Greater(n, 0);
                    Assert.AreEqual(ContextKind.RootPlate, anchors[0].Kind);
                    Assert.Less(anchors[0].X, 0f, "a log in the left lane has its root plate on the left, even on a right bend");
                }
            }
        }

        [Test]
        public void ContextLayout_BoulderBankIsOnTheStartSide()
        {
            var anchors = new ContextAnchor[ContextLayout.MaxAnchors];
            int n = ContextLayout.ForObstacle(1UL, 9, ObstacleArchetype.Mover, LaneMasks.Lane0, 0, 1, 0f, LaneWidthM, BendCurvature, out _, out _, anchors);
            Assert.AreEqual(1, n);
            Assert.AreEqual(ContextKind.ScreeBank, anchors[0].Kind);
            Assert.Less(anchors[0].X, 0f);
            n = ContextLayout.ForObstacle(1UL, 9, ObstacleArchetype.Mover, LaneMasks.Lane2, 2, 1, 0f, LaneWidthM, BendCurvature, out _, out _, anchors);
            Assert.Greater(anchors[0].X, 0f);
        }

        [Test]
        public void KeepOut_ReportsTheSameCirclesTheRigIsBuiltFrom()
        {
            var frame = new PathFrame(RouteTuning.CreateDefault(), new StraightRouteSource());
            frame.BeginRun(11UL, 0.0);
            var obstacle = new ObstacleInstance
            {
                Id = 7,
                Archetype = ObstacleArchetype.LowBarrier,
                LaneMask = LaneMasks.Lane0,
                FromLane = 0,
                ToLane = 0,
                Z = 100.0,
                DepthM = 0.6f,
            };

            var scratch = new ContextAnchor[ContextLayout.MaxAnchors];
            var output = new KeepOutCircle[ContextKeepOut.MaxPerObstacle];
            int added = ContextKeepOut.Add(11UL, in obstacle, frame, LaneWidthM, BendCurvature, 0.5f, scratch, output, 0);

            var anchors = new ContextAnchor[ContextLayout.MaxAnchors];
            int expected = ContextLayout.ForObstacle(
                11UL, 7, ObstacleArchetype.LowBarrier, LaneMasks.Lane0, 0, 0, 0f, LaneWidthM, BendCurvature, out _, out _, anchors);
            Assert.AreEqual(expected, added);
            for (int i = 0; i < added; i++)
            {
                Assert.AreEqual(anchors[i].X, output[i].X, 0f);
                Assert.AreEqual(anchors[i].RadiusM + 0.5f, output[i].RadiusM, Epsilon);
                Assert.AreEqual(100.3 + anchors[i].ZOffsetM, output[i].S, 1e-4);
            }
        }

        [Test]
        public void KeepOut_GapsAndStrikesHaveNoContext()
        {
            var frame = new PathFrame(RouteTuning.CreateDefault(), new StraightRouteSource());
            frame.BeginRun(11UL, 0.0);
            var scratch = new ContextAnchor[ContextLayout.MaxAnchors];
            var output = new KeepOutCircle[ContextKeepOut.MaxPerObstacle];
            var gap = new ObstacleInstance { Id = 1, Archetype = ObstacleArchetype.Gap, LaneMask = LaneMasks.All, Z = 50.0, DepthM = 3f };
            var strike = new ObstacleInstance { Id = 2, Archetype = ObstacleArchetype.LaneStrike, LaneMask = LaneMasks.Lane1, Z = 80.0, DepthM = 2f };
            Assert.AreEqual(0, ContextKeepOut.Add(1UL, in gap, frame, LaneWidthM, BendCurvature, 0f, scratch, output, 0));
            Assert.AreEqual(0, ContextKeepOut.Add(1UL, in strike, frame, LaneWidthM, BendCurvature, 0f, scratch, output, 0));
        }

        // ------------------------------------------------------------ strike pose contract (current behavior)

        [Test]
        public void StrikePose_ColumnExistsOnlyWhileActive()
        {
            LaneStrikePhase[] hidden = { LaneStrikePhase.Dormant, LaneStrikePhase.Warning, LaneStrikePhase.Rest };
            for (int i = 0; i < hidden.Length; i++)
            {
                StrikePose.Evaluate(hidden[i], 10, 0.5f, out bool visible, out float bottom);
                Assert.IsFalse(visible, hidden[i].ToString());
                Assert.AreEqual(0f, bottom, 0f);
            }

            StrikePose.Evaluate(LaneStrikePhase.Active, 0, 0f, out bool active, out _);
            Assert.IsTrue(active);
        }

        [Test]
        public void StrikePose_DropsToTheGroundInThreeTicks_AndStaysThere()
        {
            StrikePose.Evaluate(LaneStrikePhase.Active, 0, 0f, out _, out float start);
            Assert.AreEqual(StrikePose.DropHeightM, start, Epsilon);
            float previous = start;
            for (int tick = 0; tick < 36; tick++)
            {
                StrikePose.Evaluate(LaneStrikePhase.Active, tick, 0.5f, out _, out float bottom);
                Assert.LessOrEqual(bottom, previous + Epsilon);
                previous = bottom;
            }

            StrikePose.Evaluate(LaneStrikePhase.Active, StrikePose.DropTicks, 0f, out _, out float landed);
            Assert.AreEqual(0f, landed, Epsilon);
            StrikePose.Evaluate(LaneStrikePhase.Active, 30, 0.9f, out _, out float held);
            Assert.AreEqual(0f, held, 0f);
        }
    }
}
