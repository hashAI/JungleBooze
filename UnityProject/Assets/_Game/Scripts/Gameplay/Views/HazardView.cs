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
    /// <item>Lane denial (thorn patch, spec 005 A5): one grounded <see cref="ObstacleRig"/> per row: a root cage and
    /// cane wall that fills each covered lane's hitbox (embedded in the ground), ink thorn hooks, ochre stripes on three
    /// front posts (red always flanked by ink, never on the thorns), a woody root crown beside the path it grows
    /// from, a contact pad and debris. Built once at the spawn distance; canes tremble 1 degree at the top.</item>
    /// <item>Lane strike: an ink-framed plate marks the lane at all times; during the warning red-and-ink chevrons
    /// pulse on it (faster as the strike nears); when the strike is active a stone column drops into the lane (the
    /// box); in the rest phase only the plate stays.</item>
    /// </list>
    /// Pooled; no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class HazardView : MonoBehaviour, IRunView
    {
        private const int ThornRigCapacity = 6;
        private const int ThornPartCapacity = 96;
        private const int StrikeCapacity = 8;
        private const float BehindM = 10f;
        private const float PlateHeightM = 0.02f;
        private const float FrameM = 0.15f;
        private const float ChevronBarM = 0.9f;
        private const float ChevronThicknessM = 0.16f;
        private const float BandHeightM = 0.2f;
        private const float StripeHeightM = 0.1f;
        private const float MaxClockStepS = 0.1f;

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
        private ObstacleRig[] _thornRigs;
        private StrikePiece[] _strikes;
        private int _thornRigsShown;
        private int _renderStamp;
        private ObstacleGroundingTuning _tuning;
        private RigMaterials _materials;
        private ObstacleArtPool _art;
        private ObstacleRigBuilder _builder;
        private ObstacleRigAnimator _animator;
        private RigBuildInfo _info;
        private ulong _runSeed;
        private float _clockS;
        private bool _primed;
        private int _strikesShown;
        private TrackSimulation _track;
        private PathFrame _frame;

        /// <summary>Thorn pieces and strike pieces shown last frame (tests).</summary>
        public int ShownThornCount => _thornRigsShown;

        /// <summary>Number of pooled thorn rigs (the F7 audit walks them).</summary>
        public int RigCount => _thornRigs != null ? _thornRigs.Length : 0;

        /// <summary>The pooled thorn rig at <paramref name="index"/> (live or not).</summary>
        public ObstacleRig GetRig(int index)
        {
            return _thornRigs[index];
        }

        /// <summary>Reduce Motion: quieter cane tremble.</summary>
        public bool ReduceMotion
        {
            get => _animator != null && _animator.ReduceMotion;
            set
            {
                if (_animator != null)
                {
                    _animator.ReduceMotion = value;
                }
            }
        }

        public int ShownStrikeCount => _strikesShown;

        /// <summary>
        /// Debug aid: true when a thorn patch or strike column for <paramref name="obstacleId"/> is shown this frame with
        /// at least one enabled renderer. Allocates; call once per death at most.
        /// </summary>
        public bool IsDrawn(int obstacleId)
        {
            for (int i = 0; _thornRigs != null && i < _thornRigs.Length; i++)
            {
                if (_thornRigs[i].Live && _thornRigs[i].ObstacleId == obstacleId && _thornRigs[i].Root.activeInHierarchy
                    && HasEnabledRenderer(_thornRigs[i].Root.transform))
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
            _tuning = new ObstacleGroundingTuning();
            _materials = new RigMaterials(kit);
            _info = new RigBuildInfo();
            Transform holder = new GameObject("ArtPool").transform;
            holder.SetParent(transform, false);
            _art = new ObstacleArtPool(
                holder,
                _tuning.UseLegacyArt
                    ? new[] { ObstacleArtSlot.ThornCage, ObstacleArtSlot.CaneWall, ObstacleArtSlot.RootCrown, ObstacleArtSlot.ThornLitter, ObstacleArtSlot.LegacyThorn }
                    : new[] { ObstacleArtSlot.ThornCage, ObstacleArtSlot.CaneWall, ObstacleArtSlot.RootCrown, ObstacleArtSlot.ThornLitter },
                3);
            _builder = new ObstacleRigBuilder(_materials, _art, _tuning);
            _animator = new ObstacleRigAnimator(_tuning);

            Material placeholder = _materials.Opaque(GroundingPalette.Thorn);
            _thornRigs = new ObstacleRig[ThornRigCapacity];
            for (int i = 0; i < ThornRigCapacity; i++)
            {
                _thornRigs[i] = new ObstacleRig(transform, "Thorns" + i, ThornPartCapacity, 0, placeholder, _materials.Dust);
            }

            _strikes = new StrikePiece[StrikeCapacity];
            for (int i = 0; i < StrikeCapacity; i++)
            {
                _strikes[i] = CreateStrike(kit, i);
            }
        }

        private void OnDestroy()
        {
            if (_materials != null)
            {
                _materials.Dispose();
            }
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            _runSeed = session.RunSeed;
            _primed = false;
            for (int i = 0; _thornRigs != null && i < _thornRigs.Length; i++)
            {
                if (_thornRigs[i].Live)
                {
                    _art.Release(_thornRigs[i]);
                    _thornRigs[i].Release();
                }
            }

            Render(session, 1f, 0f);
            _primed = true;
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_thornRigs == null)
            {
                return;
            }

            float dt = Mathf.Clamp(realDeltaSeconds, 0f, MaxClockStepS);
            _clockS += dt;
            _renderStamp++;
            int strikes = 0;
            if (_track != null)
            {
                RunnerSimulation runner = session.Runner;
                RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + Mathf.Max(_viewDistanceM, _tuning.SpawnAheadM);
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
                        PlaceThorn(o, heroZ);
                    }
                    else if (o.Archetype == ObstacleArchetype.LaneStrike && strikes < StrikeCapacity)
                    {
                        PlaceStrike(_strikes[strikes++], o, _track.Kit.LaneStrike, hazards, alpha);
                    }
                }
            }

            int liveRigs = 0;
            for (int i = 0; i < _thornRigs.Length; i++)
            {
                ObstacleRig rig = _thornRigs[i];
                if (!rig.Live)
                {
                    continue;
                }

                if (rig.Stamp != _renderStamp)
                {
                    _art.Release(rig);
                    rig.Release();
                    continue;
                }

                liveRigs++;
            }

            for (int i = strikes; i < _strikesShown; i++)
            {
                _strikes[i].Root.SetActive(false);
            }

            _thornRigsShown = liveRigs;
            _strikesShown = strikes;
        }

        /// <summary>Finds or builds the rig of a thorn row (built once, at the spawn distance) and moves it.</summary>
        private void PlaceThorn(in ObstacleInstance o, double heroZ)
        {
            ObstacleShape shape = _track.Kit.LaneDenial;
            float depth = o.DepthM > 0f ? o.DepthM : shape.DepthM;
            ObstacleRig rig = null;
            ObstacleRig free = null;
            for (int i = 0; i < _thornRigs.Length; i++)
            {
                if (_thornRigs[i].Live && _thornRigs[i].ObstacleId == o.Id)
                {
                    rig = _thornRigs[i];
                    break;
                }

                if (!_thornRigs[i].Live && free == null)
                {
                    free = _thornRigs[i];
                }
            }

            float distanceAhead = (float)(o.Z - heroZ);
            if (rig == null)
            {
                if (free == null)
                {
                    return;
                }

                rig = free;
                double sampleS = o.Z + (o.DepthM * 0.5);
                _frame.Sample(sampleS, out PathPose pose);
                _info.Kind = o.Archetype;
                _info.ObstacleId = o.Id;
                _info.RunSeed = _runSeed;
                _info.LaneMask = o.LaneMask;
                _info.FromLane = o.FromLane;
                _info.ToLane = o.ToLane;
                _info.Shape = shape;
                _info.DepthM = depth;
                _info.LaneWidthM = _runnerConfig.LaneWidthM;
                _info.EmbedM = GroundingMath.EmbedDepth(pose.Surface, GroundingMath.Roll(_runSeed, o.Id, 6u), pose.GradePct, depth);
                _info.AnchorCount = ContextLayout.ForObstacle(
                    _runSeed,
                    o.Id,
                    o.Archetype,
                    o.LaneMask,
                    o.FromLane,
                    o.ToLane,
                    pose.Curvature,
                    _info.LaneWidthM,
                    _tuning.BendCurvature,
                    out _info.Variant,
                    out _info.Skin,
                    _info.Anchors);
                _builder.Build(rig, _info);
                rig.Live = true;
                if (_primed && Debug.isDebugBuild && GroundingMath.IsInsidePopInLimit(distanceAhead, _tuning.PopInMinDistM))
                {
                    Debug.LogWarning("[JungleBooze] Late pop-in: thorn patch " + o.Id + " built " + distanceAhead.ToString("F1")
                        + " m ahead, inside the " + _tuning.PopInMinDistM.ToString("F0") + " m limit.");
                }
            }

            rig.Stamp = _renderStamp;
            rig.CenterS = o.Z + (depth * 0.5);
            PathPlacement.Place(_frame, rig.Root.transform, rig.CenterS, 0f, 0f);
            if (!rig.Root.activeSelf)
            {
                rig.Root.SetActive(true);
            }

            _animator.AnimateSway(rig, _clockS, distanceAhead, true);
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
            StrikePose.Evaluate(o.StrikePhase, o.StrikeTicks, alpha, out bool active, out float columnBottomM);

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
                piece.Column.localPosition = new Vector3(0f, columnBottomM, 0f);
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
