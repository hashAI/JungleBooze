using System;

namespace JungleBooze.Gameplay.Vine
{
    /// <summary>
    /// Optional second query of a track (GDD 7): the vines placed by vine sections. The runner checks once, at
    /// construction, whether its <c>ITrackQuery</c> also implements this; tracks without vines simply do not.
    /// Deterministic, read-only and allocation-free, like <c>ITrackQuery</c>.
    /// </summary>
    public interface IVineTrackQuery
    {
        /// <summary>
        /// Writes every vine whose grab point z lies in [zMin, zMax] into <paramref name="buffer"/>, ordered by z
        /// (then id), and returns the number written (at most buffer.Length).
        /// </summary>
        int GetVines(double zMin, double zMax, Span<VineAnchor> buffer);
    }
}
