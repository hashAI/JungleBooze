using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Spec 001 section 9 (AC-34 to AC-44) with the spec 002 changes (final boxes, relative motion for movers,
    /// table 5.2). Obstacles come from <see cref="TestTrackQuery"/> with the boxes of spec 002 table 5.1.
    /// Tick maths in the comments: with the ramp off, HERO's center z after tick t is (t + 1) × speed / 60, and
    /// HERO's box spans z ± 0.25 m.
    /// </summary>
    public sealed class RunnerCollisionTests
    {
        private static RunnerTestHarness Harness(double speedMps, TestTrackQuery track, RunnerDesignValues values = null)
        {
            RunnerDesignValues v = values ?? RunnerDesignValues.CreateDefault();
            v.RunStartRampMs = 0f;
            return new RunnerTestHarness(v, SpeedCurve.CreateConstant(speedMps), track);
        }

        private static RunnerEvent SingleDeath(RunnerTestHarness h)
        {
            List<RunnerEvent> died = h.EventsOf(RunnerEventType.Died);
            Assert.AreEqual(1, died.Count, "exactly one Died event");
            return died[0];
        }

        private static void AssertHit(RunnerEvent died, long tick, ObstacleArchetype archetype, int id, bool afterStumble)
        {
            Assert.AreEqual(tick, died.Tick, "death tick");
            Assert.AreEqual((short)DeathCause.Hit, died.Value, "cause");
            Assert.AreEqual((byte)archetype, died.Archetype, "archetype");
            Assert.AreEqual(id, died.EntityId, "obstacle id");
            Assert.AreEqual(afterStumble, died.HasFlag(RunnerEventFlags.AfterStumble), "afterStumble");
        }

        // ---- AC-34 ----

        [Test]
        public void AC34_RunningIntoFullBlock_DiesOnFirstOverlappingTick()
        {
            // 10 m/s: HERO's front is at 4.917 m after tick 27 and 5.083 m after tick 28.
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(7, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.RunThrough(27);
            Assert.IsFalse(h.State.IsDead);

            h.Step(); // tick 28
            Assert.IsTrue(h.State.IsDead);
            Assert.AreEqual(Locomotion.Dead, h.State.Locomotion);
            AssertHit(SingleDeath(h), 28, ObstacleArchetype.FullBlock, 7, false);
            Assert.AreEqual(DeathCause.Hit, h.Sim.DeathCause);
            Assert.AreEqual(ObstacleArchetype.FullBlock, h.Sim.DeathArchetype);
            Assert.AreEqual(7, h.Sim.DeathEntityId);
            Assert.AreEqual(4.75, h.State.Z, 1e-9, "HERO is frozen at the moment of contact, front on the face");
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
        }

        // ---- AC-35 ----

        [Test]
        public void AC35_TimedJumpOverLowBarrier_NoContact_NearMissWhenClearanceSmall()
        {
            // Jump on tick 0 at 10 m/s. HERO overlaps the barrier's z range on ticks 22 to 29; the lowest feet
            // height in that span is 0.939 m (tick 29), a clearance of 0.139 m <= 0.35 m.
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.LowBarrier(3, 1, 4.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.Step(InputCommand.Jump);
            h.RunThrough(45);

            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
            Assert.AreEqual(0, h.Sim.DazeTicksLeft);
            List<RunnerEvent> near = h.EventsOf(RunnerEventType.NearMiss);
            Assert.AreEqual(1, near.Count);
            Assert.AreEqual(3, near[0].EntityId);
            Assert.AreEqual((byte)ObstacleArchetype.LowBarrier, near[0].Archetype);
            Assert.AreEqual(29L, near[0].Tick, "emitted on the tick HERO's back clears the barrier");
            Assert.AreEqual(1, h.CountOf(RunnerEventType.Landed));
        }

        [Test]
        public void AC35_JumpOverLowBarrierAtApex_NoNearMissWhenClearanceLarge()
        {
            // Barrier under the apex: lowest feet height during the z overlap is about 1.38 m (clearance 0.58 m).
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.LowBarrier(3, 1, 3.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.Step(InputCommand.Jump);
            h.RunThrough(45);

            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.NearMiss));
        }

        [Test]
        public void AC35_NoJump_RunningIntoLowBarrierIsLethal()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.LowBarrier(3, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.RunThrough(40);
            AssertHit(SingleDeath(h), 28, ObstacleArchetype.LowBarrier, 3, false);
        }

        // ---- AC-36 ----

        [Test]
        public void AC36_RunningIntoHighBarrier_Dies()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.HighBarrier(4, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.RunThrough(40);
            AssertHit(SingleDeath(h), 28, ObstacleArchetype.HighBarrier, 4, false);
        }

        [Test]
        public void AC36_SlidingUnderHighBarrier_NoContact()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.HighBarrier(4, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.RunThrough(19);
            h.Step(InputCommand.Slide); // tick 20; HERO's front reaches the barrier on tick 28
            h.RunThrough(70);

            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
            Assert.AreEqual(59L, h.EventsOf(RunnerEventType.SlideEnded)[0].Tick, "slide ran its normal 39 ticks");
        }

        [Test]
        public void AC36_SlideStartedOnTheContactTick_StillPassesUnder()
        {
            // A slide command shrinks the box for the whole tick it is processed on (generous to the player).
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.HighBarrier(4, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.RunThrough(27);
            h.Step(InputCommand.Slide); // tick 28, the first overlapping tick
            h.RunThrough(40);

            Assert.IsFalse(h.State.IsDead);
        }

        // ---- AC-37 ----

        [Test]
        public void AC37_JumpingUpIntoHighBarrierFromSlide_Dies()
        {
            // 6 m/s. HERO slides under a high barrier (front at 2.0 m) and jumps on tick 20 while under it:
            // the head rises through the barrier's underside (entry from below, lethal).
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.HighBarrier(4, 1, 2.0));
            RunnerTestHarness h = Harness(6.0, track);

            h.Step(InputCommand.Slide); // tick 0
            h.RunThrough(19);
            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(Locomotion.Sliding, h.State.Locomotion);

            h.Step(InputCommand.Jump); // tick 20
            Assert.IsTrue(h.State.IsDead);
            AssertHit(SingleDeath(h), 20, ObstacleArchetype.HighBarrier, 4, false);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
        }

        // ---- AC-38 ----

        /// <summary>
        /// 6 m/s, full block in lane 2 from z = 2.0 m. HERO's front passes it on tick 17. MoveRight on tick 20:
        /// X = 0.637 m after tick 20, 1.176 m after tick 21, when HERO's right side (1.526 m) enters the block's
        /// left side (1.38 m). Side stumble on tick 21.
        /// </summary>
        private static RunnerTestHarness SideStumbleSetup(TestTrackQuery track, int stumbleTick = 21)
        {
            RunnerTestHarness h = Harness(6.0, track);
            h.RunThrough(stumbleTick - 2);
            h.Step(InputCommand.MoveRight);
            h.Step();
            return h;
        }

        [Test]
        public void AC38_LaneMoveIntoSideOfFullBlock_StumbleBounceBackAndDaze()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(5, 2, 2.0));
            RunnerTestHarness h = SideStumbleSetup(track);
            const long s = 21;

            List<RunnerEvent> stumbles = h.EventsOf(RunnerEventType.Stumbled);
            Assert.AreEqual(1, stumbles.Count);
            Assert.AreEqual(s, stumbles[0].Tick);
            Assert.IsTrue(stumbles[0].HasFlag(RunnerEventFlags.Side));
            Assert.IsFalse(stumbles[0].HasFlag(RunnerEventFlags.Top));
            Assert.AreEqual(5, stumbles[0].EntityId);
            Assert.AreEqual((byte)ObstacleArchetype.FullBlock, stumbles[0].Archetype);
            Assert.AreEqual(-1, stumbles[0].Dir, "bounces back toward the origin lane");

            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(180, h.State.DazeTicksLeft);
            Assert.AreEqual(180, h.Sim.DazeTicksLeft);
            Assert.IsTrue(h.State.StumbleBounceActive);
            Assert.AreEqual(1, h.State.TargetLane, "the lane move was cancelled");

            h.RunThrough(s + 8);
            Assert.AreNotEqual(0f, h.State.X, "still bouncing");
            Assert.IsTrue(h.Sim.IsBouncing);

            h.Step(); // s + 9
            Assert.AreEqual(0f, h.State.X, "back at the origin lane center 9 ticks later");
            Assert.IsFalse(h.Sim.IsBouncing);
            Assert.AreEqual(180 - 9, h.State.DazeTicksLeft);

            h.RunThrough(s + 40);
            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(1, h.CountOf(RunnerEventType.Stumbled), "the obstacle is ignored for the rest of its pass");
            Assert.AreEqual(0, h.CountOf(RunnerEventType.NearMiss), "a stumble never counts as a near-miss");
        }

        [Test]
        public void AC38_RuleL2_LateralDuringBounceIsQueued_JumpWorks()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(5, 2, 2.0));
            RunnerTestHarness h = SideStumbleSetup(track);
            const long s = 21;

            h.Step(InputCommand.MoveLeft); // s + 1
            Assert.AreEqual(CommandOutcome.Queued, h.Sim.LastOutcomes.MoveLeft);
            Assert.AreEqual(-1, h.Sim.QueuedLateral);

            h.Step(InputCommand.Jump); // s + 2
            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.Jump);
            Assert.AreEqual(Locomotion.Airborne, h.State.Locomotion);

            int startsBefore = h.CountOf(RunnerEventType.LaneChangeStarted);
            h.RunThrough(s + 9);
            Assert.AreEqual(0f, h.State.X, "the bounce ends on the origin lane center");
            Assert.AreEqual(startsBefore, h.CountOf(RunnerEventType.LaneChangeStarted), "queued move waits for the bounce");

            h.Step(); // s + 10
            List<RunnerEvent> starts = h.EventsOf(RunnerEventType.LaneChangeStarted);
            RunnerEvent queued = starts[starts.Count - 1];
            Assert.AreEqual(s + 10, queued.Tick);
            Assert.IsTrue(queued.HasFlag(RunnerEventFlags.Queued));
            Assert.AreEqual(0, queued.Lane);

            h.RunThrough(s + 16);
            Assert.AreEqual(RunnerTestHarness.LaneX(0), h.State.X);
            Assert.AreEqual(h.Sim.Outcomes.CommandsReceived, h.Sim.Outcomes.Total);
            Assert.AreEqual(0, h.Sim.Outcomes.Get(CommandOutcome.Queued), "no command left pending");
        }

        // ---- AC-39 ----

        [Test]
        public void AC39_ComingDownOntoLowBarrierTop_TopStumbleNoBounce_LandsBeyond()
        {
            // Jump on tick 0 at 10 m/s. Feet are at 0.833 m after tick 30 and 0.717 m after tick 31; HERO is over
            // the barrier (z 5.0 to 5.6 m) from tick 28, so the feet come down through its top on tick 31.
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.LowBarrier(6, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.Step(InputCommand.Jump);
            h.RunThrough(30);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));

            h.Step(); // tick 31
            List<RunnerEvent> stumbles = h.EventsOf(RunnerEventType.Stumbled);
            Assert.AreEqual(1, stumbles.Count);
            Assert.AreEqual(31L, stumbles[0].Tick);
            Assert.IsTrue(stumbles[0].HasFlag(RunnerEventFlags.Top));
            Assert.IsFalse(stumbles[0].HasFlag(RunnerEventFlags.Side));
            Assert.AreEqual(6, stumbles[0].EntityId);
            Assert.AreEqual((byte)ObstacleArchetype.LowBarrier, stumbles[0].Archetype);
            Assert.AreEqual(0, stumbles[0].Dir, "no bounce");
            Assert.IsFalse(h.State.StumbleBounceActive);
            Assert.AreEqual(Locomotion.Airborne, h.State.Locomotion, "keeps the jump arc");
            Assert.AreEqual(180, h.State.DazeTicksLeft);

            h.RunThrough(36);
            List<RunnerEvent> landed = h.EventsOf(RunnerEventType.Landed);
            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(36L, landed[0].Tick);
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
            Assert.AreEqual(0f, h.State.X);
            Assert.Greater(h.State.Z - 0.25, 5.6, "landed on the ground beyond the barrier");

            h.RunThrough(60);
            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.NearMiss));
        }

        // ---- AC-40 ----

        private static TestTrackQuery TwoBlocksInLane2()
        {
            return new TestTrackQuery()
                .AddBox(ObstacleFixtures.FullBlock(1, 2, 2.0, 4.0))
                .AddBox(ObstacleFixtures.FullBlock(2, 2, 20.0, 20.0));
        }

        [Test]
        public void AC40_SecondStumble179TicksLater_IsLethalAfterStumble()
        {
            RunnerTestHarness h = SideStumbleSetup(TwoBlocksInLane2());
            const long s = 21;
            Assert.AreEqual(1, h.CountOf(RunnerEventType.Stumbled));

            h.RunThrough(s + 177);
            h.Step(InputCommand.MoveRight); // s + 178; contact one tick later
            Assert.AreEqual(2, h.State.DazeTicksLeft);
            h.Step(); // s + 179

            Assert.IsTrue(h.State.IsDead);
            AssertHit(SingleDeath(h), s + 179, ObstacleArchetype.FullBlock, 2, true);
            Assert.IsTrue(h.Sim.DeathAfterStumble);
            Assert.AreEqual(1, h.CountOf(RunnerEventType.Stumbled), "the lethal clip is a death, not a stumble");
        }

        [Test]
        public void AC40_SecondStumble181TicksLater_IsANewStumble()
        {
            RunnerTestHarness h = SideStumbleSetup(TwoBlocksInLane2());
            const long s = 21;

            h.RunThrough(s + 179);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.DazeEnded));
            h.Step(InputCommand.MoveRight); // s + 180
            List<RunnerEvent> dazeEnded = h.EventsOf(RunnerEventType.DazeEnded);
            Assert.AreEqual(1, dazeEnded.Count);
            Assert.AreEqual(s + 180, dazeEnded[0].Tick);
            Assert.AreEqual(0, h.State.DazeTicksLeft);

            h.Step(); // s + 181

            Assert.IsFalse(h.State.IsDead);
            List<RunnerEvent> stumbles = h.EventsOf(RunnerEventType.Stumbled);
            Assert.AreEqual(2, stumbles.Count);
            Assert.AreEqual(s + 181, stumbles[1].Tick);
            Assert.AreEqual(2, stumbles[1].EntityId);
            Assert.AreEqual(180, h.State.DazeTicksLeft, "a new daze window starts");
        }

        // ---- AC-41 ----

        private static RunnerDesignValues FiveTickMoves(float easeExponent)
        {
            RunnerDesignValues v = RunnerDesignValues.CreateDefault();
            v.LaneSwitchMs = 80f; // 5 ticks
            v.LaneSwitchEaseExponent = easeExponent;
            return v;
        }

        private static ObstacleBox WideBlockInLane1(double zFront)
        {
            // Test obstacle 2.4 m wide (wider than the kit's 2.04 m), so the geometry alone would still overlap.
            return ObstacleFixtures.Box(9, ObstacleArchetype.FullBlock, 1, 2.4f, 0f, 3f, zFront, 1.0);
        }

        [Test]
        public void AC41_EdgeForgiveness_MovingAwayAt144_NotHit()
        {
            // Linear 5-tick move: X = 0.48, 0.96, 1.44 m after ticks 0, 1, 2. 6 m/s: HERO's front reaches 0.5 m
            // during tick 2, when X = 1.44 m (HERO's left side 1.09 m is still inside the block's 1.2 m edge).
            var track = new TestTrackQuery().AddBox(WideBlockInLane1(0.5));
            RunnerTestHarness h = Harness(6.0, track, FiveTickMoves(1f));
            Assert.AreEqual(5, h.Config.LaneSwitchTicks);

            h.Step(InputCommand.MoveRight);
            h.Step();
            RunnerState s2 = h.Step(); // tick 2
            Assert.AreEqual(1.44f, s2.X, 1e-5f);
            Assert.Less(s2.X - 0.35f, 1.2f, "the boxes overlap geometrically");

            h.RunThrough(30);
            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
            Assert.AreEqual(RunnerTestHarness.LaneX(2), h.State.X);
        }

        [Test]
        public void AC41_EdgeForgiveness_At140_NormalOverlapRuleApplies()
        {
            // Ease exponent 1.7138 makes X = 1.40 m after tick 1 of a 5-tick move. The block's front is reached
            // during tick 1: below the 1.44 m threshold, so the front contact is lethal as usual.
            const float exponent = 1.7138309f;
            RunnerTestHarness control = Harness(6.0, new TestTrackQuery(), FiveTickMoves(exponent));
            control.Step(InputCommand.MoveRight);
            Assert.AreEqual(1.40f, control.Step().X, 1e-4f);

            var track = new TestTrackQuery().AddBox(WideBlockInLane1(0.4));
            RunnerTestHarness h = Harness(6.0, track, FiveTickMoves(exponent));
            h.Step(InputCommand.MoveRight);
            h.Step(); // tick 1

            Assert.IsTrue(h.State.IsDead);
            AssertHit(SingleDeath(h), 1, ObstacleArchetype.FullBlock, 9, false);
        }

        [Test]
        public void AC41_EdgeForgivenessRule_Threshold()
        {
            const double threshold = 1.44;
            const double tol = 1e-5;
            Assert.IsTrue(CollisionRules.IsEdgeForgiven(1.2, 1.44, 0.0, threshold, tol), "moving away at 1.44");
            Assert.IsTrue(CollisionRules.IsEdgeForgiven(-1.2, -1.5, 0.0, threshold, tol), "moving away to the left");
            Assert.IsFalse(CollisionRules.IsEdgeForgiven(1.2, 1.40, 0.0, threshold, tol), "1.40 is below the threshold");
            Assert.IsFalse(CollisionRules.IsEdgeForgiven(1.6, 1.5, 0.0, threshold, tol), "reversing toward the lane");
            Assert.IsFalse(CollisionRules.IsEdgeForgiven(1.5, 1.5, 0.0, threshold, tol), "not moving sideways");
            Assert.IsTrue(CollisionRules.IsEdgeForgiven(2.4 + 1.2, 2.4 + 1.5, 2.4, threshold, tol), "per box center");
        }

        // ---- AC-42 ----

        [Test]
        public void AC42_NoTunneling_ThinBlockAt40MetersPerSecond()
        {
            // 40 m/s = 0.667 m per tick. HERO's front is at 10.25 m after tick 14 and its back at 10.417 m after
            // tick 15, so no tick ends overlapping the 0.1 m deep block at 10.28 to 10.38 m. The sweep still hits it.
            var thin = ObstacleFixtures.FullBlock(11, 1, 10.28, 0.1);
            var track = new TestTrackQuery().AddBox(thin);
            RunnerTestHarness h = Harness(40.0, track);

            RunnerTestHarness noBlock = Harness(40.0, new TestTrackQuery());
            for (int tick = 0; tick <= 20; tick++)
            {
                RunnerState s = noBlock.Step();
                bool overlapsAtEnd = s.Z + 0.25 > thin.ZMin && s.Z - 0.25 < thin.ZMax;
                Assert.IsFalse(overlapsAtEnd, "fixture: no tick ends inside the block (tick " + tick + ")");
            }

            h.RunThrough(20);
            Assert.IsTrue(h.State.IsDead);
            AssertHit(SingleDeath(h), 15, ObstacleArchetype.FullBlock, 11, false);
        }

        // ---- AC-43 ----

        [Test]
        public void AC43_Invulnerable_ObstaclesHarmless_GapStillKills()
        {
            var track = new TestTrackQuery()
                .AddBox(ObstacleFixtures.FullBlock(1, 1, 5.0))
                .AddBox(ObstacleFixtures.HighBarrier(2, 1, 10.0))
                .AddBox(ObstacleFixtures.LowBarrier(3, 1, 15.0))
                .AddGap(30.0, 300.0);
            RunnerTestHarness h = Harness(10.0, track);
            h.Sim.SetInvulnerableTicks(10000);

            for (int i = 0; i < 400 && !h.State.IsDead; i++)
            {
                h.Step();
            }

            Assert.IsTrue(h.State.IsDead);
            RunnerEvent died = SingleDeath(h);
            Assert.AreEqual((short)DeathCause.Fell, died.Value, "the gap kills even while invulnerable");
            Assert.Greater(h.State.InvulnerableTicks, 0);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.NearMiss), "no near-miss while invulnerable");
            Assert.AreEqual(0, h.Sim.DazeTicksLeft);
        }

        [Test]
        public void AC43_InvulnerabilityRunsOut_ObstaclesHitAgain()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(1, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.Sim.SetInvulnerableTicks(10);
            h.RunThrough(9);
            Assert.AreEqual(0, h.State.InvulnerableTicks);
            h.RunThrough(40);
            AssertHit(SingleDeath(h), 28, ObstacleArchetype.FullBlock, 1, false);
        }

        // ---- AC-44 ----

        /// <summary>
        /// HERO 0.5 m wide (exact binary bounds) runs straight at 6 m/s in lane 1. A mover box slides in from the
        /// right while HERO runs into its front. The fixture reports Prev bounds directly so the entry times are
        /// exact: z enters at t = 0.5 and x at <paramref name="xEnterTime"/>.
        /// </summary>
        private static RunnerTestHarness MoverEntryRace(double xEnterTime)
        {
            RunnerDesignValues v = RunnerDesignValues.CreateDefault();
            v.PlayerHitboxWidthM = 0.5f;
            var track = new TestTrackQuery();
            RunnerTestHarness h = Harness(6.0, track, v);
            h.RunThrough(9);

            double perTick = 6.0 * RunnerConfig.TickSeconds;
            double zFront = h.State.Z + 0.25 + 0.5 * perTick;
            const float travel = 0.125f;
            float xMinNow = (float)(0.25 - travel * (1.0 - xEnterTime));
            var box = new ObstacleBox
            {
                Id = 1,
                Archetype = ObstacleArchetype.Mover,
                Lane = 2,
                XMinPrev = xMinNow + travel,
                XMaxPrev = xMinNow + travel + 1.9f,
                XMin = xMinNow,
                XMax = xMinNow + 1.9f,
                YMin = 0f,
                YMax = 1.9f,
                ZMin = zFront,
                ZMax = zFront + 1.6,
            };
            track.AddBox(box);
            h.Step(); // tick 10
            return h;
        }

        [Test]
        public void AC44_ExactTieBetweenXAndZ_IsAStumble()
        {
            RunnerTestHarness h = MoverEntryRace(0.5);
            Assert.IsFalse(h.State.IsDead, "ties go to the player");
            List<RunnerEvent> stumbles = h.EventsOf(RunnerEventType.Stumbled);
            Assert.AreEqual(1, stumbles.Count);
            Assert.IsTrue(stumbles[0].HasFlag(RunnerEventFlags.Side));
            Assert.AreEqual(10L, stumbles[0].Tick);
        }

        [Test]
        public void AC44_Control_XFirstThenZ_IsAFrontHit()
        {
            RunnerTestHarness h = MoverEntryRace(0.25);
            Assert.IsTrue(h.State.IsDead);
            AssertHit(SingleDeath(h), 10, ObstacleArchetype.Mover, 1, false);
        }

        // ---- Spec 002 AC-209 / table 5.2: mover pushing into HERO from the side ----

        private sealed class ScriptedMoverHooks : IRunnerStepHooks
        {
            private readonly TestTrackQuery _track;

            public ScriptedMoverHooks(TestTrackQuery track)
            {
                _track = track;
            }

            public void OnTrackUpdate(RunnerSimulation runner, in RunnerTickInfo info)
            {
                if (info.Tick < 10)
                {
                    _track.SettleBoxes();
                    return;
                }

                float center = 2.4f - 0.08f * (info.Tick - 9);
                _track.MoveBox(1, center < 0f ? 0f : center);
            }

            public void OnCoinPickups(RunnerSimulation runner, in RunnerTickInfo info)
            {
            }

            public void OnScore(RunnerSimulation runner, in RunnerTickInfo info)
            {
            }
        }

        [Test]
        public void SideContactFromMoverIntoStandingHero_IsAStumbleNotADeath()
        {
            // 6 m/s. Mover (z 1.0 to 2.6 m) starts in lane 2 and slides left 0.08 m per tick from tick 10. HERO is
            // alongside it from tick 7; its left side passes HERO's right side (0.35 m) on tick 23.
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.Mover(1, 2, 1.0));
            RunnerTestHarness h = Harness(6.0, track);
            h.Sim.StepHooks = new ScriptedMoverHooks(track);

            h.RunThrough(40);

            Assert.IsFalse(h.State.IsDead);
            List<RunnerEvent> stumbles = h.EventsOf(RunnerEventType.Stumbled);
            Assert.AreEqual(1, stumbles.Count);
            Assert.AreEqual(23L, stumbles[0].Tick);
            Assert.IsTrue(stumbles[0].HasFlag(RunnerEventFlags.Side));
            Assert.AreEqual((byte)ObstacleArchetype.Mover, stumbles[0].Archetype);
            Assert.AreEqual(0, stumbles[0].Dir, "no lane move to cancel, so no bounce");
            Assert.AreEqual(0f, h.State.X);
        }

        [Test]
        public void SideContactOnEveryArchetype_IsAStumble()
        {
            ObstacleArchetype[] kinds =
            {
                ObstacleArchetype.LowBarrier,
                ObstacleArchetype.HighBarrier,
                ObstacleArchetype.FullBlock,
                ObstacleArchetype.Mover,
            };

            foreach (ObstacleArchetype kind in kinds)
            {
                ObstacleBox box;
                switch (kind)
                {
                    case ObstacleArchetype.LowBarrier:
                        // Long low barrier so HERO is alongside it when the lane move arrives.
                        box = ObstacleFixtures.Box(5, kind, 2, ObstacleFixtures.BarrierWidthM, 0f, 0.8f, 2.0, 2.0);
                        break;
                    case ObstacleArchetype.HighBarrier:
                        box = ObstacleFixtures.Box(5, kind, 2, ObstacleFixtures.BarrierWidthM, 1.1f, 3.0f, 2.0, 2.0);
                        break;
                    case ObstacleArchetype.Mover:
                        box = ObstacleFixtures.Mover(5, 2, 2.0);
                        break;
                    default:
                        box = ObstacleFixtures.FullBlock(5, 2, 2.0);
                        break;
                }

                RunnerTestHarness h = SideStumbleSetup(new TestTrackQuery().AddBox(box));
                Assert.IsFalse(h.State.IsDead, kind.ToString());
                List<RunnerEvent> stumbles = h.EventsOf(RunnerEventType.Stumbled);
                Assert.AreEqual(1, stumbles.Count, kind.ToString());
                Assert.IsTrue(stumbles[0].HasFlag(RunnerEventFlags.Side), kind.ToString());
                Assert.AreEqual((byte)kind, stumbles[0].Archetype, kind.ToString());
            }
        }

        [Test]
        public void FrontContactOnMover_IsLethal()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.Mover(8, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.RunThrough(40);
            AssertHit(SingleDeath(h), 28, ObstacleArchetype.Mover, 8, false);
        }

        [Test]
        public void TwoLaneBlock_BothBoxesShareOneId_OneDeathEvent()
        {
            // A 2-lane full block is two boxes with one id; HERO in lane 1 hits one of them.
            var track = new TestTrackQuery()
                .AddBox(ObstacleFixtures.FullBlock(4, 1, 5.0))
                .AddBox(ObstacleFixtures.FullBlock(4, 2, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.RunThrough(40);
            AssertHit(SingleDeath(h), 28, ObstacleArchetype.FullBlock, 4, false);
        }

        [Test]
        public void ObstacleInAnotherLane_NoContactNoNearMiss()
        {
            var track = new TestTrackQuery()
                .AddBox(ObstacleFixtures.FullBlock(1, 0, 5.0))
                .AddBox(ObstacleFixtures.FullBlock(2, 2, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.RunThrough(60);
            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.NearMiss), "1.03 m to each side is not a near-miss");
        }

        // ---- Spec 001 6.4.5: slide extends under a real high barrier ----

        [Test]
        public void SlideTimerEndsUnderHighBarrier_SlideExtendsUntilClear()
        {
            // 10 m/s, high barrier from 3.0 to 13.0 m. The slide timer runs out on tick 39 (z = 6.67 m) while
            // under it; HERO's back clears 13.0 m on tick 79, which is when it stands up.
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.HighBarrier(1, 1, 3.0, 10.0));
            RunnerTestHarness h = Harness(10.0, track);

            h.Step(InputCommand.Slide);
            h.RunThrough(78);
            Assert.AreEqual(Locomotion.Sliding, h.State.Locomotion);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.SlideEnded));

            h.Step(); // tick 79
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
            List<RunnerEvent> ended = h.EventsOf(RunnerEventType.SlideEnded);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(79L, ended[0].Tick);
            Assert.AreEqual((short)SlideEndReason.Timeout, ended[0].Value);

            h.RunThrough(100);
            Assert.IsFalse(h.State.IsDead);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Stumbled));
        }

        // ---- AC-45 (daze part) ----

        [Test]
        public void AC45_DazeCounterContinuesThroughPauseResumed()
        {
            RunnerTestHarness paused = SideStumbleSetup(new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(5, 2, 2.0)));
            RunnerTestHarness reference = SideStumbleSetup(new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(5, 2, 2.0)));
            Assert.AreEqual(180, paused.State.DazeTicksLeft);

            paused.Step(); // 22
            reference.Step();
            paused.Step(InputCommand.PauseResumed); // 23, mid-bounce
            reference.Step();
            Assert.AreEqual(178, paused.State.DazeTicksLeft);
            Assert.IsTrue(paused.Sim.IsBouncing, "the bounce continues too");

            for (int i = 0; i < 200; i++)
            {
                paused.Step();
                reference.Step();
                Assert.AreEqual(reference.Sim.ComputeStateHash(), paused.Sim.ComputeStateHash(), "tick " + paused.LastTick);
            }

            Assert.AreEqual(1, paused.CountOf(RunnerEventType.DazeEnded));
            Assert.AreEqual(21L + 180L, paused.EventsOf(RunnerEventType.DazeEnded)[0].Tick);
        }

        // ---- Commands after death ----

        [Test]
        public void AfterHitDeath_CommandsIgnored_StateFrozen()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(7, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            h.RunThrough(28);
            ulong frozenZ = (ulong)System.BitConverter.DoubleToInt64Bits(h.State.Z);

            h.Step(InputCommand.MoveLeft | InputCommand.Jump);
            Assert.AreEqual(CommandOutcome.Ignored, h.Sim.LastOutcomes.MoveLeft);
            Assert.AreEqual(CommandOutcome.Ignored, h.Sim.LastOutcomes.Jump);
            Assert.AreEqual(frozenZ, (ulong)System.BitConverter.DoubleToInt64Bits(h.State.Z));
            Assert.AreEqual(1, h.CountOf(RunnerEventType.Died));
        }
    }
}
