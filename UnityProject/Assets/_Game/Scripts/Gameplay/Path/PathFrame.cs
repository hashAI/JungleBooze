using System;
using JungleBooze.Core;
using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Presentation-only mapping from path space (<c>s</c> along the route, <c>x</c> lateral, <c>y</c> height) to
    /// WorldRoot space (spec 003 section 3). The simulation never reads it (AC-301). Samples sit in a preallocated
    /// ring every <see cref="RouteTuning.SampleSpacingM"/>; no query allocates.
    /// With the straight route (T1) the mapping is the identity: world (x, y, z) = (x, y, s).
    /// [ASSUMED] T1 interpolates linearly between samples (1 m apart); T2 may switch to Catmull-Rom.
    /// [ASSUMED] Outside the built range <see cref="Sample"/> clamps to the nearest sample and then continues along
    /// its tangent, so the straight route stays an exact identity everywhere (spec says plain clamp).
    /// </summary>
    public sealed class PathFrame
    {
        /// <summary>Slack samples in the ring on top of behind + ahead.</summary>
        private const int RingSlack = 16;

        private readonly RouteTuning _tuning;
        private readonly IRouteSource _source;
        private readonly double _spacing;
        private readonly RouteSample[] _samples;

        private double _baseS;
        private int _nextIndex;
        private double _originX;
        private double _originY;
        private double _originZ;

        /// <summary>Creates a frame over <paramref name="source"/> and starts an empty run so queries are always valid.</summary>
        public PathFrame(RouteTuning tuning, IRouteSource source)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _spacing = Math.Max(0.25, tuning.SampleSpacingM);
            int capacity = (int)Math.Ceiling((tuning.BehindM + tuning.AheadM) / _spacing) + RingSlack;
            _samples = new RouteSample[Math.Max(capacity, 8)];
            BeginRun(0UL, -tuning.BehindM);
        }

        /// <summary>The run seed the route was begun with.</summary>
        public ulong RunSeed { get; private set; }

        /// <summary>The run's Route random stream (<see cref="RandomStreamIds.Route"/>). Reserved; unused by the straight route.</summary>
        public IRandom RouteRandom { get; private set; }

        /// <summary>The run's Scenery random stream (<see cref="RandomStreamIds.Scenery"/>). Reserved for scenery placement.</summary>
        public IRandom SceneryRandom { get; private set; }

        /// <summary>The tuning this frame reads.</summary>
        public RouteTuning Tuning => _tuning;

        /// <summary>Arc length of the oldest sample still stored.</summary>
        public double BuiltStartS => _baseS + FirstIndex * _spacing;

        /// <summary>Arc length of the newest sample.</summary>
        public double BuiltEndS => _baseS + (_nextIndex - 1) * _spacing;

        /// <summary>Total floating-origin translation applied so far (zero unless <see cref="ApplyRebase"/> was called).</summary>
        public Vector3 OriginOffset => new Vector3((float)_originX, (float)_originY, (float)_originZ);

        private int FirstIndex => Math.Max(0, _nextIndex - _samples.Length);

        /// <summary>
        /// Starts a new route for a run: keeps the seed, forks the Route and Scenery streams from the run seed (same
        /// convention as the other subsystems: <c>new Pcg32Random(runSeed).Fork(id)</c>), clears the ring and the
        /// origin offset, and builds <see cref="RouteTuning.AheadM"/> from <paramref name="startS"/>. Allocates (once per run).
        /// </summary>
        public void BeginRun(ulong runSeed, double startS)
        {
            RunSeed = runSeed;
            var root = new Pcg32Random(runSeed);
            RouteRandom = root.Fork(RandomStreamIds.Route);
            SceneryRandom = root.Fork(RandomStreamIds.Scenery);
            _baseS = startS;
            _nextIndex = 0;
            _originX = 0.0;
            _originY = 0.0;
            _originZ = 0.0;
            _source.Begin(RouteRandom, startS);
            Extend(startS + _tuning.AheadM);
        }

        /// <summary>Appends route samples until the route covers <paramref name="sEnd"/>. No allocation.</summary>
        public void Extend(double sEnd)
        {
            int target = (int)Math.Ceiling((sEnd - _baseS) / _spacing) + 1;
            if (target < 2)
            {
                target = 2;
            }

            while (_nextIndex < target)
            {
                int slot = _nextIndex % _samples.Length;
                _source.NextSample(_baseS + _nextIndex * _spacing, out _samples[slot]);
                _nextIndex++;
            }
        }

        /// <summary>True if the route covers <paramref name="s"/>.</summary>
        public bool IsBuilt(double s)
        {
            return s >= BuiltStartS && s <= BuiltEndS;
        }

        /// <summary>Pose of the centerline at <paramref name="s"/>. No allocation.</summary>
        public PathPose Sample(double s)
        {
            Sample(s, out PathPose pose);
            return pose;
        }

        /// <summary>Pose of the centerline at <paramref name="s"/>, written to <paramref name="pose"/>. No allocation.</summary>
        public void Sample(double s, out PathPose pose)
        {
            double firstS = BuiltStartS;
            double lastS = BuiltEndS;
            double c = s < firstS ? firstS : (s > lastS ? lastS : s);

            double u = (c - _baseS) / _spacing;
            int i0 = (int)Math.Floor(u);
            int maxI0 = _nextIndex - 2;
            if (i0 > maxI0)
            {
                i0 = maxI0;
            }

            if (i0 < FirstIndex)
            {
                i0 = FirstIndex;
            }

            double t = u - i0;
            ref RouteSample a = ref _samples[i0 % _samples.Length];
            ref RouteSample b = ref _samples[(i0 + 1) % _samples.Length];

            float yaw = (float)(a.YawRad + (b.YawRad - a.YawRad) * t);
            float pitch = (float)(a.PitchRad + (b.PitchRad - a.PitchRad) * t);
            float bank = (float)(a.BankRad + (b.BankRad - a.BankRad) * t);

            float sy = Mathf.Sin(yaw);
            float cy = Mathf.Cos(yaw);
            float sp = Mathf.Sin(pitch);
            float cp = Mathf.Cos(pitch);
            float sb = Mathf.Sin(bank);
            float cb = Mathf.Cos(bank);

            var tangent = new Vector3(sy * cp, sp, cy * cp);
            var right0 = new Vector3(cy, 0f, -sy);
            Vector3 up0 = Vector3.Cross(tangent, right0);

            double beyond = s - c;
            pose.Center = new Vector3(
                (float)(a.X + (b.X - a.X) * t + tangent.x * beyond + _originX),
                (float)(a.Y + (b.Y - a.Y) * t + tangent.y * beyond + _originY),
                (float)(a.Z + (b.Z - a.Z) * t + tangent.z * beyond + _originZ));
            pose.Tangent = tangent;
            pose.Right = (right0 * cb) - (up0 * sb);
            pose.Up = (up0 * cb) + (right0 * sb);
            pose.Curvature = (float)(a.Curvature + (b.Curvature - a.Curvature) * t);
            pose.GradePct = Mathf.Tan(pitch) * 100f;
            pose.BankDeg = bank * Mathf.Rad2Deg;
            pose.Layer = a.Layer;
            pose.Surface = a.Surface;
            pose.HalfWidthM = a.HalfWidthM;
        }

        /// <summary><c>C(s) + x * N(s) + y * U(s)</c> in WorldRoot space: the one mapping every view uses.</summary>
        public Vector3 ToWorld(double s, float x, float y)
        {
            Sample(s, out PathPose pose);
            return pose.Center + (pose.Right * x) + (pose.Up * y);
        }

        /// <summary>Frame rotation (forward = tangent, up = banked up) for oriented views.</summary>
        public Quaternion RotationAt(double s)
        {
            Sample(s, out PathPose pose);
            return Quaternion.LookRotation(pose.Tangent, pose.Up);
        }

        /// <summary>Surface offset at lateral <paramref name="x"/> from bank: <c>x * tan(bank)</c> (scenery bases, shadows).</summary>
        public float GroundHeightAt(double s, float x)
        {
            Sample(s, out PathPose pose);
            return x * Mathf.Tan(pose.BankDeg * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Floating-origin hook (spec 003 section 3.1). Returns the translation to apply to WorldRoot so the camera
        /// returns near the origin (minus the camera position, rounded to the snap), or zero when disabled
        /// (default) or when the camera is within the threshold (800 m). Pure: it changes nothing until the caller
        /// translates WorldRoot and calls <see cref="ApplyRebase"/> in the same frame.
        /// </summary>
        public Vector3 ComputeRebase(Vector3 cameraWorldPosition)
        {
            if (!_tuning.FloatingOriginEnabled)
            {
                return Vector3.zero;
            }

            float threshold = _tuning.FloatingOriginThresholdM;
            if (cameraWorldPosition.sqrMagnitude <= threshold * threshold)
            {
                return Vector3.zero;
            }

            float snap = Mathf.Max(1f, _tuning.FloatingOriginSnapM);
            return new Vector3(
                -Mathf.Round(cameraWorldPosition.x / snap) * snap,
                -Mathf.Round(cameraWorldPosition.y / snap) * snap,
                -Mathf.Round(cameraWorldPosition.z / snap) * snap);
        }

        /// <summary>Records that WorldRoot was translated by <paramref name="delta"/>; later poses include it.</summary>
        public void ApplyRebase(Vector3 delta)
        {
            _originX += delta.x;
            _originY += delta.y;
            _originZ += delta.z;
        }
    }
}
