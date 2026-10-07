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
    /// <see cref="GapView"/>. No allocations after <see cref="Init"/>.
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
        }

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private Piece[] _pieces;
        private int _shown;
        private TrackSimulation _track;

        /// <summary>Pieces shown last frame (tests).</summary>
        public int ShownPieceCount => _shown;

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, float viewDistanceM)
        {
            _runnerConfig = runnerConfig;
            _viewDistanceM = viewDistanceM;
            _pieces = new Piece[PieceCapacity];
            for (int i = 0; i < PieceCapacity; i++)
            {
                var piece = new Piece();
                Transform root = new GameObject("Obstacle" + i).transform;
                root.SetParent(transform, false);
                piece.Root = root.gameObject;
                piece.Cube = kit.Create(PrimitiveType.Cube, "Body", root, StylePalette.HazardWood);
                piece.Sphere = kit.Create(PrimitiveType.Sphere, "Boulder", root, StylePalette.HazardStone);
                piece.Band = kit.Create(PrimitiveType.Cube, "HazardBand", root, StylePalette.HazardRed);
                piece.Stripe = kit.Create(PrimitiveType.Cube, "InkStripe", root, StylePalette.Ink);
                piece.Kind = ObstacleArchetype.None;
                piece.Root.SetActive(false);
                _pieces[i] = piece;
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
            if (_pieces == null)
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
                for (int i = 0; i < count && used < PieceCapacity; i++)
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

                    ObstacleShape shape = _track.Kit.GetShape(o.Archetype);
                    if (o.Archetype == ObstacleArchetype.Mover)
                    {
                        float x = Mathf.Lerp(o.MoverXPrev, o.MoverX, alpha);
                        Place(_pieces[used++], o, shape, x);
                        continue;
                    }

                    for (int lane = 0; lane < LaneMasks.LaneCount && used < PieceCapacity; lane++)
                    {
                        if (LaneMasks.Contains(o.LaneMask, lane))
                        {
                            Place(_pieces[used++], o, shape, _runnerConfig.LaneCenterX(lane));
                        }
                    }
                }
            }

            for (int i = used; i < _shown; i++)
            {
                _pieces[i].Root.SetActive(false);
            }

            _shown = used;
        }

        private static void Place(Piece piece, in ObstacleInstance o, ObstacleShape shape, float x)
        {
            float width = shape.WidthM;
            float depth = o.DepthM > 0f ? o.DepthM : shape.DepthM;
            float height = shape.TopM - shape.BottomM;
            piece.Root.transform.localPosition = new Vector3(x, 0f, (float)o.Z + depth * 0.5f);
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
