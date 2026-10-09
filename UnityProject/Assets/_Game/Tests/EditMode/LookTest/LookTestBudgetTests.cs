using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Look test v2 budget skeleton (ENVIRONMENT_STRATEGY 4.2/4.3): renderer naming by layer, per-layer tallies,
    /// the default budget lines adding up to the totals, mesh merging and the canopy dapple texture.
    /// </summary>
    public sealed class LookTestBudgetTests
    {
        private LookTestConfigAsset _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [TestCase("L1 Walls (Leaves)", "L1")]
        [TestCase("W Mist (MistCard)", "W")]
        [TestCase("FX Pista stand-in", "FX")]
        [TestCase("Body", "?")]
        [TestCase("", "?")]
        public void LayerComesFromTheRendererName(string name, string layer)
        {
            Assert.AreEqual(layer, LookTestBatchSet.LayerOf(name));
        }

        [Test]
        public void RendererNameStartsWithTheLayer()
        {
            string name = LookTestBatchSet.RendererName("L3", "Canopy", "Leaves");
            Assert.AreEqual("L3 Canopy (Leaves)", name);
            Assert.AreEqual("L3", LookTestBatchSet.LayerOf(name));
        }

        [Test]
        public void TallyAddsUpPerLayerAndFlagsOverBudget()
        {
            var tally = new LookTestBudgetTally();
            tally.AddMain("L1 Walls (Leaves)", 1, 40000);
            tally.AddMain("L1 Walls (Leaves)", 1, 30000);
            tally.AddMain("L0 Ground (Ground)", 1, 5000);
            tally.AddShadow(1, 20000);
            Assert.AreEqual(2, tally.Draws("L1"));
            Assert.AreEqual(70000, tally.Triangles("L1"));
            Assert.AreEqual(3, tally.MainDraws);
            Assert.AreEqual(75000, tally.MainTriangles);
            Assert.IsTrue(tally.WithinTotals(_config));
            StringAssert.DoesNotContain("OVER", tally.Format(_config, 6));

            tally.AddMain("L1 Walls (Leaves)", 1, 400000);
            Assert.IsFalse(tally.WithinTotals(_config));
            StringAssert.Contains("OVER", tally.Format(_config, 6));
        }

        [Test]
        public void DefaultLayerBudgetsFitTheTotals()
        {
            int draws = 0;
            long tris = 0;
            foreach (LookTestBudgetLine line in _config.BudgetLines)
            {
                draws += line.Draws;
                tris += line.Triangles;
            }

            Assert.LessOrEqual(draws, _config.MainDrawBudget);
            Assert.LessOrEqual(tris, _config.MainTriangleBudget);
            Assert.AreEqual(250, _config.MainDrawBudget, "ADR 0004 Decision 7");
            Assert.AreEqual(350000, _config.MainTriangleBudget, "ADR 0004 Decision 7");
        }

        [Test]
        public void AccumulatorMergesMeshesIntoOneSubmeshAndPaints()
        {
            var quad = new Mesh();
            quad.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.forward, new Vector3(1f, 0f, 1f) });
            quad.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            var acc = new LookTestMeshAccumulator();
            acc.Append(quad, Matrix4x4.Translate(new Vector3(5f, 0f, 0f)), (w, l, src) => new Color(w.x, 0f, 0.5f, 1f));
            acc.Append(quad, Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)), null);
            Mesh merged = acc.ToMesh("Merged");
            Assert.AreEqual(1, merged.subMeshCount);
            Assert.AreEqual(8, merged.vertexCount);
            Assert.AreEqual(4, LookTestMeshFactory.TriangleCount(merged));
            Assert.AreEqual(5f, merged.colors[0].r, 1e-4f, "painter sees world positions");
            Assert.AreEqual(0.5f, merged.colors[0].b, 1e-4f);

            // A mirrored instance keeps facing up (winding flipped).
            int[] tris = merged.triangles;
            Vector3[] v = merged.vertices;
            Vector3 n = Vector3.Cross(v[tris[7]] - v[tris[6]], v[tris[8]] - v[tris[6]]);
            Assert.Greater(n.y, 0f);
            Object.DestroyImmediate(quad);
            Object.DestroyImmediate(merged);
        }

        [Test]
        public void AtmosCardsShareTheirCentre()
        {
            var acc = new LookTestMeshAccumulator();
            acc.AppendCard(new Vector3(1f, 2f, 3f), Vector3.up, 4f, 10f, 0.5f);
            Mesh mesh = acc.ToMesh("Card");
            Assert.AreEqual(4, mesh.vertexCount);
            foreach (Vector3 p in mesh.vertices)
            {
                Assert.AreEqual(new Vector3(1f, 2f, 3f), p);
            }

            var size = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, size);
            Assert.AreEqual(new Vector4(4f, 10f, 0.5f, 0f), size[0]);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void DappleValuesAreUniformSoTheLitFractionIsExact()
        {
            float[] values = LookTestTextureGenerator.DappleValues(7, 64, 30);
            Assert.AreEqual(64 * 64, values.Length);
            int above = 0;
            foreach (float v in values)
            {
                Assert.That(v, Is.InRange(0f, 1f));
                if (v > 0.78f)
                {
                    above++;
                }
            }

            Assert.AreEqual(0.22f, above / (float)values.Length, 0.01f);
            Assert.AreEqual(values, LookTestTextureGenerator.DappleValues(7, 64, 30), "deterministic");
        }
    }
}
