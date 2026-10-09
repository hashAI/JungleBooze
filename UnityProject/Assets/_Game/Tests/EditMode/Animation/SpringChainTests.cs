using System;
using JungleBooze.Gameplay.Animation;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Animation
{
    /// <summary>Ponytail spring: snaps to the pose, keeps bone lengths, respects the angle limit, frame-rate independent, no allocation.</summary>
    public sealed class SpringChainTests
    {
        private static readonly Vector3 Root = new Vector3(0f, 1.6f, -0.1f);

        private static Vector3[] Targets()
        {
            return new[] { Root + new Vector3(0f, -0.1f, -0.03f), Root + new Vector3(0f, -0.21f, -0.06f), Root + new Vector3(0f, -0.32f, -0.08f) };
        }

        private static SpringChain Chain()
        {
            return new SpringChain(3) { Stiffness = 0.16f, Damping = 0.12f, Gravity = 6f, MaxAngleDeg = 55f };
        }

        [Test]
        public void FirstStepSnapsToTheAnimatedPose()
        {
            SpringChain c = Chain();
            Vector3[] t = Targets();
            c.Step(Root, t, Vector3.down, Vector3.zero, 1f / 60f);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0f, (c[i] - t[i]).magnitude, 1e-6f);
            }
        }

        [Test]
        public void KeepsBoneLengthsAndAngleLimitUnderHardInertia()
        {
            SpringChain c = Chain();
            Vector3[] t = Targets();
            c.Step(Root, t, Vector3.down, Vector3.zero, 1f / 60f);
            for (int i = 0; i < 120; i++)
            {
                c.Step(Root, t, Vector3.down, new Vector3(80f, 0f, -80f), 1f / 60f);
            }

            Vector3 parent = Root;
            Vector3 animatedParent = Root;
            for (int i = 0; i < 3; i++)
            {
                float rest = (t[i] - animatedParent).magnitude;
                Assert.AreEqual(rest, (c[i] - parent).magnitude, 1e-4f, "length " + i);
                float angle = Vector3.Angle(c[i] - parent, t[i] - animatedParent);
                Assert.LessOrEqual(angle, 55.01f, "angle " + i);
                parent = c[i];
                animatedParent = t[i];
            }

            Assert.Greater((c[2] - t[2]).magnitude, 0.05f, "the hair swings");
        }

        [Test]
        public void SettlesBackWhenTheBodyStops()
        {
            SpringChain c = Chain();
            c.Gravity = 0f;
            Vector3[] t = Targets();
            c.Step(Root, t, Vector3.down, Vector3.zero, 1f / 60f);
            for (int i = 0; i < 30; i++)
            {
                c.Step(Root, t, Vector3.down, new Vector3(40f, 0f, 0f), 1f / 60f);
            }

            for (int i = 0; i < 240; i++)
            {
                c.Step(Root, t, Vector3.down, Vector3.zero, 1f / 60f);
            }

            Assert.Less((c[2] - t[2]).magnitude, 0.01f);
        }

        [Test]
        public void SameMotionAt30And60And120Hz()
        {
            Vector3 Run(int hz)
            {
                SpringChain c = Chain();
                Vector3[] t = Targets();
                c.Step(Root, t, Vector3.down, Vector3.zero, 1f / hz);
                for (int i = 0; i < hz / 2; i++)
                {
                    c.Step(Root, t, Vector3.down, new Vector3(30f, 0f, 0f), 1f / hz);
                }

                return c[2];
            }

            Vector3 a = Run(30);
            Vector3 b = Run(60);
            Vector3 d = Run(120);
            Assert.Less((a - b).magnitude, 0.01f);
            Assert.Less((d - b).magnitude, 0.01f);
        }

        [Test]
        public void StepDoesNotAllocate()
        {
            SpringChain c = Chain();
            Vector3[] t = Targets();
            c.Step(Root, t, Vector3.down, Vector3.zero, 1f / 60f);
            c.Step(Root, t, Vector3.down, Vector3.one, 1f / 60f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                c.Step(Root, t, Vector3.down, new Vector3(i % 7, 0f, 0f), 1f / 60f);
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
