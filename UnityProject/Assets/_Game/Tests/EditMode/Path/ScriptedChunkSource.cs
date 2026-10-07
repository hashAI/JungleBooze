using System.Collections.Generic;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>
    /// A chunk source with a fixed list of zones and gateways, for route tests that must not depend on the real
    /// generator. <see cref="NoticeLimitM"/> hides a zone until the route is that close to it (short notice).
    /// </summary>
    internal sealed class ScriptedChunkSource : IRouteChunkSource
    {
        private readonly List<RouteZone> _zones = new List<RouteZone>();
        private readonly List<double> _worldSwitchS = new List<double>();

        /// <summary>Zones are reported only when they start within this distance (default: always).</summary>
        public double NoticeLimitM = double.PositiveInfinity;

        public double CommittedEndS => double.PositiveInfinity;

        public void AddVine(double startS, double lengthM)
        {
            _zones.Add(new RouteZone { Kind = RouteBeatKind.SwingZone, StartS = startS, EndS = startS + lengthM, Serial = _zones.Count + 1 });
        }

        public void AddGateway(double startS, double lengthM)
        {
            _zones.Add(new RouteZone { Kind = RouteBeatKind.Gateway, StartS = startS, EndS = startS + lengthM, Serial = _zones.Count + 1 });
            _worldSwitchS.Add(startS + (lengthM * 0.5));
        }

        public bool TryFindZone(double s, double lookaheadM, out RouteZone zone)
        {
            double look = lookaheadM < NoticeLimitM ? lookaheadM : NoticeLimitM;
            for (int i = 0; i < _zones.Count; i++)
            {
                RouteZone candidate = _zones[i];
                if (candidate.EndS > s && candidate.StartS <= s + look)
                {
                    zone = candidate;
                    return true;
                }
            }

            zone = default(RouteZone);
            return false;
        }

        public WorldKind WorldKindAt(double s)
        {
            int n = 0;
            for (int i = 0; i < _worldSwitchS.Count; i++)
            {
                if (_worldSwitchS[i] <= s)
                {
                    n++;
                }
            }

            return (WorldKind)(n % 4);
        }

        /// <summary>The four default world gateways (1100, 2300, 3600, 5000 m) and a vine section every 400 to 900 m from a seed.</summary>
        public static ScriptedChunkSource CreateFor(ulong seed, double lengthM)
        {
            var source = new ScriptedChunkSource();
            double[] gateways = { 1100.0, 2300.0, 3600.0, 5000.0 };
            for (int i = 0; i < gateways.Length; i++)
            {
                source.AddGateway(gateways[i] - 27.0, 54.0);
            }

            var rng = new System.Random(unchecked((int)(seed * 2654435761UL)));
            double s = 300.0 + (rng.NextDouble() * 400.0);
            while (s < lengthM)
            {
                double length = 60.0 + (rng.Next(0, 3) * 25.0);
                bool nearGateway = false;
                for (int i = 0; i < gateways.Length; i++)
                {
                    if (s + length + 120.0 > gateways[i] - 27.0 && s < gateways[i] + 150.0)
                    {
                        nearGateway = true;
                    }
                }

                if (!nearGateway)
                {
                    source.AddVine(s, length);
                }

                s += length + 400.0 + (rng.NextDouble() * 500.0);
            }

            return source;
        }
    }
}
