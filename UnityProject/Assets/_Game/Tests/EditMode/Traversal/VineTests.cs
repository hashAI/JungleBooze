using System;
using JungleBooze.Core;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using JungleBooze.Tests.EditMode.World;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Traversal
{
    /// <summary>Vine swing (spec 103 §5, AC-103-15…24, F5/F7).</summary>
    public sealed class VineTests
    {
        private const float Speed = 12f;

        private static ChunkLayoutBuilder Gorge(bool wallAfterLanding = false)
        {
            var b = new ChunkLayoutBuilder("Vine", 200f);
            b.Width(0f, -3.5f, 3.5f).Width(60f, -1.2f, 1.2f).Width(150f, -1.2f, 1.2f).Width(170f, -3.5f, 3.5f)
                .Gap(96f, 104f)
                .Vine(99f, 0f, 96f, 104f, 108.6f, 109.6f, false);
            if (wallAfterLanding)
            {
                b.BlkSpan(113f, -1.2f, 1.2f, "Wall after the landing");
            }

            return b;
        }

        private static TraversalRig Rig(float startX = 0f, bool wall = false)
        {
            return TraversalRig.Create(Gorge(wall), 200f, Speed, 80f, startX);
        }

        /// <summary>Runs to the grab, then swipes up on swing tick <paramref name="swipeTick"/> (−1 = never). Returns the release event.</summary>
        private static RunEvent ReleaseWith(int swipeTick, out TraversalRig rig)
        {
            rig = Rig();
            rig.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            for (int i = 0; i < 80 && rig.State.Mode == MoveMode.Swing; i++)
            {
                rig.Step(rig.Sim.SwingTick + 1 == swipeTick ? InputCommand.Jump : InputCommand.None);
            }

            return rig.Last(RunEventType.VineRelease);
        }

        [Test]
        public void AC103_15_GrabsOnTheFirstTickInTheZone_FromRunningOrAir_ClearsBuffered()
        {
            TraversalRig rig = Rig();
            float before = rig.State.S;
            while (rig.State.Mode == MoveMode.Run)
            {
                before = rig.State.S;
                rig.Step();
            }

            Assert.AreEqual(MoveMode.Swing, rig.State.Mode);
            Assert.Less(before, 94.5f, "not in the zone on the tick before");
            Assert.GreaterOrEqual(rig.State.S, 94.5f, "grab on the first tick with s ≥ lip − 1.5");
            Assert.IsTrue(rig.Has(RunEventType.VineGrab, rig.State.Tick));

            // Jump into the zone with a swipe buffered in the air: the grab clears it (no early release).
            TraversalRig air = Rig();
            air.StepUntil(st => st.S >= 90f);
            air.Step(InputCommand.Jump);
            air.StepUntil(st => st.S >= 93.8f);
            air.Step(InputCommand.Jump);
            Assert.AreEqual(InputCommand.Jump, air.State.Buffered);
            air.StepUntil(st => st.Mode == MoveMode.Swing, 120);
            Assert.IsFalse(air.State.Grounded, "grabbed while airborne");
            Assert.AreEqual(InputCommand.None, air.State.Buffered, "buffered command cleared at the grab");
            air.StepUntil(st => st.Mode != MoveMode.Swing, 120);
            Assert.AreEqual(60f, air.Last(RunEventType.VineRelease).Value, "auto-release: the cleared swipe never released it");
        }

        [Test]
        public void AC103_16_F5_EveryReachableXOnTheFunnelGrabs()
        {
            for (float x = -0.9f; x <= 0.901f; x += 0.1f)
            {
                TraversalRig rig = Rig(x);
                Assert.IsTrue(rig.StepUntil(st => st.Mode == MoveMode.Swing, 600), "x " + x);
            }
        }

        [Test]
        public void AC103_17_ThetaMatchesTheFormula_SwingLasts60Ticks()
        {
            TraversalRig rig = Rig();
            float[] p = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            foreach (float phase in p)
            {
                double expected = 10.0 - (45.0 * Math.Cos(Math.PI * phase));
                Assert.AreEqual(expected, rig.Sim.SwingThetaDeg((int)Math.Round(phase * 60)), 0.1, "p " + phase);
            }

            rig.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            long grab = rig.State.Tick;
            rig.StepUntil(st => st.Mode != MoveMode.Swing, 120);
            Assert.AreEqual(grab + 60, rig.State.Tick, "released on swing tick 60");
            Assert.AreEqual(60f, rig.Last(RunEventType.VineRelease).Value);
        }

        [Test]
        public void AC103_18_LateralInputIsDiscarded()
        {
            TraversalRig control = Rig();
            TraversalRig steered = Rig();
            control.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            steered.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            for (int i = 0; i < 70; i++)
            {
                bool swinging = steered.State.Mode == MoveMode.Swing;
                control.Step();
                steered.Step(InputCommand.None, swinging ? TraversalRig.Mm(i % 2 == 0 ? 0.8f : -0.3f) : (short)0);
                Assert.AreEqual(control.State.X, steered.State.X, 1e-5f);
                Assert.AreEqual(control.State.XTarget, steered.State.XTarget, 1e-5f, "the delta is not stored for after the release");
            }
        }

        [Test]
        public void AC103_19_F7_ReleaseTiming_HeldEarly_PerfectWindow_Auto()
        {
            RunEvent e = ReleaseWith(20, out _);
            Assert.AreEqual(36f, e.Value, "early swipe held to p 0.60");
            Assert.AreEqual(0, e.Reason);
            for (int k = 45; k <= 54; k++)
            {
                e = ReleaseWith(k, out TraversalRig rig);
                Assert.AreEqual(k, e.Value, "released on the swipe's tick");
                Assert.AreEqual(1, e.Reason, "Perfect at " + k);
                Assert.AreEqual(1, rig.Count(RunEventType.VineRelease));
            }

            Assert.AreEqual(0, ReleaseWith(44, out _).Reason, "tick 44 is Good");
            Assert.AreEqual(0, ReleaseWith(55, out _).Reason, "tick 55 is Good");
            e = ReleaseWith(-1, out _);
            Assert.AreEqual(60f, e.Value);
            Assert.AreEqual(0, e.Reason);
        }

        [Test]
        public void AC103_20_LaunchSpeedAlongTheTangent()
        {
            foreach (int k in new[] { 40, 50, -1 })
            {
                TraversalRig rig = Rig();
                rig.StepUntil(st => st.Mode == MoveMode.Swing, 600);
                while (rig.State.Mode == MoveMode.Swing)
                {
                    rig.Step(rig.Sim.SwingTick + 1 == k ? InputCommand.Jump : InputCommand.None);
                }

                int tick = (int)rig.Last(RunEventType.VineRelease).Value;
                bool perfect = rig.State.LastReleasePerfect;
                float speed = (float)Math.Sqrt((rig.State.LaunchSpeed * rig.State.LaunchSpeed) + (rig.State.Vy * rig.State.Vy));
                Assert.AreEqual(perfect ? 13.5f : 10f, speed, 1e-3f, "k " + k);
                double angle = Math.Atan2(rig.State.Vy, rig.State.LaunchSpeed) * 180.0 / Math.PI;
                Assert.AreEqual(rig.Sim.SwingThetaDeg(tick), angle, 0.1, "along the arc tangent");
            }
        }

        [Test]
        public void AC103_21_C8_EveryReleaseLands_OnlyPerfectReachesTheColumn()
        {
            ChunkLibrary lib = ShippedContent.Shared.Library;
            ChunkRuntime c8 = lib.GetEntry(lib.Find("C_Canopy_VineSpan_01", "Default"));
            var validator = new ChunkValidator(ShippedAssets.Config(), new WorldDirectorConfig(), null, ShippedContent.Shared.Pickups.CrystalPad);
            int open = 36;
            for (int vine = 0; vine < c8.VineCount; vine++)
            {
                foreach (float speed in new[] { 11f, 14f })
                {
                    for (int k = open; k <= 61; k++)
                    {
                        int tick = k == 61 ? -1 : k;
                        ChunkValidator.VineTrial trial = validator.TryVine(c8, vine, speed, tick, null);
                        string what = "vine " + (vine + 1) + " tick " + tick + " at " + speed;
                        Assert.IsTrue(trial.Landed, what + " lands");
                        Assert.AreEqual(0, trial.Hits, what);
                        Assert.GreaterOrEqual(trial.LandingS, c8.GetVine(vine).LandingS, what + " on the platform");
                        if (trial.Perfect)
                        {
                            Assert.Greater(trial.ColumnItems, 0, what + ": a Perfect arc collects the column");
                        }
                        else
                        {
                            Assert.AreEqual(0, trial.ColumnItems, what + ": a Good arc never reaches the column");
                        }
                    }
                }
            }
        }

        [Test]
        public void AC103_23_SwipeDownOrFlick_DroppedWithSwing()
        {
            TraversalRig control = Rig();
            TraversalRig input = Rig();
            control.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            input.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            control.Steps(5);
            input.Step(InputCommand.Slide);
            input.Steps(3);
            input.Step(InputCommand.DodgeLeft);
            Assert.AreEqual(2, input.State.DroppedInputs);
            Assert.AreEqual((byte)DropReason.Swing, input.Last(RunEventType.InputDropped).Reason);
            Assert.AreEqual(control.State.S, input.State.S, 1e-5f, "swing unchanged");
            Assert.AreEqual(control.State.Y, input.State.Y, 1e-5f);
            Assert.AreEqual(MoveMode.Swing, input.State.Mode);
        }

        [Test]
        public void AC103_24_LandingBlendsFromTheLaunchSpeedTo_vd_In18Ticks()
        {
            TraversalRig rig = Rig();
            rig.StepUntil(st => st.Mode == MoveMode.Swing, 600);
            rig.StepUntil(st => st.Mode == MoveMode.Run && st.VineAir, 120);
            float launch = rig.State.LaunchSpeed;
            Assert.Less(launch, Speed);
            rig.StepUntil(st => st.Grounded, 120);
            long landed = rig.State.Tick;
            rig.Steps(18);
            Assert.AreEqual(landed + 18, rig.State.Tick);
            Assert.AreEqual(Speed, rig.State.Speed, 1e-3f);
        }

        [Test]
        public void Revive_AfterADeathRightAfterLanding_StartsAtTheFunnel()
        {
            TraversalRig rig = Rig(0f, true);
            rig.StepUntil(st => st.Dead, 900);
            Assert.IsTrue(rig.State.Dead);
            Assert.Greater(rig.State.S, 104f, "died after landing");
            Assert.IsTrue(rig.Sim.Revive());
            Assert.LessOrEqual(rig.State.S, 96f - 8f + 1e-3f, "the takeoff funnel 8 m before the lip");
            Assert.GreaterOrEqual(rig.State.S, 96f - 9.5f);
            Assert.IsTrue(rig.StepUntil(st => st.Mode == MoveMode.Swing, 300), "the swing starts over");
        }
    }
}
