namespace JungleBooze.Gameplay.Movement
{
    /// <summary>A placed vine in path space (spec 103 §5.2): anchor, length, takeoff lip and landing platform.</summary>
    public readonly struct VinePoint
    {
        public VinePoint(int id, float anchorS, float x, float anchorY, float length, float lipS, float landingS, float takeoffY, int chunkSerial)
        {
            Id = id;
            AnchorS = anchorS;
            X = x;
            AnchorY = anchorY;
            Length = length;
            LipS = lipS;
            LandingS = landingS;
            TakeoffY = takeoffY;
            ChunkSerial = chunkSerial;
        }

        public int Id { get; }

        /// <summary>sA.</summary>
        public float AnchorS { get; }

        /// <summary>xVine.</summary>
        public float X { get; }

        /// <summary>Absolute anchor height (takeoff floor + hA), m.</summary>
        public float AnchorY { get; }

        public float Length { get; }

        public float LipS { get; }

        /// <summary>Start of the landing platform.</summary>
        public float LandingS { get; }

        public float TakeoffY { get; }

        public int ChunkSerial { get; }
    }
}
