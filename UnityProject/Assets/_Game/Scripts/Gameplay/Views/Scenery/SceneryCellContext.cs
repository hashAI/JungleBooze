using JungleBooze.Gameplay.Path;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>What the placer needs to know about the route at one 10 m cell. Built by the view from the PathFrame.</summary>
    public struct SceneryCellContext
    {
        /// <summary>
        /// Signed curvature in rad/m (positive = turning right) with the largest magnitude over the cell and a little
        /// either side of it, so a bend that starts just outside the cell already opens the corridor.
        /// </summary>
        public float Curvature;

        public RouteBeatKind Beat;

        public PathLayer Layer;

        /// <summary>World index (Jungle 0, River 1, Mountains 2, Ruins 3).</summary>
        public int WorldIndex;

        /// <summary>
        /// True within 50 m before and 40 m after a vine section's chunk: the anchor tree and its limb must stay visible,
        /// so everything keeps 11 m from the centerline and nothing hangs over the corridor. The beat itself is not
        /// changed. The swing beat (<see cref="RouteBeatKind.SwingZone"/>) always counts as framed.
        /// </summary>
        public bool SwingFrame;
    }
}
