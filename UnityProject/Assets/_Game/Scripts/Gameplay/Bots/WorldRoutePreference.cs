using System;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Bots
{
    /// <summary>
    /// Route choice for bots on a <see cref="WorldPath"/>: take the first open route in <see cref="Order"/> that
    /// passes through a split (e.g. Secret, then Risky, then Safe), or a fixed route index (the validator).
    /// Allocation-free.
    /// </summary>
    public sealed class WorldRoutePreference : IForkPreference
    {
        private readonly WorldPath _path;

        public WorldRoutePreference(WorldPath path, params RouteType[] order)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            Order = order ?? new[] { RouteType.Safe };
        }

        /// <summary>Route types in order of preference.</summary>
        public RouteType[] Order { get; set; }

        /// <summary>When ≥ 0, always take this route index of every chunk that has it (validator).</summary>
        public int FixedRoute { get; set; } = -1;

        public int PreferredSide(in ForkPoint fork)
        {
            if (!_path.TryGetForkChunk(fork.Id, out int serial, out int divider))
            {
                return 0;
            }

            ChunkRuntime chunk = _path.Chunk(serial).Chunk;
            if (FixedRoute >= 0)
            {
                return FixedRoute < chunk.RouteCount ? SideFor(chunk.GetRoute(FixedRoute), divider) : 0;
            }

            for (int o = 0; o < Order.Length; o++)
            {
                for (int r = 0; r < chunk.RouteCount; r++)
                {
                    ChunkRoute route = chunk.GetRoute(r);
                    if (route.Type != Order[o] || route.Locked)
                    {
                        continue;
                    }

                    int side = SideFor(route, divider);
                    if (side != 0)
                    {
                        return side;
                    }
                }
            }

            return 0;
        }

        private static int SideFor(ChunkRoute route, int divider)
        {
            if (route.Locked)
            {
                return 0;
            }

            for (int k = 0; k < route.Steps.Count; k++)
            {
                if (route.Steps[k].Divider == divider)
                {
                    return route.Steps[k].Side;
                }
            }

            return 0;
        }
    }
}
