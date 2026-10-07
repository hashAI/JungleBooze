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
        private const float BranchThicknessM = 0.35f;
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
            public Transform Rope;
            public Transform Branch;
            public Transform Core;
            public Transform Halo;
            public Transform Icon;
            public GameObject Signpost;
        }

        private RunnerConfig _runnerConfig;
        private float _viewDistanceM;
        private float _signpostX;
        private Slot[] _slots;
        private int _shown;
        private TrackSimulation _track;
        private VineConfig _vines;
        private PathFrame _frame;

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
            for (int i = 0; i < Capacity; i++)
            {
                var slot = new Slot();
                Transform root = new GameObject("Vine" + i).transform;
                root.SetParent(transform, false);
                slot.Root = root.gameObject;
                slot.Rope = kit.Create(PrimitiveType.Cylinder, "Rope", root, StylePalette.VineRope).transform;
                slot.Branch = kit.Create(PrimitiveType.Cube, "Branch", root, StylePalette.VineRope).transform;
                // Real branch (fits a unit cube, scaled lane-wide by the placement code) replaces the cube.
                EnvironmentArt.AttachAndHide(slot.Branch, EnvironmentArt.VineBranch);

                slot.Core = kit.Create(PrimitiveType.Sphere, "GlowCore", root, StylePalette.VineGlowCore).transform;
                slot.Core.localScale = new Vector3(CoreSizeM, CoreSizeM, CoreSizeM);
                slot.Halo = kit.Create(PrimitiveType.Sphere, "GlowHalo", root, StylePalette.SunGold).transform;
                slot.Icon = new GameObject("RingIcon").transform;
                slot.Icon.SetParent(root, false);
                for (int k = 0; k < IconSegments; k++)
                {
                    float a = 2f * Mathf.PI * k / IconSegments;
                    kit.Create(
                        PrimitiveType.Cube,
                        "IconSegment",
                        slot.Icon,
                        StylePalette.SunGold,
                        new Vector3(Mathf.Cos(a) * IconRadiusM, Mathf.Sin(a) * IconRadiusM, 0f),
                        new Vector3(IconSegmentM, IconSegmentM, IconSegmentM * 0.5f));
                }

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
                slot.Root.SetActive(false);
                _slots[i] = slot;
            }

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
            if (_track != null && _vines != null)
            {
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + _viewDistanceM + SignpostLeadM;
                float grabY = _vines.GrabPointHeightM;
                float ropeLength = _vines.SwingRadiusM;
                int count = _track.VineCount;
                for (int i = 0; i < count && used < Capacity; i++)
                {
                    ref readonly VineInstance v = ref _track.GetVine(i);
                    if (v.Z > maxZ)
                    {
                        break;
                    }

                    if (v.Z < minZ || (carried && v.Id == state.VineId))
                    {
                        continue;
                    }

                    Slot slot = _slots[used++];
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

                    // Cylinders are 2 m tall at scale 1.
                    slot.Rope.localPosition = new Vector3(0f, grabY + ropeLength * 0.5f, 0f);
                    slot.Rope.localScale = new Vector3(RopeDiameterM, ropeLength * 0.5f, RopeDiameterM);
                    slot.Branch.localPosition = new Vector3(0f, grabY + ropeLength, 0f);
                    slot.Branch.localScale = new Vector3(_runnerConfig.LaneWidthM, BranchThicknessM, BranchThicknessM);

                    bool aimed = carried && v.Id == state.AimVineId;
                    float halo = HaloSizeM * (0.85f + 0.3f * pulse) * (aimed ? AimedHaloScale : 1f);
                    slot.Halo.localPosition = new Vector3(0f, grabY, 0.05f);
                    slot.Halo.localScale = new Vector3(halo, halo, halo * 0.3f);
                    slot.Core.localPosition = new Vector3(0f, grabY, -0.05f);
                    float icon = 0.9f + 0.2f * pulse;
                    slot.Icon.localPosition = new Vector3(0f, grabY, -0.1f);
                    slot.Icon.localScale = new Vector3(icon, icon, 1f);

                    bool sign = v.Row == 0;
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
                }
            }

            for (int i = used; i < _shown; i++)
            {
                _slots[i].Root.SetActive(false);
            }

            _shown = used;
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
