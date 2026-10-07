namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// One placed scenery prop in path space (spec 003 section 11.2). Plain value from <see cref="SceneryPlacer"/>;
    /// no Unity types, so the placement is testable as pure math.
    /// </summary>
    public readonly struct SceneryPiece
    {
        public SceneryPiece(
            SceneryModel model,
            SceneryBand band,
            int side,
            float lateralM,
            double s,
            float scaleXz,
            float scaleY,
            float yawDeg,
            float rollDeg,
            float footprintM,
            float heightM)
        {
            Model = model;
            Band = band;
            Side = side;
            LateralM = lateralM;
            S = s;
            ScaleXz = scaleXz;
            ScaleY = scaleY;
            YawDeg = yawDeg;
            RollDeg = rollDeg;
            FootprintM = footprintM;
            HeightM = heightM;
        }

        public SceneryModel Model { get; }

        public SceneryBand Band { get; }

        /// <summary>+1 = right of the path, -1 = left.</summary>
        public int Side { get; }

        /// <summary>Distance of the prop's axis from the centerline (always positive; the side gives the sign).</summary>
        public float LateralM { get; }

        /// <summary>Arc length along the route.</summary>
        public double S { get; }

        /// <summary>Horizontal scale (x and z).</summary>
        public float ScaleXz { get; }

        /// <summary>Vertical scale.</summary>
        public float ScaleY { get; }

        public float YawDeg { get; }

        /// <summary>Lean about the forward axis in degrees (light shafts lean outward).</summary>
        public float RollDeg { get; }

        /// <summary>Horizontal reach of the whole prop below 7 m height, measured from its axis (metres).</summary>
        public float FootprintM { get; }

        /// <summary>Height of the prop's pivot above the ground (hanging vines start overhead).</summary>
        public float HeightM { get; }

        /// <summary>Signed route x of the axis.</summary>
        public float X => Side * LateralM;

        /// <summary>Distance from the centerline to the nearest part of the prop.</summary>
        public float InnerEdgeM => LateralM - FootprintM;
    }
}
