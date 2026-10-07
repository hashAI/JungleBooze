using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Grounded obstacles (spec 005 wave 1, style guide 4.1): each frame it polls the <see cref="TrackSimulation"/>
    /// obstacle ring and keeps one pooled <see cref="ObstacleRig"/> per obstacle row (low barrier, high barrier, full
    /// block, mover). A rig is built once, when the row first comes within the spawn distance (100 m, fully fogged):
    /// a seeded variant (hash of run seed and obstacle id), a body that fills the hitbox and is embedded in the ground,
    /// the supports and origin pieces beside the path (root plate, stump, support limb, scree bank), a contact pad and
    /// debris. After that only transforms move: the hanging mat sways, the boulder rocks, rolls (no slip), pops its chock
    /// and raises dust. Nothing changes state closer than 45 m. Art slots (<see cref="ObstacleArtSlots"/>) replace the
    /// procedural pieces when the models exist. Hazard red appears only as ochre marks flanked by ink.
    /// Thorn patches and strikes are drawn by <see cref="HazardView"/>, gaps by <see cref="GapView"/>.
    /// No allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class ObstacleView : MonoBehaviour, IRunView
    {
        private const int RigCapacity = 14;
        private const int PuffRigCount = 4;
        private const int PuffsPerRig = 12;
        private const int PartCapacity = 64;
        private const int ArtRigFactor = 6;
        private const float BehindM = 10f;
        private const float MaxClockStepS = 0.1f;

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private ObstacleRig[] _rigs;
        private int _shown;
        private int _renderStamp;
        private TrackSimulation _track;
        private PathFrame _frame;
        private ObstacleGroundingTuning _tuning;
        private RigMaterials _materials;
        private ObstacleArtPool _art;
        private ObstacleRigBuilder _builder;
        private ObstacleRigAnimator _animator;
        private RigBuildInfo _info;
        private ulong _runSeed;
        private float _clockS;
        private bool _primed;
        private bool _warnedPoolFull;

        /// <summary>Rigs shown last frame (tests).</summary>
        public int ShownPieceCount => _shown;

        /// <summary>Number of pooled rigs (the F7 audit walks them).</summary>
        public int RigCount => _rigs != null ? _rigs.Length : 0;

        /// <summary>The pooled rig at <paramref name="index"/> (live or not).</summary>
        public ObstacleRig GetRig(int index)
        {
            return _rigs[index];
        }

        /// <summary>Tuning of the grounded rigs; change it before <see cref="Init"/>.</summary>
        public ObstacleGroundingTuning Tuning
        {
            get
            {
                if (_tuning == null)
                {
                    _tuning = new ObstacleGroundingTuning();
                }

                return _tuning;
            }
        }

        /// <summary>Reduce Motion: fewer dust puffs and a quieter sway.</summary>
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

        /// <summary>
        /// Debug aid: true when a rig for <paramref name="obstacleId"/> is shown this frame with at least one enabled
        /// renderer. Allocates; call once per death at most.
        /// </summary>
        public bool IsDrawn(int obstacleId)
        {
            for (int i = 0; _rigs != null && i < _rigs.Length; i++)
            {
                ObstacleRig rig = _rigs[i];
                if (!rig.Live || rig.ObstacleId != obstacleId || !rig.Root.activeInHierarchy)
                {
                    continue;
                }

                Renderer[] renderers = rig.Root.GetComponentsInChildren<Renderer>(false);
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
            _tuning = Tuning;
            _materials = new RigMaterials(kit);
            _info = new RigBuildInfo();

            ObstacleArtSlot[] slots = _tuning.UseLegacyArt ? LegacySlots : Slots;
            Transform holder = new GameObject("ArtPool").transform;
            holder.SetParent(transform, false);
            _art = new ObstacleArtPool(holder, slots, ArtRigFactor);
            _builder = new ObstacleRigBuilder(_materials, _art, _tuning);
            _animator = new ObstacleRigAnimator(_tuning);

            Material placeholder = _materials.Opaque(GroundingPalette.Bark);
            _rigs = new ObstacleRig[RigCapacity];
            for (int i = 0; i < RigCapacity; i++)
            {
                _rigs[i] = new ObstacleRig(transform, "Obstacle" + i, PartCapacity, i < PuffRigCount ? PuffsPerRig : 0, placeholder, _materials.Dust);
            }
        }

        private static readonly ObstacleArtSlot[] Slots =
        {
            ObstacleArtSlot.LogTrunk, ObstacleArtSlot.LogTrunkB, ObstacleArtSlot.LogTrunkC, ObstacleArtSlot.RootPlate,
            ObstacleArtSlot.RootPlateB, ObstacleArtSlot.Stump, ObstacleArtSlot.RockHump, ObstacleArtSlot.HangMat,
            ObstacleArtSlot.HangMatB, ObstacleArtSlot.HangMatC, ObstacleArtSlot.Limb, ObstacleArtSlot.LianaTie,
            ObstacleArtSlot.ButtressFin, ObstacleArtSlot.StandingStone, ObstacleArtSlot.WedgedSlab, ObstacleArtSlot.RootWeb,
            ObstacleArtSlot.BarrelBoulder, ObstacleArtSlot.Wallow, ObstacleArtSlot.Chock, ObstacleArtSlot.Furrow,
            ObstacleArtSlot.SandBar,
        };

        private static readonly ObstacleArtSlot[] LegacySlots =
        {
            ObstacleArtSlot.LogTrunk, ObstacleArtSlot.LogTrunkB, ObstacleArtSlot.LogTrunkC, ObstacleArtSlot.RootPlate,
            ObstacleArtSlot.RootPlateB, ObstacleArtSlot.Stump, ObstacleArtSlot.RockHump, ObstacleArtSlot.HangMat,
            ObstacleArtSlot.HangMatB, ObstacleArtSlot.HangMatC, ObstacleArtSlot.Limb, ObstacleArtSlot.LianaTie,
            ObstacleArtSlot.ButtressFin, ObstacleArtSlot.StandingStone, ObstacleArtSlot.WedgedSlab, ObstacleArtSlot.RootWeb,
            ObstacleArtSlot.BarrelBoulder, ObstacleArtSlot.Wallow, ObstacleArtSlot.Chock, ObstacleArtSlot.Furrow,
            ObstacleArtSlot.SandBar, ObstacleArtSlot.LegacyLow, ObstacleArtSlot.LegacyHigh, ObstacleArtSlot.LegacyFull,
        };

        /// <summary>Tints the obstacle and mover bodies (world themes, GDD 9). Allocation free.</summary>
        public void ApplyTheme(in WorldTheme theme)
        {
            if (_materials == null)
            {
                return;
            }

            _materials.ApplyTheme(theme.ObstacleBody, theme.MoverBody);
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
            _warnedPoolFull = false;
            for (int i = 0; _rigs != null && i < _rigs.Length; i++)
            {
                // New run: ids start over, so no rig may carry a binding from the last one.
                if (_rigs[i].Live)
                {
                    _art.Release(_rigs[i]);
                    _rigs[i].Release();
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
            if (_rigs == null)
            {
                return;
            }

            float dt = Mathf.Clamp(realDeltaSeconds, 0f, MaxClockStepS);
            _clockS += dt;
            _renderStamp++;
            if (_track != null)
            {
                RunnerSimulation runner = session.Runner;
                RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + Mathf.Max(_viewDistanceM, _tuning.SpawnAheadM);
                float speed = runner.Current.Speed;
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

                    PlaceRig(o, heroZ, alpha, dt, speed);
                }
            }

            // Whatever was not claimed this frame has left the view.
            int live = 0;
            for (int i = 0; i < _rigs.Length; i++)
            {
                ObstacleRig rig = _rigs[i];
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

                live++;
            }

            _shown = live;
        }

        private void PlaceRig(in ObstacleInstance o, double heroZ, float alpha, float dt, float speed)
        {
            ObstacleShape shape = _track.Kit.GetShape(o.Archetype);
            ObstacleRig rig = FindRig(o.Id);
            float depth = o.DepthM > 0f ? o.DepthM : shape.DepthM;
            if (rig == null)
            {
                rig = AcquireRig(o.Archetype);
                if (rig == null)
                {
                    if (!_warnedPoolFull && Debug.isDebugBuild)
                    {
                        _warnedPoolFull = true;
                        Debug.LogWarning("[JungleBooze] Obstacle rig pool is full: an obstacle is not drawn (raise ObstacleView.RigCapacity).");
                    }

                    return;
                }

                Bind(rig, o, shape, depth, (float)(o.Z - heroZ));
            }

            rig.Stamp = _renderStamp;
            rig.CenterS = o.Z + (depth * 0.5);
            PathPlacement.Place(_frame, rig.Root.transform, rig.CenterS, 0f, 0f);
            if (!rig.Root.activeSelf)
            {
                rig.Root.SetActive(true);
            }

            float distanceAhead = (float)(o.Z - heroZ);
            switch (o.Archetype)
            {
                case ObstacleArchetype.HighBarrier:
                    _animator.AnimateSway(rig, _clockS, distanceAhead, false);
                    break;
                case ObstacleArchetype.Mover:
                {
                    float x = Mathf.Lerp(o.MoverXPrev, o.MoverX, alpha);
                    float triggerDistance = speed * _track.Kit.MoverTriggerLeadS;
                    _animator.AnimateMover(rig, in o, x, _clockS, dt, distanceAhead, triggerDistance, speed);
                    break;
                }
            }
        }

        private ObstacleRig FindRig(int obstacleId)
        {
            for (int i = 0; i < _rigs.Length; i++)
            {
                if (_rigs[i].Live && _rigs[i].ObstacleId == obstacleId)
                {
                    return _rigs[i];
                }
            }

            return null;
        }

        /// <summary>A free rig; movers take one of the rigs that own dust puffs, everything else the others first.</summary>
        private ObstacleRig AcquireRig(ObstacleArchetype kind)
        {
            bool wantPuffs = kind == ObstacleArchetype.Mover;
            ObstacleRig fallback = null;
            for (int i = 0; i < _rigs.Length; i++)
            {
                ObstacleRig rig = _rigs[i];
                if (rig.Live)
                {
                    continue;
                }

                if ((rig.Puffs.Length > 0) == wantPuffs)
                {
                    return rig;
                }

                if (fallback == null)
                {
                    fallback = rig;
                }
            }

            return fallback;
        }

        /// <summary>Builds the rig for an obstacle that just came within the spawn distance (once per obstacle).</summary>
        private void Bind(ObstacleRig rig, in ObstacleInstance o, ObstacleShape shape, float depth, float distanceAheadM)
        {
            // The route at the middle of the row decides the surface, grade and bend side. ContextKeepOut uses the
            // same sample point so the scenery keep-out agrees with the rig.
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
            _info.EmbedM = o.Archetype == ObstacleArchetype.Mover
                ? _tuning.EmbedMinM
                : GroundingMath.EmbedDepth(pose.Surface, GroundingMath.Roll(_runSeed, o.Id, 6u), pose.GradePct, depth);
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

            if (o.Archetype == ObstacleArchetype.Mover)
            {
                // A mover first seen already moving or settled must not replay the pop or the settle.
                rig.PrevPhase = o.Phase;
                if (o.Phase != MoverPhase.Idle)
                {
                    rig.PopClockS = 99f;
                    rig.SettleClockS = o.Phase == MoverPhase.Settled ? 99f : -1f;
                    if (rig.Chock != null)
                    {
                        rig.Chock.gameObject.SetActive(false);
                    }
                }
            }

            if (_primed && Debug.isDebugBuild && GroundingMath.IsInsidePopInLimit(distanceAheadM, _tuning.PopInMinDistM))
            {
                Debug.LogWarning("[JungleBooze] Late pop-in: obstacle " + o.Id + " (" + o.Archetype + ") built " + distanceAheadM.ToString("F1")
                    + " m ahead, inside the " + _tuning.PopInMinDistM.ToString("F0") + " m limit.");
            }
        }
    }
}
