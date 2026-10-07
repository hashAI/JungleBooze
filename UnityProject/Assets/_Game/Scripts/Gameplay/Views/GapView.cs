using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box ravines: for every live gap in the <see cref="TrackSimulation"/> ring, an ink slab just above the
    /// ground tiles covers the gap's lanes and length (the lanes <c>HasGround</c> reports as empty; an outer lane's
    /// gap reaches the path edge), and a red-and-ink striped marker lies on the ground just before the near edge
    /// (style guide 4.1: never red on the void itself). Pooled; no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class GapView : MonoBehaviour, IRunView
    {
        private const int Capacity = 16;
        private const float BehindM = 10f;
        private const float PathMarginM = 0.6f;
        private const float SlabTopM = 0.03f;
        private const float SlabThicknessM = 0.06f;
        private const float MarkerDepthM = 0.3f;
        private const float MarkerHeightM = 0.02f;
        private const float MarkerGapToEdgeM = 0.1f;

        private sealed class Slot
        {
            public GameObject Root;
        }

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private float _pathHalfWidthM;
        private Slot[] _slots;
        private Transform[] _slabs;
        private Transform[] _redMarkers;
        private Transform[] _inkMarkers;
        private Transform[] _edgeNear;
        private Transform[] _edgeFar;
        private int _shown;
        private TrackSimulation _track;
        private PathFrame _frame;

        /// <summary>Gaps shown last frame (tests).</summary>
        public int ShownGapCount => _shown;

        /// <summary>The route things are placed on (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM)
        {
            _frame = PathPlacement.OrIdentity(_frame);
            _runnerConfig = runnerConfig;
            _viewDistanceM = viewDistanceM;
            _pathHalfWidthM = runnerConfig.LaneCount * runnerConfig.LaneWidthM * 0.5f + PathMarginM;
            _slots = new Slot[Capacity];
            _slabs = new Transform[Capacity];
            _redMarkers = new Transform[Capacity];
            _inkMarkers = new Transform[Capacity];
            _edgeNear = new Transform[Capacity];
            _edgeFar = new Transform[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                Transform root = new GameObject("Gap" + i).transform;
                root.SetParent(transform, false);
                _slots[i] = new Slot { Root = root.gameObject };
                _slabs[i] = kit.Create(PrimitiveType.Cube, "Void", root, StylePalette.Void).transform;
                _redMarkers[i] = kit.Create(PrimitiveType.Cube, "EdgeMarkerRed", root, StylePalette.HazardRed).transform;
                _inkMarkers[i] = kit.Create(PrimitiveType.Cube, "EdgeMarkerInk", root, StylePalette.Ink).transform;

                // Optional ravine lips (art: lip face at local z=0, 1 m wide and deep, body toward -Z).
                // The near lip sits at the gap start; the far lip is turned around at the gap end.
                _edgeNear[i] = EnvironmentArt.Attach(root, EnvironmentArt.RavineEdge);
                _edgeFar[i] = EnvironmentArt.Attach(root, EnvironmentArt.RavineEdge);
                if (_edgeFar[i] != null)
                {
                    _edgeFar[i].localRotation = Quaternion.Euler(0f, 180f, 0f);
                }

                _slots[i].Root.SetActive(false);
            }
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_slots == null)
            {
                return;
            }

            int used = 0;
            if (_track != null)
            {
                RunnerSimulation runner = session.Runner;
                RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + _viewDistanceM;
                int count = _track.ObstacleCount;
                float laneHalf = _runnerConfig.LaneWidthM * 0.5f;
                for (int i = 0; i < count && used < Capacity; i++)
                {
                    ref readonly ObstacleInstance o = ref _track.GetObstacle(i);
                    if (o.Z > maxZ)
                    {
                        break;
                    }

                    if (o.Archetype != ObstacleArchetype.Gap || o.Z + o.GapLengthM < minZ)
                    {
                        continue;
                    }

                    int lo = LaneMasks.Lowest(o.LaneMask);
                    int hi = LaneMasks.Highest(o.LaneMask);
                    float x0 = lo == 0 ? -_pathHalfWidthM : _runnerConfig.LaneCenterX(lo) - laneHalf;
                    float x1 = hi == LaneMasks.LaneCount - 1 ? _pathHalfWidthM : _runnerConfig.LaneCenterX(hi) + laneHalf;
                    float width = x1 - x0;
                    float cx = (x0 + x1) * 0.5f;
                    float length = o.GapLengthM;

                    // The slab is rigid, so the root sits at the middle of the gap with the route's rotation there;
                    // children are laid out from the gap start (local z = -length / 2) to the gap end (+length / 2).
                    float half = length * 0.5f;
                    PathPlacement.Place(_frame, _slots[used].Root.transform, o.Z + half, 0f, 0f);
                    if (!_slots[used].Root.activeSelf)
                    {
                        _slots[used].Root.SetActive(true);
                    }

                    _slabs[used].localPosition = new Vector3(cx, SlabTopM - SlabThicknessM * 0.5f, 0f);
                    _slabs[used].localScale = new Vector3(width, SlabThicknessM, length);
                    _redMarkers[used].localPosition = new Vector3(cx, MarkerHeightM * 0.5f, -half - (MarkerGapToEdgeM + MarkerDepthM * 0.5f));
                    _redMarkers[used].localScale = new Vector3(width, MarkerHeightM, MarkerDepthM);
                    _inkMarkers[used].localPosition = new Vector3(cx, MarkerHeightM * 0.5f, -half - (MarkerGapToEdgeM + MarkerDepthM * 1.5f));
                    _inkMarkers[used].localScale = new Vector3(width, MarkerHeightM, MarkerDepthM);
                    if (_edgeNear[used] != null)
                    {
                        _edgeNear[used].localPosition = new Vector3(cx, 0f, -half);
                        _edgeNear[used].localScale = new Vector3(width, 1f, 1f);
                        _edgeFar[used].localPosition = new Vector3(cx, 0f, half);
                        _edgeFar[used].localScale = new Vector3(width, 1f, 1f);
                    }

                    used++;
                }
            }

            for (int i = used; i < _shown; i++)
            {
                _slots[i].Root.SetActive(false);
            }

            _shown = used;
        }
    }
}
