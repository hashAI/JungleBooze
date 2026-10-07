using System;
using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Route tuning of one world (spec 003 sections 4.3, 4.4 and 13). All values are [ASSUMED] first guesses taken
    /// from the spec tables. Beat weights are "percent of route length": the generator divides by the mean beat
    /// length, so the share of length (not of beats) follows the table.
    /// </summary>
    [Serializable]
    public sealed class RouteWorldTuning
    {
        [SerializeField] private string _name = "Jungle";
        [SerializeField] private float _rMinM = 75f;
        [SerializeField] private float _gradeMaxPct = 10f;
        [SerializeField] private float _bankMaxDeg = 6f;

        [Tooltip("Chance (0..1) that a climbing or falling beat climbs when the route is on its elevation centre.")]
        [SerializeField] private float _riseBias = 0.5f;

        [Tooltip("Drift of the elevation centre in m per km (River falls, Mountains climb). Also sets a baseline grade of 0.14 percent per m/km on normal beats.")]
        [SerializeField] private float _netElevationPerKmM;

        [Tooltip("The route steers back when it is further than this from the elevation centre.")]
        [SerializeField] private float _elevationCorridorM = 8f;

        [Tooltip("Weights in RouteBeatKind order (Straight..Descent). SwingZone and Gateway entries are unused.")]
        [SerializeField] private float[] _beatWeights = new float[RouteTuning.BeatKindCount];

        [Tooltip("Ground under normal beats (Mountains run on ledges).")]
        [SerializeField] private PathSurface _baseSurface = PathSurface.Trail;

        public string Name => _name;

        /// <summary>Tightest sustained turn radius in m.</summary>
        public float RMinM => _rMinM;

        /// <summary>Largest sustained grade in percent.</summary>
        public float GradeMaxPct => _gradeMaxPct;

        /// <summary>Bank at the curvature 1/75 in degrees (also the cap).</summary>
        public float BankMaxDeg => _bankMaxDeg;

        public float RiseBias => _riseBias;

        public float NetElevationPerKmM => _netElevationPerKmM;

        public float ElevationCorridorM => _elevationCorridorM;

        public PathSurface BaseSurface => _baseSurface;

        /// <summary>Weight of <paramref name="kind"/> (0 when the table is shorter).</summary>
        public float GetWeight(RouteBeatKind kind)
        {
            int i = (int)kind;
            return _beatWeights != null && i >= 0 && i < _beatWeights.Length ? _beatWeights[i] : 0f;
        }

        /// <summary>The four worlds in <c>WorldKind</c> order: Jungle, River, Mountains, Ruins.</summary>
        public static RouteWorldTuning[] CreateDefaults()
        {
            return new[]
            {
                Create("Jungle", 75f, 10f, 0.5f, 0f, PathSurface.Trail, 8, 18, 18, 16, 12, 8, 8, 4, 0),
                Create("River", 90f, 10f, 0.35f, -35f, PathSurface.Trail, 6, 38, 10, 8, 6, 12, 6, 8, 4),
                Create("Mountains", 80f, 14f, 0.7f, 80f, PathSurface.Ledge, 12, 12, 14, 20, 4, 24, 6, 0, 8),
                Create("Ruins", 85f, 10f, 0.5f, 0f, PathSurface.Trail, 14, 14, 14, 8, 4, 18, 8, 4, 12),
            };
        }

        private static RouteWorldTuning Create(
            string name, float rMin, float gradeMax, float riseBias, float netPerKm, PathSurface baseSurface,
            float straight, float gentle, float bend, float sBend, float roll, float riseFall, float clearing, float crossing, float bridge)
        {
            var w = new RouteWorldTuning
            {
                _name = name,
                _rMinM = rMin,
                _gradeMaxPct = gradeMax,
                _riseBias = riseBias,
                _netElevationPerKmM = netPerKm,
                _baseSurface = baseSurface,
                _beatWeights = new float[RouteTuning.BeatKindCount],
            };

            w._beatWeights[(int)RouteBeatKind.Straight] = straight;
            w._beatWeights[(int)RouteBeatKind.GentleBend] = gentle;
            w._beatWeights[(int)RouteBeatKind.Bend] = bend;
            w._beatWeights[(int)RouteBeatKind.SBend] = sBend;
            w._beatWeights[(int)RouteBeatKind.Roll] = roll;
            w._beatWeights[(int)RouteBeatKind.RiseFall] = riseFall;
            w._beatWeights[(int)RouteBeatKind.Clearing] = clearing;
            w._beatWeights[(int)RouteBeatKind.Crossing] = crossing;
            w._beatWeights[(int)RouteBeatKind.Bridge] = bridge;
            return w;
        }
    }
}
