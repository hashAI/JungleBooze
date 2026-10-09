using System;
using JungleBooze.Core;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Traversal
{
    /// <summary>Swimming (spec 103 §4, AC-103-01…14 and F2/F3).</summary>
    public sealed class SwimTests
    {
        private const float Speed = 12f;
        private const float SwimSpeed = 8.4f;

        private static float Line(TraversalRig rig) => rig.State.WaterY + rig.Sim.Config.Swim.SwimLineHeight;

        [Test]
        public void AC103_01_EnterAt09_ExitBelow06_NoFlickerBetween()
        {
            var b = new ChunkLayoutBuilder("Ford", 200f);
            b.Width(0f, -4.5f, 4.5f).Width(200f, -4.5f, 4.5f).Water(0f, 200f, -4.5f, 4.5f)
                .Floor(20f, 40f, 0f, -1.0f);
            for (float s = 40f; s < 120f; s += 10f)
            {
                // Depth oscillates between 0.65 and 0.85 m: inside the hysteresis band.
                b.Floor(s, s + 5f, -0.85f, -0.85f).Floor(s + 5f, s + 10f, -0.65f, -0.65f);
            }

            b.Floor(120f, 130f, -0.65f, -0.3f).Floor(130f, 200f, -0.3f, -0.3f);
            TraversalRig rig = TraversalRig.Create(b, 200f, Speed);
            float enteredAt = -1f;
            float exitedAt = -1f;
            int modeChanges = 0;
            MoveMode last = rig.State.Mode;
            while (rig.State.S < 180f)
            {
                rig.Step();
                if (rig.State.Mode != last)
                {
                    modeChanges++;
                    last = rig.State.Mode;
                    if (last == MoveMode.Swim)
                    {
                        enteredAt = rig.State.S;
                    }
                    else
                    {
                        exitedAt = rig.State.S;
                    }
                }
            }

            Assert.AreEqual(2, modeChanges, "enter once, exit once (no flicker between 0.6 and 0.9 m)");
            Assert.AreEqual(38f, enteredAt, 0.5f, "depth reaches 0.9 m at s 38");
            Assert.Greater(exitedAt, 120f);
            Assert.Less(exitedAt, 124f, "depth falls below 0.6 m just after s 121");
            Assert.AreEqual(1, rig.Count(RunEventType.WaterEnter));
            Assert.AreEqual(1, rig.Count(RunEventType.WaterExit));
        }

        [Test]
        public void AC103_02_SwimSpeed_BlendsIn15Ticks_IncludesForwardCurrent_BlendsOutIn18()
        {
            var b = new ChunkLayoutBuilder("Ramp", 300f);
            b.Width(0f, -4.5f, 4.5f).Width(300f, -4.5f, 4.5f).Water(0f, 300f, -4.5f, 4.5f)
                .Floor(20f, 22f, 0f, -1.8f).Floor(22f, 200f, -1.8f, -1.8f).Floor(200f, 202f, -1.8f, 0f)
                .Current(0f, 300f, 0f, 2.0f);
            TraversalRig rig = TraversalRig.Create(b, 300f, Speed);
            Assert.IsTrue(rig.StepUntil(st => st.Mode == MoveMode.Swim));
            long entry = rig.State.Tick;
            rig.Steps(15);
            Assert.AreEqual(entry + 15, rig.State.Tick);
            Assert.AreEqual(SwimSpeed + 2.0f, rig.State.Speed, 1e-3f, "0.70 × v(d) + forward current");
            Assert.IsTrue(rig.StepUntil(st => st.Mode == MoveMode.Run));
            long exit = rig.State.Tick;
            rig.Steps(18);
            Assert.AreEqual(exit + 18, rig.State.Tick);
            Assert.AreEqual(Speed, rig.State.Speed, 1e-3f, "back to v(d) within 18 ticks");
        }

        [Test]
        public void AC103_03_F2_SwimLateralStep()
        {
            TraversalRig rig = TraversalRig.Create(TraversalRig.Pool(), 400f, Speed, startX: -1f);
            rig.Steps(30);
            Assert.AreEqual(MoveMode.Swim, rig.State.Mode);
            float x0 = rig.State.X;
            rig.Step(InputCommand.None, TraversalRig.Mm(2f));
            rig.Steps(3);
            Assert.GreaterOrEqual(rig.State.X - x0, 0.05f, "≥ 5 cm within 4 ticks");
            float max = rig.State.X;
            for (int i = 4; i < 36; i++)
            {
                rig.Step();
                max = Math.Max(max, rig.State.X);
            }

            Assert.AreEqual(x0 + 2f, rig.State.X, 0.10f, "within 0.10 m by 0.60 s");
            for (int i = 0; i < 60; i++)
            {
                rig.Step();
                max = Math.Max(max, rig.State.X);
            }

            Assert.LessOrEqual(max - (x0 + 2f), 0.03f, "overshoot ≤ 0.03 m");
        }

        [Test]
        public void AC103_04_LateralCurrent_DriftsTheTarget_DragHoldsPosition()
        {
            ChunkLayoutBuilder b = TraversalRig.Pool().Current(0f, 400f, 1.5f, 0f);
            TraversalRig rig = TraversalRig.Create(b, 400f, Speed, startX: -3f);
            rig.Steps(2);
            float xt = rig.State.XTarget;
            rig.Steps(60);
            Assert.AreEqual(xt + 1.5f, rig.State.XTarget, 0.01f, "1.5 m per second");

            TraversalRig hold = TraversalRig.Create(b, 400f, Speed, startX: -1f);
            float x = hold.State.X;
            float worst = 0f;
            for (int i = 0; i < 150; i++)
            {
                hold.Step(InputCommand.None, -25);
                worst = Math.Max(worst, Math.Abs(hold.State.X - x));
            }

            Assert.LessOrEqual(worst, 0.05f, "a drag of −1.5 m/s holds x");
        }

        [Test]
        public void AC103_05_F3_DivePhases_SubmergedExactlyUnder()
        {
            TraversalRig rig = TraversalRig.Create(TraversalRig.Pool(), 400f, Speed);
            rig.Steps(20);
            float line = Line(rig);
            float yBefore = rig.State.Y;
            rig.Step(InputCommand.Slide);
            long t0 = rig.State.Tick;
            Assert.AreEqual(DivePhase.Down, rig.State.Dive);
            Assert.Less(rig.State.Y, yBefore, "moves on the swipe tick (0 added ticks)");
            Assert.IsTrue(rig.Has(RunEventType.Dive, t0));
            int down = 1;
            int under = 0;
            int up = 0;
            for (int i = 0; i < 80 && rig.State.Dive != DivePhase.None; i++)
            {
                rig.Step();
                switch (rig.State.Dive)
                {
                    case DivePhase.Down:
                        down++;
                        Assert.IsFalse(rig.State.Submerged);
                        break;
                    case DivePhase.Under:
                        under++;
                        Assert.IsTrue(rig.State.Submerged);
                        Assert.AreEqual(line - 1.10f, rig.State.Y, 1e-4f);
                        break;
                    case DivePhase.Up:
                        up++;
                        Assert.IsFalse(rig.State.Submerged);
                        break;
                }
            }

            Assert.AreEqual(6, down);
            Assert.AreEqual(33, under);
            Assert.AreEqual(12, up);
            Assert.AreEqual(line, rig.State.Y, 1e-4f);
        }

        private static long ContactTick(ChunkLayoutBuilder layout)
        {
            TraversalRig probe = TraversalRig.Create(layout, 400f, Speed);
            probe.StepUntil(st => st.Hits > 0, 2000);
            return probe.State.Tick;
        }

        private static int HitsWith(ChunkLayoutBuilder layout, long actionTick, InputCommand command)
        {
            TraversalRig rig = TraversalRig.Create(layout, 400f, Speed);
            while (rig.State.Tick < 1500)
            {
                rig.Step(rig.State.Tick + 1 == actionTick ? command : InputCommand.None);
            }

            return rig.State.Hits;
        }

        private static int CleanWindow(ChunkLayoutBuilder layout, long contact, InputCommand command)
        {
            int clean = 0;
            for (long t = contact - 50; t < contact; t++)
            {
                clean += HitsWith(layout, t, command) == 0 ? 1 : 0;
            }

            return clean;
        }

        [Test]
        public void AC103_06_FloatingLog_DiveWindow_LeapWindow_SurfaceBumps()
        {
            ChunkLayoutBuilder layout = TraversalRig.Pool().FloatingLog(100f);
            long contact = ContactTick(layout);
            TraversalRig surface = TraversalRig.Create(layout, 400f, Speed);
            surface.StepUntil(st => st.S > 110f);
            Assert.AreEqual(1, surface.State.Hits, "staying on the surface: one Bump");
            Assert.AreEqual((byte)HitKind.Bump, surface.Last(RunEventType.Hit).Reason);

            for (int lead = 6; lead <= 21; lead++)
            {
                Assert.AreEqual(0, HitsWith(layout, contact - lead, InputCommand.Slide), "dive " + lead + " ticks (" + (lead / 60f).ToString("0.00") + " s) before contact");
            }

            Assert.GreaterOrEqual(CleanWindow(layout, contact, InputCommand.Jump), 3, "a matching leap window exists");
        }

        [Test]
        public void AC103_07_Snag_LeapPassesDiveHits_LowBranch_DivePassesLeapHits()
        {
            ChunkLayoutBuilder snag = TraversalRig.Pool().Snag(100f);
            long contact = ContactTick(snag);
            Assert.GreaterOrEqual(CleanWindow(snag, contact, InputCommand.Jump), 3, "a leap clears a Snag");
            Assert.AreEqual(0, CleanWindow(snag, contact, InputCommand.Slide), "a dive never clears a Snag");

            ChunkLayoutBuilder branch = TraversalRig.Pool().LowBranch(100f);
            contact = ContactTick(branch);
            Assert.AreEqual(0, HitsWith(branch, contact - 12, InputCommand.Slide), "a dive passes under a LowBranch");
            int apexTicks = 17;
            Assert.Greater(HitsWith(branch, contact - apexTicks, InputCommand.Jump), 0, "a leap at apex hits a LowBranch");
            Assert.AreEqual(0, CleanWindow(branch, contact, InputCommand.Jump), "no leap timing clears a LowBranch");
        }

        [Test]
        public void AC103_08_LeapApexAndAirtime_SameAtEverySwimSpeed()
        {
            foreach (float speed in new[] { 8f, 12f, 16f })
            {
                TraversalRig rig = TraversalRig.Create(TraversalRig.Pool(), 400f, speed);
                rig.Steps(20);
                float line = Line(rig);
                rig.Step(InputCommand.Jump);
                long start = rig.State.Tick;
                Assert.IsTrue(rig.Has(RunEventType.Leap, start), "leap fires on the swipe tick");
                float apex = rig.State.Y;
                while (rig.State.Leaping)
                {
                    rig.Step();
                    apex = Math.Max(apex, rig.State.Y);
                }

                float airtime = (rig.State.Tick - start) / 60f;
                Assert.AreEqual(1.20f, apex - line, 0.03f, "apex at " + speed);
                Assert.AreEqual(0.55f, airtime, 0.03f, "airtime at " + speed);
                Assert.AreEqual(1, rig.Count(RunEventType.Splash));
            }
        }

        [Test]
        public void AC103_09_LeapCancelsDive_RisesTwiceAsFast_HeldUnderALog()
        {
            TraversalRig rig = TraversalRig.Create(TraversalRig.Pool(), 400f, Speed);
            rig.Steps(20);
            rig.Step(InputCommand.Slide);
            rig.Steps(10);
            Assert.AreEqual(DivePhase.Under, rig.State.Dive);
            rig.Step(InputCommand.Jump);
            Assert.AreEqual(DivePhase.Up, rig.State.Dive);
            int upTicks = 1;
            while (rig.State.Dive == DivePhase.Up)
            {
                rig.Step();
                upTicks += rig.State.Dive == DivePhase.Up ? 1 : 0;
            }

            Assert.AreEqual(6, upTicks, "Up at 2× (12 → 6 ticks)");
            Assert.IsTrue(rig.State.Leaping, "leaps on surfacing");

            // Under a long FloatingLog the swipe is held for 21 ticks, then dropped (Ceiling); she stays under.
            var b = TraversalRig.Pool();
            b.Variant.Obstacles.Add(new Gameplay.Course.CourseObstacle { Class = ObstacleClass.FloatingLog, SMin = 60f, SMax = 90f, XMin = -4.5f, XMax = 4.5f, YMin = -0.4f, YMax = 0.4f, Label = "Long log" });
            TraversalRig held = TraversalRig.Create(b, 400f, Speed);
            held.StepUntil(st => st.S > 57f);
            held.Step(InputCommand.Slide);
            held.StepUntil(st => st.S > 66f);
            Assert.IsTrue(held.State.Submerged);
            held.Step(InputCommand.Jump);
            long pressed = held.State.Tick;
            held.StepUntil(st => st.DroppedInputs > 0, 60);
            Assert.AreEqual(1, held.State.DroppedInputs);
            Assert.LessOrEqual(held.State.Tick - pressed, 21);
            Assert.AreEqual((byte)DropReason.Ceiling, held.Last(RunEventType.InputDropped).Reason);
            Assert.IsTrue(held.State.Submerged, "still under the log");
            held.StepUntil(st => st.S > 100f);
            Assert.AreEqual(0, held.State.Hits);
        }

        [Test]
        public void AC103_10_SwipeDownWhileLeaping_DivesIn_AndDivesOnSplashDown()
        {
            TraversalRig rig = TraversalRig.Create(TraversalRig.Pool(), 400f, Speed);
            rig.Steps(20);
            rig.Step(InputCommand.Jump);
            rig.Steps(12);
            rig.Step(InputCommand.Slide);
            Assert.LessOrEqual(rig.State.Vy, -14f);
            rig.StepUntil(st => !st.Leaping, 60);
            Assert.AreEqual(DivePhase.Down, rig.State.Dive, "a full dive starts on splash-down");
            rig.Step();
            Assert.AreEqual(DivePhase.Down, rig.State.Dive);
            rig.StepUntil(st => st.Dive == DivePhase.Under, 20);
            Assert.IsTrue(rig.State.Submerged);
        }

        private static ChunkLayoutBuilder Bank()
        {
            // Land until 30, then a 1.8 m drop into deep water.
            var b = new ChunkLayoutBuilder("Bank", 300f);
            b.Width(0f, -4.5f, 4.5f).Width(300f, -4.5f, 4.5f).Water(30f, 300f, -4.5f, 4.5f).Floor(30f, 300f, -1.8f, -1.8f);
            return b;
        }

        [Test]
        public void AC103_11_EntryTick_BufferedJumpLeaps_FastFallDives_SlideEnds()
        {
            TraversalRig jump = TraversalRig.Create(Bank(), 300f, Speed);
            jump.StepUntil(st => st.S > 26f);
            jump.Step(InputCommand.Jump);
            jump.StepUntil(st => st.Vy < 0f && st.Y < 0.5f, 120);
            jump.Step(InputCommand.Jump);
            Assert.AreEqual(InputCommand.Jump, jump.State.Buffered, "held in the air");
            jump.StepUntil(st => st.Mode == MoveMode.Swim, 30);
            long entry = jump.State.Tick;
            Assert.IsTrue(jump.Has(RunEventType.WaterEnter, entry));
            Assert.IsTrue(jump.Has(RunEventType.Leap, entry), "a buffered jump becomes a leap on the entry tick");

            TraversalRig fast = TraversalRig.Create(Bank(), 300f, Speed);
            fast.StepUntil(st => st.S > 28f);
            fast.Step(InputCommand.Jump);
            fast.StepUntil(st => st.S > 31.5f);
            fast.Step(InputCommand.Slide);
            Assert.IsTrue(fast.State.FastFalling);
            fast.StepUntil(st => st.Mode == MoveMode.Swim, 60);
            fast.Step();
            Assert.AreEqual(DivePhase.Down, fast.State.Dive, "a fast-fall becomes a dive");

            var wade = new ChunkLayoutBuilder("Wade", 300f);
            wade.Width(0f, -4.5f, 4.5f).Width(300f, -4.5f, 4.5f).Water(0f, 300f, -4.5f, 4.5f).Floor(20f, 26f, 0f, -1.2f).Floor(26f, 300f, -1.2f, -1.2f);
            TraversalRig slide = TraversalRig.Create(wade, 300f, Speed);
            slide.StepUntil(st => st.S > 21f);
            slide.Step(InputCommand.Slide);
            Assert.IsTrue(slide.State.Sliding);
            slide.StepUntil(st => st.Mode == MoveMode.Swim, 60);
            Assert.IsFalse(slide.State.Sliding, "a slide in progress ends on the entry tick");
            Assert.IsTrue(slide.Has(RunEventType.SlideEnd, slide.State.Tick));
        }

        [Test]
        public void AC103_12_WaterNeverCrashesOrFalls_RockBumpsAndPushes()
        {
            ChunkLayoutBuilder b = TraversalRig.Pool().Rock(80f, 1.4f, 0.3f);
            b.Variant.Obstacles.Add(new Gameplay.Course.CourseObstacle { Class = ObstacleClass.Blocker, SMin = 150f, SMax = 150.6f, XMin = -4.5f, XMax = 4.5f, YMin = -1.8f, YMax = 2.5f, Label = "Wall in water" });
            TraversalRig rig = TraversalRig.Create(b, 400f, Speed);
            rig.StepUntil(st => st.Hits > 0, 1000);
            Assert.AreEqual((byte)HitKind.Bump, rig.Last(RunEventType.Hit).Reason);
            Assert.IsTrue(rig.State.XTarget > 1.0f || rig.State.XTarget < -0.4f, "pushed to the near free side");
            rig.StepUntil(st => st.S > 170f || st.Dead);
            Assert.IsFalse(rig.State.Dead);
            for (int i = 0; i < rig.Events.Count; i++)
            {
                if (rig.Events[i].Type == RunEventType.Hit)
                {
                    Assert.AreEqual((byte)HitKind.Bump, rig.Events[i].Reason, "never Crash in water");
                }
            }

            // At health 1 a Bump ends the run with cause Health (never Crash/Fall).
            TraversalRig last = TraversalRig.Create(TraversalRig.Pool().FloatingLog(60f).FloatingLog(90f).FloatingLog(120f), 400f, Speed);
            last.StepUntil(st => st.Dead, 2000);
            Assert.IsTrue(last.State.Dead);
            Assert.AreEqual(DeathCause.Health, last.State.Cause);
        }

        private static ChunkLayoutBuilder Arch()
        {
            ChunkLayoutBuilder b = TraversalRig.Pool();
            b.DeepDive(100f, 110f, -3.5f, -0.5f, 150f, -2f, "test arch")
                .Rock(125f, 1.4f, -2f)
                .Snag(135f);
            return b;
        }

        [Test]
        public void AC103_13_DeepBreath_FollowsThePassage144Ticks_IgnoresInputAndObstacles()
        {
            TraversalRig without = TraversalRig.Create(Arch(), 400f, Speed, startX: -2f);
            without.StepUntil(st => st.S > 104f);
            without.Step(InputCommand.Slide);
            Assert.AreEqual(MoveMode.Swim, without.State.Mode);
            Assert.AreEqual(DivePhase.Down, without.State.Dive, "without Deep Breath: a normal dive");

            TraversalRig with = TraversalRig.Create(Arch(), 400f, Speed, startX: -2f, deepBreath: true);
            with.StepUntil(st => st.S > 104f);
            with.Step(InputCommand.Slide);
            Assert.AreEqual(MoveMode.DeepDive, with.State.Mode);
            long start = with.State.Tick;
            int ticks = 1;
            while (with.State.Mode == MoveMode.DeepDive)
            {
                with.Step(InputCommand.DodgeRight, TraversalRig.Mm(0.5f));
                ticks++;
            }

            Assert.AreEqual(144, ticks, "the passage lasts 144 ticks");
            Assert.AreEqual(150f, with.State.S, 1e-3f, "surfaces at the exit s");
            Assert.AreEqual(-2f, with.State.X, 1e-3f, "and the exit x (input ignored)");
            Assert.AreEqual(MoveMode.Swim, with.State.Mode);
            Assert.AreEqual(0, with.State.Hits, "invulnerable to the rock and the snag on the way");
            Assert.IsTrue(with.Has(RunEventType.DeepDiveEnd, start + 143));
        }

        [Test]
        public void AC103_14_UnderwaterCoins_OnlyWhileSubmerged()
        {
            ChunkLayoutBuilder b = TraversalRig.Pool().Line(100f, 110f, 0f, 1.5f, -1.1f);
            TraversalRig surface = TraversalRig.Create(b, 400f, Speed);
            surface.StepUntil(st => st.S > 120f);
            Assert.AreEqual(0, surface.State.Coins, "surface swimmer passes over them");

            TraversalRig diver = TraversalRig.Create(b, 400f, Speed);
            diver.StepUntil(st => st.S > 97.5f);
            diver.Step(InputCommand.Slide);
            int coins = 0;
            while (diver.State.S < 120f)
            {
                // Swipe down again while Under restarts the Under timer (stays down across the line).
                diver.Step(diver.State.Dive == DivePhase.Under && diver.State.S < 108f && diver.State.Tick % 20 == 0 ? InputCommand.Slide : InputCommand.None);
                if (diver.State.Coins > coins)
                {
                    Assert.IsTrue(diver.State.Submerged, "collected only while Submerged");
                    coins = diver.State.Coins;
                }
            }

            Assert.Greater(coins, 4);
        }

        [Test]
        public void AC103_35_CoinPads_RunSlideSwim()
        {
            // Run: x pad = half width 0.25 + 0.35; y pad 0.25 below the feet.
            AssertPad(new ChunkLayoutBuilder("Flat", 200f).Point(50f, 0.59f, 0.9f), 1, "run, x inside the pad");
            AssertPad(new ChunkLayoutBuilder("Flat", 200f).Point(50f, 0.62f, 0.9f), 0, "run, x outside the pad");
            AssertPad(new ChunkLayoutBuilder("Flat", 200f).Point(50f, 0f, 1.72f), 1, "run, 0.22 above the 1.5 m box");
            AssertPad(new ChunkLayoutBuilder("Flat", 200f).Point(50f, 0f, 1.78f), 0, "run, 0.28 above the box");

            // Swim: box 0.6 wide centred on the body line (0.35 above the surface), height 0.7.
            AssertPad(TraversalRig.Pool().Point(50f, 0.64f, 0.4f), 1, "swim, x inside");
            AssertPad(TraversalRig.Pool().Point(50f, 0.67f, 0.4f), 0, "swim, x outside");
            AssertPad(TraversalRig.Pool().Point(50f, 0f, 0.92f), 1, "swim, 0.22 above the box top 0.70");
            AssertPad(TraversalRig.Pool().Point(50f, 0f, 0.98f), 0, "swim, 0.28 above");
        }

        private static void AssertPad(ChunkLayoutBuilder b, int expected, string what)
        {
            TraversalRig rig = TraversalRig.Create(b, 200f, Speed);
            rig.StepUntil(st => st.S > 60f);
            Assert.AreEqual(expected, rig.State.Coins, what);
        }
    }
}
