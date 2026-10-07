namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Where one context piece stands, relative to the rig center (x lateral, z along the path) with its footprint
    /// radius and height. The rig builder and <see cref="ContextKeepOut"/> read the same anchors.
    /// </summary>
    public struct ContextAnchor
    {
        public ContextKind Kind;
        public float X;
        public float ZOffsetM;
        public float RadiusM;
        public float HeightM;
    }
}
