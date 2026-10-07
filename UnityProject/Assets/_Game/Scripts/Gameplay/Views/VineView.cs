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
    /// Gray-box vines for the fixed-pivot swing (spec 004 sections 3 and 8, GDD 7.2, 7.3). Every vine in the
    /// <see cref="TrackSimulation"/> ring is ONE visible branch growing out of its own anchor tree (trunk 6 to 9 m
    /// beside the path, base on the ground, or on the forest floor below the bough in the High layer), curving over the
    /// path with its tip exactly at the pivot (lane centre, vine z, <see cref="VineConfig.PivotHeightM"/> = 17 m). A
    /// leafy knot hides the top of the rope, and the liana (a tapered strip of cylinders, or the Vine_Liana art) hangs
    /// from the pivot as a damped pendulum: at rest it sways about 5 degrees with the true pendulum period (fading to
    /// 0.5 degrees near the grab zone), while the hero holds it the rope follows the simulation's theta exactly from
    /// the pivot to the hand, and after the release it keeps swinging from the released theta and omega (decay about
    /// 2 s) and returns to the rest sway. Cues at the rope end: a gold tuft with an orange flower, a glow core, a pulsing
    /// halo and a ring icon; the vines not aimed at are dimmed 25 percent. Also: the signpost 25 m before the first vine,
    /// the landing glade 12 m past the last pivot of a section, the release ring around the hand (fills with the swing
    /// phase, Perfect band in sun-gold), the PERFECT or GOOD stamp, speed lines and a sun-gold trail for a Perfect.
    /// Everything is placed through the PathFrame, so branches and trees follow the bends and the canopy layers.
    /// Pooled; no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class VineView : MonoBehaviour, IRunView
    {
        private const int Capacity = 12;
        private const float BehindM = 6f;
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

        // The rig: anchor tree, one branch, tapered rope, leaf tufts, landing glade. All gray-box primitives.
        private const float RigTrailM = 40f;
        private const float DimFactor = 0.75f;
        private const float TuftBelowGrabM = 0.4f;
        private const int TuftLeaves = 5;
        private const float TuftRadiusM = 0.2f;
        private const float GladeLiftM = 0.04f;
        private const int GladeBushes = 4;
        private const float GladeBushLateralM = 6f;
        private const float GladeBushSizeM = 1.3f;

        private const int BranchSegments = 5;
        private const int RopeSegments = 4;
        private const float RopeTopDiameterM = 0.1f;
        private const float RopeBottomDiameterM = 0.07f;
        private const float SegmentOverlap = 1.03f;
        private const float TrunkFlareM = 2.2f;
        private const float TrunkFlareWidth = 1.35f;
        private const float TrunkSinkM = 0.6f;
        private const float LeafLiftM = 0.3f;

        /// <summary>
        /// In the High layer the forest floor is this far below the bough (mirrors <c>BoughView</c>'s 24 m), so the anchor
        /// trunk comes up from there and the branch (17 m above the bough) stays clear of it. [ASSUMED]
        /// </summary>
        private const float HighFloorDropM = 24f;

        // Art slot conventions, assumed until the asset agent confirms (the report lists them).
        private const float TrunkArtRadiusM = 1.5f;
        private const float TrunkArtHeightM = 34f;
        private const float BranchArtLengthM = 8f;
        private const float LianaArtLengthM = 6f;

        private const float PostStepSeconds = 1f / 120f;
        private const int PostCapacity = 3;
        private const float PostMaxDeltaSeconds = 0.1f;

        private static readonly Color BarkColor = new Color(0.369f, 0.275f, 0.188f, 1f);
        private static readonly Color GladeGrass = new Color(0.498f, 0.761f, 0.353f, 1f);

        /// <summary>The Tree_Branch art runs along its local +X from the trunk toward the path.</summary>
        private static readonly Quaternion BranchArtAxis = Quaternion.Euler(0f, -90f, 0f);

        /// <summary>Branch points that carry a leaf tuft, and the tuft widths (the first sits at the trunk).</summary>
        private static readonly int[] LeafPointIndex = { 0, 2, 3, 4 };

        private static readonly float[] LeafSizeM = { 1.9f, 1.3f, 1.1f, 0.95f };

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
            public Transform Trunk;
            public Transform TrunkBody;
            public Transform TrunkFlare;
            public Transform Crown;
            public Transform TrunkArtRoot;
            public Transform TrunkArt;
            public Transform[] BranchPieces;
            public Transform[] BranchLeaves;
            public Transform BranchArtRoot;
            public Transform BranchArt;
            public Transform[] RopePieces;
            public Transform RopeArtRoot;
            public Transform RopeArt;
            public Transform Knot;
            public Transform Glade;

            // Dimming of the cue pieces and the rope (about 25 percent) without per-frame allocation.
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

        private float _ropeLengthM;
        private float _pivotHeightM;
        private float _gravityMps2;
        private float _swayPeriodS;
        private float _goodStartPhase;
        private float _perfectStartPhase;
        private float _perfectEndPhase;
        private readonly Vector3[] _branchPoints = new Vector3[BranchSegments + 1];

        // The rope after a release keeps swinging as a damped pendulum (presentation copy, not in the hash).
        private readonly int[] _postId = new int[PostCapacity];
        private readonly float[] _postTheta = new float[PostCapacity];
        private readonly float[] _postOmega = new float[PostCapacity];
        private int _postNext;
        private bool _releasePending;

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
            _signpostX = (runnerConfig.LaneCount * runnerConfig.LaneWidthM * 0.5f) + PathMarginM;
            _ropeLengthM = vineConfig.RopeLengthM;
            _pivotHeightM = vineConfig.PivotHeightM;
            _gravityMps2 = vineConfig.SwingGravityMps2;
            _swayPeriodS = SwingRigMath.SwayPeriodSeconds(_ropeLengthM, _gravityMps2);
            _goodStartPhase = (float)vineConfig.PhaseAt(vineConfig.GoodStartTick);
            _perfectStartPhase = (float)vineConfig.PhaseAt(vineConfig.PerfectStartTick);
            _perfectEndPhase = (float)vineConfig.PhaseAt(vineConfig.PerfectEndTick);
            _slots = new Slot[Capacity];
            float padWidth = runnerConfig.LaneCount * runnerConfig.LaneWidthM;
            for (int i = 0; i < Capacity; i++)
            {
                var slot = new Slot();
                var dim = new List<MeshRenderer>();
                Transform root = new GameObject("Vine" + i).transform;
                root.SetParent(transform, false);
                slot.Root = root.gameObject;

                BuildCues(kit, dim, root, slot);
                BuildSignpost(kit, root, slot);

                // The rig: anchor tree, its one branch, the hanging rope and the leafy knot at the pivot, the landing glade.
                Transform rig = new GameObject("SwingRig" + i).transform;
                rig.SetParent(transform, false);
                slot.Rig = rig.gameObject;
                BuildTree(kit, rig, slot);
                BuildBranch(kit, rig, slot);
                BuildRope(kit, dim, rig, slot);
                slot.Knot = CreateKnot(kit, rig);
                BuildGlade(kit, rig, slot, padWidth);

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
                float a = (Mathf.PI * 0.5f) - (2f * Mathf.PI * phase);
                Vector3 p = new Vector3(Mathf.Cos(a) * RingRadiusM, Mathf.Sin(a) * RingRadiusM, 0f);
                kit.Create(
                    PrimitiveType.Cube, "RingOutline", _ringRoot, StylePalette.Ink,
                    p + new Vector3(0f, 0f, 0.02f), new Vector3(RingSegmentM * 1.3f, RingSegmentM * 1.3f, 0.02f));
                _ringFill[k] = kit.Create(
                    PrimitiveType.Cube, "RingFill", _ringRoot, RingColor(phase), p,
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
                line.localRotation = Quaternion.Euler(0f, 0f, (a * Mathf.Rad2Deg) - 90f);
            }

            _speedLinesRoot.gameObject.SetActive(false);

            _trail = new Transform[TrailLength];
            _trailPositions = new Vector3[TrailLength];
            for (int k = 0; k < TrailLength; k++)
            {
                float size = (0.45f * (1f - (k / (float)TrailLength))) + 0.1f;
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

        /// <summary>The "grab me" cues at the rope end: glow core, halo, ring icon and the gold tuft with an orange flower.</summary>
        private static void BuildCues(GrayBoxKit kit, List<MeshRenderer> dim, Transform root, Slot slot)
        {
            Transform cues = new GameObject("Cues").transform;
            cues.SetParent(root, false);
            slot.Cues = cues.gameObject;
            slot.Core = MakeDimmable(
                kit, dim, PrimitiveType.Sphere, "GlowCore", cues, StylePalette.VineGlowCore,
                new Vector3(0f, 0f, -0.05f), new Vector3(CoreSizeM, CoreSizeM, CoreSizeM));
            slot.Halo = MakeDimmable(
                kit, dim, PrimitiveType.Sphere, "GlowHalo", cues, StylePalette.SunGold, new Vector3(0f, 0f, 0.05f), Vector3.one);
            slot.Icon = new GameObject("RingIcon").transform;
            slot.Icon.SetParent(cues, false);
            slot.Icon.localPosition = new Vector3(0f, 0f, -0.1f);
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
            tuft.localPosition = new Vector3(0f, -TuftBelowGrabM, 0f);
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
        }

        private static void BuildSignpost(GrayBoxKit kit, Transform root, Slot slot)
        {
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
        }

        /// <summary>
        /// The anchor tree: trunk cylinder with a flared foot and a leaf crown. The Tree_Trunk art (native radius 1.5 m,
        /// height 34 m, base at the origin) is scaled to this tree and replaces the cylinder when it is present.
        /// </summary>
        private static void BuildTree(GrayBoxKit kit, Transform rig, Slot slot)
        {
            slot.Trunk = new GameObject("AnchorTrunk").transform;
            slot.Trunk.SetParent(rig, false);
            slot.TrunkBody = kit.Create(PrimitiveType.Cylinder, "Trunk", slot.Trunk, BarkColor).transform;
            slot.TrunkFlare = kit.Create(PrimitiveType.Cylinder, "TrunkFlare", slot.Trunk, BarkColor).transform;
            slot.Crown = kit.Create(PrimitiveType.Sphere, "Canopy", slot.Trunk, StylePalette.DeepCanopyTeal).transform;
            slot.TrunkArtRoot = new GameObject("TrunkArt").transform;
            slot.TrunkArtRoot.SetParent(slot.Trunk, false);
            slot.TrunkArt = EnvironmentArt.Attach(slot.TrunkArtRoot, EnvironmentArt.TreeTrunk);
            if (slot.TrunkArt != null)
            {
                slot.TrunkBody.gameObject.SetActive(false);
                slot.TrunkFlare.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The one branch: a tapered chain of cylinders along the curve from the trunk to the pivot, with leaf tufts. The
        /// Tree_Branch art (native length 8 m along +X, base at the origin inside the trunk) is stretched to the chord
        /// and replaces the cylinders when it is present.
        /// </summary>
        private static void BuildBranch(GrayBoxKit kit, Transform rig, Slot slot)
        {
            slot.BranchPieces = new Transform[BranchSegments];
            for (int k = 0; k < BranchSegments; k++)
            {
                slot.BranchPieces[k] = kit.Create(PrimitiveType.Cylinder, "Branch", rig, BarkColor).transform;
            }

            slot.BranchLeaves = new Transform[LeafPointIndex.Length];
            for (int k = 0; k < LeafPointIndex.Length; k++)
            {
                Color leaf = (k & 1) == 0 ? StylePalette.JungleGreen : StylePalette.DeepCanopyTeal;
                slot.BranchLeaves[k] = kit.Create(PrimitiveType.Sphere, "BranchLeaves", rig, leaf).transform;
            }

            slot.BranchArtRoot = new GameObject("BranchArt").transform;
            slot.BranchArtRoot.SetParent(rig, false);
            slot.BranchArt = EnvironmentArt.Attach(slot.BranchArtRoot, EnvironmentArt.TreeBranch);
            if (slot.BranchArt != null)
            {
                for (int k = 0; k < BranchSegments; k++)
                {
                    slot.BranchPieces[k].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// The rope: a few tapered cylinders along the pivot-to-hand line. The Vine_Liana art (native length 6 m, top
        /// anchor at the origin, hanging along -Y) is stretched to the rope length and replaces them when it is present.
        /// </summary>
        private static void BuildRope(GrayBoxKit kit, List<MeshRenderer> dim, Transform rig, Slot slot)
        {
            slot.RopePieces = new Transform[RopeSegments];
            for (int k = 0; k < RopeSegments; k++)
            {
                slot.RopePieces[k] = MakeDimmable(
                    kit, dim, PrimitiveType.Cylinder, "Rope", rig, StylePalette.VineRope, Vector3.zero, Vector3.one);
            }

            slot.RopeArtRoot = new GameObject("RopeArt").transform;
            slot.RopeArtRoot.SetParent(rig, false);
            slot.RopeArt = EnvironmentArt.Attach(slot.RopeArtRoot, EnvironmentArt.VineLiana);
            if (slot.RopeArt != null)
            {
                for (int k = 0; k < RopeSegments; k++)
                {
                    slot.RopePieces[k].gameObject.SetActive(false);
                }
            }
        }

        private static void BuildGlade(GrayBoxKit kit, Transform rig, Slot slot, float padWidth)
        {
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

        /// <summary>The leafy knot at the pivot: two leaf puffs hiding the rope top, with the rope loop showing under them.</summary>
        private static Transform CreateKnot(GrayBoxKit kit, Transform parent)
        {
            Transform knot = new GameObject("Knot").transform;
            knot.SetParent(parent, false);
            kit.Create(
                PrimitiveType.Sphere, "KnotLeaves", knot, StylePalette.JungleGreen,
                new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 0.65f, 0.8f));
            kit.Create(
                PrimitiveType.Sphere, "KnotLeavesSide", knot, StylePalette.DeepCanopyTeal,
                new Vector3(0.25f, 0.2f, 0.1f), new Vector3(0.55f, 0.45f, 0.55f));
            kit.Create(
                PrimitiveType.Sphere, "KnotLoop", knot, StylePalette.VineRope,
                new Vector3(0f, -0.3f, 0f), new Vector3(0.22f, 0.22f, 0.22f));
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
            _stampLeft = 0f;
            _speedLinesLeft = 0f;
            _trailCount = 0;
            _trailClock = 0f;
            _releasePending = false;
            for (int i = 0; i < PostCapacity; i++)
            {
                _postId[i] = 0;
            }

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

            _releasePending = true;
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

            float step = session.InHitPause ? 0f : realDeltaSeconds;
            _clock += step;

            RunnerSimulation runner = session.Runner;
            RunnerState state = runner.Current;
            RunnerInterpolation.Evaluate(runner.Previous, state, alpha, out float heroX, out float heroY, out double heroZ);
            bool carried = state.Locomotion == Locomotion.Carried;
            float pulse = 0.5f + (0.5f * Mathf.Sin(_clock * PulseHz * 2f * Mathf.PI));

            // The real pendulum angle (the simulation's theta), interpolated between the two ticks.
            float heldTheta = 0f;
            float phase = 0f;
            if (carried)
            {
                RunnerState previous = runner.Previous;
                bool both = previous.Locomotion == Locomotion.Carried;
                heldTheta = both ? Mathf.Lerp(previous.SwingAngleRad, state.SwingAngleRad, alpha) : state.SwingAngleRad;
                phase = both ? Mathf.Lerp(previous.SwingPhase, state.SwingPhase, alpha) : state.SwingPhase;
            }

            if (_releasePending)
            {
                _releasePending = false;
                StartPostRelease(runner.ReleasedVineId, (float)runner.ReleasedSwingThetaRad, (float)runner.ReleasedSwingOmegaRadS);
            }

            StepPostRelease(step);
            RenderVines(state, heroZ, carried, heldTheta, pulse);
            RenderHandRing(heroX, heroY, heroZ, carried, phase);
            RenderFeedback(state, heroX, heroY, heroZ, realDeltaSeconds, session.InHitPause);
        }

        private void RenderVines(in RunnerState state, double heroZ, bool carried, float heldTheta, float pulse)
        {
            int used = 0;
            int targetId = carried ? state.AimVineId : 0;
            if (_track != null)
            {
                double minZ = heroZ - RigTrailM;
                double maxZ = heroZ + SwingRigMath.RigDrawAheadM(_viewDistanceM, SignpostLeadM);
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
                        AssignVine(slot, v.Id, v.ChunkSerial, v.Row, _runnerConfig.LaneCenterX(v.Lane));
                    }

                    bool held = carried && v.Id == state.VineId;
                    bool behind = v.Z < heroZ - BehindM;
                    if (targetId == 0 && !carried && !behind && v.Z >= heroZ - (_vines.GrabZoneLengthM * 0.5f))
                    {
                        targetId = v.Id;
                    }

                    float laneX = _runnerConfig.LaneCenterX(v.Lane);
                    _frame.Sample(v.Z, out PathPose pivotPose);
                    if (!slot.Root.activeSelf)
                    {
                        slot.Root.SetActive(true);
                    }

                    if (!slot.Rig.activeSelf)
                    {
                        slot.Rig.SetActive(true);
                    }

                    // The rope: the simulation's theta while held, else the rest sway plus any swing left from a release.
                    float theta = held ? heldTheta : LooseAngle(slot, v.Id, (float)(v.Z - heroZ));
                    PlaceTrunk(slot, pivotPose);
                    PlaceBranch(slot, laneX, pivotPose);
                    Vector3 pivot = _branchPoints[BranchSegments];
                    PlaceRope(slot, pivot, v.Z, laneX, theta, out Vector3 ropeEnd, out Quaternion endRotation);

                    bool cues = !held && !behind;
                    if (slot.Cues.activeSelf != cues)
                    {
                        slot.Cues.SetActive(cues);
                    }

                    if (cues)
                    {
                        bool aimed = carried && v.Id == state.AimVineId;
                        float halo = HaloSizeM * (0.85f + (0.3f * pulse)) * (aimed ? AimedHaloScale : 1f);
                        slot.Cues.transform.localPosition = ropeEnd;
                        slot.Cues.transform.localRotation = endRotation;
                        slot.Halo.localScale = new Vector3(halo, halo, halo * 0.3f);
                        float icon = 0.9f + (0.2f * pulse);
                        slot.Icon.localScale = new Vector3(icon, icon, 1f);
                    }

                    bool sign = v.Row == 0 && !behind;
                    if (slot.Signpost.activeSelf != sign)
                    {
                        slot.Signpost.SetActive(sign);
                    }

                    if (sign)
                    {
                        // At the path edge, SignpostLeadM before the first vine along the route.
                        float side = laneX > 0.01f ? 1f : -1f;
                        _frame.Sample(v.Z - SignpostLeadM, out PathPose signPose);
                        slot.Signpost.transform.localPosition = PathPlacement.Point(signPose, side * _signpostX, 0f);
                        slot.Signpost.transform.localRotation = PathPlacement.Orientation(signPose);
                    }

                    slot.Knot.localPosition = pivot;
                    slot.Knot.localRotation = PathPlacement.Orientation(pivotPose);

                    bool last = i + 1 >= count || _track.GetVine(i + 1).ChunkSerial != v.ChunkSerial;
                    PlaceGlade(slot, v.Z, last, behind);
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
        private void AssignVine(Slot slot, int vineId, int chunkSerial, int row, float laneX)
        {
            slot.AssignedId = vineId;
            slot.VineId = vineId;
            ulong seed = _frame.RunSeed;
            slot.Tree = SwingRigMath.PlaceTree(seed, RandomStreamIds.Scenery, chunkSerial, row, laneX);
            slot.SwayPhase = SwingRigMath.SwayPhaseRad(seed, RandomStreamIds.Scenery, chunkSerial, row);
        }

        /// <summary>Rope angle of a vine nobody holds: the rest sway (fading near the grab zone) plus the swing left from a release.</summary>
        private float LooseAngle(Slot slot, int vineId, float distanceToGrabM)
        {
            float amplitude = SwingRigMath.SwayAmplitudeForDistance(distanceToGrabM);
            float angle = SwingRigMath.SwayAngleRad(_clock, slot.SwayPhase, amplitude, _swayPeriodS);
            for (int i = 0; i < PostCapacity; i++)
            {
                if (_postId[i] == vineId)
                {
                    angle += _postTheta[i];
                }
            }

            return angle;
        }

        /// <summary>
        /// The anchor trunk beside the path at the vine's z. On the Floor layer the base is on the ground; in the High
        /// layer the trunk comes up from the forest floor below the bough, so the branch (17 m up) stays clear of it.
        /// </summary>
        private void PlaceTrunk(Slot slot, in PathPose pose)
        {
            float radius = slot.Tree.TrunkRadiusM;
            float baseY = pose.Layer == PathLayer.High ? -HighFloorDropM : -TrunkSinkM;
            float total = slot.Tree.HeightM - baseY;
            float diameter = radius * 2f;
            slot.Trunk.localPosition = PathPlacement.Point(pose, slot.Tree.TrunkX, baseY);
            slot.Trunk.localRotation = PathPlacement.Orientation(pose);
            slot.TrunkBody.localPosition = new Vector3(0f, total * 0.5f, 0f);
            slot.TrunkBody.localScale = new Vector3(diameter, total * 0.5f, diameter);
            slot.TrunkFlare.localPosition = new Vector3(0f, TrunkFlareM * 0.5f, 0f);
            slot.TrunkFlare.localScale = new Vector3(diameter * TrunkFlareWidth, TrunkFlareM * 0.5f, diameter * TrunkFlareWidth);
            slot.Crown.localPosition = new Vector3(0f, total - 3f, 0f);
            slot.Crown.localScale = new Vector3(radius * 6f, 5f, radius * 6f);
            slot.TrunkArtRoot.localScale = new Vector3(radius / TrunkArtRadiusM, total / TrunkArtHeightM, radius / TrunkArtRadiusM);
        }

        /// <summary>
        /// The one branch from the trunk axis (inside the trunk) to the pivot, tapering 0.8 m to 0.35 m, with leaf tufts.
        /// Leaves <see cref="_branchPoints"/> filled: index 0 at the trunk, the last at the pivot (lane centre, vine z, 17 m).
        /// </summary>
        private void PlaceBranch(Slot slot, float laneX, in PathPose pose)
        {
            for (int k = 0; k <= BranchSegments; k++)
            {
                SwingRigMath.BranchPoint(slot.Tree, laneX, _pivotHeightM, k / (float)BranchSegments, out float bx, out float by);
                _branchPoints[k] = PathPlacement.Point(pose, bx, by);
            }

            if (slot.BranchArt != null)
            {
                Vector3 chord = _branchPoints[BranchSegments] - _branchPoints[0];
                float length = chord.magnitude;
                if (length > 0.01f)
                {
                    float scale = length / BranchArtLengthM;
                    slot.BranchArtRoot.localPosition = _branchPoints[0];
                    slot.BranchArtRoot.localRotation = Quaternion.LookRotation(chord / length, pose.Up) * BranchArtAxis;
                    slot.BranchArtRoot.localScale = new Vector3(scale, scale, scale);
                }
            }
            else
            {
                for (int k = 0; k < BranchSegments; k++)
                {
                    Vector3 a = _branchPoints[k];
                    Vector3 b = _branchPoints[k + 1];
                    Vector3 d = b - a;
                    float length = d.magnitude;
                    if (length <= 0.01f)
                    {
                        continue;
                    }

                    float diameter = SwingRigMath.BranchDiameterM((k + 0.5f) / BranchSegments);
                    Transform piece = slot.BranchPieces[k];
                    piece.localPosition = (a + b) * 0.5f;
                    piece.localRotation = Quaternion.FromToRotation(Vector3.up, d / length);
                    piece.localScale = new Vector3(diameter, length * 0.5f * SegmentOverlap, diameter);
                }
            }

            for (int k = 0; k < LeafPointIndex.Length; k++)
            {
                float size = LeafSizeM[k];
                Transform leaves = slot.BranchLeaves[k];
                leaves.localPosition = _branchPoints[LeafPointIndex[k]] + (pose.Up * LeafLiftM);
                leaves.localScale = new Vector3(size, size * 0.6f, size);
            }
        }

        /// <summary>
        /// The rope from the pivot to the rope end at <c>(pivotS + L sin(theta), PivotHeight - L cos(theta))</c> in the
        /// vine's lane: a straight tapered strip (or the stretched Vine_Liana art). Returns the rope end and the frame
        /// rotation there for the cues.
        /// </summary>
        private void PlaceRope(Slot slot, Vector3 pivot, double pivotS, float laneX, float theta, out Vector3 end, out Quaternion endRotation)
        {
            double endS = pivotS + SwingRigMath.RopeEndOffsetS(_ropeLengthM, theta);
            float endY = SwingRigMath.RopeEndHeightM(_pivotHeightM, _ropeLengthM, theta);
            _frame.Sample(endS, out PathPose endPose);
            end = PathPlacement.Point(endPose, laneX, endY);
            endRotation = PathPlacement.Orientation(endPose);

            Vector3 d = end - pivot;
            float length = d.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Vector3 dir = d / length;
            if (slot.RopeArt != null)
            {
                slot.RopeArtRoot.localPosition = pivot;
                slot.RopeArtRoot.localRotation = Quaternion.FromToRotation(Vector3.down, dir);
                slot.RopeArtRoot.localScale = new Vector3(1f, length / LianaArtLengthM, 1f);
                return;
            }

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, dir);
            float segment = length / RopeSegments;
            for (int k = 0; k < RopeSegments; k++)
            {
                float diameter = Mathf.Lerp(RopeTopDiameterM, RopeBottomDiameterM, (k + 0.5f) / RopeSegments);
                Transform piece = slot.RopePieces[k];
                piece.localPosition = pivot + (dir * (segment * (k + 0.5f)));
                piece.localRotation = rotation;
                piece.localScale = new Vector3(diameter, segment * 0.5f * SegmentOverlap, diameter);
            }
        }

        /// <summary>Landing glade: a cushion and bushes on the far rim of the last vine of a section.</summary>
        private void PlaceGlade(Slot slot, double pivotS, bool lastOfSection, bool behind)
        {
            bool glade = lastOfSection && !behind;
            if (slot.Glade.gameObject.activeSelf != glade)
            {
                slot.Glade.gameObject.SetActive(glade);
            }

            if (glade)
            {
                _frame.Sample(pivotS + SwingRigMath.GladeStartAfterGrabM + (SwingRigMath.GladeLengthM * 0.5f), out PathPose gladePose);
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

        /// <summary>Starts the damped swing of a released vine's rope from the released theta and omega (replaces the oldest of three).</summary>
        private void StartPostRelease(int vineId, float theta, float omega)
        {
            if (vineId == 0)
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < PostCapacity; i++)
            {
                if (_postId[i] == vineId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = _postNext;
                _postNext = (_postNext + 1) % PostCapacity;
            }

            _postId[index] = vineId;
            _postTheta[index] = theta;
            _postOmega[index] = omega;
        }

        private void StepPostRelease(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            float remaining = dt > PostMaxDeltaSeconds ? PostMaxDeltaSeconds : dt;
            for (int i = 0; i < PostCapacity; i++)
            {
                if (_postId[i] == 0)
                {
                    continue;
                }

                float theta = _postTheta[i];
                float omega = _postOmega[i];
                float left = remaining;
                while (left > 0f)
                {
                    float h = left > PostStepSeconds ? PostStepSeconds : left;
                    SwingRigMath.StepPostRelease(ref theta, ref omega, _gravityMps2, _ropeLengthM, SwingRigMath.PostReleaseDampingPerS, h);
                    left -= h;
                }

                if (SwingRigMath.PostReleaseSettled(theta, omega))
                {
                    _postId[i] = 0;
                    _postTheta[i] = 0f;
                    _postOmega[i] = 0f;
                }
                else
                {
                    _postTheta[i] = theta;
                    _postOmega[i] = omega;
                }
            }
        }

        /// <summary>The release ring around the hand: fills with the swing phase, the Perfect band in sun-gold, flashes inside it.</summary>
        private void RenderHandRing(float heroX, float heroY, double heroZ, bool carried, float phase)
        {
            if (_ringRoot.gameObject.activeSelf != carried)
            {
                _ringRoot.gameObject.SetActive(carried);
            }

            if (!carried)
            {
                return;
            }

            _frame.Sample(heroZ, out PathPose handPose);
            Quaternion rot = PathPlacement.Orientation(handPose);
            Vector3 hand = PathPlacement.Point(handPose, heroX, heroY + _vines.HandToFeetM);

            bool inPerfect = phase >= _perfectStartPhase && phase < _perfectEndPhase;
            float flash = inPerfect ? 1f + (0.25f * Mathf.Abs(Mathf.Sin(_clock * RingFlashHz * Mathf.PI))) : 1f;
            _ringRoot.localPosition = hand + (rot * new Vector3(0f, 0f, -RingTowardCameraM));
            _ringRoot.localRotation = rot;
            _ringRoot.localScale = new Vector3(flash, flash, 1f);
            for (int k = 0; k < RingSegments; k++)
            {
                bool filled = _ringPhase[k] <= phase || (_ringPhase[k] >= _perfectStartPhase && _ringPhase[k] < _perfectEndPhase);
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
                    float u = 1f - (_stampLeft / StampSeconds);
                    _stamp.transform.localPosition = hero + (rot * new Vector3(0f, StampAboveFeetM + (StampRiseM * u), StampAheadM));
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
                float u = 1f - (_speedLinesLeft / SpeedLineSeconds);
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

        /// <summary>Too early: muted path tone; Good: parchment; Perfect: sun-gold (spec 004 4.4 windows).</summary>
        private Color RingColor(float phase)
        {
            if (phase < _goodStartPhase)
            {
                return StylePalette.PathAlternate;
            }

            if (phase >= _perfectStartPhase && phase < _perfectEndPhase)
            {
                return StylePalette.SunGold;
            }

            return StylePalette.Parchment;
        }
    }
}
