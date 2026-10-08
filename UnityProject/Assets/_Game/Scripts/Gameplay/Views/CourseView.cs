using System.Collections.Generic;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Builds the gray-box geometry of a <see cref="CoursePath"/> at setup (spec 101 §6 colours): a raised path
    /// slab with holes for gaps and raised ramps, the safe/risky branch tints, foliage hedges on the edges, a low
    /// forest floor so gaps read as drops, obstacle boxes (logs as cylinders, thorns with spikes, branches on
    /// posts), the fork divider, spinning coins, section signs and the finish arch. Coins hide on pickup and come
    /// back on restart. Setup allocates; per-frame work is a coin spin loop.
    /// </summary>
    public sealed class CourseView : MonoBehaviour
    {
        private const float SlabBottom = -4f;
        private const float GroundY = -4f;
        private const float HedgeHeight = 0.45f;
        private const float HedgeWidth = 0.5f;
        private const float StripStep = 2f;
        private const float CoinRadius = 0.28f;
        private const float CoinSpinDegPerSecond = 180f;

        private CoursePath _course;
        private FeelTestPalette _palette;
        private GameObject[] _coins;
        private GameObject[] _obstacles;
        private float _spin;

        public CoursePath Course => _course;

        public int CoinCount => _coins == null ? 0 : _coins.Length;

        public void Build(CoursePath course, FeelTestPalette palette, Font labelFont)
        {
            _course = course;
            _palette = palette;
            BuildFloor();
            BuildObstacles();
            BuildForks();
            BuildCoins();
            BuildFinish();
            if (labelFont != null)
            {
                BuildSigns(labelFont);
            }

            Transform ground = ViewUtil.Box("ForestFloor", transform, palette.Ground, new Vector3(-80f, GroundY - 0.2f, -60f), new Vector3(80f, GroundY, course.EndS + 200f), false);
            ground.GetComponent<Renderer>().receiveShadows = true;
        }

        public void ResetRun()
        {
            for (int i = 0; i < _coins.Length; i++)
            {
                _coins[i].SetActive(true);
            }

            for (int i = 0; i < _obstacles.Length; i++)
            {
                _obstacles[i].SetActive(true);
            }
        }

        public void OnRunEvent(in RunEvent e)
        {
            if (e.Type == RunEventType.Coin && e.Id >= 0 && e.Id < _coins.Length)
            {
                _coins[e.Id].SetActive(false);
            }
        }

        /// <summary>Hides obstacles a revive removed.</summary>
        public void SyncRemoved(RunnerSimulation sim)
        {
            for (int i = 0; i < _obstacles.Length; i++)
            {
                if (sim.IsObstacleRemoved(i) && _obstacles[i].activeSelf)
                {
                    _obstacles[i].SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (_coins == null)
            {
                return;
            }

            _spin = Mathf.Repeat(_spin + (Time.unscaledDeltaTime * CoinSpinDegPerSecond), 360f);
            Quaternion rotation = Quaternion.Euler(90f, _spin, 0f);
            for (int i = 0; i < _coins.Length; i++)
            {
                _coins[i].transform.localRotation = rotation;
            }
        }

        private void BuildFloor()
        {
            // Breakpoints in s: width keys, floor patches, forks, plus a regular step.
            var cuts = new List<float> { -40f, _course.EndS };
            for (float s = 0f; s < _course.EndS; s += StripStep)
            {
                cuts.Add(s);
            }

            for (int i = 0; i < _course.WidthKeyCount; i++)
            {
                cuts.Add(_course.GetWidthKey(i).S);
            }

            for (int i = 0; i < _course.FloorPatchCount; i++)
            {
                CourseFloorPatch p = _course.GetFloorPatch(i);
                cuts.Add(p.SMin);
                cuts.Add(p.SMax);
            }

            for (int i = 0; i < _course.ForkCount; i++)
            {
                cuts.Add(_course.GetFork(i).SFront);
                cuts.Add(_course.GetFork(i).SMerge);
            }

            cuts.Sort();

            var mesh = new MeshBuilder(4);
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float a = cuts[i];
                float b = cuts[i + 1];
                if (b - a < 0.001f)
                {
                    continue;
                }

                float mid = (a + b) * 0.5f;
                int fork = ForkAt(mid);
                if (fork >= 0)
                {
                    CourseFork f = _course.GetForkData(fork);
                    int safe = f.SafeSide;
                    Column(mesh, a, b, f.LeftXMin, f.LeftXMin, f.LeftXMax, f.LeftXMax, safe < 0 ? 1 : 2, true, false);
                    Column(mesh, a, b, f.RightXMin, f.RightXMin, f.RightXMax, f.RightXMax, safe < 0 ? 2 : 1, false, true);
                }
                else
                {
                    _course.GetOuterBounds(a, out float a0, out float a1);
                    _course.GetOuterBounds(b, out float b0, out float b1);
                    Column(mesh, a, b, a0, b0, a1, b1, 0, true, true);
                }
            }

            var go = new GameObject("PathSlab");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh("FeelCoursePath");
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { _palette.Path, _palette.PathSafe, _palette.PathRisky, _palette.PathSide, _palette.Hedge };
        }

        private int ForkAt(float s)
        {
            for (int i = 0; i < _course.ForkCount; i++)
            {
                ForkPoint f = _course.GetFork(i);
                if (s >= f.SFront && s < f.SMerge)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>One strip of floor between s = a and s = b, edges interpolated, with skirts and hedges.</summary>
        private void Column(MeshBuilder mesh, float a, float b, float xa0, float xb0, float xa1, float xb1, int submesh, bool leftHedge, bool rightHedge)
        {
            float probeX = ((xa0 + xa1) * 0.5f) + 0.01f;
            float eps = 0.001f;
            if (!_course.TryGetFloor((a + b) * 0.5f, probeX, out _))
            {
                return;
            }

            _course.TryGetFloor(a + eps, probeX, out float ya);
            _course.TryGetFloor(b - eps, probeX, out float yb);

            var p00 = new Vector3(xa0, ya, a);
            var p01 = new Vector3(xa1, ya, a);
            var p10 = new Vector3(xb0, yb, b);
            var p11 = new Vector3(xb1, yb, b);
            mesh.Quad(submesh, p00, p10, p11, p01);

            // Side skirts down to the forest floor.
            mesh.Quad(3, new Vector3(xa0, SlabBottom, a), new Vector3(xb0, SlabBottom, b), p10, p00);
            mesh.Quad(3, p01, p11, new Vector3(xb1, SlabBottom, b), new Vector3(xa1, SlabBottom, a));

            // Front/back faces where the floor before/after is missing (gap lips) or changes height.
            bool before = _course.TryGetFloor(a - eps, probeX, out float yBefore);
            if (!before || yBefore < ya - 0.05f)
            {
                mesh.Quad(3, new Vector3(xa0, before ? yBefore : SlabBottom, a), p00, p01, new Vector3(xa1, before ? yBefore : SlabBottom, a));
            }

            bool after = _course.TryGetFloor(b + eps, probeX, out float yAfter);
            if (!after || yAfter < yb - 0.05f)
            {
                mesh.Quad(3, p11, p10, new Vector3(xb0, after ? yAfter : SlabBottom, b), new Vector3(xb1, after ? yAfter : SlabBottom, b));
            }

            if (leftHedge)
            {
                Hedge(mesh, a, b, xa0, xb0, ya, yb, -1f);
            }

            if (rightHedge)
            {
                Hedge(mesh, a, b, xa1, xb1, ya, yb, 1f);
            }
        }

        private static void Hedge(MeshBuilder mesh, float a, float b, float xa, float xb, float ya, float yb, float side)
        {
            float wa = xa + (side * HedgeWidth);
            float wb = xb + (side * HedgeWidth);
            var ia = new Vector3(xa, ya + HedgeHeight, a);
            var ib = new Vector3(xb, yb + HedgeHeight, b);
            var oa = new Vector3(wa, ya + HedgeHeight, a);
            var ob = new Vector3(wb, yb + HedgeHeight, b);
            var ia0 = new Vector3(xa, ya, a);
            var ib0 = new Vector3(xb, yb, b);
            if (side > 0f)
            {
                mesh.Quad(0, ia0, ib0, ib, ia, true);
                mesh.Quad(0, ia, ib, ob, oa, true);
            }
            else
            {
                mesh.Quad(0, ia, ib, ib0, ia0, true);
                mesh.Quad(0, oa, ob, ib, ia, true);
            }
        }

        private void BuildObstacles()
        {
            var root = new GameObject("Obstacles").transform;
            root.SetParent(transform, false);
            _obstacles = new GameObject[_course.ObstacleCount];
            for (int id = 0; id < _course.ObstacleCount; id++)
            {
                ObstacleBox box = _course.GetObstacle(id);
                var go = new GameObject(_course.GetObstacleLabel(id));
                go.transform.SetParent(root, false);
                _obstacles[id] = go;
                var min = new Vector3(box.XMin, box.YMin, box.SMin);
                var max = new Vector3(box.XMax, box.YMax, box.SMax);
                switch (box.Class)
                {
                    case ObstacleClass.Low:
                        if (box.WalkableTop)
                        {
                            Transform log = ViewUtil.Primitive(PrimitiveType.Cylinder, "Log", go.transform, _palette.Low, true);
                            float radius = Mathf.Min(box.YMax - box.YMin, box.SMax - box.SMin) * 0.5f;
                            log.localPosition = new Vector3(box.CenterX, box.YMax - radius, (box.SMin + box.SMax) * 0.5f);
                            log.localRotation = Quaternion.Euler(0f, 0f, 90f);
                            log.localScale = new Vector3(radius * 2f, (box.XMax - box.XMin) * 0.5f, (box.SMax - box.SMin));
                            ViewUtil.Box("Top", go.transform, _palette.Low, new Vector3(box.XMin, box.YMax - 0.12f, box.SMin), new Vector3(box.XMax, box.YMax, box.SMax), true);
                        }
                        else
                        {
                            ViewUtil.Box("Root", go.transform, _palette.Low, min, max, true);
                        }

                        break;
                    case ObstacleClass.High:
                        ViewUtil.Box("Branch", go.transform, _palette.High, min, max, true);
                        float postLeft = box.XMin;
                        float postRight = box.XMax;
                        ViewUtil.Box("PostL", go.transform, _palette.High, new Vector3(postLeft - 0.3f, box.YMin - 6f, box.SMin + 0.1f), new Vector3(postLeft, box.YMax, box.SMax - 0.1f), true);
                        ViewUtil.Box("PostR", go.transform, _palette.High, new Vector3(postRight, box.YMin - 6f, box.SMin + 0.1f), new Vector3(postRight + 0.3f, box.YMax, box.SMax - 0.1f), true);
                        break;
                    case ObstacleClass.Blocker:
                        ViewUtil.Box("Rock", go.transform, _palette.Blocker, min, max, true);
                        break;
                    default:
                        ViewUtil.Box("Bed", go.transform, _palette.Thorns, min, new Vector3(max.x, min.y + 0.15f, max.z), false);
                        int rows = Mathf.Max(1, Mathf.RoundToInt((box.SMax - box.SMin) / 0.5f));
                        int cols = Mathf.Max(1, Mathf.RoundToInt((box.XMax - box.XMin) / 0.5f));
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                Transform spike = ViewUtil.Primitive(PrimitiveType.Cube, "Spike", go.transform, _palette.Thorns, false);
                                float h = box.YMax - box.YMin;
                                spike.localPosition = new Vector3(box.XMin + ((c + 0.5f) * (box.XMax - box.XMin) / cols), box.YMin + (h * 0.45f), box.SMin + ((r + 0.5f) * (box.SMax - box.SMin) / rows));
                                spike.localRotation = Quaternion.Euler(45f, 0f, 45f);
                                spike.localScale = new Vector3(0.12f, h * 0.9f, 0.12f);
                            }
                        }

                        break;
                }
            }
        }

        private void BuildForks()
        {
            for (int i = 0; i < _course.ForkCount; i++)
            {
                CourseFork f = _course.GetForkData(i);
                float c = f.DividerCenterX;
                float hw = f.DividerHalfWidth;
                ViewUtil.Box("Divider " + f.Name, transform, _palette.Divider, new Vector3(c - hw, SlabBottom, f.SFront), new Vector3(c + hw, 3.2f, f.SMerge), true);
            }
        }

        private void BuildCoins()
        {
            var root = new GameObject("Coins").transform;
            root.SetParent(transform, false);
            _coins = new GameObject[_course.CoinCount];
            for (int id = 0; id < _course.CoinCount; id++)
            {
                CoinPoint coin = _course.GetCoin(id);
                Transform t = ViewUtil.Primitive(PrimitiveType.Cylinder, "Coin", root, _palette.Coin, false);
                t.localPosition = new Vector3(coin.X, coin.Y, coin.S);
                t.localScale = new Vector3(CoinRadius * 2f, 0.03f, CoinRadius * 2f);
                t.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _coins[id] = t.gameObject;
            }
        }

        private void BuildFinish()
        {
            float s = _course.FinishS;
            _course.GetOuterBounds(s, out float x0, out float x1);
            var root = new GameObject("FinishArch").transform;
            root.SetParent(transform, false);
            ViewUtil.Box("PostL", root, _palette.Finish, new Vector3(x0 - 0.6f, SlabBottom, s - 0.3f), new Vector3(x0, 4.5f, s + 0.3f), true);
            ViewUtil.Box("PostR", root, _palette.Finish, new Vector3(x1, SlabBottom, s - 0.3f), new Vector3(x1 + 0.6f, 4.5f, s + 0.3f), true);
            ViewUtil.Box("Bar", root, _palette.Finish, new Vector3(x0 - 0.6f, 4.0f, s - 0.3f), new Vector3(x1 + 0.6f, 4.6f, s + 0.3f), true);
            ViewUtil.Box("Line", root, _palette.Finish, new Vector3(x0, 0f, s - 0.15f), new Vector3(x1, 0.01f, s + 0.15f), false);
        }

        private void BuildSigns(Font font)
        {
            var root = new GameObject("Signs").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < _course.SectionCount; i++)
            {
                CourseSection section = _course.GetSection(i);
                _course.GetOuterBounds(section.SMin, out float x0, out _);
                Transform post = ViewUtil.Box("Sign " + section.Name, root, _palette.Marker, new Vector3(x0 - 1.4f, 0f, section.SMin - 0.1f), new Vector3(x0 - 1.25f, 2.2f, section.SMin + 0.1f), false);
                var label = new GameObject("Label");
                label.transform.SetParent(post.parent, false);
                label.transform.localPosition = new Vector3(x0 - 1.3f, 2.5f, section.SMin);
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = section.Name;
                text.font = font;
                text.fontSize = 48;
                text.characterSize = 0.06f;
                text.anchor = TextAnchor.LowerCenter;
                text.color = new Color(0.12f, 0.1f, 0.14f);
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        /// <summary>Small mesh builder with submeshes (setup only).</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _indices;
            private readonly int _hedgeSubmesh;
            private readonly List<int> _hedge = new List<int>();

            public MeshBuilder(int submeshes)
            {
                _indices = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                {
                    _indices[i] = new List<int>();
                }

                _hedgeSubmesh = submeshes;
            }

            public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool hedge = false)
            {
                Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
                int start = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _vertices.Add(d);
                for (int i = 0; i < 4; i++)
                {
                    _normals.Add(normal);
                }

                _uvs.Add(new Vector2(a.x, a.z));
                _uvs.Add(new Vector2(b.x, b.z));
                _uvs.Add(new Vector2(c.x, c.z));
                _uvs.Add(new Vector2(d.x, d.z));
                List<int> target = hedge ? _hedge : _indices[submesh];
                target.Add(start);
                target.Add(start + 1);
                target.Add(start + 2);
                target.Add(start);
                target.Add(start + 2);
                target.Add(start + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _indices.Length + 1;
                for (int i = 0; i < _indices.Length; i++)
                {
                    mesh.SetTriangles(_indices[i], i);
                }

                mesh.SetTriangles(_hedge, _hedgeSubmesh);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
