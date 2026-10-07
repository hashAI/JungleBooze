using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Vine;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box vines (GDD 7.2, 7.3). For every live vine in the <see cref="TrackSimulation"/> ring: a rope hanging
    /// from a canopy branch down to the grab point, a near-white glow core with a pulsing sun-gold halo and a ring
    /// icon pulsing at 2 Hz, and (first vine of a section) a sun-gold signpost at the path edge 25 m before it. The
    /// next vine aimed at during a swing glows bigger. While Pista swings, the vine she holds is drawn as a rope
    /// from her hand to the pendulum pivot, with the release ring around the hand: it fills with the swing phase,
    /// shows the Perfect band in sun-gold and flashes while the phase is inside it. On release: a "PERFECT!" or
    /// "GOOD" stamp, and for a Perfect radial speed lines (0.4 s) and a sun-gold trail during the launch.
    /// Opaque primitives stand in for the additive glow cards. Pooled; no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class VineView : MonoBehaviour, IRunView
    {
        private const int Capacity = 12;
        private const float BehindM = 6f;
        private const float RopeDiameterM = 0.08f;
        private const float CoreSizeM = 0.32f;
        private const float HaloSizeM = 0.6f;
        private const float AimedHaloScale = 1.5f;
        private const float PulseHz = 2f;
        private const int IconSegments = 8;
        private const float IconRadiusM = 0.55f;
        private const float IconSegmentM = 0.12f;
        private const float SignpostLeadM = 25f;
        private const float SignpostHeightM = 2.2f;
        private const float PathMarginM = 0.6f;

        // Natural swing rig (spec 003 section 8): span, anchor tree, tuft cue, landing glade. All gray-box primitives.
        private const int SpanSegments = 4;
        private const float SpanThicknessM = 0.6f;
        private const float LimbThicknessM = 0.7f;
        private const float RigTrailM = 40f;
        private const float DimFactor = 0.75f;
        private const float TuftBelowGrabM = 0.4f;
        private const int TuftLeaves = 5;
        private const float TuftRadiusM = 0.2f;
        private const float KnotLeafM = 0.55f;
        private const float KnotGoldM = 0.22f;
        private const float GladeLiftM = 0.04f;
        private const int GladeBushes = 4;
        private const float GladeBushLateralM = 6f;
        private const float GladeBushSizeM = 1.3f;

        private static readonly Color BarkColor = new Color(0.369f, 0.275f, 0.188f, 1f);
        private static readonly Color GladeGrass = new Color(0.498f, 0.761f, 0.353f, 1f);
        private static readonly Quaternion LimbArtAxis = Quaternion.Euler(0f, -90f, 0f);
        private static readonly Quaternion CylinderAlongZ = Quaternion.Euler(90f, 0f, 0f);

        private const int RingSegments = 20;
        private const float RingRadiusM = 0.6f;
        private const float RingTowardCameraM = 0.35f;
        private const float RingSegmentM = 0.13f;
        private const float RingFlashHz = 6f;

        private const float StampSeconds = 0.8f;
        private const float StampRiseM = 0.6f;
        private const float StampAboveFeetM = 3.0f;
        private const float StampAheadM = 1.5f;
        private const float SpeedLineSeconds = 0.4f;
        private const int SpeedLines = 8;
        private const int TrailLength = 6;
        private const float TrailSpacingSeconds = 0.04f;

        private sealed class Slot
        {
            public GameObject Root;
            public GameObject Cues;
            public Transform Core;
            public Transform Halo;
            public Transform Icon;
            public GameObject Signpost;

            // Rig (children of the view, route-space coordinates).
            public GameObject Rig;
            public Transform[] Span;
            public Transform Trunk;
            public Transform TrunkCylinder;
            public Transform Canopy;
            public Transform Limb;
            public Transform LimbCube;
            public Transform RopeHolder;
            public Transform RopeCylinder;
            public Transform Knot;
            public Transform Glade;

            // Dimming of the cue pieces (about 25 percent) without per-frame allocation.
            public MeshRenderer[] Dimmable;
            public Material[] Normal;
            public Material[] Dim;
            public bool Dimmed;

            public int AssignedId;
            public int VineId;
            public SwingTree Tree;
            public float SwayPhase;
        }

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private float _signpostX;
        private Slot[] _slots;
        private int _shown;
        private TrackSimulation _track;
        private VineConfig _vines;
        private PathFrame _frame;

        private float _spanLengthM;
        private Transform _swingKnot;
        private Transform _activeRope;
        private Transform _ringRoot;
        private Transform[] _ringFill;
        private float[] _ringPhase;
        private Transform _speedLinesRoot;
        private Transform[] _trail;
        private Vector3[] _trailPositions;
        private int _trailCount;
        private float _trailClock;
        private TextMesh _stamp;
        private float _stampLeft;
        private float _speedLinesLeft;
        private float _clock;

        /// <summary>Vines shown last frame (tests).</summary>
        public int ShownVineCount => _shown;

        /// <summary>The route things are placed on (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, VineConfig vineConfig, float viewDistanceM, Font font)
        {
            _frame = PathPlacement.OrIdentity(_frame);
            _runnerConfig = runnerConfig;
            _vines = vineConfig;
            _viewDistanceM = viewDistanceM;
            _signpostX = runnerConfig.LaneCount * runnerConfig.LaneWidthM * 0.5f + PathMarginM;
            _slots = new Slot[Capacity];
            float grabY = vineConfig.GrabPointHeightM;
            float ropeLength = vineConfig.SwingRadiusM;
            float pivotAhead = SwingRigMath.RestPivotAheadM(ropeLength, vineConfig.SwingStartAngleRad);
            _spanLengthM = SwingRigMath.SpanLeadM + SwingRigMath.SpanLengthM(
                pivotAhead, SwingRigMath.DefaultMaxSpeedMps, vineConfig.SwingTicks * (float)RunnerConfig.TickSeconds);
            float padWidth = runnerConfig.LaneCount * runnerConfig.LaneWidthM;
            for (int i = 0; i < Capacity; i++)
            {
                var slot = new Slot();
                var dim = new List<MeshRenderer>();
                Transform root = new GameObject("Vine" + i).transform;
                root.SetParent(transform, false);
                slot.Root = root.gameObject;

                // Cues at the grab point: glow core, halo, ring icon and the gold tuft with a flower ("grab me").
                Transform cues = new GameObject("Cues").transform;
                cues.SetParent(root, false);
                slot.Cues = cues.gameObject;
                slot.Core = MakeDimmable(
                    kit, dim, PrimitiveType.Sphere, "GlowCore", cues, StylePalette.VineGlowCore,
                    Vector3.zero, new Vector3(CoreSizeM, CoreSizeM, CoreSizeM));
                slot.Halo = MakeDimmable(
                    kit, dim, PrimitiveType.Sphere, "GlowHalo", cues, StylePalette.SunGold, Vector3.zero, Vector3.one);
                slot.Icon = new GameObject("RingIcon").transform;
                slot.Icon.SetParent(cues, false);
                for (int k = 0; k < IconSegments; k++)
                {
                    float a = 2f * Mathf.PI * k / IconSegments;
                    MakeDimmable(
                        kit,
                        dim,
                        PrimitiveType.Cube,
                        "IconSegment",
                        slot.Icon,
                        StylePalette.SunGold,
                        new Vector3(Mathf.Cos(a) * IconRadiusM, Mathf.Sin(a) * IconRadiusM, 0f),
                        new Vector3(IconSegmentM, IconSegmentM, IconSegmentM * 0.5f));
                }

                Transform tuft = new GameObject("Tuft").transform;
                tuft.SetParent(cues, false);
                tuft.localPosition = new Vector3(0f, grabY - TuftBelowGrabM, 0f);
                for (int k = 0; k < TuftLeaves; k++)
                {
                    float a = 2f * Mathf.PI * k / TuftLeaves;
                    MakeDimmable(
                        kit,
                        dim,
                        PrimitiveType.Sphere,
                        "TuftLeaf",
                        tuft,
                        StylePalette.SunGold,
                        new Vector3(Mathf.Sin(a) * TuftRadiusM * 0.8f, Mathf.Cos(a) * TuftRadiusM * 0.8f, 0f),
                        new Vector3(TuftRadiusM, TuftRadiusM, TuftRadiusM * 0.5f));
                }

                MakeDimmable(
                    kit, dim, PrimitiveType.Sphere, "TuftFlower", tuft, StylePalette.PulpOrange,
                    new Vector3(0f, 0f, -0.08f), new Vector3(TuftRadiusM, TuftRadiusM, TuftRadiusM));
                EnvironmentArt.ReplaceGroup(tuft, EnvironmentArt.VineTuft);

                Transform sign = new GameObject("Signpost").transform;
                sign.SetParent(root, false);
                kit.Create(
                    PrimitiveType.Cube, "Post", sign, StylePalette.HazardWood,
                    new Vector3(0f, SignpostHeightM * 0.5f, 0f), new Vector3(0.12f, SignpostHeightM, 0.12f));
                kit.Create(
                    PrimitiveType.Cube, "Board", sign, StylePalette.SunGold,
                    new Vector3(0f, SignpostHeightM, 0f), new Vector3(0.9f, 0.5f, 0.08f));
                EnvironmentArt.ReplaceGroup(sign, EnvironmentArt.Signpost);
                slot.Signpost = sign.gameObject;

                // The rig: overhead span, anchor tree with its limb, the hanging rope and its hidden knot, the landing glade.
                Transform rig = new GameObject("SwingRig" + i).transform;
                rig.SetParent(transform, false);
                slot.Rig = rig.gameObject;
                slot.Span = new Transform[SpanSegments];
                for (int k = 0; k < SpanSegments; k++)
                {
                    slot.Span[k] = kit.Create(PrimitiveType.Cylinder, "Span", rig, BarkColor).transform;
                }

                slot.Trunk = new GameObject("AnchorTrunk").transform;
                slot.Trunk.SetParent(rig, false);
                slot.TrunkCylinder = kit.Create(PrimitiveType.Cylinder, "Trunk", slot.Trunk, BarkColor).transform;
                slot.Canopy = kit.Create(PrimitiveType.Sphere, "Canopy", slot.Trunk, StylePalette.DeepCanopyTeal).transform;
                EnvironmentArt.ReplaceGroup(slot.Trunk, EnvironmentArt.TreeTrunk);

                // The limb's local +X runs from the trunk toward the path (the art convention of Tree_Branch).
                slot.Limb = new GameObject("AnchorLimb").transform;
                slot.Limb.SetParent(rig, false);
                slot.LimbCube = kit.Create(PrimitiveType.Cube, "LimbCube", slot.Limb, BarkColor).transform;
                if (EnvironmentArt.ReplaceGroup(slot.Limb, EnvironmentArt.TreeBranch) == null)
                {
                    // The older lane-wide branch art (fits a unit cube) still works on the limb when it exists.
                    EnvironmentArt.AttachAndHide(slot.LimbCube, EnvironmentArt.VineBranch);
                }

                // The rope hangs from the hidden knot (holder origin) down to the grab point (art convention: hangs -Y).
                slot.RopeHolder = new GameObject("RopeHolder").transform;
                slot.RopeHolder.SetParent(rig, false);
                slot.RopeCylinder = MakeDimmable(
                    kit,
                    dim,
                    PrimitiveType.Cylinder,
                    "Rope",
                    slot.RopeHolder,
                    StylePalette.VineRope,
                    new Vector3(0f, -ropeLength * 0.5f, 0f),
                    new Vector3(RopeDiameterM, ropeLength * 0.5f, RopeDiameterM));
                EnvironmentArt.ReplaceGroup(slot.RopeHolder, EnvironmentArt.VineLiana);

                slot.Knot = CreateKnot(kit, dim, rig);

                slot.Glade = new GameObject("LandingGlade").transform;
                slot.Glade.SetParent(rig, false);
                kit.Create(
                    PrimitiveType.Cube, "Cushion", slot.Glade, GladeGrass,
                    Vector3.zero, new Vector3(padWidth, 0.02f, SwingRigMath.GladeLengthM));
                for (int k = 0; k < GladeBushes; k++)
                {
                    float sx = (k & 1) == 0 ? -1f : 1f;
                    float sz = k < 2 ? -1f : 1f;
                    kit.Create(
                        PrimitiveType.Sphere,
                        "GladeBush",
                        slot.Glade,
                        StylePalette.JungleGreen,
                        new Vector3(sx * GladeBushLateralM, GladeBushSizeM * 0.3f, sz * SwingRigMath.GladeLengthM * 0.35f),
                        new Vector3(GladeBushSizeM, GladeBushSizeM * 0.6f, GladeBushSizeM));
                }

                slot.Dimmable = dim.ToArray();
                slot.Normal = new Material[slot.Dimmable.Length];
                slot.Dim = new Material[slot.Dimmable.Length];
                for (int k = 0; k < slot.Dimmable.Length; k++)
                {
                    Material normal = slot.Dimmable[k].sharedMaterial;
                    Color c = normal.color;
                    slot.Normal[k] = normal;
                    slot.Dim[k] = kit.GetMaterial(new Color(c.r * DimFactor, c.g * DimFactor, c.b * DimFactor, c.a), normal);
                }

                slot.AssignedId = -1;
                slot.Root.SetActive(false);
                slot.Rig.SetActive(false);
                _slots[i] = slot;
            }

            _swingKnot = CreateKnot(kit, null, transform);
            _swingKnot.gameObject.SetActive(false);

            _activeRope = kit.Create(PrimitiveType.Cylinder, "ActiveRope", transform, StylePalette.VineRope).transform;
            _activeRope.gameObject.SetActive(false);

            // Release ring: an ink outline segment and a fill segment per step of the swing phase.
            _ringRoot = new GameObject("ReleaseRing").transform;
            _ringRoot.SetParent(transform, false);
            _ringFill = new Transform[RingSegments];
            _ringPhase = new float[RingSegments];
            for (int k = 0; k < RingSegments; k++)
            {
                float phase = (k + 0.5f) / RingSegments;
                _ringPhase[k] = phase;

                // Clockwise from the top, like a clock hand.
                float a = Mathf.PI * 0.5f - 2f * Mathf.PI * phase;
                Vector3 p = new Vector3(Mathf.Cos(a) * RingRadiusM, Mathf.Sin(a) * RingRadiusM, 0f);
                kit.Create(
                    PrimitiveType.Cube, "RingOutline", _ringRoot, StylePalette.Ink,
                    p + new Vector3(0f, 0f, 0.02f), new Vector3(RingSegmentM * 1.3f, RingSegmentM * 1.3f, 0.02f));
                _ringFill[k] = kit.Create(
                    PrimitiveType.Cube, "RingFill", _ringRoot, RingColor(phase, vineConfig), p,
                    new Vector3(RingSegmentM, RingSegmentM, 0.03f));
            }

            _ringRoot.gameObject.SetActive(false);

            _speedLinesRoot = new GameObject("PerfectSpeedLines").transform;
            _speedLinesRoot.SetParent(transform, false);
            for (int k = 0; k < SpeedLines; k++)
            {
                float a = 2f * Mathf.PI * k / SpeedLines;
                Transform line = kit.Create(
                    PrimitiveType.Cube, "SpeedLine", _speedLinesRoot, StylePalette.SunGold,
                    new Vector3(Mathf.Cos(a) * 1.4f, Mathf.Sin(a) * 1.4f, 0f), new Vector3(0.06f, 0.9f, 0.06f));
                line.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }

            _speedLinesRoot.gameObject.SetActive(false);

            _trail = new Transform[TrailLength];
            _trailPositions = new Vector3[TrailLength];
            for (int k = 0; k < TrailLength; k++)
            {
                float size = 0.45f * (1f - k / (float)TrailLength) + 0.1f;
                _trail[k] = kit.Create(
                    PrimitiveType.Sphere, "GoldTrail", transform, StylePalette.SunGold, Vector3.zero, new Vector3(size, size, size));
                _trail[k].gameObject.SetActive(false);
            }

            if (font != null)
            {
                var stampObject = new GameObject("ReleaseStamp");
                stampObject.transform.SetParent(transform, false);
                MeshRenderer stampRenderer = stampObject.AddComponent<MeshRenderer>();
                _stamp = stampObject.AddComponent<TextMesh>();
                _stamp.font = font;
                _stamp.fontSize = 64;
                _stamp.characterSize = 0.06f;
                _stamp.anchor = TextAnchor.MiddleCenter;
                _stamp.alignment = TextAlignment.Center;
                _stamp.fontStyle = FontStyle.Bold;
                _stamp.text = string.Empty;
                stampRenderer.sharedMaterial = font.material;
                stampObject.SetActive(false);
            }
        }

        private static Transform MakeDimmable(
            GrayBoxKit kit,
            List<MeshRenderer> dim,
            PrimitiveType type,
            string name,
            Transform parent,
            Color color,
            Vector3 localPosition,
            Vector3 localScale)
        {
            Transform t = kit.Create(type, name, parent, color, localPosition, localScale);
            dim.Add(t.GetComponent<MeshRenderer>());
            return t;
        }

        /// <summary>A leafy knot with a small sun-gold glow-lit knot hanging from it (hides where the liana loops over the span).</summary>
        private static Transform CreateKnot(GrayBoxKit kit, List<MeshRenderer> dim, Transform parent)
        {
            Transform knot = new GameObject("Knot").transform;
            knot.SetParent(parent, false);
            Vector3 leafScale = new Vector3(KnotLeafM, KnotLeafM * 0.8f, KnotLeafM);
            Vector3 goldScale = new Vector3(KnotGoldM, KnotGoldM, KnotGoldM);
            Vector3 goldPosition = new Vector3(0f, -0.3f, 0f);
            if (dim != null)
            {
                MakeDimmable(kit, dim, PrimitiveType.Sphere, "KnotLeaves", knot, StylePalette.JungleGreen, Vector3.zero, leafScale);
                MakeDimmable(kit, dim, PrimitiveType.Sphere, "KnotGlow", knot, StylePalette.SunGold, goldPosition, goldScale);
            }
            else
            {
                kit.Create(PrimitiveType.Sphere, "KnotLeaves", knot, StylePalette.JungleGreen, Vector3.zero, leafScale);
                kit.Create(PrimitiveType.Sphere, "KnotGlow", knot, StylePalette.SunGold, goldPosition, goldScale);
            }

            return knot;
        }

        /// <summary>
        /// Puts the glyphs of the release stamps ("PERFECT", "GOOD", ...) into the font texture now (setup time), so the
        /// first release does not stall. Same size and style as the stamp text.
        /// </summary>
        public void PrewarmFont()
        {
            if (_stamp == null || _stamp.font == null)
            {
                return;
            }

            _stamp.font.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !?+-.", _stamp.fontSize, FontStyle.Bold);
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            if (_vines == null)
            {
                _vines = session.Runner.Vines;
            }
            _stampLeft = 0f;
            _speedLinesLeft = 0f;
            _trailCount = 0;
            _trailClock = 0f;
            if (_slots != null)
            {
                // A new run restarts vine ids and may use a new seed: recompute every slot's tree and sway.
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i].AssignedId = -1;
                }
            }

            if (_stamp != null)
            {
                _stamp.gameObject.SetActive(false);
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (e.Type != RunnerEventType.VineReleased)
            {
                return;
            }

            var grade = (VineReleaseGrade)e.Value;
            if (grade == VineReleaseGrade.Perfect)
            {
                ShowStamp("PERFECT!", StylePalette.SunGold);
                _speedLinesLeft = SpeedLineSeconds;
                _trailCount = 0;
            }
            else if (grade == VineReleaseGrade.Good)
            {
                ShowStamp("GOOD", StylePalette.Parchment);
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_slots == null)
            {
                return;
            }

            if (!session.InHitPause)
            {
                _clock += realDeltaSeconds;
            }

            RunnerSimulation runner = session.Runner;
            RunnerState state = runner.Current;
            RunnerInterpolation.Evaluate(runner.Previous, state, alpha, out float heroX, out float heroY, out double heroZ);
            bool carried = state.Locomotion == Locomotion.Carried;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_clock * PulseHz * 2f * Mathf.PI);

            RenderIdleVines(state, heroZ, carried, pulse);
            RenderSwing(runner, alpha, heroX, heroY, heroZ, carried);
            RenderFeedback(state, heroX, heroY, heroZ, realDeltaSeconds, session.InHitPause);
        }

        private void RenderIdleVines(in RunnerState state, double heroZ, bool carried, float pulse)
        {
            int used = 0;
            int targetId = carried ? state.AimVineId : 0;
            if (_track != null && _vines != null)
            {
                double minZ = heroZ - RigTrailM;
                double maxZ = heroZ + _viewDistanceM + SignpostLeadM;
                float grabY = _vines.GrabPointHeightM;
                float ropeLength = _vines.SwingRadiusM;
                float pivotY = SwingRigMath.RestPivotHeightM(grabY, ropeLength, _vines.SwingStartAngleRad);
                int count = _track.VineCount;
                for (int i = 0; i < count && used < Capacity; i++)
                {
                    ref readonly VineInstance v = ref _track.GetVine(i);
                    if (v.Z > maxZ)
                    {
                        break;
                    }

                    if (v.Z < minZ)
                    {
                        continue;
                    }

                    Slot slot = _slots[used++];
                    if (slot.AssignedId != v.Id)
                    {
                        AssignVine(slot, v.Id, v.ChunkSerial, v.Row);
                    }

                    // The vine the runner is holding keeps its span and tree; only its rest rope and cues go away.
                    bool held = carried && v.Id == state.VineId;
                    bool behind = v.Z < heroZ - BehindM;
                    if (targetId == 0 && !carried && !behind && v.Z >= heroZ - _vines.GrabZoneLengthM * 0.5f)
                    {
                        targetId = v.Id;
                    }

                    float laneX = _runnerConfig.LaneCenterX(v.Lane);
                    _frame.Sample(v.Z, out PathPose vinePose);
                    Vector3 rootPosition = PathPlacement.Point(vinePose, laneX, 0f);
                    Quaternion rootRotation = PathPlacement.Orientation(vinePose);
                    slot.Root.transform.localPosition = rootPosition;
                    slot.Root.transform.localRotation = rootRotation;
                    if (!slot.Root.activeSelf)
                    {
                        slot.Root.SetActive(true);
                    }

                    if (!slot.Rig.activeSelf)
                    {
                        slot.Rig.SetActive(true);
                    }

                    bool cues = !held && !behind;
                    if (slot.Cues.activeSelf != cues)
                    {
                        slot.Cues.SetActive(cues);
                    }

                    if (cues)
                    {
                        bool aimed = carried && v.Id == state.AimVineId;
                        float halo = HaloSizeM * (0.85f + 0.3f * pulse) * (aimed ? AimedHaloScale : 1f);
                        slot.Halo.localPosition = new Vector3(0f, grabY, 0.05f);
                        slot.Halo.localScale = new Vector3(halo, halo, halo * 0.3f);
                        slot.Core.localPosition = new Vector3(0f, grabY, -0.05f);
                        float icon = 0.9f + 0.2f * pulse;
                        slot.Icon.localPosition = new Vector3(0f, grabY, -0.1f);
                        slot.Icon.localScale = new Vector3(icon, icon, 1f);
                    }

                    bool sign = v.Row == 0 && !behind;
                    if (slot.Signpost.activeSelf != sign)
                    {
                        slot.Signpost.SetActive(sign);
                    }

                    if (sign)
                    {
                        // At the path edge, SignpostLeadM before the vine along the route (placed in world, then
                        // expressed in the vine root's frame because the signpost is its child).
                        float side = laneX > 0.01f ? 1f : -1f;
                        _frame.Sample(v.Z - SignpostLeadM, out PathPose signPose);
                        Quaternion inverseRoot = Quaternion.Inverse(rootRotation);
                        slot.Signpost.transform.localPosition = inverseRoot * (PathPlacement.Point(signPose, side * _signpostX, 0f) - rootPosition);
                        slot.Signpost.transform.localRotation = inverseRoot * PathPlacement.Orientation(signPose);
                    }

                    bool rope = cues;
                    if (slot.RopeHolder.gameObject.activeSelf != rope)
                    {
                        slot.RopeHolder.gameObject.SetActive(rope);
                        slot.Knot.gameObject.SetActive(rope);
                    }

                    if (rope)
                    {
                        // Hangs from the knot (parked ahead of the grab point at the span height), swaying about the
                        // grab point so the glow stays put (spec 8.2 "already swaying", fades near the grab zone).
                        float amplitude = SwingRigMath.SwayAmplitudeForDistance((float)(v.Z - heroZ));
                        float sway = SwingRigMath.SwayAngleRad(_clock, slot.SwayPhase, amplitude);
                        double angle = _vines.SwingStartAngleRad + sway;
                        var localUp = new Vector3(0f, (float)System.Math.Cos(angle), -(float)System.Math.Sin(angle));
                        Vector3 knot = rootPosition + (rootRotation * new Vector3(0f, grabY + (localUp.y * ropeLength), localUp.z * ropeLength));
                        slot.RopeHolder.localPosition = knot;
                        slot.RopeHolder.localRotation = rootRotation * Quaternion.FromToRotation(Vector3.down, -localUp);
                        slot.Knot.localPosition = knot;
                        slot.Knot.localRotation = rootRotation;
                    }

                    bool last = i + 1 >= count || _track.GetVine(i + 1).ChunkSerial != v.ChunkSerial;
                    RenderRig(slot, v.Z, laneX, pivotY, last, behind);
                }
            }

            for (int i = 0; i < used; i++)
            {
                Slot slot = _slots[i];
                bool dimmed = targetId != 0 && slot.VineId != targetId;
                if (slot.Dimmed != dimmed)
                {
                    ApplyDim(slot, dimmed);
                }
            }

            for (int i = used; i < _shown; i++)
            {
                _slots[i].Root.SetActive(false);
                _slots[i].Rig.SetActive(false);
            }

            _shown = used;
        }

        /// <summary>Per-vine stateless data: anchor tree and sway phase from the run seed and the Scenery stream id.</summary>
        private void AssignVine(Slot slot, int vineId, int chunkSerial, int row)
        {
            slot.AssignedId = vineId;
            slot.VineId = vineId;
            ulong seed = _frame.RunSeed;
            SwingTree tree = SwingRigMath.PlaceTree(seed, RandomStreamIds.Scenery, chunkSerial, row);
            slot.Tree = tree;
            slot.SwayPhase = SwingRigMath.SwayPhaseRad(seed, RandomStreamIds.Scenery, chunkSerial, row);
            slot.TrunkCylinder.localPosition = new Vector3(0f, tree.HeightM * 0.5f, 0f);
            slot.TrunkCylinder.localScale = new Vector3(tree.TrunkRadiusM * 2f, tree.HeightM * 0.5f, tree.TrunkRadiusM * 2f);
            slot.Canopy.localPosition = new Vector3(0f, tree.HeightM - 3f, 0f);
            slot.Canopy.localScale = new Vector3(tree.TrunkRadiusM * 6f, 5f, tree.TrunkRadiusM * 6f);
        }

        private void RenderRig(Slot slot, double grabS, float laneX, float pivotY, bool lastOfSection, bool behind)
        {
            // Anchor tree and limb: beside the path, visible long before the grab; gone once behind the runner.
            bool tree = !behind;
            if (slot.Trunk.gameObject.activeSelf != tree)
            {
                slot.Trunk.gameObject.SetActive(tree);
                slot.Limb.gameObject.SetActive(tree);
            }

            double spanStart = grabS - SwingRigMath.SpanLeadM;
            _frame.Sample(spanStart, out PathPose startPose);
            if (tree)
            {
                slot.Trunk.localPosition = PathPlacement.Point(startPose, slot.Tree.TrunkX, 0f);
                slot.Trunk.localRotation = PathPlacement.Orientation(startPose);
                Vector3 limbStart = PathPlacement.Point(startPose, slot.Tree.TrunkX, pivotY);
                Vector3 limbVector = PathPlacement.Point(startPose, laneX, pivotY) - limbStart;
                float limbLength = limbVector.magnitude;
                if (limbLength > 0.01f)
                {
                    slot.Limb.localPosition = limbStart;
                    slot.Limb.localRotation = Quaternion.LookRotation(limbVector / limbLength, startPose.Up) * LimbArtAxis;
                    slot.LimbCube.localPosition = new Vector3(limbLength * 0.5f, 0f, 0f);
                    slot.LimbCube.localScale = new Vector3(limbLength, LimbThicknessM, LimbThicknessM);
                }
            }

            // The span at the pivot height, from the limb along the route over the vine's lane, in short pieces so it bends with the route.
            Vector3 previous = PathPlacement.Point(startPose, laneX, pivotY);
            for (int k = 0; k < SpanSegments; k++)
            {
                _frame.Sample(spanStart + (_spanLengthM * (k + 1) / SpanSegments), out PathPose segPose);
                Vector3 next = PathPlacement.Point(segPose, laneX, pivotY);
                Vector3 d = next - previous;
                float length = d.magnitude;
                Transform seg = slot.Span[k];
                if (length > 0.01f)
                {
                    seg.localPosition = (previous + next) * 0.5f;
                    seg.localRotation = Quaternion.LookRotation(d / length, segPose.Up) * CylinderAlongZ;
                    seg.localScale = new Vector3(SpanThicknessM, length * 0.5f * 1.02f, SpanThicknessM);
                }

                previous = next;
            }

            // Landing glade: a cushion and bushes on the far rim of the last vine of a section.
            if (slot.Glade.gameObject.activeSelf != lastOfSection)
            {
                slot.Glade.gameObject.SetActive(lastOfSection);
            }

            if (lastOfSection)
            {
                _frame.Sample(grabS + SwingRigMath.GladeStartAfterGrabM + (SwingRigMath.GladeLengthM * 0.5f), out PathPose gladePose);
                slot.Glade.localPosition = PathPlacement.Point(gladePose, 0f, GladeLiftM);
                slot.Glade.localRotation = PathPlacement.Orientation(gladePose);
            }
        }

        private static void ApplyDim(Slot slot, bool dimmed)
        {
            slot.Dimmed = dimmed;
            Material[] source = dimmed ? slot.Dim : slot.Normal;
            for (int k = 0; k < slot.Dimmable.Length; k++)
            {
                slot.Dimmable[k].sharedMaterial = source[k];
            }
        }

        private void RenderSwing(RunnerSimulation runner, float alpha, float heroX, float heroY, double heroZ, bool carried)
        {
            if (_activeRope.gameObject.activeSelf != carried)
            {
                _activeRope.gameObject.SetActive(carried);
            }

            if (_ringRoot.gameObject.activeSelf != carried)
            {
                _ringRoot.gameObject.SetActive(carried);
            }

            if (_swingKnot.gameObject.activeSelf != carried)
            {
                _swingKnot.gameObject.SetActive(carried);
            }

            if (!carried || _vines == null)
            {
                return;
            }

            RunnerState current = runner.Current;
            RunnerState previous = runner.Previous;
            bool both = previous.Locomotion == Locomotion.Carried;
            float angle = both ? Mathf.Lerp(previous.SwingAngleRad, current.SwingAngleRad, alpha) : current.SwingAngleRad;
            float phase = both ? Mathf.Lerp(previous.SwingPhase, current.SwingPhase, alpha) : current.SwingPhase;

            // The rope is drawn in the route frame at the hero (pendulum angle in the frame's y/z plane), then mapped.
            _frame.Sample(heroZ, out PathPose handPose);
            Quaternion rot = PathPlacement.Orientation(handPose);
            Vector3 hand = PathPlacement.Point(handPose, heroX, heroY + RunnerView.HandAboveFeetM);
            Vector3 localUp = new Vector3(0f, Mathf.Cos(angle), -Mathf.Sin(angle));
            Vector3 up = rot * localUp;
            float length = _vines.SwingRadiusM;
            _activeRope.localPosition = hand + up * (length * 0.5f);
            _activeRope.localRotation = rot * Quaternion.FromToRotation(Vector3.up, localUp);
            _activeRope.localScale = new Vector3(RopeDiameterM, length * 0.5f, RopeDiameterM);

            // The hidden knot rides the span with the runner: it is the rope's top end (rope end = hand, AC-318).
            _swingKnot.localPosition = hand + (up * length);
            _swingKnot.localRotation = rot;

            bool inPerfect = phase >= _vines.PerfectStartPhase && phase < _vines.PerfectEndPhase;
            float flash = inPerfect ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(_clock * RingFlashHz * Mathf.PI)) : 1f;
            _ringRoot.localPosition = hand + (rot * new Vector3(0f, 0f, -RingTowardCameraM));
            _ringRoot.localRotation = rot;
            _ringRoot.localScale = new Vector3(flash, flash, 1f);
            for (int k = 0; k < RingSegments; k++)
            {
                bool filled = _ringPhase[k] <= phase || (_ringPhase[k] >= _vines.PerfectStartPhase && _ringPhase[k] < _vines.PerfectEndPhase);
                GameObject fill = _ringFill[k].gameObject;
                if (fill.activeSelf != filled)
                {
                    fill.SetActive(filled);
                }

                // Unreached Perfect segments show as small gold marks so the band is always visible.
                float s = _ringPhase[k] <= phase ? RingSegmentM : RingSegmentM * 0.5f;
                _ringFill[k].localScale = new Vector3(s, s, 0.03f);
            }
        }

        private void RenderFeedback(in RunnerState state, float heroX, float heroY, double heroZ, float dt, bool frozen)
        {
            float step = frozen ? 0f : dt;
            _frame.Sample(heroZ, out PathPose pose);
            Quaternion rot = PathPlacement.Orientation(pose);
            Vector3 hero = PathPlacement.Point(pose, heroX, heroY);

            if (_stamp != null)
            {
                bool showStamp = _stampLeft > 0f;
                if (_stamp.gameObject.activeSelf != showStamp)
                {
                    _stamp.gameObject.SetActive(showStamp);
                }

                if (showStamp)
                {
                    float u = 1f - _stampLeft / StampSeconds;
                    _stamp.transform.localPosition = hero + (rot * new Vector3(0f, StampAboveFeetM + StampRiseM * u, StampAheadM));
                    _stamp.transform.localRotation = rot;
                    float scale = u < 0.15f ? Mathf.Lerp(1.6f, 1f, u / 0.15f) : 1f;
                    _stamp.transform.localScale = new Vector3(scale, scale, scale);
                    _stampLeft -= step;
                }
            }

            bool lines = _speedLinesLeft > 0f;
            if (_speedLinesRoot.gameObject.activeSelf != lines)
            {
                _speedLinesRoot.gameObject.SetActive(lines);
            }

            if (lines)
            {
                float u = 1f - _speedLinesLeft / SpeedLineSeconds;
                _speedLinesRoot.localPosition = hero + (rot * new Vector3(0f, 0.9f, 0f));
                _speedLinesRoot.localRotation = rot;
                float spread = 1f + u;
                _speedLinesRoot.localScale = new Vector3(spread, spread, 1f);
                _speedLinesLeft -= step;
            }

            // Sun-gold trail during a Perfect launch (style guide 10).
            bool trail = state.InVineFlight && state.LastReleaseGrade == VineReleaseGrade.Perfect;
            if (trail)
            {
                _trailClock += step;
                if (_trailClock >= TrailSpacingSeconds || _trailCount == 0)
                {
                    _trailClock = 0f;
                    for (int k = TrailLength - 1; k > 0; k--)
                    {
                        _trailPositions[k] = _trailPositions[k - 1];
                    }

                    _trailPositions[0] = hero + (rot * new Vector3(0f, 0.9f, 0f));
                    if (_trailCount < TrailLength)
                    {
                        _trailCount++;
                    }
                }
            }
            else
            {
                _trailCount = 0;
            }

            for (int k = 0; k < TrailLength; k++)
            {
                bool on = k < _trailCount;
                GameObject go = _trail[k].gameObject;
                if (go.activeSelf != on)
                {
                    go.SetActive(on);
                }

                if (on)
                {
                    _trail[k].localPosition = _trailPositions[k];
                }
            }
        }

        private void ShowStamp(string text, Color color)
        {
            if (_stamp == null)
            {
                return;
            }

            _stamp.text = text;
            _stamp.color = color;
            _stampLeft = StampSeconds;
        }

        /// <summary>Too early: muted path tone; Good: parchment; Perfect: sun-gold (GDD 7.3 step 4 windows).</summary>
        private static Color RingColor(float phase, VineConfig vines)
        {
            if (phase < vines.GoodStartPhase)
            {
                return StylePalette.PathAlternate;
            }

            if (phase >= vines.PerfectStartPhase && phase < vines.PerfectEndPhase)
            {
                return StylePalette.SunGold;
            }

            return StylePalette.Parchment;
        }
    }
}
