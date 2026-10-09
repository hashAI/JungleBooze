using System.Collections.Generic;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Fills one 25 m segment of the look test (ENVIRONMENT_STRATEGY 4.2 layers L0-L3) into the merge plan:
    /// ground and river, path-edge roots and stones (L0), foliage walls of leaf clumps and CC0 verge plants (L1),
    /// large framing leaf masses near the trail, trunks and vines (L2) and the canopy roof (L3, no shadows; the
    /// dappled light comes from the canopy light in the shaders). Density follows the enclosure curve, so open
    /// stretches are open. Seeded per segment: the same config always gives the same segment.
    /// </summary>
    public static class LookTestScatter
    {
        private const string Walls = "Walls";

        public static void BuildSegment(LookTestBuildContext ctx, int segment)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            float s0 = segment * ctx.SegmentLength;
            float s1 = s0 + ctx.SegmentLength;
            var rng = new Pcg32Random((ulong)(uint)c.Seed, 1000UL + (ulong)segment);
            LookTestBatchSet b = ctx.Batches;

            b.Get(segment, "L0", "Ground", ctx.Ground, false, LookTestBatchSet.Group.Ground)
                .Append(LookTestMeshFactory.GroundStrip(layout, s0, s1, "Ground"), Matrix4x4.identity, null);

            Mesh river = LookTestMeshFactory.RiverStrip(layout, s0, s1, "River");
            if (river != null)
            {
                b.Get(segment, "W", "River", ctx.River, false, LookTestBatchSet.Group.Water).Append(river, Matrix4x4.identity, null);
            }

            LookTestMeshAccumulator wood = b.Get(segment, "L2", "Wood", ctx.Bark, true, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator walls = b.Get(segment, "L1", Walls, ctx.Leaves, true, LookTestBatchSet.Group.Plants);
            LookTestMeshAccumulator roof = b.Get(segment, "L3", "Canopy", ctx.Leaves, false, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator rocks = b.Get(segment, "L0", "Stones", ctx.Boulder, true, LookTestBatchSet.Group.Rocks);

            CanopyTrees(ctx, rng, s0, s1, wood, roof);
            UnderstoryTrees(ctx, rng, s0, s1, wood, walls);
            WallClumps(ctx, rng, s0, s1, walls);
            FrameClumps(ctx, rng, s0, s1, walls);
            Roof(ctx, rng, s0, s1, roof);
            Vines(ctx, rng, s0, s1, wood);
            EdgeRoots(ctx, rng, s0, s1, wood);
            Stones(ctx, rng, s0, s1, rocks);
            VergePlants(ctx, rng, segment, s0, s1);
            ForestWall(ctx, rng, segment, s0, s1);
        }

        /// <summary>
        /// L4 stand-in: low-poly canopy masses on the far hills and the far plateau, so the land beyond the walls
        /// reads as forest (impostors replace them in the art step).
        /// </summary>
        private static void ForestWall(LookTestBuildContext ctx, IRandom rng, int segment, float s0, float s1)
        {
            LookTestStretchLayout layout = ctx.Layout;
            LookTestMeshAccumulator wall = ctx.Batches.Get(segment, "L4", "Forest wall", ctx.FarCanopy, false, LookTestBatchSet.Group.Trees);
            for (int i = 0; i < 26; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d;
                if (rng.Chance(0.7f))
                {
                    d = -rng.NextFloat(32f, ctx.Config.StripHalfWidthM - 1f);
                }
                else
                {
                    float edge = layout.RightEdge(s);
                    if (edge < 36f)
                    {
                        continue;
                    }

                    d = rng.NextFloat(32f, edge - 3f);
                }

                if (layout.RiverDistance(s, d) < ctx.Config.RiverHalfWidthM * 1.6f)
                {
                    continue;
                }

                float width = rng.NextFloat(7f, 13f);
                float height = rng.NextFloat(10f, 20f);
                Vector3 p = layout.Path.World(s, d, layout.GroundY(s, d) + height * 0.55f);
                wall.Append(LookTestMeshFactory.Boulder(rng, 1f, 0), Matrix4x4.TRS(p, Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f), new Vector3(width, height, width)), ctx.Solid(s, d));
            }
        }

        /// <summary>True where nothing may stand: the runnable width (+ clearance), the river, or off the plateau.</summary>
        public static bool Blocked(LookTestStretchLayout layout, float s, float d, float clearance)
        {
            LookTestConfigAsset c = layout.Config;
            if (Mathf.Abs(d) < c.RunnableHalfWidthM + clearance)
            {
                return true;
            }

            if (layout.RiverDistance(s, d) < c.RiverHalfWidthM * 1.25f + clearance)
            {
                return true;
            }

            return d > 0f && d > layout.RightEdge(s) - 1f - clearance * 0.5f;
        }

        /// <summary>
        /// True inside a reveal (enclosure below 0.15, 22 m right / 40 m left of the trail, up to 30 m on): no tall
        /// trees there, so vistas stay open (ENVIRONMENT_STRATEGY 4.1, the reveal part of the cadence).
        /// </summary>
        public static bool InClearing(LookTestStretchLayout layout, float s, float d)
        {
            // Wider on the left: after the basin the trail turns left, so the left forest would stand in the vista.
            if (d > 22f || d < -40f)
            {
                return false;
            }

            for (float back = 0f; back <= 30f; back += 5f)
            {
                if (layout.Enclosure(s - back) < 0.15f)
                {
                    return true;
                }
            }

            return false;
        }

        private static float WallD(IRandom rng, float near, float far, float bias)
        {
            float side = rng.Chance(0.5f) ? -1f : 1f;
            return side * (near + (far - near) * Mathf.Pow(rng.NextFloat(), bias));
        }

        private static void CanopyTrees(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator wood, LookTestMeshAccumulator roof)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.CanopyTreesPerSegment * 3 && i < 200; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d = WallD(rng, c.RunnableHalfWidthM + 5f, 34f, 1.3f);
                if (Blocked(layout, s, d, 3f) || InClearing(layout, s, d) || rng.NextFloat() > 0.25f + 0.75f * Mathf.Max(layout.Enclosure(s), LookTestMath.Smooth(10f, 24f, Mathf.Abs(d))))
                {
                    continue;
                }

                float height = rng.NextFloat(24f, 36f);
                float radius = rng.NextFloat(0.5f, 1.25f);
                Vector3 foot = layout.Path.World(s, d, layout.GroundY(s, d) - 0.3f);
                Quaternion turn = Quaternion.Euler(rng.NextFloat(-3f, 3f), rng.NextFloat(0f, 360f), rng.NextFloat(-3f, 3f));
                Matrix4x4 m = Matrix4x4.TRS(foot, turn, Vector3.one);
                wood.Append(LookTestMeshFactory.Trunk(rng, height, radius, c.BarkTileM, 10, 12), m, ctx.Solid(s, d));
                Mesh crown = LookTestMeshFactory.Crown(rng, height, 44, 4.6f);
                roof.Append(crown, m, ctx.Foliage(s, d, foot.y + height * 0.5f, height * 0.5f, 0.6f));
            }
        }

        private static void UnderstoryTrees(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator wood, LookTestMeshAccumulator walls)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.UnderstoryTreesPerSegment; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d = WallD(rng, c.RunnableHalfWidthM + 2.5f, 20f, 1.4f);
                if (Blocked(layout, s, d, 2f) || InClearing(layout, s, d))
                {
                    continue;
                }

                float height = rng.NextFloat(6f, 12f);
                Vector3 foot = layout.Path.World(s, d, layout.GroundY(s, d) - 0.2f);
                // Lean over the trail so crowns frame the run instead of lining it.
                float lean = Mathf.Abs(d) < 9f ? rng.NextFloat(5f, 14f) : rng.NextFloat(-3f, 3f);
                Vector3 inward = -layout.Path.Right(s) * Mathf.Sign(d);
                Quaternion turn = Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, inward)) * Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f);
                Matrix4x4 m = Matrix4x4.TRS(foot, turn, Vector3.one);
                wood.Append(LookTestMeshFactory.Trunk(rng, height, rng.NextFloat(0.12f, 0.22f), c.BarkTileM, 8, 10), m, ctx.Solid(s, d));
                walls.Append(LookTestMeshFactory.Crown(rng, height, 22, 2.8f), m, ctx.Foliage(s, d, foot.y + height * 0.4f, height * 0.6f, 0.5f));
            }
        }

        private static void WallClumps(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator walls)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.WallClumpsPerSegment; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d = WallD(rng, c.RunnableHalfWidthM + 1.4f, 26f, 1.7f);
                if (Blocked(layout, s, d, 1.2f))
                {
                    continue;
                }

                // Open stretches keep only the near verge and the far forest.
                float ad = Mathf.Abs(d);
                if (rng.NextFloat() > 0.35f + 0.65f * Mathf.Max(layout.Enclosure(s), LookTestMath.Smooth(12f, 22f, ad)))
                {
                    continue;
                }

                float far = LookTestMath.Smooth(c.RunnableHalfWidthM + 2f, 18f, ad);
                float radius = Mathf.Lerp(0.8f, 2.6f, far) * rng.NextFloat(0.75f, 1.25f);
                float height = Mathf.Lerp(1.1f, 3.8f, far) * rng.NextFloat(0.75f, 1.3f);
                int cards = Mathf.RoundToInt(Mathf.Lerp(12f, 24f, rng.NextFloat()));
                Vector3 foot = layout.Path.World(s, d, layout.GroundY(s, d) - 0.15f);
                Mesh clump = LookTestMeshFactory.Clump(rng, radius, height, cards, Mathf.Lerp(1.1f, 2.2f, far), 0f, "Clump");
                walls.Append(clump, Matrix4x4.TRS(foot, Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f), Vector3.one), ctx.Foliage(s, d, foot.y, height, 0.35f));
            }
        }

        /// <summary>Big leaf masses right at the verge: they overlap the lower corners of the frame (strategy 4.1).</summary>
        private static void FrameClumps(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator walls)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            int count = c.FrameClumpsPerSegment;
            for (int i = 0; i < count; i++)
            {
                float s = s0 + (i + rng.NextFloat(0.2f, 0.8f)) * ctx.SegmentLength / count;
                float d = (i % 2 == 0 ? -1f : 1f) * (c.RunnableHalfWidthM + rng.NextFloat(1.1f, 2.4f));
                if (Blocked(layout, s, d, 0.8f))
                {
                    continue;
                }

                float radius = rng.NextFloat(1.6f, 2.6f);
                float height = rng.NextFloat(1.8f, 3.0f);
                Vector3 foot = layout.Path.World(s, d, layout.GroundY(s, d) - 0.2f);
                Mesh clump = LookTestMeshFactory.Clump(rng, radius, height, 34, 1.7f, 0f, "Frame");
                walls.Append(clump, Matrix4x4.TRS(foot, Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f), Vector3.one), ctx.Foliage(s, d, foot.y, height, 0.4f));
            }
        }

        private static void Roof(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator roof)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.RoofClumpsPerSegment * 2; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d = rng.NextFloat(-24f, 24f);
                if (rng.NextFloat() > layout.CanopyCover(s, d, 0f) || (d > 0f && d > layout.RightEdge(s) - 2f))
                {
                    continue;
                }

                float height = Mathf.Lerp(c.CanopyRoofHeightM.x, c.CanopyRoofHeightM.y, rng.NextFloat());
                // Lower over the walls than over the trail, so the roof reads as an arch.
                height -= 3f * (1f - LookTestMath.Smooth(4f, 14f, Mathf.Abs(d)));
                float radius = rng.NextFloat(4f, 7.5f);
                Vector3 center = layout.Path.World(s, d, layout.TrailHeight(s) + height);
                Mesh clump = LookTestMeshFactory.Clump(rng, radius, rng.NextFloat(2.5f, 4f), 24, 3.6f, 0.75f, "Roof");
                roof.Append(clump, Matrix4x4.TRS(center, Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f), Vector3.one), ctx.Foliage(s, d, center.y - 3f, 6f, 0.25f));
            }
        }

        private static void Vines(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator wood)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.VinesPerSegment; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d = WallD(rng, c.RunnableHalfWidthM + 0.9f, 14f, 1.2f);
                if (rng.NextFloat() > layout.Enclosure(s) || Blocked(layout, s, d, 0.6f))
                {
                    continue;
                }

                float top = layout.TrailHeight(s) + Mathf.Lerp(c.CanopyRoofHeightM.x, c.CanopyRoofHeightM.y, rng.NextFloat()) - 2f;
                float bottom = layout.GroundY(s, d) + rng.NextFloat(2.2f, 7f);
                Vector3 a = layout.Path.World(s, d, top);
                Vector3 sway = new Vector3(rng.NextFloat(-1f, 1f), 0f, rng.NextFloat(-1f, 1f)) * 0.8f;
                var spine = new List<Vector3>();
                var radii = new List<float>();
                const int points = 9;
                float radius = rng.NextFloat(0.04f, 0.1f);
                for (int k = 0; k < points; k++)
                {
                    float t = (float)k / (points - 1);
                    spine.Add(a + Vector3.down * (top - bottom) * t + sway * Mathf.Sin(t * Mathf.PI) + new Vector3(0.15f * Mathf.Sin(t * 9f + i), 0f, 0f));
                    radii.Add(radius * Mathf.Lerp(1.2f, 0.6f, t));
                }

                wood.Append(LookTestMeshFactory.Tube("Vine", spine, radii, 5, 0.5f, 0, 0f, 0f, i), Matrix4x4.identity, ctx.Solid(s, d));
            }
        }

        /// <summary>Roots creeping over the runnable edges (strategy 4.1: 4-5 m visible trail inside 7 m runnable).</summary>
        private static void EdgeRoots(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator wood)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.EdgeRootsPerSegment; i++)
            {
                float s = rng.NextFloat(s0, s1 - 4f);
                float side = rng.Chance(0.5f) ? -1f : 1f;
                if (layout.RiverDistance(s, side * c.RunnableHalfWidthM) < c.RiverHalfWidthM * 1.6f)
                {
                    continue;
                }

                float run = c.RunnableHalfWidthM;
                float length = rng.NextFloat(3f, 6f);
                var spine = new List<Vector3>();
                var radii = new List<float>();
                float radius = rng.NextFloat(0.12f, 0.28f);
                const int points = 8;
                for (int k = 0; k < points; k++)
                {
                    float t = (float)k / (points - 1);
                    float ss = s + length * t;
                    float d = side * Mathf.Lerp(run - rng.NextFloat(0.2f, 0.9f) * (1f - t), run + 2.8f, t * t);
                    float y = layout.GroundY(ss, d) + radius * 0.25f + 0.05f * Mathf.Sin(t * 7f);
                    spine.Add(layout.Path.World(ss, d, y));
                    radii.Add(radius * Mathf.Lerp(0.55f, 1.1f, t));
                }

                wood.Append(LookTestMeshFactory.Tube("Root", spine, radii, 6, 0.7f, 0, 0f, 0f, i), Matrix4x4.identity, ctx.Solid(s, side * run));
            }
        }

        private static void Stones(LookTestBuildContext ctx, IRandom rng, float s0, float s1, LookTestMeshAccumulator rocks)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            for (int i = 0; i < c.BouldersPerSegment; i++)
            {
                float s = rng.NextFloat(s0, s1);
                float d = WallD(rng, c.RunnableHalfWidthM + 0.4f, c.RunnableHalfWidthM + 6f, 1.5f);
                float size = rng.NextFloat(0.3f, 1.1f);
                if (layout.RiverDistance(s, d) < c.RiverHalfWidthM * 1.5f)
                {
                    size *= 1.4f;
                }
                else if (Blocked(layout, s, d, 0.3f))
                {
                    continue;
                }

                AppendBoulder(ctx, rng, rocks, s, d, size);
            }

            // Stepping stones and river boulders at the ford.
            if (layout.RiverActive(s0) || layout.RiverActive(s1))
            {
                for (int i = 0; i < 14; i++)
                {
                    float s = rng.NextFloat(s0, s1);
                    if (!layout.RiverActive(s))
                    {
                        continue;
                    }

                    float d = layout.RiverD(s) + rng.NextFloat(-1.2f, 1.2f) * c.RiverHalfWidthM;
                    if (Mathf.Abs(d) < c.RunnableHalfWidthM + 0.3f || (d > 0f && d > layout.RightEdge(s) - 2f))
                    {
                        continue;
                    }

                    AppendBoulder(ctx, rng, rocks, s, d, rng.NextFloat(0.4f, 1.4f));
                }
            }
        }

        public static void AppendBoulder(LookTestBuildContext ctx, IRandom rng, LookTestMeshAccumulator rocks, float s, float d, float size)
        {
            LookTestStretchLayout layout = ctx.Layout;
            Vector3 p = layout.Path.World(s, d, layout.GroundY(s, d) - size * 0.25f);
            Quaternion r = Quaternion.Euler(rng.NextFloat(-8f, 8f), rng.NextFloat(0f, 360f), rng.NextFloat(-8f, 8f));
            rocks.Append(LookTestMeshFactory.Boulder(rng, 1f), Matrix4x4.TRS(p, r, Vector3.one * size), ctx.Solid(s, d));
        }

        /// <summary>CC0 ferns and plants on the verges (the near detail layer; few, because the scans are dense).</summary>
        private static void VergePlants(LookTestBuildContext ctx, IRandom rng, int segment, float s0, float s1)
        {
            LookTestConfigAsset c = ctx.Config;
            LookTestStretchLayout layout = ctx.Layout;
            if (ctx.Ferns.Count > 0)
            {
                for (int i = 0; i < c.FernsPerSegment; i++)
                {
                    float s = rng.NextFloat(s0, s1);
                    float d = WallD(rng, c.RunnableHalfWidthM + 0.25f, c.RunnableHalfWidthM + 5f, 1.6f);
                    if (Blocked(layout, s, d, 0.2f))
                    {
                        continue;
                    }

                    LookTestBuildContext.ModelVariant v = ctx.Ferns[rng.NextInt(0, ctx.Ferns.Count)];
                    float height = rng.NextFloat(0.9f, 1.8f);
                    Vector3 foot = layout.Path.World(s, d, layout.GroundY(s, d) - 0.05f);
                    LookTestMeshAccumulator target = ctx.Batches.Get(segment, "L0", "Ferns", v.Material, false, LookTestBatchSet.Group.Detail);
                    ctx.AppendModel(target, v, foot, rng.NextFloat(0f, 360f), height, ctx.Foliage(s, d, foot.y, height, 1f));
                }
            }

            if (ctx.Plants.Count > 0)
            {
                for (int i = 0; i < c.PlantsPerSegment; i++)
                {
                    float s = rng.NextFloat(s0, s1);
                    float d = WallD(rng, c.RunnableHalfWidthM + 0.6f, c.RunnableHalfWidthM + 6f, 1.4f);
                    if (Blocked(layout, s, d, 0.4f))
                    {
                        continue;
                    }

                    LookTestBuildContext.ModelVariant v = ctx.Plants[rng.NextInt(0, ctx.Plants.Count)];
                    float height = rng.NextFloat(0.9f, 2.0f);
                    Vector3 foot = layout.Path.World(s, d, layout.GroundY(s, d) - 0.05f);
                    LookTestMeshAccumulator target = ctx.Batches.Get(segment, "L0", "Plants", v.Material, false, LookTestBatchSet.Group.Detail);
                    ctx.AppendModel(target, v, foot, rng.NextFloat(0f, 360f), height, ctx.Foliage(s, d, foot.y, height, 1f));
                }
            }
        }
    }
}
