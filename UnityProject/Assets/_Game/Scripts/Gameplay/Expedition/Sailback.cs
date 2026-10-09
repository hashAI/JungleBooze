namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>One sailback (plain data, path space). Views read it; only <see cref="SailbackSystem"/> writes it.</summary>
    public struct Sailback
    {
        public CreatureState State;
        public long StateTick;

        /// <summary>Spawn group (serial × stride + spawn index) and member index.</summary>
        public int Group;
        public int Member;
        public int ChunkSerial;
        public bool Ambient;
        public bool Roost;

        public float S;
        public float X;
        public float Y;

        public float PerchS;
        public float PerchX;
        public float PerchY;

        public float EndS;
        public float EndX;
        public float EndY;

        /// <summary>Glide start (after the launch drop).</summary>
        public float GlideS0;
        public float GlideX0;
        public float GlideY0;

        public float LaunchDistance;

        /// <summary>Alerted by Pista passing close (launches when the alert ends, whatever Δs).</summary>
        public bool Startled;

        /// <summary>The perched animal looks at Pista.</summary>
        public bool HeadTracking;

        /// <summary>Sail opening 0…1 (presentation).</summary>
        public float Sail;
    }
}
