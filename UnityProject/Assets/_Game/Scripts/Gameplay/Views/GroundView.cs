using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box ground: a cream path made of flat tiles that are recycled ahead of HERO, a dashed marker down the
    /// center of each lane, ink path edges and green jungle verges. The tiles alternate two path tones so forward
    /// motion is readable. Stage B's track view replaces the path tiles with chunk geometry (gaps); the lane markers
    /// and verges can stay. No allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class GroundView : MonoBehaviour, IRunView
    {
        // Gray-box geometry (not tuning; replaced by real track art).
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

        private Transform[] _tiles;
        private MeshRenderer[] _tileRenderers;
        private long[] _tileIndex;
        private Material _pathMaterial;
        private Material _pathAlternateMaterial;
        private Material _vergeMaterial;
        private Transform _strips;

        // Foliage dressing along the verges (optional art; recycled like the tiles, deterministic by slot index).
        private const float DressSpacingM = 9f;
        private const float DressInsetMinM = 1.5f;
        private const float DressInsetSpanM = 9f;
        private static readonly string[] DressNames = { EnvironmentArt.TreeA, EnvironmentArt.TreeB, EnvironmentArt.Bush };
        private GameObject[][] _dress;
        private Transform[] _dressRoots;
        private long[] _dressIndex;
        private int _dressPerSide;
        private float _pathHalfWidthM;

        /// <summary>Number of recycled path tiles.</summary>
        public int TileCount => _tiles == null ? 0 : _tiles.Length;

        /// <summary>Builds the tiles. <paramref name="viewDistanceM"/> = how far ahead the ground must reach (fog end).</summary>
        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM)
        {
            float pathWidth = runnerConfig.LaneCount * runnerConfig.LaneWidthM + 2f * PathMarginM;
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

            // Real path art (authored 1 m wide by 1 m long, top at y=0, scaled to the tile): replaces the cube.
            Transform firstArt = EnvironmentArt.Attach(_tiles[0], EnvironmentArt.PathTile);
            if (firstArt != null)
            {
                firstArt.localScale = new Vector3(pathWidth, 1f, TileLengthM);
                _tileRenderers[0].enabled = false;
                for (int i = 1; i < tileCount; i++)
                {
                    Transform art = EnvironmentArt.Attach(_tiles[i], EnvironmentArt.PathTile);
                    art.localScale = new Vector3(pathWidth, 1f, TileLengthM);
                    _tileRenderers[i].enabled = false;
                }
            }

            _pathHalfWidthM = pathWidth * 0.5f;
            InitDressing(tileCount);

            // Own materials, so a world theme can recolor the ground without touching other views' shared colors.
            Material template = _tileRenderers[0].sharedMaterial;
            _pathMaterial = new Material(template) { name = "World_Path", color = StylePalette.CreamPath };
            _pathAlternateMaterial = new Material(template) { name = "World_PathAlternate", color = StylePalette.PathAlternate };
            _vergeMaterial = new Material(template) { name = "World_Verge", color = StylePalette.JungleGreen };

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
                    StylePalette.JungleGreen,
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

        /// <summary>Recolors the path tiles and the verges (world themes, GDD 9). Allocation free.</summary>
        public void ApplyTheme(in WorldTheme theme)
        {
            if (_pathMaterial == null)
            {
                return;
            }

            _pathMaterial.color = theme.Path;
            _pathAlternateMaterial.color = theme.PathAlternate;
            _vergeMaterial.color = theme.Verge;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_pathMaterial);
            DestroyMaterial(_pathAlternateMaterial);
            DestroyMaterial(_vergeMaterial);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void InitDressing(int tileCount)
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
                    float yaw = ((h >> 20) % 360u);
                    _dressRoots[slot].localPosition = new Vector3(x, 0f, (float)((index + 0.5) * DressSpacingM));
                    _dressRoots[slot].localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
        }

        public void BeginRun(GameSession session)
        {
            for (int i = 0; i < _tileIndex.Length; i++)
            {
                _tileIndex[i] = long.MinValue;
            }

            if (_dressIndex != null)
            {
                for (int i = 0; i < _dressIndex.Length; i++)
                {
                    _dressIndex[i] = long.MinValue;
                }
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_tiles == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double z);

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
            RenderDressing(z);
        }
    }
}
