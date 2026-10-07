using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The ground and the verge dressing.
    /// <para>Trail mode (when <c>Ground_Trail_basecolor</c> exists): one long trail mesh with a stylized dirt texture
    /// (painted lane wear, pebbles and a grassy edge; repeats every <see cref="EnvironmentLookConfig.GroundTileM"/> along
    /// the run) and two jungle-floor strips, all moved forward in whole texture repeats so the texture stays put on the
    /// ground and there are never gaps. Three draw calls.</para>
    /// <para>Gray-box mode (no trail texture): the cream tiles with lane dashes, ink edges and flat verges.</para>
    /// <para>Verges: pre-merged 12 m jungle wall segments (<c>Jungle_WallA/B/C</c>, one draw call each) recycled on both
    /// sides and picked by <see cref="EnvironmentArt.Hash"/>; without them, single trees and bushes every 9 m.</para>
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
        private const float StripLengthM = 160f;
        private const float StripBehindM = 20f;
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

        // Gray-box mode.
        private Transform[] _tiles;
        private MeshRenderer[] _tileRenderers;
        private long[] _tileIndex;
        private Transform _strips;

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

        /// <summary>Builds the ground with the default look. <paramref name="viewDistanceM"/> = how far ahead it must reach (fog end).</summary>
        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM)
        {
            Init(kit, runnerConfig, viewDistanceM, null);
        }

        /// <summary>Builds the ground and dressing. <paramref name="look"/> null = defaults.</summary>
        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM, EnvironmentLookConfig look)
        {
            _look = look ?? EnvironmentLookConfig.CreateDefault();
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
                // Move in whole texture repeats so the dirt and the floor stay fixed on the ground.
                double tile = _look.GroundTileM;
                long step = (long)System.Math.Floor(z / tile);
                if (step != _groundStep)
                {
                    _groundStep = step;
                    _groundRoot.localPosition = new Vector3(0f, 0f, (float)(step * tile));
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

            _groundRoot = new GameObject("Ground").transform;
            _groundRoot.SetParent(transform, false);

            _trailMesh = BuildStrips("Trail", behind, ahead, tile, 0f, -halfWidth, halfWidth, 1f / (2f * halfWidth), halfWidth, false);
            AddRenderer("Trail", _trailMesh, _pathMaterial);

            _floorMesh = BuildStrips("JungleFloor", behind, ahead, tile, -_look.FloorDropM, _look.FloorInnerM, _look.FloorOuterM, 1f / tile, 0f, true);
            AddRenderer("JungleFloor", _floorMesh, _vergeMaterial);
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
        /// Flat strip from x0 to x1 (and, when <paramref name="mirrored"/>, the same strip at -x1..-x0), split every
        /// <paramref name="tile"/> m from -behind to +ahead tiles. UV: u = (x + uOffset) * uScale, v = z / tile, so whole
        /// tile moves leave the texture in place. Faces up. Setup-time only.
        /// </summary>
        private static Mesh BuildStrips(
            string name, int behind, int ahead, float tile, float y, float x0, float x1, float uScale, float uOffset, bool mirrored)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            AddStrip(vertices, uvs, normals, triangles, behind, ahead, tile, y, x0, x1, uScale, uOffset);
            if (mirrored)
            {
                AddStrip(vertices, uvs, normals, triangles, behind, ahead, tile, y, -x1, -x0, uScale, uOffset);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddStrip(
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals, List<int> triangles,
            int behind, int ahead, float tile, float y, float x0, float x1, float uScale, float uOffset)
        {
            int start = vertices.Count;
            for (int row = -behind; row <= ahead; row++)
            {
                float z = row * tile;
                vertices.Add(new Vector3(x0, y, z));
                vertices.Add(new Vector3(x1, y, z));
                uvs.Add(new Vector2((x0 + uOffset) * uScale, row));
                uvs.Add(new Vector2((x1 + uOffset) * uScale, row));
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
            }

            int rows = behind + ahead;
            for (int r = 0; r < rows; r++)
            {
                // a = (x0, z), b = (x1, z), c = (x1, z + tile), d = (x0, z + tile); clockwise seen from above.
                int a = start + r * 2;
                int b = a + 1;
                int c = a + 3;
                int d = a + 2;
                triangles.Add(a);
                triangles.Add(d);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
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

                _tiles[i] = tile;
                _tileRenderers[i] = ground.GetComponent<MeshRenderer>();
                _tileIndex[i] = long.MinValue;
            }

            // Own materials, so a world theme can recolor the ground without touching other views' shared colors.
            Material template = _tileRenderers[0].sharedMaterial;
            _pathMaterial = new Material(template) { name = "World_Path", color = StylePalette.CreamPath };
            _pathAlternateMaterial = new Material(template) { name = "World_PathAlternate", color = StylePalette.PathAlternate };
            _vergeMaterial = new Material(template) { name = "World_Verge", color = StylePalette.JungleFloor };

            // Long strips that follow HERO (uniform color, so following shows no motion): path edges and verges.
            _strips = new GameObject("Strips").transform;
            _strips.SetParent(transform, false);
            float halfPath = pathWidth * 0.5f;
            float stripCenterZ = StripLengthM * 0.5f - StripBehindM;
            for (int side = -1; side <= 1; side += 2)
            {
                Transform verge = kit.Create(
                    PrimitiveType.Cube,
                    side < 0 ? "VergeLeft" : "VergeRight",
                    _strips,
                    StylePalette.JungleFloor,
                    new Vector3(side * (halfPath + VergeWidthM * 0.5f), -GroundThicknessM * 0.5f - MarkerLiftM, stripCenterZ),
                    new Vector3(VergeWidthM, GroundThicknessM, StripLengthM));
                verge.GetComponent<MeshRenderer>().sharedMaterial = _vergeMaterial;

                kit.Create(
                    PrimitiveType.Cube,
                    side < 0 ? "EdgeLeft" : "EdgeRight",
                    _strips,
                    StylePalette.Ink,
                    new Vector3(side * (halfPath - EdgeWidthM * 0.5f), MarkerLiftM, stripCenterZ),
                    new Vector3(EdgeWidthM, MarkerLiftM, StripLengthM));
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
                _tiles[slot].localPosition = new Vector3(0f, 0f, (float)((index + 0.5) * TileLengthM));
                _tileRenderers[slot].sharedMaterial = (index & 1L) == 0L ? _pathMaterial : _pathAlternateMaterial;
            }

            _strips.localPosition = new Vector3(0f, 0f, (float)z);
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
            _wallVariants = available.Count;
            _wallsPerSide = Mathf.CeilToInt((_look.WallBehindM + viewDistanceM + length) / length) + 1;
            int total = _wallsPerSide * 2;
            _walls = new GameObject[total][];
            _wallRoots = new Transform[total];
            _wallIndex = new long[total];
            _wallFlip = new bool[_wallVariants];

            var parent = new GameObject("JungleWalls").transform;
            parent.SetParent(transform, false);
            for (int i = 0; i < total; i++)
            {
                Transform root = new GameObject("Wall" + i).transform;
                root.SetParent(parent, false);
                _wallRoots[i] = root;
                _wallIndex[i] = long.MinValue;
                _walls[i] = new GameObject[_wallVariants];
                for (int v = 0; v < _wallVariants; v++)
                {
                    Transform art = EnvironmentArt.Attach(root, available[v]);
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
                    int variant = (int)(h % (uint)_wallVariants);
                    GameObject[] options = _walls[slot];
                    for (int v = 0; v < options.Length; v++)
                    {
                        options[v].SetActive(v == variant);
                    }

                    float yaw = right != _wallFlip[variant] ? 0f : 180f;
                    float mirror = _look.WallMirror && ((h >> 4) & 1u) != 0u ? -1f : 1f;
                    float height = _look.WallHeightScaleMin + ((h >> 8) % 1000u) / 1000f * _look.WallHeightScaleSpan;
                    float x = (right ? 1f : -1f) * (_pathHalfWidthM + _look.WallInsetM);
                    Transform root = _wallRoots[slot];
                    root.localPosition = new Vector3(x, 0f, (float)((index + 0.5) * length));
                    root.localRotation = Quaternion.Euler(0f, yaw, 0f);
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
                    _dressRoots[slot].localPosition = new Vector3(x, 0f, (float)((index + 0.5) * DressSpacingM));
                    _dressRoots[slot].localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
        }
    }
}
