using System;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// What the track tells the runner (spec 001 section 6.5, extended by spec 002 section 6).
    /// <para><b>Contract summary (stage B1, final for FP1)</b></para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="HasGround"/>(x, zMin, zMax): true if ANY part of the footprint
    /// [x − h, x + h] × [zMin, zMax] lies over ground, where h = <c>RunnerConfig.PlayerHitboxWidthM / 2</c>
    /// (the implementation takes h from the <see cref="RunnerConfig"/> it was built with). Partial support counts
    /// as ground. The runner passes zMin/zMax = Z ∓ <c>PlayerHitboxDepthM / 2</c>. Ground is missing only inside
    /// gaps, per lane band, for z in [nearEdge, nearEdge + length).
    /// </description></item>
    /// <item><description>
    /// <see cref="GetBoxes"/>(zMin, zMax, buffer): writes every obstacle box whose z range [ZMin, ZMax] overlaps
    /// [zMin, zMax] (closed intervals) into <c>buffer</c>, sorted by <see cref="ObstacleBox.Id"/>
    /// ascending (boxes of one id in lane order), and returns how many it wrote. Never writes more than
    /// buffer.Length; if more boxes overlap, it writes the lowest ids and returns buffer.Length (the runner counts
    /// that as an overflow and reports it in dev builds). Gaps produce no boxes. Box positions are the state after
    /// the track update of the current tick (step 7a); <c>XMinPrev/XMaxPrev</c> are the previous tick's.
    /// </description></item>
    /// <item><description>
    /// <see cref="TryGetNextGapEdge"/>(lane, fromZ, out nearEdge, out length): the first gap in
    /// <c>lane</c> whose near edge is ≥ fromZ. For bots and debug overlays; movement never calls it.
    /// </description></item>
    /// <item><description>
    /// All members are deterministic, read-only (they never change track state) and allocation-free.
    /// The track itself advances in <see cref="IRunnerStepHooks.OnTrackUpdate"/> (step 7a), never inside a query.
    /// </description></item>
    /// </list>
    /// Implementations: <see cref="FlatTrackQuery"/> (no gaps, no boxes), <see cref="TestTrackQuery"/> (fixtures),
    /// and the generated track's <c>ChunkTrackQuery</c> (stage B2, <c>JungleBooze.Gameplay.Track</c>).
    /// </summary>
    public interface ITrackQuery
    {
        /// <summary>
        /// True if any ground lies under the footprint [x − h, x + h] × [zMin, zMax], with
        /// h = <c>RunnerConfig.PlayerHitboxWidthM / 2</c>.
        /// </summary>
        bool HasGround(float x, double zMin, double zMax);

        /// <summary>
        /// Writes the obstacle boxes whose z range overlaps [zMin, zMax] into <paramref name="buffer"/> in id order
        /// and returns the number written (at most buffer.Length). Must not allocate.
        /// </summary>
        int GetBoxes(double zMin, double zMax, Span<ObstacleBox> buffer);

        /// <summary>
        /// Finds the first gap in <paramref name="lane"/> whose near edge is at or after <paramref name="fromZ"/>.
        /// For bots and debug overlays; not used by movement.
        /// </summary>
        bool TryGetNextGapEdge(int lane, double fromZ, out double nearEdge, out float length);
    }
}
