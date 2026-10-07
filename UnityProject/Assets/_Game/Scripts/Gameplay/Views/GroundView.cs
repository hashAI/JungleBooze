using System.Collections.Generic;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The ground and the verge dressing.
    /// <para>Trail mode (when <c>Ground_Trail_basecolor</c> exists): a ribbon mesh with a stylized dirt texture
    /// (painted lane wear, pebbles and a grassy edge; repeats every <see cref="EnvironmentLookConfig.GroundTileM"/> along
    /// the run) and two jungle-floor strips. The ribbons are built along the route (spec 003 section 5) with a cross
    /// section every 2.5 m or less; the vertices are re-laid on pre-allocated arrays each time HERO passes a whole
    /// texture repeat, so the texture stays put on the ground and there are never gaps. Two draw calls.</para>
    /// <para>Gray-box mode (no trail texture): the cream tiles with lane dashes, ink edges and flat verges, each tile
    /// placed on the route with the route rotation (rigid 12 m pieces).</para>
    /// <para>Verges: pre-merged 12 m jungle wall segments (<c>Jungle_WallA/B/C</c>, one draw call each) recycled on both
    /// sides, placed per segment along the route and picked by <see cref="EnvironmentArt.Hash"/>; without them, single
    /// trees and bushes every 9 m.</para>
    /// <see cref="ApplyTheme"/> tints path and verge per world. No allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class GroundView : MonoBehaviour, IRunView
    {
        // Gray-box geometry (fallback when the trail texture is missing).
        private const float TileLengthM = 12f;
        private const float TilesBehindM = 12f;
        private const float GroundThicknessM = 0.2f;
        private const float PathMarginM = 0.6f;
        private const float VergeWidthM = 40f;
        private const float RingSpacingMaxM = 2.5f;
        private const float DashLengthM = 4f;
        private const float DashWidthM = 0.12f;
        private const float EdgeWidthM = 0.15f;
        private const float MarkerLiftM = 0.01f;

        private EnvironmentLookConfig _look;
        private float _pathHalfWidthM;

        // Trail mode.
        private Transform _groundRoot;
        private Mesh _trailMesh;
        private Mesh _floorMesh;
        private long _groundStep = long.MinValue;
        private PathFrame _frame;
        private int _ringFirst;
        private int _ringCount;
        private float _ringStepM;
        private float _trailHalfWidthM;
        private Vector3[] _trailVertices;
        private Vector3[] _trailNormals;
        private Vector3[] _floorVertices;
        private Vector3[] _floorNormals;

        // Gray-box mode.
        private Transform[] _tiles;
        private MeshRenderer[] _tileRenderers;
        private long[] _tileIndex;

        private Material _pathMaterial;
        private Material _pathAlternateMaterial;
        private Material _vergeMaterial;

        // Jungle wall segments (recycled per side, deterministic by slot index).
        private GameObject[][] _walls;
        private Transform[] _wallRoots;
        private long[] _wallIndex;
        private bool[] _wallFlip;
        private int _wallsPerSide;
        private int _wallVariants;
        private int[] _wallUsable;

        // Fallback dressing: single foliage models along the verges.
        private const float DressSpacingM = 9f;
        private const float DressInsetMinM = 1.5f;
        private const float DressInsetSpanM = 9f;
        private static readonly string[] DressNames = { EnvironmentArt.TreeA, EnvironmentArt.TreeB, EnvironmentArt.Bush };
        private GameObject[][] _dress;
        private Transform[] _dressRoots;
        private long[] _dressIndex;
        private int _dressPerSide;

        /// <summary>Number of recycled gray-box path tiles (0 in trail mode).</summary>
        public int TileCount => _tiles == null ? 0 : _tiles.Length;

        /// <summary>True when the textured trail is used instead of the gray-box tiles.</summary>
        public bool UsesTrail => _groundRoot != null;

        /// <summary>Number of recycled jungle wall segments (both sides; 0 when the wall art is missing).</summary>
        public int WallSegmentCount => _wallRoots == null ? 0 : _wallRoots.Length;

        /// <summary>The route the ground and the verges follow (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        /// <summary>Builds the ground with the default look. <paramref name="viewDistanceM"/> = how far ahead it must reach (fog end).</summary>
        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM)
        {
            Init(kit, runnerConfig, viewDistanceM, null);
        }

        /// <summary>Builds the ground and dressing. <paramref name="look"/> null = defaults.</summary>
        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM, EnvironmentLookConfig look)
        {
            _look = look ?? EnvironmentLookConfig.CreateDefault();
            _frame = PathPlacement.OrIdentity(_frame);
            float pathWidth = runnerConfig.LaneCount * runnerConfig.LaneWidthM + 2f * PathMarginM;
            _pathHalfWidthM = pathWidth * 0.5f;

            if (!InitTrail(viewDistanceM))
            {
                InitTiles(kit, runnerConfig, pathWidth, viewDistanceM);
            }

            if (!InitWalls(viewDistanceM))
            {
                InitDressing(viewDistanceM);
            }
        }

        /// <summary>Recolors path and verges (world themes, GDD 9). Allocation free.</summary>
        public void ApplyTheme(in WorldTheme theme)
        {
            if (_pathMaterial == null)
            {
                return;
            }

            _pathMaterial.color = theme.Path;
            if (_pathAlternateMaterial != null)
            {
                _pathAlternateMaterial.color = theme.PathAlternate;
            }

            _vergeMaterial.color = theme.Verge;
        }

        public void BeginRun(GameSession session)
        {
            _groundStep = long.MinValue;
            ResetIndices(_tileIndex);
            ResetIndices(_wallIndex);
            ResetIndices(_dressIndex);
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_groundRoot == null && _tiles == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double z);

            if (_groundRoot != null)
            {
                // Re-lay the ribbons in whole texture repeats so the dirt and the floor stay fixed on the ground.
                double tile = _look.GroundTileM;
                long step = (long)System.Math.Floor(z / tile);
                if (step != _groundStep)
                {
                    _groundStep = step;
                    RebuildRibbons(step * tile);
                }
            }
            else
            {
                RenderTiles(z);
            }

            RenderWalls(z);
            RenderDressing(z);
        }

        private void OnDestroy()
        {
            ReleaseObject(_pathMaterial);
            ReleaseObject(_pathAlternateMaterial);
            ReleaseObject(_vergeMaterial);
            ReleaseObject(_trailMesh);
            ReleaseObject(_floorMesh);
        }

        private static void ReleaseObject(Object o)
        {
            if (o != null)
            {
                Destroy(o);
            }
        }

        private static void ResetIndices(long[] indices)
        {
            if (indices == null)
            {
                return;
            }

            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = long.MinValue;
            }
        }

        // ---- Trail mode ----

        private bool InitTrail(float viewDistanceM)
        {
            Texture2D trail = EnvironmentArt.LoadTexture(EnvironmentArt.TrailTexture);
            if (trail == null)
            {
                return false;
            }

            Texture2D floor = EnvironmentArt.LoadTexture(EnvironmentArt.FloorTexture);
            _pathMaterial = EnvironmentArt.CreateLitMaterial("World_Path", trail, StylePalette.CreamPath);
            _vergeMaterial = EnvironmentArt.CreateLitMaterial("World_Verge", floor, StylePalette.JungleFloor);
            if (_pathMaterial == null || _vergeMaterial == null)
            {
                ReleaseObject(_pathMaterial);
                ReleaseObject(_vergeMaterial);
                _pathMaterial = null;
                _vergeMaterial = null;
                return false;
            }

            SetGroundSampling(trail, TextureWrapMode.Clamp);
            SetGroundSampling(floor, TextureWrapMode.Repeat);

            float tile = _look.GroundTileM;
            int behind = Mathf.CeilToInt(_look.GroundBehindM / tile);
            int ahead = Mathf.CeilToInt((viewDistanceM + _look.GroundExtraAheadM) / tile);
            float halfWidth = _look.TrailHalfWidthM;
            _trailHalfWidthM = halfWidth;

            // Cross sections every tile / sub m (at most RingSpacingMaxM); one tile step moves the whole set.
            int sub = Mathf.Max(1, Mathf.CeilToInt(tile / RingSpacingMaxM));
            _ringStepM = tile / sub;
            _ringFirst = -behind * sub;
            _ringCount = (behind + ahead) * sub + 1;
            _trailVertices = new Vector3[_ringCount * 2];
            _trailNormals = new Vector3[_trailVertices.Length];
            _floorVertices = new Vector3[_ringCount * 4];
            _floorNormals = new Vector3[_floorVertices.Length];

            _groundRoot = new GameObject("Ground").transform;
            _groundRoot.SetParent(transform, false);

            _trailMesh = BuildRibbonMesh(
                "Trail", _ringCount, _ringFirst, sub, new[] { -halfWidth }, new[] { halfWidth }, 1f / (2f * halfWidth), halfWidth);
            AddRenderer("Trail", _trailMesh, _pathMaterial);

            _floorMesh = BuildRibbonMesh(
                "JungleFloor", _ringCount, _ringFirst, sub,
                new[] { _look.FloorInnerM, -_look.FloorOuterM }, new[] { _look.FloorOuterM, -_look.FloorInnerM }, 1f / tile, 0f);
            AddRenderer("JungleFloor", _floorMesh, _vergeMaterial);
            RebuildRibbons(0.0);
            return true;
        }

        /// <summary>
        /// Trail U spans the cross-section (clamped, so the grass edge never wraps to the far side); V and the floor
        /// repeat. Mipmaps stay on; anisotropic filtering keeps the ground sharp at the camera's grazing angle.
        /// </summary>
        private void SetGroundSampling(Texture2D texture, TextureWrapMode wrapU)
        {
            if (texture == null)
            {
                return;
            }

            texture.wrapModeU = wrapU;
            texture.wrapModeV = TextureWrapMode.Repeat;
            texture.anisoLevel = _look.GroundAnisoLevel;
        }

        /// <summary>
        /// Topology and UVs for one or more ribbons (strip k runs from x0s[k] to x1s[k] across), each with
        /// <paramref name="ringCount"/> cross sections. UV: u = (x + uOffset) * uScale, v = ring number / sub, so a whole
        /// tile step leaves the texture in place. The vertex positions are set later by <see cref="RebuildRibbons"/>.
        /// Faces up. Setup-time only.
        /// </summary>
        private static Mesh BuildRibbonMesh(
            string name, int ringCount, int firstRing, int sub, float[] x0s, float[] x1s, float uScale, float uOffset)
        {
            int strips = x0s.Length;
            var vertices = new Vector3[strips * ringCount * 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[strips * (ringCount - 1) * 6];
            int t = 0;
            for (int strip = 0; strip < strips; strip++)
            {
                int start = strip * ringCount * 2;
                for (int i = 0; i < ringCount; i++)
                {
                    float v = (float)(firstRing + i) / sub;
                    uvs[start + i * 2] = new Vector2((x0s[strip] + uOffset) * uScale, v);
                    uvs[start + i * 2 + 1] = new Vector2((x1s[strip] + uOffset) * uScale, v);
                    normals[start + i * 2] = Vector3.up;
                    normals[start + i * 2 + 1] = Vector3.up;
                }

                for (int r = 0; r < ringCount - 1; r++)
                {
                    // a = (x0, ring), b = (x1, ring), c = (x1, ring + 1), d = (x0, ring + 1); clockwise seen from above.
                    int a = start + r * 2;
                    int b = a + 1;
                    int c = a + 3;
                    int d = a + 2;
                    triangles[t++] = a;
                    triangles[t++] = d;
                    triangles[t++] = c;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Lays the trail and floor cross sections along the route from <paramref name="baseS"/> (a whole number of
        /// tiles). Each ring is the route pose at its s: the trail spans +-half width along the banked right axis, the
        /// floor strips sit a little below it. Writes into the pre-allocated arrays; no allocation.
        /// </summary>
        private void RebuildRibbons(double baseS)
        {
            float half = _trailHalfWidthM;
            float inner = _look.FloorInnerM;
            float outer = _look.FloorOuterM;
            float drop = _look.FloorDropM;
            int leftStripStart = _ringCount * 2;
            for (int i = 0; i < _ringCount; i++)
            {
                _frame.Sample(baseS + (_ringFirst + i) * (double)_ringStepM, out PathPose pose);
                Vector3 center = pose.Center;
                Vector3 right = pose.Right;
                Vector3 up = pose.Up;
                Vector3 low = center - (up * drop);

                int t = i * 2;
                _trailVertices[t] = center - (right * half);
                _trailVertices[t + 1] = center + (right * half);
                _trailNormals[t] = up;
                _trailNormals[t + 1] = up;

                _floorVertices[t] = low + (right * inner);
                _floorVertices[t + 1] = low + (right * outer);
                _floorVertices[leftStripStart + t] = low - (right * outer);
                _floorVertices[leftStripStart + t + 1] = low - (right * inner);
                _floorNormals[t] = up;
                _floorNormals[t + 1] = up;
                _floorNormals[leftStripStart + t] = up;
                _floorNormals[leftStripStart + t + 1] = up;
            }

            _trailMesh.SetVertices(_trailVertices);
            _trailMesh.SetNormals(_trailNormals);
            _trailMesh.RecalculateBounds();
            _floorMesh.SetVertices(_floorVertices);
            _floorMesh.SetNormals(_floorNormals);
            _floorMesh.RecalculateBounds();
        }

        private void AddRenderer(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_groundRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ---- Gray-box mode ----

        private void InitTiles(GrayBoxKit kit, RunnerConfig runnerConfig, float pathWidth, float viewDistanceM)
        {
            int tileCount = Mathf.CeilToInt((TilesBehindM + viewDistanceM) / TileLengthM) + 1;
            _tiles = new Transform[tileCount];
            _tileRenderers = new MeshRenderer[tileCount];
            _tileIndex = new long[tileCount];
            var vergeRenderers = new MeshRenderer[tileCount * 2];
            float halfPath = pathWidth * 0.5f;

            for (int i = 0; i < tileCount; i++)
            {
                var tile = new GameObject("PathTile" + i).transform;
                tile.SetParent(transform, false);

                Transform ground = kit.Create(
                    PrimitiveType.Cube,
                    "Path",
                    tile,
                    StylePalette.CreamPath,
                    new Vector3(0f, -GroundThicknessM * 0.5f, 0f),
                    new Vector3(pathWidth, GroundThicknessM, TileLengthM));

                for (int lane = 0; lane < runnerConfig.LaneCount; lane++)
                {
                    kit.Create(
                        PrimitiveType.Cube,
                        "LaneMarker" + lane,
                        tile,
                        StylePalette.LaneMarker,
                        new Vector3(runnerConfig.LaneCenterX(lane), MarkerLiftM, 0f),
                        new Vector3(DashWidthM, MarkerLiftM, DashLengthM));
                }

                // Verges and ink edges ride with the tile (rigid pieces following the route), so they bend with it.
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform verge = kit.Create(
                        PrimitiveType.Cube,
                        side < 0 ? "VergeLeft" : "VergeRight",
                        tile,
                        StylePalette.JungleFloor,
                        new Vector3(side * (halfPath + VergeWidthM * 0.5f), -GroundThicknessM * 0.5f - MarkerLiftM, 0f),
                        new Vector3(VergeWidthM, GroundThicknessM, TileLengthM));
                    vergeRenderers[i * 2 + (side < 0 ? 0 : 1)] = verge.GetComponent<MeshRenderer>();

                    kit.Create(
                        PrimitiveType.Cube,
                        side < 0 ? "EdgeLeft" : "EdgeRight",
                        tile,
                        StylePalette.Ink,
                        new Vector3(side * (halfPath - EdgeWidthM * 0.5f), MarkerLiftM, 0f),
                        new Vector3(EdgeWidthM, MarkerLiftM, TileLengthM));
                }

                _tiles[i] = tile;
                _tileRenderers[i] = ground.GetComponent<MeshRenderer>();
                _tileIndex[i] = long.MinValue;
            }

            // Own materials, so a world theme can recolor the ground without touching other views' shared colors.
            Material template = RuntimeMaterialTemplates.GetOpaqueTemplate();
            _pathMaterial = new Material(template) { name = "World_Path", color = StylePalette.CreamPath };
            _pathAlternateMaterial = new Material(template) { name = "World_PathAlternate", color = StylePalette.PathAlternate };
            _vergeMaterial = new Material(template) { name = "World_Verge", color = StylePalette.JungleFloor };

            for (int i = 0; i < vergeRenderers.Length; i++)
            {
                vergeRenderers[i].sharedMaterial = _vergeMaterial;
            }
        }

        private void RenderTiles(double z)
        {
            long first = (long)System.Math.Floor((z - TilesBehindM) / TileLengthM);
            for (int i = 0; i < _tiles.Length; i++)
            {
                // Tile index k always sits in slot k mod count, so only the tile that fell behind moves.
                long index = first + i;
                int slot = (int)(((index % _tiles.Length) + _tiles.Length) % _tiles.Length);
                if (_tileIndex[slot] == index)
                {
                    continue;
                }

                _tileIndex[slot] = index;
                PathPlacement.Place(_frame, _tiles[slot], (index + 0.5) * TileLengthM, 0f, 0f);
                _tileRenderers[slot].sharedMaterial = (index & 1L) == 0L ? _pathMaterial : _pathAlternateMaterial;
            }
        }

        // ---- Jungle walls ----

        private bool InitWalls(float viewDistanceM)
        {
            string[] names = EnvironmentArt.JungleWalls;
            var available = new List<string>(names.Length);
            for (int v = 0; v < names.Length; v++)
            {
                if (EnvironmentArt.Exists(names[v]))
                {
                    available.Add(names[v]);
                }
            }

            if (available.Count == 0)
            {
                return false;
            }

            float length = _look.WallSegmentM;
            int declared = available.Count;
            _wallsPerSide = Mathf.CeilToInt((_look.WallBehindM + viewDistanceM + length) / length) + 1;
            int total = _wallsPerSide * 2;
            _walls = new GameObject[total][];
            _wallRoots = new Transform[total];
            _wallIndex = new long[total];
            _wallFlip = new bool[declared];

            var parent = new GameObject("JungleWalls").transform;
            parent.SetParent(transform, false);
            for (int i = 0; i < total; i++)
            {
                Transform root = new GameObject("Wall" + i).transform;
                root.SetParent(parent, false);
                _wallRoots[i] = root;
                _wallIndex[i] = long.MinValue;
                _walls[i] = new GameObject[declared];
                for (int v = 0; v < declared; v++)
                {
                    // Null when the model would not draw (EnvironmentArt verifies it); that variant is then skipped.
                    Transform art = EnvironmentArt.Attach(root, available[v]);
                    if (art == null)
                    {
                        continue;
                    }

                    if (i == 0)
                    {
                        // Segments are authored to extend toward +x from the path edge; turn them around if an
                        // import convention ever mirrors that, so plants never land on the lanes.
                        _wallFlip[v] = ExtendsTowardNegativeX(art);
                    }

                    art.gameObject.SetActive(false);
                    _walls[i][v] = art.gameObject;
                }
            }

            _wallUsable = new int[declared];
            _wallVariants = 0;
            for (int v = 0; v < declared; v++)
            {
                if (_walls[0][v] != null)
                {
                    _wallUsable[_wallVariants++] = v;
                }
            }

            if (_wallVariants == 0)
            {
                Destroy(parent.gameObject);
                _walls = null;
                _wallRoots = null;
                _wallIndex = null;
                return false;
            }

            return true;
        }

        private static bool ExtendsTowardNegativeX(Transform art)
        {
            MeshFilter filter = art.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
            {
                return false;
            }

            Vector3 center = art.InverseTransformPoint(filter.transform.TransformPoint(filter.sharedMesh.bounds.center));
            return center.x < 0f;
        }

        private void RenderWalls(double z)
        {
            if (_walls == null)
            {
                return;
            }

            float length = _look.WallSegmentM;
            long first = (long)System.Math.Floor((z - _look.WallBehindM) / length);
            for (int side = 0; side < 2; side++)
            {
                bool right = side == 1;
                for (int n = 0; n < _wallsPerSide; n++)
                {
                    long index = first + n;
                    int slot = side * _wallsPerSide + (int)(((index % _wallsPerSide) + _wallsPerSide) % _wallsPerSide);
                    long key = index * 2 + side;
                    if (_wallIndex[slot] == key)
                    {
                        continue;
                    }

                    _wallIndex[slot] = key;
                    uint h = EnvironmentArt.Hash(key);
                    int variant = _wallUsable[(int)(h % (uint)_wallVariants)];
                    GameObject[] options = _walls[slot];
                    for (int v = 0; v < options.Length; v++)
                    {
                        if (options[v] != null)
                        {
                            options[v].SetActive(v == variant);
                        }
                    }

                    float yaw = right != _wallFlip[variant] ? 0f : 180f;
                    float mirror = _look.WallMirror && ((h >> 4) & 1u) != 0u ? -1f : 1f;
                    float height = _look.WallHeightScaleMin + ((h >> 8) % 1000u) / 1000f * _look.WallHeightScaleSpan;
                    float x = (right ? 1f : -1f) * (_pathHalfWidthM + _look.WallInsetM);
                    Transform root = _wallRoots[slot];
                    _frame.Sample((index + 0.5) * length, out PathPose pose);
                    root.localPosition = PathPlacement.Point(pose, x, 0f);
                    root.localRotation = PathPlacement.Orientation(pose) * Quaternion.Euler(0f, yaw, 0f);
                    root.localScale = new Vector3(1f, height, mirror);
                }
            }
        }

        // ---- Fallback dressing (single models) ----

        private void InitDressing(float viewDistanceM)
        {
            int variants = 0;
            for (int v = 0; v < DressNames.Length; v++)
            {
                if (EnvironmentArt.Exists(DressNames[v]))
                {
                    variants++;
                }
            }

            if (variants == 0)
            {
                return;
            }

            int tileCount = Mathf.CeilToInt((TilesBehindM + viewDistanceM) / TileLengthM) + 1;
            _dressPerSide = Mathf.CeilToInt(tileCount * TileLengthM / DressSpacingM) + 1;
            int total = _dressPerSide * 2;
            _dress = new GameObject[total][];
            _dressRoots = new Transform[total];
            _dressIndex = new long[total];
            var dressParent = new GameObject("Dressing").transform;
            dressParent.SetParent(transform, false);
            for (int i = 0; i < total; i++)
            {
                Transform root = new GameObject("Dress" + i).transform;
                root.SetParent(dressParent, false);
                _dressRoots[i] = root;
                _dressIndex[i] = long.MinValue;
                _dress[i] = new GameObject[DressNames.Length];
                for (int v = 0; v < DressNames.Length; v++)
                {
                    Transform art = EnvironmentArt.Attach(root, DressNames[v]);
                    if (art != null)
                    {
                        art.gameObject.SetActive(false);
                        _dress[i][v] = art.gameObject;
                    }
                }
            }
        }

        private void RenderDressing(double z)
        {
            if (_dress == null)
            {
                return;
            }

            long first = (long)System.Math.Floor((z - TilesBehindM) / DressSpacingM);
            for (int side = 0; side < 2; side++)
            {
                for (int n = 0; n < _dressPerSide; n++)
                {
                    long index = first + n;
                    int slot = side * _dressPerSide + (int)(((index % _dressPerSide) + _dressPerSide) % _dressPerSide);
                    long key = index * 2 + side;
                    if (_dressIndex[slot] == key)
                    {
                        continue;
                    }

                    _dressIndex[slot] = key;
                    uint h = EnvironmentArt.Hash(key);
                    int variant = (int)(h % (uint)DressNames.Length);
                    GameObject[] options = _dress[slot];
                    for (int v = 0; v < options.Length; v++)
                    {
                        if (options[v] != null)
                        {
                            options[v].SetActive(v == variant);
                        }
                    }

                    // Fall back to the first available variant when the hashed one has no art.
                    if (options[variant] == null)
                    {
                        for (int v = 0; v < options.Length; v++)
                        {
                            if (options[v] != null)
                            {
                                options[v].SetActive(true);
                                break;
                            }
                        }
                    }

                    float inset = DressInsetMinM + ((h >> 8) % 1000u) / 1000f * DressInsetSpanM;
                    float x = (side == 0 ? -1f : 1f) * (_pathHalfWidthM + inset);
                    float yaw = (h >> 20) % 360u;
                    _frame.Sample((index + 0.5) * DressSpacingM, out PathPose pose);
                    _dressRoots[slot].localPosition = PathPlacement.Point(pose, x, 0f);
                    _dressRoots[slot].localRotation = PathPlacement.Orientation(pose) * Quaternion.Euler(0f, yaw, 0f);
                }
            }
        }
    }
}
