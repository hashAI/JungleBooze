namespace JungleBooze.Gameplay.Views
{
    /// <summary>The cosmetic look of one obstacle row (spec 005 3.6): chosen by hash, never by the simulation.</summary>
    public struct ObstacleVariant
    {
        /// <summary>Skin index, 0 to skinCount - 1 (0 is the clearest one).</summary>
        public int Skin;

        /// <summary>True when the row is mirrored left to right.</summary>
        public bool Mirror;

        /// <summary>Side of the context pieces: -1 = left (-x), +1 = right (+x).</summary>
        public int ContextSide;

        /// <summary>Two more uniform values in [0, 1) for small details (debris, offsets).</summary>
        public float Detail0;

        public float Detail1;
    }
}
