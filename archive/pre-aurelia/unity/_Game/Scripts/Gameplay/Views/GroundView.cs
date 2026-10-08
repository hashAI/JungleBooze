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
        private Transform _strips;

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

            _pathMaterial = _tileRenderers[0].sharedMaterial;
            _pathAlternateMaterial = kit.GetMaterial(StylePalette.PathAlternate, _pathMaterial);

            // Long strips that follow HERO (uniform color, so following shows no motion): path edges and verges.
            _strips = new GameObject("Strips").transform;
            _strips.SetParent(transform, false);
            float halfPath = pathWidth * 0.5f;
            float stripCenterZ = StripLengthM * 0.5f - StripBehindM;
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Create(
                    PrimitiveType.Cube,
                    side < 0 ? "VergeLeft" : "VergeRight",
                    _strips,
                    StylePalette.JungleGreen,
                    new Vector3(side * (halfPath + VergeWidthM * 0.5f), -GroundThicknessM * 0.5f - MarkerLiftM, stripCenterZ),
                    new Vector3(VergeWidthM, GroundThicknessM, StripLengthM));

                kit.Create(
                    PrimitiveType.Cube,
                    side < 0 ? "EdgeLeft" : "EdgeRight",
                    _strips,
                    StylePalette.Ink,
                    new Vector3(side * (halfPath - EdgeWidthM * 0.5f), MarkerLiftM, stripCenterZ),
                    new Vector3(EdgeWidthM, MarkerLiftM, StripLengthM));
            }
        }

        public void BeginRun(GameSession session)
        {
            for (int i = 0; i < _tileIndex.Length; i++)
            {
                _tileIndex[i] = long.MinValue;
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
        }
    }
}
