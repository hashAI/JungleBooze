using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box signature hazards (GDD 8.3, style guide 4.1), polled from the <see cref="TrackSimulation"/> obstacle
    /// ring each frame:
    /// <list type="bullet">
    /// <item>Lane denial (thorn patch): per covered lane a dark thorny block the size of the hitbox, with ink thorns
    /// on the face and top and red-tinted thorn tips (red always next to ink).</item>
    /// <item>Lane strike: an ink-framed plate marks the lane at all times; during the warning red-and-ink chevrons
    /// pulse on it (faster as the strike nears); when the strike is active a stone column drops into the lane (the
    /// box); in the rest phase only the plate stays.</item>
    /// </list>
    /// Pooled; no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class HazardView : MonoBehaviour, IRunView
    {
        private const int ThornCapacity = 16;
        private const int StrikeCapacity = 8;
        private const float BehindM = 10f;
        private const int ThornsPerFace = 3;
        private const float ThornSizeM = 0.32f;
        private const float ThornTipSizeM = 0.14f;
        private const float PlateHeightM = 0.02f;
        private const float FrameM = 0.15f;
        private const float ChevronBarM = 0.9f;
        private const float ChevronThicknessM = 0.16f;
        // The strike box is lethal from its first active tick, so the column starts almost on the hitbox (it used to
        // fall from 5 m over 6 ticks, i.e. the box was live while the rock was still above it).
        private const float DropHeightM = 1f;
        private const int DropTicks = 3;
        private const float BandHeightM = 0.2f;
        private const float StripeHeightM = 0.1f;

        private sealed class ThornPiece
        {
            public GameObject Root;
            public Transform Body;
            public Transform[] Thorns;
            public Transform[] Tips;
            public int ObstacleId;
        }

        private sealed class StrikePiece
        {
            public GameObject Root;
            public Transform Frame;
            public Transform Inner;
            public GameObject Chevrons;
            public Transform Column;
            public Transform Rocks;
            public Transform Band;
            public Transform Stripe;
            public int ObstacleId;
        }

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private ThornPiece[] _thorns;
        private StrikePiece[] _strikes;
        private int _thornsShown;
        private int _strikesShown;
        private TrackSimulation _track;
        private PathFrame _frame;

        /// <summary>Thorn pieces and strike pieces shown last frame (tests).</summary>
        public int ShownThornCount => _thornsShown;

        public int ShownStrikeCount => _strikesShown;

        /// <summary>
        /// Debug aid: true when a thorn patch or strike column for <paramref name="obstacleId"/> is shown this frame with
        /// at least one enabled renderer. Allocates; call once per death at most.
        /// </summary>
        public bool IsDrawn(int obstacleId)
        {
            for (int i = 0; _thorns != null && i < _thornsShown; i++)
            {
                if (_thorns[i].ObstacleId == obstacleId && _thorns[i].Root.activeInHierarchy
                    && HasEnabledRenderer(_thorns[i].Root.transform))
                {
                    return true;
                }
            }

            for (int i = 0; _strikes != null && i < _strikesShown; i++)
            {
                // A strike only has a box while active, so what must be visible is the column.
                if (_strikes[i].ObstacleId == obstacleId && _strikes[i].Column.gameObject.activeInHierarchy
                    && HasEnabledRenderer(_strikes[i].Column))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEnabledRenderer(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled && renderers[i].sharedMaterial != null)
                {
                    return true;
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
            _thorns = new ThornPiece[ThornCapacity];
            for (int i = 0; i < ThornCapacity; i++)
            {
                _thorns[i] = CreateThorn(kit, i);
            }

            _strikes = new StrikePiece[StrikeCapacity];
            for (int i = 0; i < StrikeCapacity; i++)
            {
                _strikes[i] = CreateStrike(kit, i);
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
            if (_thorns == null)
            {
                return;
            }

            int thorns = 0;
            int strikes = 0;
            if (_track != null)
            {
                RunnerSimulation runner = session.Runner;
                RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + _viewDistanceM;
                HazardConfig hazards = _track.Hazards;
                int count = _track.ObstacleCount;
                for (int i = 0; i < count; i++)
                {
                    ref readonly ObstacleInstance o = ref _track.GetObstacle(i);
                    if (o.Z > maxZ)
                    {
                        break;
                    }

                    if (o.BackZ < minZ)
                    {
                        continue;
                    }

                    if (o.Archetype == ObstacleArchetype.LaneDenial)
                    {
                        ObstacleShape shape = _track.Kit.LaneDenial;
                        for (int lane = 0; lane < LaneMasks.LaneCount && thorns < ThornCapacity; lane++)
                        {
                            if (LaneMasks.Contains(o.LaneMask, lane))
                            {
                                PlaceThorn(_thorns[thorns++], o, shape, _runnerConfig.LaneCenterX(lane));
                            }
                        }
                    }
                    else if (o.Archetype == ObstacleArchetype.LaneStrike && strikes < StrikeCapacity)
                    {
                        PlaceStrike(_strikes[strikes++], o, _track.Kit.LaneStrike, hazards, alpha);
                    }
                }
            }

            for (int i = thorns; i < _thornsShown; i++)
            {
                _thorns[i].Root.SetActive(false);
            }

            for (int i = strikes; i < _strikesShown; i++)
            {
                _strikes[i].Root.SetActive(false);
            }

            _thornsShown = thorns;
            _strikesShown = strikes;
        }

        private ThornPiece CreateThorn(GrayBoxKit kit, int index)
        {
            var piece = new ThornPiece();
            Transform root = new GameObject("Thorns" + index).transform;
            root.SetParent(transform, false);
            piece.Root = root.gameObject;
            piece.Body = kit.Create(PrimitiveType.Cube, "Body", root, StylePalette.HazardThorn).transform;
            // Real art (fits a unit cube, scaled to the hitbox by PlaceThorn) replaces body, thorns and tips.
            // The gray-box body is only hidden once the art is verified visible (EnvironmentArt.AttachAndHide).
            bool thornArt = EnvironmentArt.AttachAndHide(piece.Body, EnvironmentArt.ThornPatch);

            int n = ThornsPerFace * 2;
            piece.Thorns = new Transform[n];
            piece.Tips = new Transform[n];
            for (int t = 0; t < n; t++)
            {
                piece.Thorns[t] = kit.Create(PrimitiveType.Cube, "Thorn" + t, root, StylePalette.Ink).transform;
                piece.Thorns[t].localRotation = Quaternion.Euler(45f, 45f, 0f);
                piece.Thorns[t].localScale = new Vector3(ThornSizeM, ThornSizeM, ThornSizeM);
                piece.Tips[t] = kit.Create(PrimitiveType.Cube, "ThornTip" + t, root, StylePalette.HazardRed).transform;
                piece.Tips[t].localRotation = Quaternion.Euler(45f, 45f, 0f);
                piece.Tips[t].localScale = new Vector3(ThornTipSizeM, ThornTipSizeM, ThornTipSizeM);
                if (thornArt)
                {
                    piece.Thorns[t].gameObject.SetActive(false);
                    piece.Tips[t].gameObject.SetActive(false);
                }
            }

            piece.Root.SetActive(false);
            return piece;
        }

        private StrikePiece CreateStrike(GrayBoxKit kit, int index)
        {
            var piece = new StrikePiece();
            Transform root = new GameObject("LaneStrike" + index).transform;
            root.SetParent(transform, false);
            piece.Root = root.gameObject;
            piece.Frame = kit.Create(PrimitiveType.Cube, "PlateFrame", root, StylePalette.Ink).transform;
            piece.Inner = kit.Create(PrimitiveType.Cube, "PlateInner", root, StylePalette.PathAlternate).transform;

            Transform chevrons = new GameObject("Chevrons").transform;
            chevrons.SetParent(root, false);
            piece.Chevrons = chevrons.gameObject;
            for (int c = 0; c < 2; c++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    // A "V" pointing at the player: two bars, ink under red.
                    Quaternion rot = Quaternion.Euler(0f, side * 40f, 0f);
                    float x = side * 0.3f;
                    float z = c * 0.7f;
                    Transform ink = kit.Create(PrimitiveType.Cube, "ChevronInk", chevrons, StylePalette.Ink).transform;
                    ink.localRotation = rot;
                    ink.localPosition = new Vector3(x, PlateHeightM + 0.005f, z);
                    ink.localScale = new Vector3(ChevronBarM + 0.08f, 0.01f, ChevronThicknessM + 0.08f);
                    Transform red = kit.Create(PrimitiveType.Cube, "ChevronRed", chevrons, StylePalette.HazardRed).transform;
                    red.localRotation = rot;
                    red.localPosition = new Vector3(x, PlateHeightM + 0.012f, z);
                    red.localScale = new Vector3(ChevronBarM, 0.01f, ChevronThicknessM);
                }
            }

            Transform column = new GameObject("Strike").transform;
            column.SetParent(root, false);
            piece.Column = column;
            piece.Rocks = kit.Create(PrimitiveType.Cube, "Rocks", column, StylePalette.HazardStone).transform;
            piece.Band = kit.Create(PrimitiveType.Cube, "HazardBand", column, StylePalette.HazardRed).transform;
            piece.Stripe = kit.Create(PrimitiveType.Cube, "InkStripe", column, StylePalette.Ink).transform;
            // Real art (unit cube, scaled to the full hitbox by PlaceStrike) replaces rocks and marker bands.
            if (EnvironmentArt.AttachAndHide(piece.Rocks, EnvironmentArt.StrikeColumn))
            {
                piece.Band.gameObject.SetActive(false);
                piece.Stripe.gameObject.SetActive(false);
            }

            piece.Root.SetActive(false);
            return piece;
        }

        private void PlaceThorn(ThornPiece piece, in ObstacleInstance o, ObstacleShape shape, float x)
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

            piece.Body.localPosition = new Vector3(0f, shape.BottomM + height * 0.5f, 0f);
            piece.Body.localScale = new Vector3(width, height, depth);

            // Thorns: a row on the front face (toward the player) and a row along the top front edge.
            float step = width / (ThornsPerFace + 1);
            for (int t = 0; t < ThornsPerFace; t++)
            {
                float tx = -width * 0.5f + step * (t + 1);
                float faceY = shape.BottomM + height * (0.35f + 0.2f * (t % 2));
                SetThorn(piece, t, new Vector3(tx, faceY, -depth * 0.5f));
                SetThorn(piece, ThornsPerFace + t, new Vector3(tx, shape.TopM, -depth * 0.5f + 0.3f));
            }
        }

        private static void SetThorn(ThornPiece piece, int index, Vector3 position)
        {
            piece.Thorns[index].localPosition = position;
            piece.Tips[index].localPosition = position + new Vector3(0f, 0.08f, -ThornSizeM * 0.45f);
        }

        private void PlaceStrike(StrikePiece piece, in ObstacleInstance o, ObstacleShape shape, HazardConfig hazards, float alpha)
        {
            float depth = o.DepthM > 0f ? o.DepthM : shape.DepthM;
            float width = shape.WidthM;
            piece.ObstacleId = o.Id;
            PathPlacement.Place(_frame, piece.Root.transform, o.Z + depth * 0.5f, _runnerConfig.LaneCenterX(o.FromLane), 0f);
            if (!piece.Root.activeSelf)
            {
                piece.Root.SetActive(true);
            }

            piece.Frame.localPosition = new Vector3(0f, PlateHeightM * 0.5f, 0f);
            piece.Frame.localScale = new Vector3(width, PlateHeightM, depth);
            piece.Inner.localPosition = new Vector3(0f, PlateHeightM * 0.5f + 0.002f, 0f);
            piece.Inner.localScale = new Vector3(width - 2f * FrameM, PlateHeightM, depth - 2f * FrameM);

            bool warning = o.StrikePhase == LaneStrikePhase.Warning;
            bool active = o.StrikePhase == LaneStrikePhase.Active;

            // Warning: chevrons pulse 3 Hz, then 6 Hz in the last third (the strike is near).
            bool chevronsOn = false;
            if (warning)
            {
                float t = o.StrikeTicks + alpha;
                bool late = o.StrikeTicks * 3 >= hazards.WarningTicks * 2;
                int period = late ? 10 : 20;
                chevronsOn = ((int)t % period) < period / 2;
            }

            if (piece.Chevrons.activeSelf != chevronsOn)
            {
                piece.Chevrons.SetActive(chevronsOn);
            }

            if (piece.Column.gameObject.activeSelf != active)
            {
                piece.Column.gameObject.SetActive(active);
            }

            if (active)
            {
                float height = shape.TopM - shape.BottomM;
                float drop = Mathf.Clamp01((o.StrikeTicks + alpha) / DropTicks);
                float y = (1f - drop) * DropHeightM;
                piece.Column.localPosition = new Vector3(0f, y, 0f);
                piece.Rocks.localPosition = new Vector3(0f, shape.BottomM + height * 0.5f, 0f);
                piece.Rocks.localScale = new Vector3(width, height, depth);
                piece.Band.localPosition = new Vector3(0f, 1f, 0f);
                piece.Band.localScale = new Vector3(width + 0.01f, BandHeightM, depth + 0.01f);
                piece.Stripe.localPosition = new Vector3(0f, 1f - BandHeightM * 0.5f - StripeHeightM * 0.5f, 0f);
                piece.Stripe.localScale = new Vector3(width + 0.01f, StripeHeightM, depth + 0.01f);
            }
        }
    }
}
