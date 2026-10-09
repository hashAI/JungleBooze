using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>One decision of a route: which side of divider <see cref="Divider"/> (index in the variant) to take.</summary>
    [Serializable]
    public struct RouteStep
    {
        public int Divider;

        /// <summary>−1 left, +1 right.</summary>
        public int Side;

        public RouteStep(int divider, int side)
        {
            Divider = divider;
            Side = side;
        }
    }
}
