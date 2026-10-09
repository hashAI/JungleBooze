using System.Collections.Generic;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using UnityEngine;

namespace JungleBooze.Gameplay.Views.World
{
    /// <summary>
    /// Bakes the gray-box geometry of one chunk variant into a single mesh in chunk-local space (x lateral, y up,
    /// z = s): path slab per lane with route tints (safe green, risky amber, secret violet), Part B placeholder
    /// tints (water blue, canopy brown), shallow-water ford, side skirts, gap lips, edge hedges, obstacle boxes,
    /// dividers, water curtains and a seam stripe. Setup time only (allocates); placed chunks reuse the mesh.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public const int SubPath = 0;
        public const int SubSafe = 1;
        public const int SubRisky = 2;
        public const int SubSecret = 3;
        public const int SubSide = 4;
        public const int SubHedge = 5;
        public const int SubLow = 6;
        public const int SubHigh = 7;
        public const int SubBlocker = 8;
        public const int SubThorns = 9;
        public const int SubDivider = 10;
        public const int SubWater = 11;
        public const int SubCanopy = 12;
        public const int SubShallow = 13;
        public const int SubCurtain = 14;
        public const int SubMarker = 15;
        public const int SubmeshCount = 16;

        private const float SlabBottom = -4f;
        private const float StripStep = 2f;
        private const float HedgeHeight = 0.45f;
        private const float HedgeWidth = 0.5f;
        private const float Eps = 0.001f;

        public static Mesh Build(ChunkRuntime chunk)
        {
            var mesh = new Builder();
            BuildFloor(mesh, chunk);
            BuildObstacles(mesh, chunk);
            BuildDividers(mesh, chunk);
            BuildZones(mesh, chunk);

            // Seam stripe at the entry.
            chunk.GetOuterBounds(0f, out float x0, out float x1);
            mesh.Box(SubMarker, new Vector3(x0, 0f, 0f), new Vector3(x1, 0.015f, 0.25f));
            return mesh.ToMesh(chunk.Id + "_" + chunk.VariantName);
        }

        private static void BuildFloor(Builder mesh, ChunkRuntime c)
        {
            var cuts = new List<float> { 0f, c.Length };
            for (float s = 0f; s < c.Length; s += StripStep)
            {
                cuts.Add(s);
            }

            for (int i = 0; i < c.WidthKeyCount; i++)
            {
                cuts.Add(c.GetWidthKey(i).S);
            }

            for (int i = 0; i < c.FloorCount; i++)
            {
                cuts.Add(c.GetFloor(i).SMin);
                cuts.Add(c.GetFloor(i).SMax);
            }

            for (int i = 0; i < c.DividerCount; i++)
            {
                cuts.Add(c.GetDivider(i).SFront);
                cuts.Add(c.GetDivider(i).SMerge);
            }

            for (int i = 0; i < c.TraversalCount; i++)
            {
                cuts.Add(c.GetTraversal(i).SMin);
                cuts.Add(c.GetTraversal(i).SMax);
            }

            cuts.Sort();
            var xs = new List<float>(16);
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float a = cuts[i];
                float b = cuts[i + 1];
                if (b - a < 0.01f || a < 0f || b > c.Length)
                {
                    continue;
                }

                float mid = (a + b) * 0.5f;
                c.GetOuterBounds(mid, out float outerMin, out float outerMax);

                // Lane edges (outer + dividers) and floor-patch x edges split the strip into columns.
                xs.Clear();
                xs.Add(outerMin);
                xs.Add(outerMax);
                for (int d = 0; d < c.DividerCount; d++)
                {
                    ChunkDivider dv = c.GetDivider(d);
                    if (mid >= dv.SFront && mid < dv.SMerge)
                    {
                        xs.Add(dv.XMin);
                        xs.Add(dv.XMax);
                    }
                }

                for (int f = 0; f < c.FloorCount; f++)
                {
                    CourseFloorPatch p = c.GetFloor(f);
                    if (mid >= p.SMin && mid < p.SMax)
                    {
                        if (p.XMin > outerMin && p.XMin < outerMax)
                        {
                            xs.Add(p.XMin);
                        }

                        if (p.XMax > outerMin && p.XMax < outerMax)
                        {
                            xs.Add(p.XMax);
                        }
                    }
                }

                xs.Sort();
                for (int k = 0; k < xs.Count - 1; k++)
                {
                    float cx0 = xs[k];
                    float cx1 = xs[k + 1];
                    if (cx1 - cx0 < 0.01f)
                    {
                        continue;
                    }

                    float xc = (cx0 + cx1) * 0.5f;
                    if (InsideDivider(c, mid, xc))
                    {
                        continue;
                    }

                    Column(mesh, c, a, b, cx0, cx1, xc, outerMin, outerMax);
                }
            }
        }

        private static bool InsideDivider(ChunkRuntime c, float s, float x)
        {
            for (int d = 0; d < c.DividerCount; d++)
            {
                ChunkDivider dv = c.GetDivider(d);
                if (s >= dv.SFront && s < dv.SMerge && x > dv.XMin && x < dv.XMax)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Column(Builder mesh, ChunkRuntime c, float a, float b, float x0, float x1, float xc, float outerMin, float outerMax)
        {
            float mid = (a + b) * 0.5f;
            if (!c.TryGetFloor(mid, xc, out _))
            {
                return;
            }

            c.TryGetFloor(a + Eps, xc, out float ya);
            c.TryGetFloor(b - Eps, xc, out float yb);

            // Outer edges follow the width keys at both ends; inner edges (dividers, patches) stay straight.
            c.GetOuterBounds(a, out float aMin, out float aMax);
            c.GetOuterBounds(b, out float bMin, out float bMax);
            bool leftOuter = Mathf.Abs(x0 - outerMin) < 0.005f;
            bool rightOuter = Mathf.Abs(x1 - outerMax) < 0.005f;
            float xa0 = leftOuter ? aMin : x0;
            float xb0 = leftOuter ? bMin : x0;
            float xa1 = rightOuter ? aMax : x1;
            float xb1 = rightOuter ? bMax : x1;

            int sub = Tint(c, mid, xc);
            var p00 = new Vector3(xa0, ya, a);
            var p01 = new Vector3(xa1, ya, a);
            var p10 = new Vector3(xb0, yb, b);
            var p11 = new Vector3(xb1, yb, b);
            mesh.Quad(sub, p00, p10, p11, p01);

            mesh.Quad(SubSide, new Vector3(xa0, SlabBottom, a), new Vector3(xb0, SlabBottom, b), p10, p00);
            mesh.Quad(SubSide, p01, p11, new Vector3(xb1, SlabBottom, b), new Vector3(xa1, SlabBottom, a));

            bool before = c.TryGetFloor(a - Eps, xc, out float yBefore) || a <= 0f;
            if (!before || yBefore < ya - 0.05f)
            {
                mesh.Quad(SubSide, new Vector3(xa0, before ? yBefore : SlabBottom, a), p00, p01, new Vector3(xa1, before ? yBefore : SlabBottom, a));
            }

            bool after = c.TryGetFloor(b + Eps, xc, out float yAfter) || b >= c.Length;
            if (!after || yAfter < yb - 0.05f)
            {
                mesh.Quad(SubSide, p11, p10, new Vector3(xb0, after ? yAfter : SlabBottom, b), new Vector3(xb1, after ? yAfter : SlabBottom, b));
            }

            if (leftOuter)
            {
                Hedge(mesh, a, b, xa0, xb0, ya, yb, -1f);
            }

            if (rightOuter)
            {
                Hedge(mesh, a, b, xa1, xb1, ya, yb, 1f);
            }
        }

        private static int Tint(ChunkRuntime c, float s, float x)
        {
            for (int i = 0; i < c.TraversalCount; i++)
            {
                TraversalZone z = c.GetTraversal(i);
                if (s < z.SMin || s >= z.SMax || x < z.XMin || x > z.XMax)
                {
                    continue;
                }

                switch (z.Mode)
                {
                    case TraversalMode.Swim:
                    case TraversalMode.DeepDive:
                        return SubWater;
                    case TraversalMode.Vine:
                    case TraversalMode.Canopy:
                        return SubCanopy;
                    case TraversalMode.ShallowWater:
                        return SubShallow;
                }
            }

            switch (c.RouteAt(s, x))
            {
                case RouteType.Safe:
                    return SubSafe;
                case RouteType.Risky:
                    return SubRisky;
                case RouteType.Secret:
                    return SubSecret;
                default:
                    return SubPath;
            }
        }

        private static void Hedge(Builder mesh, float a, float b, float xa, float xb, float ya, float yb, float side)
        {
            float wa = xa + (side * HedgeWidth);
            float wb = xb + (side * HedgeWidth);
            var ia = new Vector3(xa, ya + HedgeHeight, a);
            var ib = new Vector3(xb, yb + HedgeHeight, b);
            var oa = new Vector3(wa, ya + HedgeHeight, a);
            var ob = new Vector3(wb, yb + HedgeHeight, b);
            var ia0 = new Vector3(xa, ya, a);
            var ib0 = new Vector3(xb, yb, b);
            if (side > 0f)
            {
                mesh.Quad(SubHedge, ia0, ib0, ib, ia);
                mesh.Quad(SubHedge, ia, ib, ob, oa);
            }
            else
            {
                mesh.Quad(SubHedge, ia, ib, ib0, ia0);
                mesh.Quad(SubHedge, oa, ob, ib, ia);
            }
        }

        private static void BuildObstacles(Builder mesh, ChunkRuntime c)
        {
            for (int i = 0; i < c.ObstacleCount; i++)
            {
                ObstacleBox o = c.GetObstacle(i);
                switch (o.Class)
                {
                    case ObstacleClass.Low:
                        mesh.Box(SubLow, new Vector3(o.XMin, o.YMin, o.SMin), new Vector3(o.XMax, o.YMax, o.SMax));
                        break;
                    case ObstacleClass.High:
                    {
                        float floor = c.TryGetFloor(o.SMin, o.CenterX, out float f) ? f : 0f;
                        float beamTop = Mathf.Min(o.YMax, o.YMin + 0.25f);
                        mesh.Box(SubHigh, new Vector3(o.XMin, o.YMin, o.SMin), new Vector3(o.XMax, beamTop, o.SMax));
                        mesh.Box(SubHigh, new Vector3(o.XMin - 0.3f, floor, o.SMin + 0.1f), new Vector3(o.XMin, o.YMax, o.SMax - 0.1f));
                        mesh.Box(SubHigh, new Vector3(o.XMax, floor, o.SMin + 0.1f), new Vector3(o.XMax + 0.3f, o.YMax, o.SMax - 0.1f));
                        for (float x = o.XMin + 0.6f; x < o.XMax - 0.3f; x += 1.2f)
                        {
                            mesh.Box(SubHedge, new Vector3(x - 0.035f, beamTop, o.SMin + 0.27f), new Vector3(x + 0.035f, o.YMax + 1.5f, o.SMin + 0.33f));
                        }

                        break;
                    }

                    case ObstacleClass.Blocker:
                        mesh.Box(SubBlocker, new Vector3(o.XMin, o.YMin, o.SMin), new Vector3(o.XMax, o.YMax, o.SMax));
                        break;
                    default:
                        mesh.Box(SubThorns, new Vector3(o.XMin, o.YMin, o.SMin), new Vector3(o.XMax, o.YMin + 0.12f, o.SMax));
                        for (float s = o.SMin + 0.25f; s < o.SMax; s += 0.5f)
                        {
                            for (float x = o.XMin + 0.25f; x < o.XMax; x += 0.5f)
                            {
                                mesh.Box(SubThorns, new Vector3(x - 0.06f, o.YMin, s - 0.06f), new Vector3(x + 0.06f, o.YMax, s + 0.06f));
                            }
                        }

                        break;
                }
            }
        }

        private static void BuildDividers(Builder mesh, ChunkRuntime c)
        {
            for (int i = 0; i < c.DividerCount; i++)
            {
                ChunkDivider d = c.GetDivider(i);
                // A low wall (the camera can follow Pista into a narrow lane beside it without being blocked) with a
                // tall post at the split so the fork reads from far away.
                c.TryGetFloor(d.SFront, d.CenterX, out float y);
                mesh.Box(SubDivider, new Vector3(d.XMin, SlabBottom, d.SFront), new Vector3(d.XMax, y + 1.0f, d.SMerge));
                mesh.Box(SubDivider, new Vector3(d.XMin, y, d.SFront), new Vector3(d.XMax, y + 3.2f, d.SFront + 1.2f));
            }
        }

        private static void BuildZones(Builder mesh, ChunkRuntime c)
        {
            for (int i = 0; i < c.TraversalCount; i++)
            {
                TraversalZone z = c.GetTraversal(i);
                if (z.Mode == TraversalMode.Curtain)
                {
                    c.TryGetFloor(z.SMin, (z.XMin + z.XMax) * 0.5f, out float y);
                    mesh.Box(SubCurtain, new Vector3(z.XMin, y, z.SMin + 1f), new Vector3(z.XMax, y + 4.5f, z.SMin + 1.15f));
                }
            }
        }

        /// <summary>Mesh builder with fixed submeshes (setup only).</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _indices = new List<int>[SubmeshCount];

            public Builder()
            {
                for (int i = 0; i < SubmeshCount; i++)
                {
                    _indices[i] = new List<int>();
                }
            }

            public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
                int start = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _vertices.Add(d);
                for (int i = 0; i < 4; i++)
                {
                    _normals.Add(normal);
                }

                _uvs.Add(new Vector2(a.x, a.z));
                _uvs.Add(new Vector2(b.x, b.z));
                _uvs.Add(new Vector2(c.x, c.z));
                _uvs.Add(new Vector2(d.x, d.z));
                List<int> target = _indices[submesh];
                target.Add(start);
                target.Add(start + 1);
                target.Add(start + 2);
                target.Add(start);
                target.Add(start + 2);
                target.Add(start + 3);
            }

            public void Box(int submesh, Vector3 min, Vector3 max)
            {
                var p000 = new Vector3(min.x, min.y, min.z);
                var p100 = new Vector3(max.x, min.y, min.z);
                var p010 = new Vector3(min.x, max.y, min.z);
                var p110 = new Vector3(max.x, max.y, min.z);
                var p001 = new Vector3(min.x, min.y, max.z);
                var p101 = new Vector3(max.x, min.y, max.z);
                var p011 = new Vector3(min.x, max.y, max.z);
                var p111 = new Vector3(max.x, max.y, max.z);
                Quad(submesh, p000, p010, p110, p100); // front (−z)
                Quad(submesh, p101, p111, p011, p001); // back (+z)
                Quad(submesh, p010, p011, p111, p110); // top
                Quad(submesh, p001, p011, p010, p000); // left
                Quad(submesh, p100, p110, p111, p101); // right
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = SubmeshCount;
                for (int i = 0; i < SubmeshCount; i++)
                {
                    mesh.SetTriangles(_indices[i], i);
                }

                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
