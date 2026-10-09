using System.Collections.Generic;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// The look-test v2 graybox landmarks (ENVIRONMENT_STRATEGY sections 5 and 10), built from tubes, clumps and
    /// sheets with the CC0 rock, bark and leaf materials:
    /// - giant stiltwoods (the first one arches its stilt roots over the trail: F1 "out of the roots");
    /// - the fork (F2): a sunlit raised root ridge on the left (risky), the wide moss trail (safe), a dark rootstone
    ///   opening with hanging strands on the right (secret);
    /// - the river fall where the river leaves the plateau (seen from the ford, F3);
    /// - the basin (F4/P1): a rootstone arch with the hero waterfall pouring from its underside into a plunge pool on
    ///   a mesa, terraced pools stepping down, near pillars (one with a fall from its side);
    /// - the backdrop (L5, rides with the run): a range of rootstone pillars and arches over a valley of forest and
    ///   mist, with thin falls;
    /// - light shafts and mist cards.
    /// Shapes are placeholders for the art steps 4-9; the point is composition, scale and light.
    /// </summary>
    public static class LookTestLandmarks
    {
        public static void Build(LookTestBuildContext ctx)
        {
            var rng = new Pcg32Random((ulong)(uint)ctx.Config.Seed, 77UL);
            Vector3[] stiltwoods = ctx.Config.Stiltwoods;
            for (int i = 0; i < stiltwoods.Length; i++)
            {
                Stiltwood(ctx, rng, stiltwoods[i].x, stiltwoods[i].y, stiltwoods[i].z, i == 0);
            }

            Fork(ctx, rng);
            RiverFall(ctx, rng);
            Basin(ctx, rng);
            Shafts(ctx, rng);
            GroundMist(ctx, rng);
        }

        // ---------------------------------------------------------------- Helpers

        public static List<Vector3> Bezier(Vector3 a, Vector3 control, Vector3 b, int points)
        {
            var list = new List<Vector3>(points);
            for (int i = 0; i < points; i++)
            {
                float t = (float)i / (points - 1);
                float u = 1f - t;
                list.Add(u * u * a + 2f * u * t * control + t * t * b);
            }

            return list;
        }

        private static List<float> Taper(int points, float from, float to)
        {
            var list = new List<float>(points);
            for (int i = 0; i < points; i++)
            {
                list.Add(Mathf.Lerp(from, to, (float)i / (points - 1)));
            }

            return list;
        }

        /// <summary>A rootstone pillar or mesa: braided column from baseY to topY, flared base, domed or flat top.</summary>
        public static Mesh Pillar(IRandom rng, Vector3 center, float baseY, float topY, float radius, bool flatTop)
        {
            return Pillar(rng, center, baseY, topY, radius, flatTop, 18, 6f);
        }

        /// <summary>Pillar with a chosen resolution (sides, meters per ring).</summary>
        public static Mesh Pillar(IRandom rng, Vector3 center, float baseY, float topY, float radius, bool flatTop, int sides, float ringStepM)
        {
            var spine = new List<Vector3>();
            var radii = new List<float>();
            float height = topY - baseY;
            int rings = Mathf.Clamp(Mathf.RoundToInt(height / ringStepM), 6, 24);
            float bendA = rng.NextFloat(0f, 6.283f);
            float bend = rng.NextFloat(0.02f, 0.08f) * height;
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                var offset = new Vector3(Mathf.Cos(bendA), 0f, Mathf.Sin(bendA)) * bend * Mathf.Sin(t * Mathf.PI * 0.8f);
                spine.Add(new Vector3(center.x, baseY + height * t, center.z) + offset);
                float profile = 1f + 0.3f * Mathf.Exp(-t * 6f) + 0.12f * Mathf.Sin(t * 9f + bendA) + 0.15f * LookTestMath.Smooth(0.8f, 1f, t);
                radii.Add(radius * profile);
            }

            // Cap: flat (mesa) or a rounded crown.
            Vector3 top = spine[spine.Count - 1];
            float topRadius = radii[radii.Count - 1];
            int capRings = 3;
            for (int i = 1; i <= capRings; i++)
            {
                float t = (float)i / capRings;
                float lift = flatTop ? 0.4f * t : topRadius * 0.35f * Mathf.Sin(t * Mathf.PI * 0.5f);
                spine.Add(top + Vector3.up * lift);
                radii.Add(topRadius * Mathf.Cos(t * Mathf.PI * 0.5f) + 0.05f);
            }

            return LookTestMeshFactory.Tube("Pillar", spine, radii, sides, 9f, rng.NextInt(5, 8), 0.4f, rng.NextFloat(0.02f, 0.06f), rng.NextFloat(0f, 6f));
        }

        /// <summary>Quaternion that turns the waterfall sheet (faces local −x, up +y) to face <paramref name="toViewer"/>.</summary>
        public static Quaternion FaceViewer(Vector3 toViewer)
        {
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude < 1e-6f)
            {
                toViewer = Vector3.back;
            }

            toViewer.Normalize();
            return Quaternion.LookRotation(Vector3.Cross(-toViewer, Vector3.up), Vector3.up);
        }

        private static void Fall(LookTestMeshAccumulator falls, Vector3 top, float footY, float width, Vector3 viewer, float setback)
        {
            Vector3 toViewer = viewer - top;
            toViewer.y = 0f;
            toViewer.Normalize();
            float height = top.y - footY;
            Vector3 foot = new Vector3(top.x, footY, top.z) + toViewer * setback;
            Mesh sheet = LookTestMeshFactory.Waterfall(width, height, setback);
            falls.Append(sheet, Matrix4x4.TRS(foot, FaceViewer(toViewer), Vector3.one), null);
        }

        private static void Billows(LookTestMeshAccumulator mist, IRandom rng, Vector3 center, int count, float spread, Vector2 width, Vector2 height)
        {
            for (int i = 0; i < count; i++)
            {
                var p = center + new Vector3(rng.NextFloat(-1f, 1f) * spread, rng.NextFloat(0f, 0.5f) * height.y, rng.NextFloat(-1f, 1f) * spread);
                mist.AppendCard(p, Vector3.up, rng.NextFloat(width.x, width.y), rng.NextFloat(height.x, height.y), rng.NextFloat(0f, 10f));
            }
        }

        // ---------------------------------------------------------------- Stiltwoods

        private static void Stiltwood(LookTestBuildContext ctx, IRandom rng, float s, float d, float scale, bool hero)
        {
            LookTestStretchLayout layout = ctx.Layout;
            int segment = ctx.SegmentOf(s);
            LookTestMeshAccumulator wood = ctx.Batches.Get(segment, "L2", "Wood", ctx.Bark, true, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator roof = ctx.Batches.Get(segment, "L3", "Canopy", ctx.Leaves, false, LookTestBatchSet.Group.Trees);
            float g = layout.GroundY(s, d);
            Vector3 basePoint = layout.Path.World(s, d, g);
            Vector3 forward = layout.Path.Forward(s);
            Vector3 right = layout.Path.Right(s);
            float stilt = 6.5f * scale;
            float trunkHeight = 40f * scale;

            var spine = new List<Vector3>();
            float bendA = rng.NextFloat(0f, 6.283f);
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                spine.Add(basePoint + Vector3.up * (stilt - 1f + trunkHeight * t) + new Vector3(Mathf.Cos(bendA), 0f, Mathf.Sin(bendA)) * 1.5f * scale * t * t);
            }

            wood.Append(LookTestMeshFactory.Tube("Stiltwood", spine, Taper(spine.Count, 2.1f * scale, 1.0f * scale), 16, 2f, 7, 0.35f, 0.04f, rng.NextFloat(0f, 6f)), Matrix4x4.identity, ctx.Solid(s, d));

            // Stilt roots fanning out to the ground.
            int roots = 11;
            for (int i = 0; i < roots; i++)
            {
                float a = (i + rng.NextFloat(-0.3f, 0.3f)) * 2f * Mathf.PI / roots;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float reach = rng.NextFloat(7f, 13f) * scale;
                float ls = s + dir.x * reach;
                float ld = d + dir.y * reach;
                if (LookTestScatter.Blocked(layout, ls, ld, 0.3f))
                {
                    continue;
                }

                Vector3 horizontal = (forward * dir.x + right * dir.y).normalized;
                Vector3 attach = basePoint + Vector3.up * rng.NextFloat(stilt * 0.7f, stilt * 1.3f) + horizontal * 1.4f * scale;
                Vector3 land = layout.Path.World(ls, ld, layout.GroundY(ls, ld) - 0.4f);
                Vector3 control = Vector3.Lerp(attach, land, 0.45f) + Vector3.up * rng.NextFloat(2.5f, 4.5f) * scale;
                List<Vector3> root = Bezier(attach, control, land, 12);
                wood.Append(LookTestMeshFactory.Tube("Stilt", root, Taper(root.Count, 0.75f * scale, 0.4f * scale), 9, 1.2f, 3, 0.3f, 0.2f, i), Matrix4x4.identity, ctx.Solid(s, d));
            }

            if (hero)
            {
                // Roots arching over the trail: underside ≥ 7 m above it where the camera passes (spec 101 corridor).
                float h = layout.TrailHeight(s);
                float[] landS = { s + 2f, s + 6f, s + 10f, s + 14f };
                for (int i = 0; i < landS.Length; i++)
                {
                    float ls = landS[i];
                    float ld = ctx.Config.RunnableHalfWidthM + rng.NextFloat(2.5f, 4.5f);
                    Vector3 attach = basePoint + Vector3.up * rng.NextFloat(stilt * 1.0f, stilt * 1.5f) + right * 1.6f * scale;
                    Vector3 land = layout.Path.World(ls, ld, layout.GroundY(ls, ld) - 0.5f);
                    Vector3 control = layout.Path.World((s + ls) * 0.5f, -1.5f, h + rng.NextFloat(10.5f, 12.5f));
                    List<Vector3> root = Bezier(attach, control, land, 18);
                    wood.Append(LookTestMeshFactory.Tube("ArchRoot", root, Taper(root.Count, 1.1f, 0.55f), 12, 1.2f, 4, 0.35f, 0.25f, 10 + i), Matrix4x4.identity, (w, l, src) => new Color(0f, 0f, 0.45f, src.a));
                }
            }

            Mesh crown = LookTestMeshFactory.Crown(rng, trunkHeight, 90, 6.5f * scale);
            Vector3 crownBase = basePoint + Vector3.up * stilt;
            roof.Append(crown, Matrix4x4.TRS(crownBase, Quaternion.identity, Vector3.one), ctx.Foliage(s, d, crownBase.y + trunkHeight * 0.5f, trunkHeight * 0.5f, 0.4f));
        }

        // ---------------------------------------------------------------- The fork (F2)

        private static void Fork(LookTestBuildContext ctx, IRandom rng)
        {
            LookTestStretchLayout layout = ctx.Layout;

            // Left: a raised root ridge, the risky route (sunlit by the sun patch in the config).
            float[] rs = { 60f, 68f, 76f, 86f, 96f, 104f, 110f };
            float[] rd = { -3.6f, -4.4f, -5.4f, -6.0f, -6.0f, -5.0f, -3.9f };
            float[] ry = { -0.6f, 0.6f, 1.8f, 2.3f, 2.1f, 0.9f, -0.6f };
            var ridge = new List<Vector3>();
            var ridgeRadii = new List<float>();
            for (int i = 0; i < rs.Length; i++)
            {
                ridge.Add(layout.Path.World(rs[i], rd[i], layout.TrailHeight(rs[i]) + ry[i]));
                ridgeRadii.Add(1.25f + 0.2f * Mathf.Sin(i * 1.7f));
            }

            LookTestMeshAccumulator ridgeWood = ctx.Batches.Get(ctx.SegmentOf(84f), "L2", "Wood", ctx.Bark, true, LookTestBatchSet.Group.Trees);
            ridgeWood.Append(LookTestMeshFactory.Tube("RootRidge", ridge, ridgeRadii, 14, 1.5f, 4, 0.35f, 0.12f, 3f), Matrix4x4.identity, ctx.Solid(84f, -6f));

            // Right: a dark rootstone opening with hanging strands (the secret route).
            int segment = ctx.SegmentOf(88f);
            LookTestMeshAccumulator stone = ctx.Batches.Get(segment, "L2", "Rootstone", ctx.Rootstone, true, LookTestBatchSet.Group.Ground);
            LookTestMeshAccumulator wood = ctx.Batches.Get(segment, "L2", "Wood", ctx.Bark, true, LookTestBatchSet.Group.Trees);
            float[] depths = { 6.6f, 10.5f, 14.5f };
            for (int k = 0; k < depths.Length; k++)
            {
                float d = depths[k];
                float sa = 84f + k * 0.6f;
                float sb = 94f - k * 0.6f;
                float h = layout.TrailHeight(89f);
                Vector3 a = layout.Path.World(sa, d, layout.GroundY(sa, d) - 1f);
                Vector3 b = layout.Path.World(sb, d, layout.GroundY(sb, d) - 1f);
                Vector3 c = layout.Path.World(89f, d, h + 13.5f - k * 1.5f);
                List<Vector3> arch = Bezier(a, c, b, 16);
                stone.Append(LookTestMeshFactory.Tube("SecretArch", arch, Taper(arch.Count, 1.6f, 1.6f), 12, 4f, 5, 0.45f, 0.1f, k), Matrix4x4.identity, (w, l, src) => new Color(0f, 0f, 1f, src.a * 0.8f));

                // Strands hanging from the arch (veilmoss placeholder).
                for (int i = 2; i < arch.Count - 2; i += 1)
                {
                    Vector3 top = arch[i] + Vector3.down * 1.2f;
                    float length = Mathf.Max(1f, top.y - (h + rng.NextFloat(0.8f, 2.5f)));
                    var strand = new List<Vector3> { top, top + Vector3.down * length * 0.5f + Vector3.right * 0.1f, top + Vector3.down * length };
                    wood.Append(LookTestMeshFactory.Tube("Strand", strand, new List<float> { 0.07f, 0.06f, 0.04f }, 4, 0.4f, 0, 0f, 0f, i), Matrix4x4.identity, (w, l, src) => new Color(0f, 0f, 1f, src.a * 0.7f));
                }
            }
        }

        // ---------------------------------------------------------------- River fall (seen from the ford)

        private static void RiverFall(LookTestBuildContext ctx, IRandom rng)
        {
            LookTestStretchLayout layout = ctx.Layout;
            float s = layout.RiverFallS();
            if (s < 0f)
            {
                return;
            }

            int segment = ctx.SegmentOf(s);
            float rd = layout.RiverD(s);
            Vector3 top = layout.Path.World(s, rd, layout.WaterY(rd));
            Vector3 viewer = layout.Path.World(ctx.Config.RiverCrossSM - 10f, 0f, top.y);
            LookTestMeshAccumulator falls = ctx.Batches.Get(segment, "W", "Falls", ctx.Waterfall, false, LookTestBatchSet.Group.Water);
            float footY = layout.BelowRight(s);
            Fall(falls, top, footY, 7f, viewer, 1.2f);
            LookTestMeshAccumulator mist = ctx.Batches.Get(segment, "W", "Mist", ctx.MistCard, false, LookTestBatchSet.Group.Water);
            Billows(mist, rng, new Vector3(top.x, footY + 3f, top.z), 6, 6f, new Vector2(12f, 20f), new Vector2(8f, 14f));
        }

        // ---------------------------------------------------------------- The basin (F4, P1)

        /// <summary>The basin seen from the ledge at s = 190 (F4, P1): a point forward/right of the ledge frame at world height y.</summary>
        public static Vector3 BasinPoint(LookTestPath path, float forward, float right, float y)
        {
            const double ledge = 190.0;
            Vector3 p = path.Position(ledge) + path.Forward(ledge) * forward + path.Right(ledge) * right;
            p.y = y;
            return p;
        }

        private static void Basin(LookTestBuildContext ctx, IRandom rng)
        {
            LookTestStretchLayout layout = ctx.Layout;
            LookTestConfigAsset c = ctx.Config;
            int segment = ctx.SegmentOf(205f);
            LookTestMeshAccumulator stone = ctx.Batches.Get(segment, "L2", "Basin", ctx.Rootstone, false, LookTestBatchSet.Group.Ground);
            LookTestMeshAccumulator tops = ctx.Batches.Get(segment, "L2", "Basin tops", ctx.FarCanopy, false, LookTestBatchSet.Group.Ground);
            LookTestMeshAccumulator falls = ctx.Batches.Get(segment, "W", "Falls", ctx.Waterfall, false, LookTestBatchSet.Group.Water);
            LookTestMeshAccumulator pools = ctx.Batches.Get(segment, "W", "Pools", ctx.Pool, false, LookTestBatchSet.Group.Water);
            LookTestMeshAccumulator mist = ctx.Batches.Get(segment, "W", "Mist", ctx.MistCard, false, LookTestBatchSet.Group.Water);
            LookTestPath path = layout.Path;
            float valley = c.ValleyFloorY - 2f;
            Vector3 viewer = path.World(181f, 0f, layout.TrailHeight(181f) + 3.6f);

            // Rootstone arch across the view; the hero fall pours from its underside.
            // Nearly on the trail axis at the ledge: portrait (≈33° horizontal view) must see the fall too.
            Vector3 footA = BasinPoint(path, 74f, -30f, valley);
            Vector3 footB = BasinPoint(path, 90f, 30f, valley);
            Vector3 apexControl = BasinPoint(path, 82f, 0f, 125f);
            List<Vector3> arch = Bezier(footA, apexControl, footB, 24);
            var archRadii = new List<float>();
            for (int i = 0; i < arch.Count; i++)
            {
                float t = (float)i / (arch.Count - 1);
                archRadii.Add(Mathf.Lerp(4.5f, 9f, Mathf.Abs(t - 0.5f) * 2f));
            }

            stone.Append(LookTestMeshFactory.Tube("HeroArch", arch, archRadii, 18, 9f, 6, 0.5f, 0.05f, 1f), Matrix4x4.identity, LookTestBuildContext.Open);
            Vector3 under = arch[arch.Count / 2] + Vector3.down * 4.8f;
            Tufts(tops, rng, arch[arch.Count / 2] + Vector3.up * 3.5f, 6f, 5);

            // Plunge pool on a mesa under the fall; terraces step down toward the ledge, each overflowing into the next.
            var mesas = new[]
            {
                new Vector4(under.x, -12f, under.z, 15f),
                (Vector4)BasinPoint(path, 64f, 17f, -20f),
                (Vector4)BasinPoint(path, 50f, 22f, -27f),
            };
            mesas[1].w = 10f;
            mesas[2].w = 8f;
            for (int i = 0; i < mesas.Length; i++)
            {
                var center = new Vector3(mesas[i].x, 0f, mesas[i].z);
                stone.Append(Pillar(rng, center, valley, mesas[i].y, mesas[i].w, true), Matrix4x4.identity, LookTestBuildContext.Open);
                pools.Append(LookTestMeshFactory.Pool(mesas[i].w * 0.85f, 2.5f, "Pool"), Matrix4x4.Translate(new Vector3(center.x, mesas[i].y + 0.45f, center.z)), null);
                Vector3 upper = i == 0 ? Vector3.zero : new Vector3(mesas[i - 1].x, mesas[i - 1].y + 0.4f, mesas[i - 1].z);
                if (i > 0)
                {
                    Vector3 toThis = (center - new Vector3(upper.x, 0f, upper.z)).normalized;
                    Fall(falls, upper + toThis * mesas[i - 1].w * 0.95f, mesas[i].y + 0.4f, 5f, viewer, 0.6f);
                }
            }

            // The last terrace spills into the valley.
            Vector3 last = new Vector3(mesas[2].x, mesas[2].y + 0.4f, mesas[2].z);
            Vector3 outward = (last - viewer);
            outward.y = 0f;
            Fall(falls, last + Vector3.Cross(Vector3.up, outward.normalized) * mesas[2].w * 0.9f, valley, 4f, viewer, 0.8f);

            Fall(falls, under, mesas[0].y + 0.4f, 10f, viewer, 1.5f);
            Billows(mist, rng, new Vector3(under.x, mesas[0].y + 1f, under.z), 6, 9f, new Vector2(14f, 24f), new Vector2(8f, 14f));
            Billows(mist, rng, new Vector3(mesas[2].x, mesas[2].y + 1f, mesas[2].z), 4, 8f, new Vector2(14f, 22f), new Vector2(8f, 14f));
            Billows(mist, rng, BasinPoint(path, 90f, 40f, valley + 8f), 6, 30f, new Vector2(40f, 70f), new Vector2(14f, 24f));

            // Near pillars with forest on top; one has a fall from its side, seen from the ford (F3).
            Vector3 fordViewer = path.World(c.RiverCrossSM - 10f, 0f, layout.TrailHeight(c.RiverCrossSM) + 3.6f);
            var pillars = new[]
            {
                (Vector4)BasinPoint(path, 170f, 150f, 150f), (Vector4)BasinPoint(path, 240f, 70f, 120f),
                (Vector4)BasinPoint(path, 130f, 200f, 175f), (Vector4)BasinPoint(path, 290f, 200f, 210f),
                (Vector4)path.World(345f, 64f, 78f),
            };
            float[] radius = { 18f, 15f, 22f, 26f, 12f };
            for (int i = 0; i < pillars.Length; i++)
            {
                var center = new Vector3(pillars[i].x, 0f, pillars[i].z);
                float top = pillars[i].y;
                stone.Append(Pillar(rng, center, valley, top, radius[i], false), Matrix4x4.identity, LookTestBuildContext.Open);
                Tufts(tops, rng, new Vector3(center.x, top + radius[i] * 0.15f, center.z), radius[i] * 1.05f, 10);
                if (i == pillars.Length - 1)
                {
                    Vector3 toFord = fordViewer - center;
                    toFord.y = 0f;
                    toFord.Normalize();
                    Vector3 lip = center + toFord * radius[i] * 0.95f + Vector3.up * (top - 22f);
                    Fall(falls, lip, valley, 5f, fordViewer, 1.5f);
                    Billows(mist, rng, new Vector3(lip.x, valley + 6f, lip.z), 5, 8f, new Vector2(20f, 34f), new Vector2(12f, 20f));
                }
            }
        }

        private static void Tufts(LookTestMeshAccumulator tops, IRandom rng, Vector3 center, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = rng.NextFloat(0f, 6.283f);
                float r = radius * Mathf.Sqrt(rng.NextFloat());
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, rng.NextFloat(-1f, 1.5f), Mathf.Sin(a) * r);
                float size = rng.NextFloat(3.5f, 7f);
                tops.Append(LookTestMeshFactory.Boulder(rng, 1f, 1), Matrix4x4.TRS(p, Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f), new Vector3(size, size * 0.8f, size)), LookTestBuildContext.Open);
            }
        }

        // ---------------------------------------------------------------- Backdrop (L5, rides with the run)

        /// <summary>
        /// The far range, valley floor, valley forest and valley mist, authored relative to the runner at s = 0
        /// (forward +z, right +x). <see cref="LookTestWorldView"/> moves the backdrop root with the run.
        /// </summary>
        public static void Backdrop(LookTestBuildContext ctx)
        {
            LookTestConfigAsset c = ctx.Config;
            var rng = new Pcg32Random((ulong)(uint)c.Seed, 88UL);
            LookTestBatchSet b = ctx.Batches;
            LookTestMeshAccumulator range = b.Get(LookTestBatchSet.Backdrop, "L5", "Range", ctx.Rootstone, false, LookTestBatchSet.Group.Ground);
            LookTestMeshAccumulator valley = b.Get(LookTestBatchSet.Backdrop, "L4", "Valley forest", ctx.FarCanopy, false, LookTestBatchSet.Group.Ground);
            LookTestMeshAccumulator falls = b.Get(LookTestBatchSet.Backdrop, "W", "Far falls", ctx.Waterfall, false, LookTestBatchSet.Group.Water);
            LookTestMeshAccumulator mist = b.Get(LookTestBatchSet.Backdrop, "W", "Valley mist", ctx.MistCard, false, LookTestBatchSet.Group.Water);
            Vector3 anchor = ctx.Layout.Path.Position(0.0);
            float floor = c.ValleyFloorY - 2f;

            var tops = new List<Vector4>();
            for (int i = 0; i < 20; i++)
            {
                float f = rng.NextFloat(420f, 1500f);
                float r = rng.NextFloat(-260f, 1150f);
                if (r < 60f && f < 700f)
                {
                    r += 300f;
                }

                float height = rng.NextFloat(130f, 360f) * Mathf.Lerp(0.8f, 1.15f, f / 1500f);
                float radius = rng.NextFloat(18f, 46f);
                Vector3 center = anchor + new Vector3(r, 0f, f);
                range.Append(Pillar(rng, center, floor, height, radius, rng.Chance(0.3f), 12, 18f), Matrix4x4.identity, LookTestBuildContext.Open);
                tops.Add(new Vector4(center.x, height, center.z, radius));
                if (rng.Chance(0.6f))
                {
                    Tufts(valley, rng, new Vector3(center.x, height + radius * 0.1f, center.z), radius, 6);
                }
            }

            // Arches between near neighbours, and thin falls from pillar sides toward the viewer.
            for (int i = 0; i < tops.Count; i++)
            {
                for (int j = i + 1; j < tops.Count; j++)
                {
                    var a = new Vector3(tops[i].x, 0f, tops[i].z);
                    var bb = new Vector3(tops[j].x, 0f, tops[j].z);
                    float dist = Vector3.Distance(a, bb);
                    if (dist < 90f || dist > 230f || !rng.Chance(0.25f))
                    {
                        continue;
                    }

                    float y = Mathf.Min(tops[i].y, tops[j].y) * rng.NextFloat(0.55f, 0.8f);
                    List<Vector3> arch = Bezier(a + Vector3.up * y * 0.8f, (a + bb) * 0.5f + Vector3.up * (y + dist * 0.25f), bb + Vector3.up * y * 0.75f, 16);
                    range.Append(LookTestMeshFactory.Tube("FarArch", arch, Taper(arch.Count, Mathf.Min(tops[i].w, 20f) * 0.45f, Mathf.Min(tops[j].w, 20f) * 0.45f), 12, 12f, 5, 0.4f, 0.03f, i), Matrix4x4.identity, LookTestBuildContext.Open);
                }
            }

            for (int i = 0; i < tops.Count; i += 3)
            {
                var center = new Vector3(tops[i].x, 0f, tops[i].z);
                Vector3 toViewer = (anchor - center);
                toViewer.y = 0f;
                toViewer.Normalize();
                Vector3 top = center + toViewer * tops[i].w * 0.95f + Vector3.up * tops[i].y * rng.NextFloat(0.55f, 0.85f);
                Fall(falls, top, floor, rng.NextFloat(6f, 12f), anchor, 2f);
            }

            // Valley floor and forest.
            Vector3 floorCenter = anchor + new Vector3(500f, floor + 1f, 700f);
            valley.Append(LookTestMeshFactory.Pool(2600f, 1f, "ValleyFloor"), Matrix4x4.Translate(floorCenter), (w, l, src) => new Color(0f, 0f, 0f, 1f));
            for (int i = 0; i < 120; i++)
            {
                float f = rng.NextFloat(80f, 1500f);
                float r = rng.NextFloat(40f, 1300f);
                float size = rng.NextFloat(10f, 26f);
                Vector3 p = anchor + new Vector3(r, floor + rng.NextFloat(-1f, 4f), f);
                valley.Append(LookTestMeshFactory.Boulder(rng, 1f, 0), Matrix4x4.TRS(p, Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f), new Vector3(size, size * 0.7f, size)), LookTestBuildContext.Open);
            }

            for (int i = 0; i < 26; i++)
            {
                Vector3 p = anchor + new Vector3(rng.NextFloat(0f, 1100f), floor + rng.NextFloat(6f, 22f), rng.NextFloat(180f, 1400f));
                mist.AppendCard(p, Vector3.up, rng.NextFloat(140f, 300f), rng.NextFloat(30f, 60f), rng.NextFloat(0f, 10f));
            }
        }

        // ---------------------------------------------------------------- Shafts and ground mist

        private static void Shafts(LookTestBuildContext ctx, IRandom rng)
        {
            Vector3 toSun = -ctx.Config.SunLightDirection.normalized;
            Vector3[] shafts = ctx.Config.LightShafts;
            for (int i = 0; i < shafts.Length; i++)
            {
                float s = shafts[i].x;
                float d = shafts[i].y;
                float length = shafts[i].z;
                Vector3 ground = ctx.Layout.Path.World(s, d, ctx.Layout.GroundY(s, d));
                Vector3 center = ground + toSun * length * 0.5f;
                LookTestMeshAccumulator target = ctx.Batches.Get(ctx.SegmentOf(s), "W", "Shafts", ctx.LightShaft, false, LookTestBatchSet.Group.Water);
                target.AppendCard(center, -toSun, rng.NextFloat(2.2f, 4.2f), length, rng.NextFloat(0f, 10f));
                if (rng.Chance(0.5f))
                {
                    target.AppendCard(center + ctx.Layout.Path.Forward(s) * rng.NextFloat(2f, 4f), -toSun, rng.NextFloat(1.2f, 2.2f), length * 0.9f, rng.NextFloat(0f, 10f));
                }
            }
        }

        private static void GroundMist(LookTestBuildContext ctx, IRandom rng)
        {
            LookTestStretchLayout layout = ctx.Layout;
            LookTestConfigAsset c = ctx.Config;

            // Low mist in the hollow under the roots and over the ford.
            for (int i = 0; i < 7; i++)
            {
                float s = rng.NextFloat(4f, 40f);
                float d = (rng.Chance(0.5f) ? -1f : 1f) * rng.NextFloat(5f, 16f);
                Vector3 p = layout.Path.World(s, d, layout.GroundY(s, d) + 1f);
                ctx.Batches.Get(ctx.SegmentOf(s), "W", "Mist", ctx.MistCard, false, LookTestBatchSet.Group.Water)
                    .AppendCard(p, Vector3.up, rng.NextFloat(7f, 12f), rng.NextFloat(2.2f, 3.5f), rng.NextFloat(0f, 10f));
            }

            for (int i = 0; i < 8; i++)
            {
                float s = c.RiverCrossSM + rng.NextFloat(-14f, 22f);
                if (!layout.RiverActive(s))
                {
                    continue;
                }

                float d = layout.RiverD(s) + rng.NextFloat(-3f, 3f);
                if (Mathf.Abs(d) < c.RunnableHalfWidthM + 2f)
                {
                    continue;
                }

                Vector3 p = layout.Path.World(s, d, layout.WaterY(layout.RiverD(s)) + 0.9f);
                ctx.Batches.Get(ctx.SegmentOf(s), "W", "Mist", ctx.MistCard, false, LookTestBatchSet.Group.Water)
                    .AppendCard(p, Vector3.up, rng.NextFloat(8f, 14f), rng.NextFloat(2f, 3.2f), rng.NextFloat(0f, 10f));
            }
        }
    }
}
