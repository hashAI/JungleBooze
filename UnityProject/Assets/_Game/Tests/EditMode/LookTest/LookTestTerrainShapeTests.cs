using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>ADR 0004 look test: the stretch is flat on the path, seamless across segments and across the loop.</summary>
    public sealed class LookTestTerrainShapeTests
    {
        private LookTestConfigAsset _config;
        private LookTestTerrainShape _shape;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
            _shape = new LookTestTerrainShape(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void PathIsExactlyFlat()
        {
            for (float z = 0f; z < _config.LoopLengthM; z += 3.7f)
            {
                for (float x = -_config.PathHalfWidthM; x <= _config.PathHalfWidthM; x += 0.6f)
                {
                    Assert.AreEqual(0f, _shape.Height(x, z), "x " + x + ", z " + z);
                }
            }
        }

        [Test]
        public void GroundIsContinuousAtThePathEdges()
        {
            for (float z = 0f; z < _config.LoopLengthM; z += 5.3f)
            {
                Assert.Less(Mathf.Abs(_shape.Height(_config.PathHalfWidthM + 0.01f, z)), 0.02f);
                Assert.Less(Mathf.Abs(_shape.Height(-_config.PathHalfWidthM - 0.01f, z)), 0.02f);
            }
        }

        [Test]
        public void EverythingRepeatsWithTheLoopLength()
        {
            float loop = _config.LoopLengthM;
            for (float x = -60f; x <= 60f; x += 2.9f)
            {
                Assert.AreEqual(_shape.Height(x, 0f), _shape.Height(x, loop), 1e-3f, "height at x " + x);
                Color a = _shape.Weights(x, 0f);
                Color b = _shape.Weights(x, loop);
                Assert.AreEqual(a.r, b.r, 1e-3f);
                Assert.AreEqual(a.g, b.g, 1e-3f);
            }

            Assert.AreEqual(_shape.RiverCenterX(0f), _shape.RiverCenterX(loop), 1e-3f);
        }

        [Test]
        public void RiverBedIsBelowTheWaterAndTheBankAbove()
        {
            for (float z = 0f; z < _config.LoopLengthM; z += 7.1f)
            {
                float center = _shape.RiverCenterX(z);
                Assert.Less(_shape.Height(center, z), _config.WaterLevelM - 0.5f, "bed at z " + z);
                Assert.Greater(_shape.Height(center - _config.RiverHalfWidthM * 1.6f, z), _config.WaterLevelM, "near bank at z " + z);
            }
        }

        [Test]
        public void GroundSegmentsMeetExactlyIncludingTheLoopSeam()
        {
            int count = _config.SegmentCount;
            var meshes = new Mesh[count];
            for (int i = 0; i < count; i++)
            {
                meshes[i] = LookTestMeshFactory.Ground(_shape, _config, i);
            }

            try
            {
                int rows = Mathf.RoundToInt(_config.LoopLengthM / count / _config.GroundStepZM);
                int perColumn = rows + 1;
                for (int i = 0; i < count; i++)
                {
                    Vector3[] a = meshes[i].vertices;
                    Vector3[] b = meshes[(i + 1) % count].vertices;
                    Assert.AreEqual(a.Length, b.Length);
                    for (int column = 0; column < a.Length / perColumn; column++)
                    {
                        Vector3 end = a[column * perColumn + rows];
                        Vector3 start = b[column * perColumn];
                        Assert.AreEqual(end.x, start.x, 1e-4f);
                        Assert.AreEqual(end.y, start.y, 1e-3f, "segment " + i + " column " + column);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < count; i++)
                {
                    Object.DestroyImmediate(meshes[i]);
                }
            }
        }

        [Test]
        public void GroundTrianglesFaceUp()
        {
            Mesh mesh = LookTestMeshFactory.Ground(_shape, _config, 3);
            try
            {
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    Assert.Greater(n.y, 0f, "triangle " + (i / 3));
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
