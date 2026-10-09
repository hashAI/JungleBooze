using JungleBooze.Core;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Traversal
{
    /// <summary>Canopy beams (spec 103 §6, AC-103-25/26).</summary>
    public sealed class CanopyTests
    {
        private const float Speed = 12f;

        /// <summary>Beam A x −1.2…1.2 until 50, gap 50–53.5, beam B x −0.2…1.8 (offset 1.0), all at floor 0.</summary>
        private static ChunkLayoutBuilder Beams()
        {
            var b = new ChunkLayoutBuilder("Beams", 200f);
            b.Width(0f, -3.5f, 3.5f).Width(20f, -1.2f, 1.2f).Width(49.99f, -1.2f, 1.2f).Width(50f, -1.2f, 1.8f)
                .Width(53.49f, -1.2f, 1.8f).Width(53.5f, -0.2f, 1.8f).Width(150f, -0.2f, 1.8f).Width(170f, -3.5f, 3.5f)
                .Gap(20f, 150f)
                .Floor(20f, 50f, 0f, 0f, -1.2f, 1.2f)
                .Floor(53.5f, 150f, 0f, 0f, -0.2f, 1.8f)
                .Zone(TraversalMode.Canopy, 20f, 150f, -1.2f, 1.8f, 0.25f, false, "test beams");
            return b;
        }

        private static TraversalRig Airborne(float x)
        {
            // Just before beam B, falling at the landing height, at a chosen x.
            TraversalRig rig = TraversalRig.Create(Beams(), 200f, Speed, 30f, 0f);
            RunnerState st = rig.State;
            st.S = 53.3f;
            st.X = x;
            st.XTarget = x;
            st.Y = 0.08f;
            st.Vy = -3f;
            st.Grounded = false;
            st.Jumped = true;
            st.GroundY = 0f;
            st.LastGroundY = 0f;
            st.AirborneSinceTick = 0;
            st.Tick = 100;
            rig.Sim.SetStateForTest(st);
            return rig;
        }

        [Test]
        public void AC103_25_EnvelopeOverTheGap_AssistWithin025_FallBeyond()
        {
            TraversalRig rig = TraversalRig.Create(Beams(), 200f, Speed, 30f, 0f);
            rig.Path.GetLateralBounds(51.7f, 0f, out float a, out float b);
            Assert.AreEqual(-1.2f, a, 1e-3f, "xLim is the envelope of both beams");
            Assert.AreEqual(1.8f, b, 1e-3f);

            // 0.20 m outside beam B's left edge (−0.2): snapped onto it over 4 ticks.
            TraversalRig near = Airborne(-0.4f);
            near.StepUntil(st => st.Grounded || st.Dead, 60);
            Assert.IsTrue(near.State.Grounded);
            Assert.IsFalse(near.State.Dead);
            Assert.AreEqual(1, near.Count(RunEventType.BeamAssist));
            long landed = near.State.Tick;
            near.Steps(4);
            Assert.AreEqual(landed + 4, near.State.Tick);
            Assert.GreaterOrEqual(near.State.X, -0.2f + 0.3f - 1e-3f, "pulled onto the beam (edge margin)");
            near.Steps(30);
            Assert.IsFalse(near.State.Dead);

            // 0.30 m outside: no floor, the fall continues (cause Fall).
            TraversalRig far = Airborne(-0.5f);
            far.StepUntil(st => st.Dead || st.Grounded, 120);
            Assert.IsTrue(far.State.Dead);
            Assert.AreEqual(DeathCause.Fall, far.State.Cause);
        }

        [Test]
        public void AC103_26_BeamEdgesNeverDamage()
        {
            foreach (float push in new[] { 0.2f, -0.2f })
            {
                TraversalRig rig = TraversalRig.Create(Beams(), 200f, Speed, 22f, 0f);
                while (rig.State.S < 48f)
                {
                    rig.Step(InputCommand.None, TraversalRig.Mm(push));
                }

                Assert.AreEqual(0, rig.State.Hits);
                Assert.IsFalse(rig.State.Dead, "soft edges: steering never kills");
                Assert.Greater(rig.Count(RunEventType.EdgeBrush), 0);
            }
        }

        [Test]
        public void BeamGap_IsATraversalResult()
        {
            TraversalRig rig = TraversalRig.Create(Beams(), 200f, Speed, 30f, 0.3f);
            rig.StepUntil(st => st.S > 47.2f);
            rig.Step(InputCommand.Jump);
            rig.StepUntil(st => st.Grounded, 120);
            Assert.IsFalse(rig.State.Dead);
            RunEvent e = rig.Last(RunEventType.TraversalResult);
            Assert.AreEqual((byte)TraversalKind.BeamGap, e.Reason);
            Assert.AreEqual(1f, e.Value);
        }
    }
}
