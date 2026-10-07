using System;
using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>One golden trace (schema v2) with its per-tick vectors. Generated data: see <c>GoldenVineVectors</c>.</summary>
    internal sealed class GoldenVineTrace
    {
        public const int StateCarried = 0;
        public const int StateFalling = 1;
        public const int StateLanded = 2;

        public GoldenVineTrace(
            string name,
            double[] vineZ,
            int[] vineLane,
            bool overChasm,
            double grabZ,
            double grabY,
            double entrySpeed,
            double speedMultiplier,
            int[] swipeTicks,
            int[] grabTicks,
            int[] releaseTicks,
            int[] releaseGrades,
            int landingTick,
            int[] state,
            double[] x,
            double[] y,
            double[] z,
            double[] theta,
            double[] omega)
        {
            Name = name;
            VineZ = vineZ;
            VineLane = vineLane;
            OverChasm = overChasm;
            GrabZ = grabZ;
            GrabY = grabY;
            EntrySpeed = entrySpeed;
            SpeedMultiplier = speedMultiplier;
            SwipeTicks = swipeTicks;
            GrabTicks = grabTicks;
            ReleaseTicks = releaseTicks;
            ReleaseGrades = releaseGrades;
            LandingTick = landingTick;
            State = state;
            X = x;
            Y = y;
            Z = z;
            Theta = theta;
            Omega = omega;
        }

        public string Name { get; }

        public double[] VineZ { get; }

        public int[] VineLane { get; }

        public bool OverChasm { get; }

        public double GrabZ { get; }

        public double GrabY { get; }

        public double EntrySpeed { get; }

        public double SpeedMultiplier { get; }

        /// <summary>Release swipes; the number of the Step call (tick 1 = the first step after the grab).</summary>
        public int[] SwipeTicks { get; }

        public int[] GrabTicks { get; }

        public int[] ReleaseTicks { get; }

        /// <summary>1 = Poor (Auto), 2 = Good, 3 = Perfect: the numbers of <see cref="VineReleaseGrade"/>.</summary>
        public int[] ReleaseGrades { get; }

        public int LandingTick { get; }

        public int[] State { get; }

        public double[] X { get; }

        public double[] Y { get; }

        public double[] Z { get; }

        public double[] Theta { get; }

        public double[] Omega { get; }

        public int TickCount => State.Length;
    }

    /// <summary>
    /// Flat test ground (<see cref="TestTrackQuery"/>) that also lists vines (<see cref="IVineTrackQuery"/>), so a
    /// <see cref="RunnerSimulation"/> built on it can grab and swing.
    /// </summary>
    internal sealed class VineTestTrack : ITrackQuery, IVineTrackQuery
    {
        private readonly List<VineAnchor> _vines = new List<VineAnchor>();

        public VineTestTrack(RunnerConfig config)
        {
            Ground = new TestTrackQuery(config);
        }

        public TestTrackQuery Ground { get; }

        public int VineCount => _vines.Count;

        public VineAnchor Anchor(int index)
        {
            return _vines[index];
        }

        public VineTestTrack AddVine(int lane, double z, int row, bool overChasm)
        {
            _vines.Add(new VineAnchor { Id = _vines.Count + 1, Group = 1, Row = row, Lane = lane, Z = z, OverChasm = overChasm });
            _vines.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Id.CompareTo(b.Id));
            return this;
        }

        public bool HasGround(float x, double zMin, double zMax)
        {
            return Ground.HasGround(x, zMin, zMax);
        }

        public int GetBoxes(double zMin, double zMax, Span<ObstacleBox> buffer)
        {
            return Ground.GetBoxes(zMin, zMax, buffer);
        }

        public bool TryGetNextGapEdge(int lane, double fromZ, out double nearEdge, out float length)
        {
            return Ground.TryGetNextGapEdge(lane, fromZ, out nearEdge, out length);
        }

        public int GetVines(double zMin, double zMax, Span<VineAnchor> buffer)
        {
            int count = 0;
            for (int i = 0; i < _vines.Count; i++)
            {
                VineAnchor v = _vines[i];
                if (v.Z < zMin || v.Z > zMax)
                {
                    continue;
                }

                if (count >= buffer.Length)
                {
                    return count;
                }

                buffer[count++] = v;
            }

            return count;
        }
    }

    /// <summary>
    /// A runner on a vine test track at a constant run speed. The first vine is at <see cref="PivotZ"/> in the middle
    /// lane; with <c>chasm</c> the 16 m chasm of the chunk data lies under it (rim pivot - 4, far edge pivot + 12).
    /// Events of every step are kept in <see cref="Events"/>. Swing tick n = the n-th <see cref="Step"/> after
    /// <see cref="GrabAt"/>.
    /// </summary>
    internal sealed class VineScenario
    {
        public const double PivotZ = 100.0;

        private readonly List<RunnerEvent> _events = new List<RunnerEvent>();
        private readonly double _runSpeedMps;

        public VineScenario(double runSpeedMps, bool chasm, double[] vineZ = null, VineDesignValues vineValues = null, double chasmEndZ = PivotZ + 12.0)
        {
            _runSpeedMps = runSpeedMps;
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            Config = RunnerConfig.FromDesignValues(values);
            Vines = VineConfig.FromDesignValues(vineValues ?? VineDesignValues.CreateDefault());
            Track = new VineTestTrack(Config);
            double[] zs = vineZ ?? new[] { PivotZ };
            for (int i = 0; i < zs.Length; i++)
            {
                Track.AddVine(1, zs[i], i, chasm && i == 0);
            }

            if (chasm)
            {
                Track.Ground.AddGap(PivotZ - 4.0, chasmEndZ);
            }

            Sim = new RunnerSimulation(Config, SpeedCurve.CreateConstant(runSpeedMps), Track, null, Vines);
        }

        private VineScenario(VineScenario twin)
        {
            Config = twin.Config;
            Vines = twin.Vines;
            Track = twin.Track;
            _runSpeedMps = twin._runSpeedMps;
            Sim = new RunnerSimulation(Config, SpeedCurve.CreateConstant(_runSpeedMps), Track, null, Vines);
        }

        public RunnerConfig Config { get; }

        public VineConfig Vines { get; }

        public VineTestTrack Track { get; }

        public RunnerSimulation Sim { get; }

        /// <summary>A second, fresh runner on the same config, vines and track (for <c>CopyStateFrom</c>).</summary>
        public VineScenario Twin()
        {
            return new VineScenario(this);
        }

        public IReadOnlyList<RunnerEvent> Events => _events;

        public RunnerState State => Sim.Current;

        /// <summary>Grabs the first vine now at <paramref name="zRelativeToPivot"/> with the given feet height and entry speed.</summary>
        public void GrabAt(double zRelativeToPivot, double feetY, double entrySpeedMps)
        {
            VineAnchor anchor = Track.Anchor(0);
            Sim.DebugGrab(in anchor, anchor.Z + zRelativeToPivot, feetY, entrySpeedMps);
            Drain();
        }

        public RunnerState Step(InputCommand commands = InputCommand.None)
        {
            Sim.Step(commands);
            Drain();
            return Sim.Current;
        }

        public int CountOf(RunnerEventType type)
        {
            int n = 0;
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Type == type)
                {
                    n++;
                }
            }

            return n;
        }

        public RunnerEvent LastOf(RunnerEventType type)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].Type == type)
                {
                    return _events[i];
                }
            }

            throw new InvalidOperationException("No " + type + " event.");
        }

        /// <summary>
        /// Steps (swiping Jump on step <paramref name="swipeSwingTick"/>; 0 = never) until HERO is no longer carried.
        /// Returns the number of steps taken, which is the swing tick of the release.
        /// </summary>
        public int RunUntilReleased(int swipeSwingTick, int maxSteps = 200)
        {
            for (int i = 1; i <= maxSteps; i++)
            {
                Step(i == swipeSwingTick ? InputCommand.Jump : InputCommand.None);
                if (State.Locomotion != Locomotion.Carried)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("Never released.");
        }

        /// <summary>Steps with no input until HERO is running again; returns the number of steps taken, or -1 after <paramref name="maxSteps"/>.</summary>
        public int RunUntilLanded(int maxSteps = 400)
        {
            for (int i = 1; i <= maxSteps; i++)
            {
                Step();
                if (State.Locomotion == Locomotion.Running || State.Locomotion == Locomotion.Dead)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Drain()
        {
            RunnerEventBuffer buffer = Sim.Events;
            for (int i = 0; i < buffer.Count; i++)
            {
                _events.Add(buffer[i]);
            }

            buffer.Clear();
        }
    }
}
