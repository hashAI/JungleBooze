using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The High layer of the route (spec 003 section 7): while <see cref="PathLayer.High"/> the ground trail is skinned
    /// as a bough. A pool of rigid 8 m pieces (a wood-brown limb with a leafy rim on each edge, 7.2 m wide in all) is
    /// laid on the route wherever the layer is High; a dark forest-floor plane far below the path and a few warm light
    /// shafts give the "ground far below" feeling; the ground skin of <see cref="GroundView"/> is hidden while the
    /// bough is under the runner. Gaps (the ink slabs of <see cref="GapView"/> stay) get pale cut ends on both sides, so
    /// they read as a broken limb. Presentation only: it reads the route and the track and changes neither. Gray-box,
    /// procedural, pooled, no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class BoughView : MonoBehaviour, IRunView
    {
        private const float PieceLengthM = 8f;
        private const float PieceOverlapM = 1f;
        private const float BehindM = 16f;
        private const float MaxViewM = 72f;
        private const float LimbWidthM = 5.6f;
        private const float LimbThicknessM = 1.2f;
        private const float LimbTopM = 0.012f;
        private const float RimWidthM = 0.8f;
        private const float RimHeightM = 0.44f;
        private const float RimTopM = 0.3f;
        private const float SkinLeadM = 20f;
        private const float BelowM = 24f;
        private const float PlaneSizeM = 700f;
        private const int ShaftCount = 3;
        private const float ShaftSpacingM = 50f;
        private const float ShaftWidthM = 3.4f;
        private const float ShaftHeightM = 36f;
        private const float ShaftCenterM = -6f;
        private const int GapCapacity = 16;
        private const float GapLookBehindM = 10f;
        private const float PathMarginM = 0.6f;
        private const float CutLengthM = 0.4f;
        private const float CutHeightM = 0.04f;

        private static readonly Color LimbColor = new Color(0.35f, 0.27f, 0.20f, 1f);
        private static readonly Color RimColor = new Color(0.23f, 0.50f, 0.25f, 1f);
        private static readonly Color CutColor = new Color(0.82f, 0.68f, 0.45f, 1f);
        private static readonly Color BelowColor = new Color(0.06f, 0.15f, 0.13f, 1f);
        private static readonly Color ShaftColor = new Color(1f, 0.83f, 0.48f, 0.16f);

        private PathFrame _frame;
        private GroundView _ground;
        private TrackSimulation _track;
        private RunnerConfig _runnerConfig;
        private float _pathHalfWidthM;
        private float _viewM;

        private Transform[] _pieces;
        private Transform[] _limbs;
        private Transform[] _rimLeft;
        private Transform[] _rimRight;
        private bool[] _pieceActive;
        private long[] _pieceIndex;

        private Transform _below;
        private bool _belowActive;

        private Transform[] _shafts;
        private bool[] _shaftActive;
        private Material _shaftMaterial;

        private Transform[] _cutNear;
        private Transform[] _cutFar;
        private int _cutShown;
        private bool _high;

        /// <summary>True while the ground skin is replaced by the bough (the route ahead of the runner is High). For tests and tools.</summary>
        public bool HighActive => _high;

        /// <summary>The route the bough follows (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        /// <summary>Builds the pools. <paramref name="ground"/> is hidden while the bough stands in for it (may be null).</summary>
        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM, GroundView ground)
        {
            _frame = PathPlacement.OrIdentity(_frame);
            _ground = ground;
            _runnerConfig = runnerConfig;
            _pathHalfWidthM = (runnerConfig.LaneCount * runnerConfig.LaneWidthM * 0.5f) + PathMarginM;
            _viewM = Mathf.Min(viewDistanceM, MaxViewM);

            int count = Mathf.CeilToInt((BehindM + _viewM) / PieceLengthM) + 2;
            _pieces = new Transform[count];
            _limbs = new Transform[count];
            _rimLeft = new Transform[count];
            _rimRight = new Transform[count];
            _pieceActive = new bool[count];
            _pieceIndex = new long[count];
            float pieceLength = PieceLengthM + PieceOverlapM;
            float rimX = (LimbWidthM + RimWidthM) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Transform root = new GameObject("BoughPiece" + i).transform;
                root.SetParent(transform, false);
                _pieces[i] = root;
                _limbs[i] = kit.Create(
                    PrimitiveType.Cube,
                    "Limb",
                    root,
                    LimbColor,
                    new Vector3(0f, LimbTopM - (LimbThicknessM * 0.5f), 0f),
                    new Vector3(LimbWidthM, LimbThicknessM, pieceLength));
                _rimLeft[i] = kit.Create(
                    PrimitiveType.Cube,
                    "RimLeft",
                    root,
                    RimColor,
                    new Vector3(-rimX, RimTopM - (RimHeightM * 0.5f), 0f),
                    new Vector3(RimWidthM, RimHeightM, pieceLength));
                _rimRight[i] = kit.Create(
                    PrimitiveType.Cube,
                    "RimRight",
                    root,
                    RimColor,
                    new Vector3(rimX, RimTopM - (RimHeightM * 0.5f), 0f),
                    new Vector3(RimWidthM, RimHeightM, pieceLength));
                _pieceIndex[i] = long.MinValue;
                root.gameObject.SetActive(false);
            }

            // The forest floor far below: one dark plane that follows the runner (it is plain colour, so it has no parallax to get wrong).
            _below = kit.Create(
                PrimitiveType.Quad,
                "ForestFloorBelow",
                transform,
                BelowColor,
                Vector3.zero,
                new Vector3(PlaneSizeM, PlaneSizeM, 1f));
            _below.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _below.gameObject.SetActive(false);

            // Light shafts: alpha-blended warm cards, no shadow maps, no bloom.
            Material template = RuntimeMaterialTemplates.GetTransparentTemplate();
            _shaftMaterial = new Material(template) { name = "Bough_LightShaft", color = ShaftColor };
            _shafts = new Transform[ShaftCount];
            _shaftActive = new bool[ShaftCount];
            for (int i = 0; i < ShaftCount; i++)
            {
                GameObject shaft = PrimitiveMeshes.Create(PrimitiveType.Quad, "LightShaft" + i, transform, _shaftMaterial);
                shaft.transform.localScale = new Vector3(ShaftWidthM, ShaftHeightM, 1f);
                shaft.SetActive(false);
                _shafts[i] = shaft.transform;
            }

            // Pale cut ends on both sides of every gap that lies on the bough.
            _cutNear = new Transform[GapCapacity];
            _cutFar = new Transform[GapCapacity];
            for (int i = 0; i < GapCapacity; i++)
            {
                _cutNear[i] = kit.Create(PrimitiveType.Cube, "CutNear" + i, transform, CutColor);
                _cutFar[i] = kit.Create(PrimitiveType.Cube, "CutFar" + i, transform, CutColor);
                _cutNear[i].gameObject.SetActive(false);
                _cutFar[i].gameObject.SetActive(false);
            }
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            if (_pieceIndex != null)
            {
                for (int i = 0; i < _pieceIndex.Length; i++)
                {
                    _pieceIndex[i] = long.MinValue;
                }
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_pieces == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);

            _frame.Sample(heroZ + SkinLeadM, out PathPose lead);
            bool high = lead.Layer == PathLayer.High;
            _high = high;
            if (_ground != null)
            {
                _ground.SetPathSkinHidden(high);
            }

            RenderPieces(heroZ);
            RenderBelow(heroZ, high);
            RenderShafts(heroZ);
            RenderGapCuts(heroZ);
        }

        private void RenderPieces(double heroZ)
        {
            int n = _pieces.Length;
            long first = (long)System.Math.Floor((heroZ - BehindM) / PieceLengthM);
            for (int i = 0; i < n; i++)
            {
                long index = first + i;
                int slot = (int)(((index % n) + n) % n);
                _frame.Sample((index + 0.5) * PieceLengthM, out PathPose pose);
                bool show = pose.Layer == PathLayer.High;
                if (show != _pieceActive[slot])
                {
                    _pieceActive[slot] = show;
                    _pieces[slot].gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                if (_pieceIndex[slot] != index)
                {
                    _pieceIndex[slot] = index;
                    DressPiece(slot, index);
                }

                _pieces[slot].localPosition = pose.Center;
                _pieces[slot].localRotation = PathPlacement.Orientation(pose);
            }
        }

        /// <summary>Small stable variation per piece (width, yaw, rim height) so the limb looks woven. No allocation.</summary>
        private void DressPiece(int slot, long index)
        {
            uint h = Hash(index);
            float yaw = (((h & 0xFFu) / 255f) - 0.5f) * 4f;
            float width = LimbWidthM + ((((h >> 8) & 0xFu) / 15f) - 0.5f) * 0.4f;
            float rimHeight = RimHeightM + (((h >> 12) & 0xFu) / 15f) * 0.2f;
            float pieceLength = PieceLengthM + PieceOverlapM;

            _limbs[slot].localRotation = Quaternion.Euler(0f, yaw, 0f);
            _limbs[slot].localScale = new Vector3(width, LimbThicknessM, pieceLength);
            float rimX = (LimbWidthM + RimWidthM) * 0.5f;
            _rimLeft[slot].localPosition = new Vector3(-rimX, RimTopM + (rimHeight - RimHeightM) - (rimHeight * 0.5f), 0f);
            _rimLeft[slot].localScale = new Vector3(RimWidthM, rimHeight, pieceLength);
            _rimRight[slot].localPosition = new Vector3(rimX, RimTopM + (rimHeight - RimHeightM) - (rimHeight * 0.5f), 0f);
            _rimRight[slot].localScale = new Vector3(RimWidthM, rimHeight, pieceLength);
        }

        private void RenderBelow(double heroZ, bool high)
        {
            if (high != _belowActive)
            {
                _belowActive = high;
                _below.gameObject.SetActive(high);
            }

            if (!high)
            {
                return;
            }

            _frame.Sample(heroZ, out PathPose pose);
            Vector3 c = pose.Center;
            _below.localPosition = new Vector3(c.x, c.y - BelowM, c.z);
        }

        private void RenderShafts(double heroZ)
        {
            long first = (long)System.Math.Floor((heroZ - 10.0) / ShaftSpacingM);
            for (int i = 0; i < ShaftCount; i++)
            {
                long index = first + i;
                int slot = (int)(((index % ShaftCount) + ShaftCount) % ShaftCount);
                uint h = Hash(index + 7919L);
                double s = ((index + 0.25) * ShaftSpacingM) + (((h & 0xFFu) / 255f) * 0.5f * ShaftSpacingM);
                _frame.Sample(s, out PathPose pose);
                bool show = pose.Layer == PathLayer.High;
                if (show != _shaftActive[slot])
                {
                    _shaftActive[slot] = show;
                    _shafts[slot].gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                float side = (h & 0x100u) == 0u ? -1f : 1f;
                float lateral = side * (4.5f + (((h >> 9) & 0xFFu) / 255f) * 4f);
                _shafts[slot].localPosition = PathPlacement.Point(pose, lateral, ShaftCenterM);
                _shafts[slot].localRotation = PathPlacement.Orientation(pose);
            }
        }

        private void RenderGapCuts(double heroZ)
        {
            int used = 0;
            if (_track != null)
            {
                double minZ = heroZ - GapLookBehindM;
                double maxZ = heroZ + _viewM;
                float laneHalf = _runnerConfig.LaneWidthM * 0.5f;
                int count = _track.ObstacleCount;
                for (int i = 0; i < count && used < GapCapacity; i++)
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

                    _frame.Sample(o.Z + (o.GapLengthM * 0.5f), out PathPose mid);
                    if (mid.Layer != PathLayer.High)
                    {
                        continue;
                    }

                    int lo = LaneMasks.Lowest(o.LaneMask);
                    int hi = LaneMasks.Highest(o.LaneMask);
                    float x0 = lo == 0 ? -_pathHalfWidthM : _runnerConfig.LaneCenterX(lo) - laneHalf;
                    float x1 = hi == LaneMasks.LaneCount - 1 ? _pathHalfWidthM : _runnerConfig.LaneCenterX(hi) + laneHalf;
                    float width = x1 - x0;
                    float cx = (x0 + x1) * 0.5f;

                    PlaceCut(_cutNear[used], o.Z - (CutLengthM * 0.5f), cx, width);
                    PlaceCut(_cutFar[used], o.Z + o.GapLengthM + (CutLengthM * 0.5f), cx, width);
                    used++;
                }
            }

            for (int i = used; i < _cutShown; i++)
            {
                _cutNear[i].gameObject.SetActive(false);
                _cutFar[i].gameObject.SetActive(false);
            }

            _cutShown = used;
        }

        private void PlaceCut(Transform cut, double s, float x, float width)
        {
            if (!cut.gameObject.activeSelf)
            {
                cut.gameObject.SetActive(true);
            }

            PathPlacement.Place(_frame, cut, s, x, CutHeightM * 0.5f);
            cut.localScale = new Vector3(width, CutHeightM, CutLengthM);
        }

        private static uint Hash(long index)
        {
            unchecked
            {
                uint x = (uint)index * 2654435761u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return x;
            }
        }

        private void OnDestroy()
        {
            if (_shaftMaterial != null)
            {
                Destroy(_shaftMaterial);
            }
        }
    }
}
