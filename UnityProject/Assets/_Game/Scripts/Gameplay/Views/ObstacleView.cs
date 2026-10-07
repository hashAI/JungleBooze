using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box obstacles (style guide 4.1): each frame it polls the <see cref="TrackSimulation"/> obstacle ring and
    /// lays pooled primitive pieces over the live boxes, one piece per occupied lane. Bodies are dark wood or stone;
    /// hazard red appears only as the archetype's marker band, always next to an ink band (never color-only). Pieces
    /// are sized from the kit's <see cref="ObstacleShape"/>, so what you see is the hitbox. Gaps are drawn by
    /// <see cref="GapView"/>. Each piece keeps its slot for as long as its obstacle (and lane) stays in view, found by
    /// obstacle id, so pieces are not toggled or re-laid out as other obstacles come and go.
    /// No allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class ObstacleView : MonoBehaviour, IRunView
    {
        private const int PieceCapacity = 64;
        private const float BehindM = 10f;
        private const float BandHeightM = 0.2f;
        private const float StripeHeightM = 0.1f;
        private const float BandOverhangM = 0.01f;

        private sealed class Piece
        {
            public GameObject Root;
            public Transform Cube;
            public Transform Sphere;
            public Transform Band;
            public Transform Stripe;
            public ObstacleArchetype Kind;
            public MeshRenderer CubeRenderer;
            public MeshRenderer SphereRenderer;
            public GameObject[] Art;
            public int ObstacleId;
            public int Lane;
            public bool Live;
            public int Stamp;
        }

        private const int ArchetypeSlots = 8;

        /// <summary>Largest per-axis factor the boulder art may be scaled up by to fill its box.</summary>
        private const float MaxArtFit = 2.5f;

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private Piece[] _pieces;
        private int _shown;
        private int _renderStamp;
        private TrackSimulation _track;
        private PathFrame _frame;
        private Material _bodyMaterial;
        private Material _moverMaterial;

        /// <summary>Pieces shown last frame (tests).</summary>
        public int ShownPieceCount => _shown;

        /// <summary>
        /// Debug aid: true when a piece for <paramref name="obstacleId"/> is shown this frame with at least one enabled
        /// renderer (gray-box or verified art). Allocates; call once per death at most.
        /// </summary>
        public bool IsDrawn(int obstacleId)
        {
            for (int i = 0; _pieces != null && i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                if (!piece.Live || piece.ObstacleId != obstacleId || !piece.Root.activeInHierarchy)
                {
                    continue;
                }

                Renderer[] renderers = piece.Root.GetComponentsInChildren<Renderer>(false);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r].enabled && renderers[r].sharedMaterial != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

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
            _pieces = new Piece[PieceCapacity];
            for (int i = 0; i < PieceCapacity; i++)
            {
                var piece = new Piece();
                Transform root = new GameObject("Obstacle" + i).transform;
                root.SetParent(transform, false);
                piece.Root = root.gameObject;
                piece.Cube = kit.Create(PrimitiveType.Cube, "Body", root, StylePalette.HazardWood).transform;
                piece.Sphere = kit.Create(PrimitiveType.Sphere, "Boulder", root, StylePalette.HazardStone).transform;
                piece.Band = kit.Create(PrimitiveType.Cube, "HazardBand", root, StylePalette.HazardRed).transform;
                piece.Stripe = kit.Create(PrimitiveType.Cube, "InkStripe", root, StylePalette.Ink).transform;
                piece.Kind = ObstacleArchetype.None;
                piece.CubeRenderer = piece.Cube.GetComponent<MeshRenderer>();
                piece.SphereRenderer = piece.Sphere.GetComponent<MeshRenderer>();
                piece.Art = new GameObject[ArchetypeSlots];
                AttachArt(piece, ObstacleArchetype.LowBarrier, piece.Cube, EnvironmentArt.LowBarrier);
                AttachArt(piece, ObstacleArchetype.HighBarrier, piece.Cube, EnvironmentArt.HighBarrier);
                AttachArt(piece, ObstacleArchetype.FullBlock, piece.Cube, EnvironmentArt.FullBlock);
                AttachArt(piece, ObstacleArchetype.Mover, piece.Sphere, EnvironmentArt.Boulder);
                piece.Root.SetActive(false);
                _pieces[i] = piece;
            }

            // Own body materials, so a world theme can tint the obstacles without touching other views' shared colors.
            _bodyMaterial = new Material(_pieces[0].CubeRenderer.sharedMaterial) { name = "World_ObstacleBody" };
            _moverMaterial = new Material(_pieces[0].SphereRenderer.sharedMaterial) { name = "World_MoverBody" };
            for (int i = 0; i < PieceCapacity; i++)
            {
                _pieces[i].CubeRenderer.sharedMaterial = _bodyMaterial;
                _pieces[i].SphereRenderer.sharedMaterial = _moverMaterial;
            }
        }

        /// <summary>Tints the obstacle and mover bodies (world themes, GDD 9). Allocation free.</summary>
        public void ApplyTheme(in WorldTheme theme)
        {
            if (_bodyMaterial == null)
            {
                return;
            }

            _bodyMaterial.color = theme.ObstacleBody;
            _moverMaterial.color = theme.MoverBody;
        }

        private void OnDestroy()
        {
            if (_bodyMaterial != null)
            {
                Destroy(_bodyMaterial);
            }

            if (_moverMaterial != null)
            {
                Destroy(_moverMaterial);
            }
        }

        /// <summary>
        /// Real art for one archetype, parented to the gray-box body that Place() already scales to the hitbox
        /// (art is authored to fit a unit cube or a unit sphere). Missing prefab: the gray-box stays.
        /// </summary>
        private static void AttachArt(Piece piece, ObstacleArchetype kind, Transform body, string prefabName)
        {
            Transform art = EnvironmentArt.Attach(body, prefabName);
            if (art == null)
            {
                return;
            }

            if (kind == ObstacleArchetype.Mover)
            {
                FitArtToBox(art);
            }

            art.gameObject.SetActive(false);
            piece.Art[(int)kind] = art.gameObject;
        }

        /// <summary>
        /// Visual only [ASSUMED]: scales the boulder art UP (never down, at most <see cref="MaxArtFit"/>) so its
        /// mesh bounds fill the unit box that the body transform stretches to the hitbox, and centres it there. The
        /// simulation box is untouched. Setup-time only.
        /// </summary>
        private static void FitArtToBox(Transform art)
        {
            Transform space = art.parent;
            if (space == null)
            {
                return;
            }

            bool has = false;
            Bounds total = default;
            MeshFilter[] filters = art.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null || mesh.vertexCount == 0)
                {
                    continue;
                }

                Bounds mb = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(
                        mb.center.x + ((corner & 1) == 0 ? -mb.extents.x : mb.extents.x),
                        mb.center.y + ((corner & 2) == 0 ? -mb.extents.y : mb.extents.y),
                        mb.center.z + ((corner & 4) == 0 ? -mb.extents.z : mb.extents.z));
                    Vector3 p = space.InverseTransformPoint(filters[i].transform.TransformPoint(local));
                    if (!has)
                    {
                        total = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        total.Encapsulate(p);
                    }
                }
            }

            Vector3 size = total.size;
            if (!has || !(size.x > 0.01f) || !(size.y > 0.01f) || !(size.z > 0.01f))
            {
                return;
            }

            var fit = new Vector3(
                Mathf.Clamp(1f / size.x, 1f, MaxArtFit),
                Mathf.Clamp(1f / size.y, 1f, MaxArtFit),
                Mathf.Clamp(1f / size.z, 1f, MaxArtFit));
            art.localScale = Vector3.Scale(art.localScale, fit);
            art.localPosition = art.localPosition - Vector3.Scale(total.center, fit);
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            for (int i = 0; _pieces != null && i < _pieces.Length; i++)
            {
                // New run: ids start over, so no piece may carry a slot from the last one.
                if (_pieces[i].Live)
                {
                    _pieces[i].Live = false;
                    _pieces[i].Root.SetActive(false);
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

            _renderStamp++;
            if (_track != null)
            {
                RunnerSimulation runner = session.Runner;
                RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + _viewDistanceM;
                int count = _track.ObstacleCount;
                for (int i = 0; i < count; i++)
                {
                    ref readonly ObstacleInstance o = ref _track.GetObstacle(i);
                    if (o.Z > maxZ)
                    {
                        break;
                    }

                    if (o.Archetype == ObstacleArchetype.Gap || o.BackZ < minZ)
                    {
                        continue;
                    }

                    if (o.Archetype == ObstacleArchetype.LaneDenial || o.Archetype == ObstacleArchetype.LaneStrike)
                    {
                        // Signature hazards are drawn by HazardView.
                        continue;
                    }

                    ObstacleShape shape = _track.Kit.GetShape(o.Archetype);
                    if (o.Archetype == ObstacleArchetype.Mover)
                    {
                        float x = Mathf.Lerp(o.MoverXPrev, o.MoverX, alpha);
                        PlaceSlot(o, -1, shape, x);
                        continue;
                    }

                    for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
                    {
                        if (LaneMasks.Contains(o.LaneMask, lane))
                        {
                            PlaceSlot(o, lane, shape, _runnerConfig.LaneCenterX(lane));
                        }
                    }
                }
            }

            // Whatever was not claimed this frame has left the view.
            int live = 0;
            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                if (!piece.Live)
                {
                    continue;
                }

                if (piece.Stamp != _renderStamp)
                {
                    piece.Live = false;
                    piece.Root.SetActive(false);
                    continue;
                }

                live++;
            }

            _shown = live;
        }

        /// <summary>
        /// Finds the piece already showing (obstacle id, lane) and updates it; otherwise takes a free piece (not yet
        /// claimed this frame, preferably hidden). Drops the obstacle when all pieces are taken.
        /// </summary>
        private void PlaceSlot(in ObstacleInstance o, int lane, ObstacleShape shape, float x)
        {
            Piece found = null;
            Piece free = null;
            Piece reusable = null;
            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                if (piece.Live && piece.ObstacleId == o.Id && piece.Lane == lane)
                {
                    found = piece;
                    break;
                }

                if (piece.Stamp == _renderStamp)
                {
                    continue;
                }

                if (!piece.Live)
                {
                    if (free == null)
                    {
                        free = piece;
                    }
                }
                else if (reusable == null)
                {
                    reusable = piece;
                }
            }

            Piece target = found ?? free ?? reusable;
            if (target == null)
            {
                return;
            }

            target.Live = true;
            target.Lane = lane;
            target.Stamp = _renderStamp;
            Place(target, o, shape, x);
        }

        private void Place(Piece piece, in ObstacleInstance o, ObstacleShape shape, float x)
        {
            float width = shape.WidthM;
            float depth = o.DepthM > 0f ? o.DepthM : shape.DepthM;
            float height = shape.TopM - shape.BottomM;
            piece.ObstacleId = o.Id;
            PathPlacement.Place(_frame, piece.Root.transform, o.Z + depth * 0.5f, x, 0f);
            if (!piece.Root.activeSelf)
            {
                piece.Root.SetActive(true);
            }

            if (piece.Kind != o.Archetype)
            {
                piece.Kind = o.Archetype;
                bool mover = o.Archetype == ObstacleArchetype.Mover;
                piece.Cube.gameObject.SetActive(!mover);
                piece.Sphere.gameObject.SetActive(mover);

                // Real art replaces the body and the red/ink marker bands (the prefab carries its own markings).
                int kindIndex = (int)o.Archetype;
                bool hasArt = kindIndex >= 0 && kindIndex < ArchetypeSlots && piece.Art[kindIndex] != null;
                for (int a = 0; a < ArchetypeSlots; a++)
                {
                    if (piece.Art[a] != null)
                    {
                        piece.Art[a].SetActive(hasArt && a == kindIndex);
                    }
                }

                piece.CubeRenderer.enabled = !hasArt;
                piece.SphereRenderer.enabled = !hasArt;
                piece.Band.gameObject.SetActive(!hasArt);
                piece.Stripe.gameObject.SetActive(!hasArt);
            }

            float centerY = shape.BottomM + height * 0.5f;
            Transform body = o.Archetype == ObstacleArchetype.Mover ? piece.Sphere : piece.Cube;
            body.localPosition = new Vector3(0f, centerY, 0f);
            body.localScale = new Vector3(width, height, depth);

            float bandY;
            float stripeY;
            float bandWidth = width + BandOverhangM;
            float bandDepth = depth + BandOverhangM;
            switch (o.Archetype)
            {
                case ObstacleArchetype.LowBarrier:
                    // Red band along the top edge, ink stripe just below it.
                    bandY = shape.TopM - BandHeightM * 0.5f;
                    stripeY = shape.TopM - BandHeightM - StripeHeightM * 0.5f;
                    break;
                case ObstacleArchetype.HighBarrier:
                    // Red and ink on the underside.
                    bandY = shape.BottomM + BandHeightM * 0.5f;
                    stripeY = shape.BottomM + BandHeightM + StripeHeightM * 0.5f;
                    break;
                case ObstacleArchetype.Mover:
                    bandY = centerY;
                    stripeY = centerY - BandHeightM * 0.5f - StripeHeightM * 0.5f;
                    bandWidth = width * 0.7f;
                    bandDepth = depth * 0.7f;
                    break;
                default:
                    // Full block: red band at hip height (about 1 m) with an ink stripe under it.
                    bandY = 1f;
                    stripeY = 1f - BandHeightM * 0.5f - StripeHeightM * 0.5f;
                    break;
            }

            piece.Band.localPosition = new Vector3(0f, bandY, 0f);
            piece.Band.localScale = new Vector3(bandWidth, BandHeightM, bandDepth);
            piece.Stripe.localPosition = new Vector3(0f, stripeY, 0f);
            piece.Stripe.localScale = new Vector3(bandWidth, StripeHeightM, bandDepth);
        }
    }
}
