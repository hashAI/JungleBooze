using JungleBooze.Editor.Scenery;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Placement and mesh helpers of the environment kit used by the hero basin (ADR 0008, iteration 3 amendment):
    /// the arch span's depth factor and island filtering (vine curtains cleared from the arch opening).
    /// </summary>
    public sealed class EnvironmentKitTests
    {
        [Test]
        public void SpanDepthScaleOnlyChangesDepth()
        {
            var piece = new EnvironmentKit.Piece { Bounds = new Bounds(new Vector3(0f, 5f, 0f), new Vector3(20f, 10f, 4f)) };
            var a = new Vector3(-30f, 0f, 50f);
            var b = new Vector3(30f, 0f, 50f);
            Matrix4x4 full = EnvironmentKit.Span(piece, a, b, 20f);
            Matrix4x4 slim = EnvironmentKit.Span(piece, a, b, 20f, 0.5f);

            // Feet and top stay put.
            Assert.That(Vector3.Distance(slim.MultiplyPoint3x4(new Vector3(-10f, 0f, 0f)), a), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(slim.MultiplyPoint3x4(new Vector3(10f, 0f, 0f)), b), Is.LessThan(1e-3f));
            Assert.AreEqual(20f, slim.MultiplyPoint3x4(new Vector3(0f, 10f, 0f)).y, 1e-3f);

            // Depth halves.
            float fullDepth = Vector3.Distance(full.MultiplyPoint3x4(new Vector3(0f, 0f, -2f)), full.MultiplyPoint3x4(new Vector3(0f, 0f, 2f)));
            float slimDepth = Vector3.Distance(slim.MultiplyPoint3x4(new Vector3(0f, 0f, -2f)), slim.MultiplyPoint3x4(new Vector3(0f, 0f, 2f)));
            Assert.AreEqual(fullDepth * 0.5f, slimDepth, 1e-3f);
        }

        [Test]
        public void KeepIslandsDropsWholeCardsOnly()
        {
            // Two separate quads (islands): one at x = 0, one at x = 10.
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f),
                    new Vector3(10f, 0f, 0f), new Vector3(11f, 0f, 0f), new Vector3(10f, 1f, 0f), new Vector3(11f, 1f, 0f),
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 },
            };

            Mesh kept = EnvironmentKit.KeepIslands(mesh, Matrix4x4.identity, card => card.center.x > 5f);
            int[] triangles = kept.triangles;
            Assert.AreEqual(6, triangles.Length);
            for (int i = 0; i < triangles.Length; i++)
            {
                Assert.That(triangles[i], Is.GreaterThanOrEqualTo(4));
            }

            // The test runs in the placed space: shifting everything by -10 keeps the other card.
            Mesh shifted = EnvironmentKit.KeepIslands(mesh, Matrix4x4.Translate(new Vector3(-10f, 0f, 0f)), card => card.center.x > -5f);
            Assert.AreEqual(6, shifted.triangles.Length);
            Assert.That(shifted.triangles[0], Is.GreaterThanOrEqualTo(4));

            Object.DestroyImmediate(kept);
            Object.DestroyImmediate(shifted);
            Object.DestroyImmediate(mesh);
        }
    }
}
