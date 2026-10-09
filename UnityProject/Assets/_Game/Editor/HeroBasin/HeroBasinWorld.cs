using System.Collections.Generic;
using JungleBooze.App.HeroBasin;
using JungleBooze.Core;
using JungleBooze.Editor.LookTest;
using JungleBooze.Editor.Scenery;
using UnityEngine;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// The 3D content of the hero basin, appended to the merge batches of segment 0: terrain (ledge promontory,
    /// basin floor, side hills), turquoise basin water and terraced rock islands with pools and spilling falls, the
    /// hero arch (RS_HeroArch, or a braided stand-in), the authored falls, forest masses, foreground plants from the
    /// atlases, light shafts and mist. Seeded; same config = same scene.
    /// </summary>
    internal sealed class HeroBasinWorld
    {
        private const int Seg = 0;

        private readonly LookTestBuildContext _ctx;
        private readonly HeroBasinConfigAsset _h;
        private readonly HeroBasinBuilder.PlantMaterials _plants;
        private readonly IRandom _rng;
        private readonly Vector3 _eye;
        private Vector3? _mouth;

        // Cascade feet that land on the basin water: (x, z, width). Foam is painted around them.
        private readonly List<Vector3> _footFoam = new List<Vector3>();

        public HeroBasinWorld(LookTestBuildContext ctx, HeroBasinConfigAsset h, HeroBasinBuilder.PlantMaterials plants, IRandom rng, Vector3 eye)
        {
            _ctx = ctx;
            _h = h;
            _plants = plants;
            _rng = rng;
            _eye = eye;
        }

        private LookTestBatchSet B => _ctx.Batches;

        public void Build()
        {
            // Falls first: rock near them is painted wet.
            var rock = new List<System.Action>();
            Arch(rock);
            Falls(rock);
            if (_ctx.Kit.Of(EnvironmentRole.Travertine).Count > 0)
            {
                Travertines(rock);
            }
            else
            {
                Terraces(rock);
            }

            KitPillars(rock);
            for (int i = 0; i < rock.Count; i++)
            {
                rock[i]();
            }

            Terrain();
            Water();
            Ledge();
            Forest();
            Foreground();
            Shafts();
            Mist();
        }

        // ---------------------------------------------------------------- Terrain

        /// <summary>Ground height at (x, z): ledge promontory near the origin, basin floor, side hills, rising far side.</summary>
        public float Height(float x, float z)
        {
            HeroBasinConfigAsset h = _h;
            float floor = h.BasinFloorY;
            Vector3 l = h.LeftHills;
            Vector3 r = h.RightHills;
            float y = floor;
            float left = LookTestMath.Smooth(l.x, l.y, x);
            float right = LookTestMath.Smooth(r.x, r.y, x);
            float bumps = 3f * Mathf.Sin(x * 0.045f + z * 0.03f) + 2f * Mathf.Sin(z * 0.07f - x * 0.02f) + 1.2f * Mathf.Sin(x * 0.13f + z * 0.11f);
            y += (l.z - floor) * left + (r.z - floor) * right + bumps * Mathf.Max(left, right);
            // Beyond the arch the land falls away to the valley the painted layers show.
            y = Mathf.Lerp(y, floor - 25f, LookTestMath.Smooth(280f, 340f, z));

            // Ledge promontory and the near left bank.
            Vector3 ledge = h.Ledge;
            // The promontory drops steeply a few meters in front of Pista and to her right; the left bank rises.
            float bank = LookTestMath.Smooth(-3f, -ledge.x - 14f, x);
            float front = ledge.y + 9f * bank;
            float near = 1f - LookTestMath.Smooth(front, front + 7f, z);
            float nearY = ledge.z - 0.6f + 0.15f * Mathf.Sin(x * 1.3f) * Mathf.Sin(z * 1.1f);
            nearY += 4f * bank;
            nearY = Mathf.Lerp(nearY, floor, LookTestMath.Smooth(ledge.x - 2f, ledge.x + 6f, x));
            y = Mathf.Lerp(y, Mathf.Max(y, nearY), near);
            return y;
        }

        private void Terrain()
        {
            var xs = new List<float>();
            for (float x = -260f; x <= 280f;)
            {
                xs.Add(x);
                float ax = Mathf.Abs(x - 1f);
                x += ax < 22f ? 1f : ax < 80f ? 3f : 7f;
            }

            var zs = new List<float>();
            for (float z = -30f; z <= 380f;)
            {
                zs.Add(z);
                z += z < 30f ? 1f : z < 120f ? 3f : 7f;
            }

            var positions = new Vector3[xs.Count, zs.Count];
            var frames = new LookTestMeshFactory.Frame[xs.Count, zs.Count];
            var uvs = new Vector2[xs.Count, zs.Count];
            var colors = new Color[xs.Count, zs.Count];
            float water = _h.BasinWaterY;
            for (int i = 0; i < xs.Count; i++)
            {
                for (int j = 0; j < zs.Count; j++)
                {
                    float x = xs[i];
                    float z = zs[j];
                    float y = Height(x, z);
                    positions[i, j] = new Vector3(x, y, z);
                    float dx = Height(x + 0.5f, z) - Height(x - 0.5f, z);
                    float dz = Height(x, z + 0.5f) - Height(x, z - 0.5f);
                    Vector3 n = new Vector3(-dx, 1f, -dz).normalized;
                    frames[i, j] = new LookTestMeshFactory.Frame { Normal = n, Tangent = Vector3.right, Bitangent = Vector3.forward };
                    uvs[i, j] = new Vector2(x, z);
                    float shore = 1f - LookTestMath.Smooth(water + 0.5f, water + 3f, y);
                    float steep = LookTestMath.Smooth(0.75f, 0.45f, n.y);
                    float soil = 0.35f * (1f - LookTestMath.Smooth(2f, 5f, Mathf.Abs(x))) * (1f - LookTestMath.Smooth(-3f, 6f, z));
                    colors[i, j] = new Color(soil, Mathf.Clamp01(Mathf.Max(shore, steep * 0.8f)), 0f, Mathf.Lerp(0.75f, 1f, n.y));
                }
            }

            Mesh terrain = LookTestMeshFactory.Grid("HeroTerrain", positions, frames, uvs, colors);
            B.Get(Seg, "L0", "Terrain", _ctx.Ground, true, LookTestBatchSet.Group.Ground).Append(terrain, Matrix4x4.identity, null);
        }

        // ---------------------------------------------------------------- Water and terraces

        private void Water()
        {
            // A grid over the basin (fine near the ledge) so depth, shore foam and edges follow the terrain.
            float water = _h.BasinWaterY;
            var xs = new List<float>();
            for (float x = -140f; x <= 170f;)
            {
                xs.Add(x);
                x += Mathf.Abs(x - 10f) < 30f ? 1.5f : 4f;
            }

            var zs = new List<float>();
            for (float z = 4f; z <= 300f;)
            {
                zs.Add(z);
                z += z < 60f ? 1.5f : 4f;
            }

            var positions = new Vector3[xs.Count, zs.Count];
            var frames = new LookTestMeshFactory.Frame[xs.Count, zs.Count];
            var uvs = new Vector2[xs.Count, zs.Count];
            var colors = new Color[xs.Count, zs.Count];
            for (int i = 0; i < xs.Count; i++)
            {
                for (int j = 0; j < zs.Count; j++)
                {
                    float x = xs[i];
                    float z = zs[j];
                    float depth = water - Height(x, z);
                    positions[i, j] = new Vector3(x, water, z);
                    frames[i, j] = new LookTestMeshFactory.Frame { Normal = Vector3.up, Tangent = Vector3.right, Bitangent = Vector3.forward };
                    uvs[i, j] = new Vector2(x, z);
                    float foam = Mathf.Max(1f - LookTestMath.Smooth(0.05f, 0.7f, depth), TerraceFoam(x, z));
                    colors[i, j] = new Color(foam, Mathf.Clamp01(depth / 3f), 0f, LookTestMath.Smooth(-0.4f, 0.15f, depth));
                }
            }

            Mesh surface = LookTestMeshFactory.Grid("BasinWater", positions, frames, uvs, colors);
            B.Get(Seg, "W", "Pools", _ctx.Pool, false, LookTestBatchSet.Group.Water).Append(surface, Matrix4x4.identity, null);
        }

        /// <summary>
        /// Aerated water below the terraces (keyframe: white water at every step, foam lines drifting toward the
        /// viewer): a ring of foam just outside each terrace rim on the camera side, and broken streaks that trail
        /// downstream (toward the camera) and fade with distance. Vertex color R of the basin water; the Water shader
        /// thresholds it with the lacy foam texture.
        /// </summary>
        private float TerraceFoam(float x, float z)
        {
            Vector4[] terraces = _h.Terraces;
            float foam = 0f;
            for (int i = 0; i < terraces.Length; i++)
            {
                Vector4 t = terraces[i];
                float dx = x - t.x;
                float dz = z - t.y;
                float r = Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(1f, t.w);
                // Only the front (downstream) half: the falls spill toward the camera.
                float front = LookTestMath.Smooth(0.1f, -0.5f, dz / Mathf.Max(1f, t.w * r));
                float ring = LookTestMath.Smooth(0.9f, 1.15f, r) * (1f - LookTestMath.Smooth(1.3f, 1.8f, r));
                float lines = 0.5f + 0.5f * Mathf.Sin(x * 0.45f + 1.7f * Mathf.Sin(z * 0.08f + i) + i * 2.1f);
                float trail = (1f - LookTestMath.Smooth(1.1f, 2.6f, r)) * lines * lines;
                foam = Mathf.Max(foam, front * Mathf.Max(ring * 0.9f, trail * 0.55f));
            }

            for (int i = 0; i < _footFoam.Count; i++)
            {
                Vector3 f = _footFoam[i];
                float dx = x - f.x;
                float dz = z - f.y;
                float r = Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(1f, f.z);
                // White water at the foot, broken streaks drifting toward the camera (-z).
                float pool = 1f - LookTestMath.Smooth(0.6f, 1.4f, r);
                float lines = 0.5f + 0.5f * Mathf.Sin(x * 0.55f + 1.9f * Mathf.Sin(z * 0.11f + i) + i * 1.3f);
                float trail = (1f - LookTestMath.Smooth(1f, 4f, r)) * LookTestMath.Smooth(0.5f, -2f, dz / Mathf.Max(1f, f.z)) * lines * lines;
                foam = Mathf.Max(foam, Mathf.Max(pool, trail * 0.7f));
            }

            return foam;
        }

        /// <summary>
        /// The travertine tiers (RS_TravertineTiers): turned so their cascades face the camera, apron on the basin
        /// water, pool surfaces from RS_TravertineTiers_Water with the water material, and a fall at every spill notch
        /// (Cascade&lt;k&gt;L/R on the lip, F at its foot), with foam where the front cascades meet the basin.
        /// </summary>
        private void Travertines(List<System.Action> rock)
        {
            EnvironmentKit.Piece tiers = _ctx.Kit.Of(EnvironmentRole.Travertine)[0];
            List<EnvironmentKit.Piece> waters = _ctx.Kit.Of(EnvironmentRole.TravertineWater);
            LookTestMeshAccumulator stone = B.Get(Seg, "L0", "Stones", _ctx.Boulder, true, LookTestBatchSet.Group.Rocks);
            LookTestMeshAccumulator pools = B.Get(Seg, "W", "Pools", _ctx.Pool, false, LookTestBatchSet.Group.Water);
            Vector4[] spots = _h.Travertines;
            for (int i = 0; spots != null && i < spots.Length; i++)
            {
                Vector4 t = spots[i];
                Vector3 toEye = _eye - new Vector3(t.x, 0f, t.y);
                float yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg + t.z;
                float sy = t.w * _h.TravertineHeightScale;
                Matrix4x4 m = Matrix4x4.TRS(new Vector3(t.x, _h.BasinWaterY - 0.3f * sy, t.y), Quaternion.Euler(0f, yaw, 0f), new Vector3(t.w, sy, t.w));
                rock.Add(() => _ctx.AppendPiece(tiers, m, stone, Seg, "L0", "Travertine", true, LookTestBatchSet.Group.Ground, _ctx.OpenWet));

                var lips = new List<Vector3>();
                foreach (KeyValuePair<string, Vector3> anchor in tiers.Anchors)
                {
                    if (anchor.Key.StartsWith("Lip", System.StringComparison.Ordinal))
                    {
                        lips.Add(m.MultiplyPoint3x4(anchor.Value));
                    }
                }

                if (waters.Count > 0)
                {
                    // The water FBX (iteration 2 export) keeps Blender's Z-up in its mesh data with an identity node,
                    // while the tiers carry the axis turn on their node: give the water the tiers' turn when it
                    // stands upright. (Reported to asset-pipeline; harmless once the export matches.)
                    Bounds wb = waters[0].Bounds;
                    Matrix4x4 waterMatrix = wb.size.y > wb.size.z * 1.5f ? m * tiers.Parts[0].Matrix * waters[0].Parts[0].Matrix.inverse : m;
                    // Shallow, clear tier pools; foam where the water reaches a lip.
                    _ctx.AppendPiece(waters[0], waterMatrix, pools, Seg, "W", "Pools", false, LookTestBatchSet.Group.Water, (world, local, source) =>
                    {
                        float near = 99f;
                        for (int k = 0; k < lips.Count; k++)
                        {
                            near = Mathf.Min(near, Vector2.Distance(new Vector2(world.x, world.z), new Vector2(lips[k].x, lips[k].z)));
                        }

                        return new Color(0.75f * (1f - LookTestMath.Smooth(0.4f, 2.2f, near)), 0.3f, 0f, 1f);
                    });
                }

                for (int k = 0; k < 16; k++)
                {
                    if (!tiers.Anchors.TryGetValue("Cascade" + k + "L", out Vector3 l) || !tiers.Anchors.TryGetValue("Cascade" + k + "R", out Vector3 r)
                        || !tiers.Anchors.TryGetValue("Cascade" + k + "F", out Vector3 f))
                    {
                        break;
                    }

                    Vector3 left = m.MultiplyPoint3x4(l);
                    Vector3 right = m.MultiplyPoint3x4(r);
                    Vector3 foot = m.MultiplyPoint3x4(f);
                    Vector3 lip = (left + right) * 0.5f + Vector3.up * 0.05f;
                    float width = Vector3.Distance(left, right);
                    Vector3 run = foot - lip;
                    run.y = 0f;
                    LookTestLandmarks.Fall(_ctx, _rng, Seg, "Falls", lip, foot.y, width, _eye, Mathf.Clamp(run.magnitude, 0.3f, 4f), LookTestLandmarks.FallFoot.Pool, 2);
                    if (foot.y < _h.BasinWaterY + 0.8f)
                    {
                        _footFoam.Add(new Vector3(foot.x, foot.z, width));
                    }
                }
            }
        }

        private void Terraces(List<System.Action> rock)
        {
            Vector4[] terraces = _h.Terraces;
            LookTestMeshAccumulator stone = B.Get(Seg, "L0", "Stones", _ctx.Boulder, true, LookTestBatchSet.Group.Rocks);
            LookTestMeshAccumulator pools = B.Get(Seg, "W", "Pools", _ctx.Pool, false, LookTestBatchSet.Group.Water);
            LookTestMeshAccumulator rocks = stone;
            for (int i = 0; i < terraces.Length; i++)
            {
                Vector4 t = terraces[i];
                var center = new Vector3(t.x, 0f, t.y);
                float top = t.z;
                float radius = t.w;
                // Elliptical, turned: travertine rims are never round.
                var shape = Matrix4x4.TRS(new Vector3(center.x, 0f, center.z), Quaternion.Euler(0f, _rng.NextFloat(0f, 180f), 0f), new Vector3(_rng.NextFloat(1.15f, 1.5f), 1f, _rng.NextFloat(0.65f, 0.85f)));
                Mesh mound = TerraceMound(Vector3.zero, _h.BasinFloorY - 2f, top, radius);
                rock.Add(() => stone.Append(mound, shape, _ctx.OpenWet));
                pools.Append(LookTestMeshFactory.Pool(radius * 0.9f, 0.8f, "TerracePool"), shape * Matrix4x4.Translate(new Vector3(0f, top + 0.42f, 0f)), null);

                // Falls spill from the rim facing the camera down to the basin.
                Vector3 toEye = _eye - center;
                toEye.y = 0f;
                toEye.Normalize();
                // A broken curtain of falls along the rim facing the camera, landing on the next level down.
                int falls = Mathf.Clamp(Mathf.RoundToInt(radius / 4f), 3, 7);
                for (int k = 0; k < falls; k++)
                {
                    float angle = (k - (falls - 1) * 0.5f) * (110f / falls) + _rng.NextFloat(-5f, 5f);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * toEye;
                    Vector3 rim = shape.MultiplyPoint3x4(Quaternion.Inverse(shape.rotation) * dir * radius * 0.97f);
                    Vector3 lip = new Vector3(rim.x, top + 0.4f, rim.z);
                    float width = _rng.NextFloat(4f, Mathf.Max(5f, radius * 0.45f));
                    float foot = LowerLevel(lip + dir * 2f, top);
                    LookTestLandmarks.Fall(_ctx, _rng, Seg, "Falls", lip, foot, width, _eye, 0.5f, LookTestLandmarks.FallFoot.Pool, 2);
                }

                // Kit rim dams (RS_PoolTerrace) along the front rim: their basin floor sits at the pool level, so the
                // water reads as held by a boulder dam rather than a round plate.
                List<EnvironmentKit.Piece> rims = _ctx.Kit.Of(EnvironmentRole.PoolTerrace);
                if (rims.Count > 0)
                {
                    int dams = radius > 18f ? 3 : 2;
                    for (int k = 0; k < dams; k++)
                    {
                        EnvironmentKit.Piece dam = rims[(i + k) % rims.Count];
                        float angle = (k - (dams - 1) * 0.5f) * (100f / dams);
                        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * toEye;
                        Vector3 rim = shape.MultiplyPoint3x4(Quaternion.Inverse(shape.rotation) * dir * radius * 0.93f);
                        float span = radius * 1.75f * 1.2f / dams;
                        float sxz = span / Mathf.Max(0.1f, dam.Bounds.size.x);
                        float sy = Mathf.Clamp(sxz * 0.45f, 1f, 2.4f);
                        float baseY = top + 0.42f - 0.5f * dam.Bounds.size.y * sy;
                        Matrix4x4 placement = Matrix4x4.TRS(new Vector3(rim.x, baseY, rim.z), Quaternion.LookRotation(-dir), new Vector3(sxz, sy, sxz));
                        rock.Add(() => _ctx.AppendPiece(dam, placement, stone, Seg, "L2", "Terrace rims", true, LookTestBatchSet.Group.Ground, _ctx.OpenWet));
                    }
                }

                // Mossy boulders along the rim.
                for (int k = 0; k < 6; k++)
                {
                    float a = _rng.NextFloat(0f, 6.283f);
                    Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * _rng.NextFloat(0.85f, 1.02f) + Vector3.up * (top + 0.1f);
                    float size = _rng.NextFloat(0.8f, 2.4f);
                    Mesh boulder = LookTestMeshFactory.Boulder(_rng, 1f);
                    rock.Add(() => rocks.Append(boulder, Matrix4x4.TRS(p, Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f), new Vector3(size, size * 0.6f, size)), _ctx.OpenWet));
                }

                // Bushes on the islands.
                if (_plants.Fronds != null)
                {
                    LookTestMeshAccumulator bushes = B.Get(Seg, "L1", "Island plants", _plants.Fronds, false, LookTestBatchSet.Group.Plants);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = _rng.NextFloat(0f, 6.283f);
                        Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * _rng.NextFloat(0.3f, 0.8f) + Vector3.up * (top + 0.4f);
                        HeroBasinPlants.Clump(bushes, _rng, HeroBasinPlants.Fronds, p, _rng.NextFloat(0f, 360f), 10, new Vector2(2f, 3.5f), new Vector2(25f, 60f), 0.35f, HeroBasinPlants.Open);
                    }
                }
            }
        }

        /// <summary>Kit pillars (RS_PillarA/B) in front of the painted range for parallax.</summary>
        private void KitPillars(List<System.Action> rock)
        {
            List<EnvironmentKit.Piece> pillars = _ctx.Kit.Of(EnvironmentRole.Pillar);
            Vector4[] spots = _h.KitPillars;
            if (pillars.Count == 0 || spots == null)
            {
                return;
            }

            LookTestMeshAccumulator stone = B.Get(Seg, "L2", "Arch", _ctx.Rootstone, false, LookTestBatchSet.Group.Ground);
            for (int i = 0; i < spots.Length; i++)
            {
                EnvironmentKit.Piece piece = pillars[i % pillars.Count];
                var foot = new Vector3(spots[i].x, Height(spots[i].x, spots[i].y) - 1.5f, spots[i].y);
                Matrix4x4 placement = EnvironmentKit.Stand(piece, foot, spots[i].w, spots[i].z, 0f);
                rock.Add(() => _ctx.AppendPiece(piece, placement, stone, Seg, "L2", "Pillars", false, LookTestBatchSet.Group.Ground, LookTestBuildContext.Open));
            }
        }

        /// <summary>
        /// A kit canopy crown (FP_CanopyCrown_A..D: main card plus two crossed cards, front +Z) centred at
        /// <paramref name="centre"/>, <paramref name="height"/> tall, turned to the camera; false if none landed.
        /// </summary>
        private bool KitCrown(Vector3 centre, float height)
        {
            List<EnvironmentKit.Piece> crowns = _ctx.Kit.Of(EnvironmentRole.CanopyCrown);
            if (crowns.Count == 0 || crowns[0].Material == null)
            {
                return false;
            }

            EnvironmentKit.Piece piece = crowns[_rng.NextInt(0, crowns.Count)];
            Vector3 toEye = _eye - centre;
            float yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg + _rng.NextFloat(-25f, 25f);
            Vector3 foot = centre - Vector3.up * height * 0.5f;
            _ctx.AppendPiece(piece, EnvironmentKit.Stand(piece, foot, yaw, height, 0f), null, Seg, "L3", "Kit crowns", false, LookTestBatchSet.Group.Trees, HeroBasinPlants.Open);
            return true;
        }

        /// <summary>A framing plant cluster (FP_FrameLeft / FP_FrameRight) at (x, z, yaw, scale) on the ground.</summary>
        private void FramePlant(string kind, Vector4 spot)
        {
            List<EnvironmentKit.Piece> clumps = _ctx.Kit.Of(EnvironmentRole.PlantClump);
            for (int i = 0; i < clumps.Count; i++)
            {
                if (clumps[i].Name.IndexOf(kind, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var foot = new Vector3(spot.x, Height(spot.x, spot.y) - 0.1f, spot.y);
                    _ctx.AppendPiece(clumps[i], Matrix4x4.TRS(foot, Quaternion.Euler(0f, spot.z, 0f), Vector3.one * spot.w), null, Seg, "L1", "Framing plants", false, LookTestBatchSet.Group.Plants, HeroBasinPlants.Open);
                    return;
                }
            }
        }

        /// <summary>Places a kit plant clump whose name contains <paramref name="kind"/>; false if none landed.</summary>
        private bool KitPlant(string kind, Vector3 foot, float yawDeg, float scale)
        {
            List<EnvironmentKit.Piece> clumps = _ctx.Kit.Of(EnvironmentRole.PlantClump);
            for (int i = 0; i < clumps.Count; i++)
            {
                if (clumps[i].Name.IndexOf(kind, System.StringComparison.OrdinalIgnoreCase) >= 0 && clumps[i].Material != null)
                {
                    Matrix4x4 placement = Matrix4x4.TRS(foot, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale);
                    _ctx.AppendPiece(clumps[i], placement, null, Seg, "L1", "Plants", false, LookTestBatchSet.Group.Plants, HeroBasinPlants.Open);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A travertine-like terrace: a lobed mound that widens downward (no overhanging lip), capped flat just
        /// under its pool so the water sits in a rock rim.
        /// </summary>
        private Mesh TerraceMound(Vector3 center, float baseY, float topY, float radius)
        {
            var spine = new List<Vector3>();
            var radii = new List<float>();
            const int rings = 7;
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                spine.Add(new Vector3(center.x, Mathf.Lerp(baseY, topY + 0.55f, t), center.z));
                radii.Add(radius * (1.35f - 0.35f * t * t));
            }

            Mesh side = LookTestMeshFactory.Tube("Terrace", spine, radii, 30, 4f, 9, 0.32f, 0.01f, _rng.NextFloat(0f, 9f));
            Mesh cap = LookTestMeshFactory.Pool(radius * 1.05f, 0.5f, "TerraceCap");
            var acc = new LookTestMeshAccumulator();
            acc.Append(side, Matrix4x4.identity, null);
            acc.Append(cap, Matrix4x4.Translate(new Vector3(center.x, topY + 0.3f, center.z)), (w, l, c) => new Color(0f, 0f, 0f, 1f));
            return acc.ToMesh("TerraceMound");
        }

        private static List<Vector3> DownwardPoints(Mesh mesh, Matrix4x4 matrix, int step)
        {
            var result = new List<Vector3>();
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < vertices.Length; i += step)
            {
                if (normals.Length > i && normalMatrix.MultiplyVector(normals[i]).normalized.y < -0.4f)
                {
                    result.Add(matrix.MultiplyPoint3x4(vertices[i]));
                }
            }

            return result;
        }

        /// <summary>Water height just below a terrace rim: the highest lower terrace pool under the point, else the basin.</summary>
        private float LowerLevel(Vector3 point, float fromTop)
        {
            float best = _h.BasinWaterY;
            Vector4[] terraces = _h.Terraces;
            for (int i = 0; i < terraces.Length; i++)
            {
                Vector4 t = terraces[i];
                if (t.z < fromTop - 0.5f && t.z + 0.42f > best && new Vector2(point.x - t.x, point.z - t.y).magnitude < t.w * 0.9f)
                {
                    best = t.z + 0.42f;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- Arch and falls

        private void Arch(List<System.Action> rock)
        {
            LookTestMeshAccumulator stone = B.Get(Seg, "L2", "Arch", _ctx.Rootstone, false, LookTestBatchSet.Group.Ground);
            EnvironmentKit.Piece piece = _ctx.Kit.Pick(EnvironmentRole.HeroArch, EnvironmentRole.Arch, _rng);
            LookTestMeshAccumulator canopy = B.Get(Seg, "L3", "Arch canopy", _ctx.Leaves, false, LookTestBatchSet.Group.Trees);
            if (piece != null)
            {
                Matrix4x4 placement = EnvironmentKit.Span(piece, _h.ArchFootA, _h.ArchFootB, _h.ArchTopY);
                rock.Add(() => _ctx.AppendPiece(piece, placement, stone, Seg, "L2", "Arch", false, LookTestBatchSet.Group.Ground, _ctx.OpenWet));
                if (piece.Anchors.TryGetValue("WaterfallMouth", out Vector3 mouth))
                {
                    _mouth = placement.MultiplyPoint3x4(mouth);
                }

                // Trees and hanging growth on the arch's upper faces.
                var tops = new List<Vector3>();
                for (int i = 0; i < piece.Parts.Count; i++)
                {
                    tops.AddRange(HeroBasinPlants.UpwardPoints(piece.Parts[i].Mesh, placement * piece.Parts[i].Matrix, 0.55f, 23));
                }

                // Vine and moss curtains: the kit's FP_ArchVines (built in the arch's space), else blade cards.
                List<EnvironmentKit.Piece> kitVines = _ctx.Kit.Of(EnvironmentRole.ArchVines);
                if (kitVines.Count > 0 && kitVines[0].Material != null)
                {
                    _ctx.AppendPiece(kitVines[0], placement, null, Seg, "L1", "Arch vines", false, LookTestBatchSet.Group.Plants, HeroBasinPlants.Open);
                }
                else if (_plants.Bellcap != null)
                {
                    LookTestMeshAccumulator vines = B.Get(Seg, "L1", "Arch vines", _plants.Bellcap, false, LookTestBatchSet.Group.Plants);
                    var under = new List<Vector3>();
                    for (int i = 0; i < piece.Parts.Count; i++)
                    {
                        under.AddRange(DownwardPoints(piece.Parts[i].Mesh, placement * piece.Parts[i].Matrix, 17));
                    }

                    for (int i = 0; i < 160 && under.Count > 0; i++)
                    {
                        Vector3 p = under[_rng.NextInt(0, under.Count)];
                        Rect cell = HeroBasinPlants.Blades[_rng.NextInt(0, HeroBasinPlants.Blades.Length)];
                        Mesh vine = HeroBasinPlants.Card(cell, _rng.NextFloat(6f, 18f), -88f, 0f, 0f, 2.5f);
                        vines.Append(vine, Matrix4x4.TRS(p, Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f), Vector3.one), (w, l, c) => new Color(c.r * 0.3f, 0f, 0f, 1f));
                    }
                }

                for (int i = 0; i < _h.ArchCrowns && tops.Count > 0; i++)
                {
                    Vector3 p = tops[_rng.NextInt(0, tops.Count)];
                    float height = _rng.NextFloat(6f, 13f);
                    canopy.Append(LookTestMeshFactory.Crown(_rng, height, 40, height * 0.45f), Matrix4x4.Translate(p + Vector3.down * height * 0.5f), Foliage(p.y, height));
                }

                return;
            }

            // Stand-in: braided strands around a Bezier spine.
            Vector3 a = _h.ArchFootA;
            Vector3 b = _h.ArchFootB;
            Vector3 control = (a + b) * 0.5f;
            control.y = 2f * (_h.ArchTopY - 20f) - 0.5f * (a.y + b.y);
            List<Vector3> spine = LookTestLandmarks.Bezier(a, control, b, 40);
            for (int s = 0; s < 6; s++)
            {
                var strand = new List<Vector3>();
                var radii = new List<float>();
                for (int i = 0; i < spine.Count; i++)
                {
                    float t = (float)i / (spine.Count - 1);
                    Vector3 tangent = (spine[Mathf.Min(i + 1, spine.Count - 1)] - spine[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 side = Vector3.Cross(tangent, Vector3.forward).normalized;
                    Vector3 up = Vector3.Cross(side, tangent);
                    float angle = s * Mathf.PI / 3f + t * 9f;
                    float bundle = Mathf.Lerp(16f, 26f, Mathf.Pow(Mathf.Abs(t - 0.5f) * 2f, 2f));
                    strand.Add(spine[i] + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * bundle * 0.55f);
                    radii.Add(bundle * 0.42f);
                }

                Mesh tube = LookTestMeshFactory.Tube("ArchStrand", strand, radii, 14, 9f, 4, 0.2f, 0.05f, s);
                rock.Add(() => stone.Append(tube, Matrix4x4.identity, _ctx.OpenWet));
            }
        }

        private void Falls(List<System.Action> rock)
        {
            HeroFall[] falls = _h.Falls;
            LookTestMeshAccumulator stone = B.Get(Seg, "L2", "Terraces", _ctx.Rootstone, true, LookTestBatchSet.Group.Ground);
            for (int i = 0; i < falls.Length; i++)
            {
                HeroFall f = falls[i];
                if (i == 0 && _mouth.HasValue)
                {
                    // The tall fall pours from the hero arch's cave mouth (RS_HeroArch_WaterfallMouth).
                    f.Top = _mouth.Value;
                }

                BrokenFall(f.Top, f.FootY, f.WidthM, f.Layers, f.IntoPool);
                if (!f.IntoPool)
                {
                    continue;
                }

                // The rock shelf the fall pours from.
                Vector3 away = f.Top - _eye;
                away.y = 0f;
                away.Normalize();
                float radius = f.WidthM * 0.8f;
                Vector3 center = f.Top + away * (radius + 1f);
                Mesh shelf = LookTestLandmarks.Pillar(_rng, new Vector3(center.x, 0f, center.z), _h.BasinFloorY - 2f, f.Top.y - 0.4f, radius, true, 22, 2.5f);
                rock.Add(() => stone.Append(shelf, Matrix4x4.identity, _ctx.OpenWet));
            }
        }

        /// <summary>
        /// A hero fall as a broken curtain (keyframe: never one uniform slab): strands of varied width with gaps between
        /// them and lips at slightly different heights, then one shared foot: impact foam (into a pool), spray, and a
        /// mist plume that climbs a good part of the fall's height.
        /// </summary>
        private void BrokenFall(Vector3 top, float footY, float width, int layers, bool intoPool)
        {
            Vector3 toEye = _eye - top;
            toEye.y = 0f;
            toEye.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toEye);
            float height = top.y - footY;
            int strands = Mathf.Clamp(Mathf.RoundToInt(width / 9f), 1, 6);
            var weights = new float[strands];
            float total = 0f;
            for (int i = 0; i < strands; i++)
            {
                weights[i] = _rng.NextFloat(0.35f, 1.6f);
                total += weights[i];
            }

            // About a fifth of the width is gaps (rock showing between the strands).
            float gapShare = strands > 1 ? 0.22f : 0f;
            float gap = width * gapShare / Mathf.Max(1, strands - 1);
            float x = -width * 0.5f;
            for (int i = 0; i < strands; i++)
            {
                float w = width * (1f - gapShare) * weights[i] / total;
                Vector3 lip = top + side * (x + w * 0.5f) + Vector3.up * _rng.NextFloat(-0.08f, 0.04f) * Mathf.Min(height, 20f) + toEye * _rng.NextFloat(-0.6f, 0.6f);
                LookTestLandmarks.Fall(_ctx, _rng, Seg, "Falls", lip, footY, w, _eye, 1.5f, LookTestLandmarks.FallFoot.None, layers);
                x += w + gap;
            }

            Vector3 foot = new Vector3(top.x, footY, top.z) + toEye * 1.5f;
            if (intoPool)
            {
                B.Get(Seg, "W", "Pools", _ctx.Pool, false, LookTestBatchSet.Group.Water)
                    .Append(LookTestMeshFactory.ImpactFoam(width * 0.7f), Matrix4x4.Translate(foot + Vector3.up * 0.08f + toEye * width * 0.15f), null);
            }

            LookTestMeshAccumulator mist = B.Get(Seg, "W", "Mist", _ctx.MistCard, false, LookTestBatchSet.Group.Water);
            float plume = Mathf.Min(height * 0.85f, width * 2.2f);
            for (int i = 0; i < 6; i++)
            {
                float w = width * _rng.NextFloat(0.35f, 0.8f);
                float l = width * _rng.NextFloat(0.5f, 1.1f);
                mist.AppendCard(foot + toEye * _rng.NextFloat(0.1f, 0.5f) * width + side * _rng.NextFloat(-0.5f, 0.5f) * width + Vector3.up * l * 0.35f, Vector3.up, w, l, _rng.NextFloat(0f, 10f), 1f);
            }

            // The plume: soft billows stacked up the face of the fall, widest near the foot.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float w = width * Mathf.Lerp(1.5f, 0.8f, t);
                mist.AppendCard(foot + toEye * width * 0.25f + side * _rng.NextFloat(-0.25f, 0.25f) * width + Vector3.up * plume * Mathf.Lerp(0.2f, 0.7f, t),
                    Vector3.up, w, plume * Mathf.Lerp(0.6f, 0.45f, t), _rng.NextFloat(0f, 10f));
            }
        }

        // ---------------------------------------------------------------- Ledge, forest, plants

        private void Ledge()
        {
            LookTestMeshAccumulator stone = B.Get(Seg, "L2", "Ledge", _ctx.Rootstone, true, LookTestBatchSet.Group.Ground);
            LookTestMeshAccumulator rocks = B.Get(Seg, "L0", "Stones", _ctx.Boulder, true, LookTestBatchSet.Group.Rocks);
            List<EnvironmentKit.Piece> outcrops = _ctx.Kit.Of(EnvironmentRole.Outcrop);

            // (x, base y, z, height, footprint radius, yaw)
            var spots = new[]
            {
                new Vector4(0.2f, -3.1f, 0.4f, 3.1f), new Vector4(3.4f, -9f, 4.5f, 9f), new Vector4(-3.6f, -4f, 3.2f, 4.2f),
                new Vector4(7.5f, -15f, 9f, 13f), new Vector4(-1.5f, -10f, 9.5f, 9.5f), new Vector4(5.2f, -2.4f, -1.2f, 2.6f),
            };
            float[] footprints = { 2.4f, 5f, 3f, 7f, 6f, 1.8f };
            List<EnvironmentKit.Piece> ledges = _ctx.Kit.Of(EnvironmentRole.Ledge);
            int first = 0;
            if (ledges.Count > 0)
            {
                // The lookout ledge Pista stands on (RS_LedgeLookout), pivot at ground level.
                // With a Stand anchor (ledge v2) Pista's feet go exactly on it; else the configured pivot position.
                Vector4 l = _h.LedgePiece;
                Quaternion turn = Quaternion.Euler(0f, l.w, 0f);
                Vector3 pivot = ledges[0].Anchors.TryGetValue("Stand", out Vector3 stand) ? _h.PistaPosition - turn * stand : new Vector3(l.x, l.y, l.z);
                _ctx.AppendPiece(ledges[0], Matrix4x4.TRS(pivot, turn, Vector3.one), stone, Seg, "L2", "Ledge", true, LookTestBatchSet.Group.Ground, LookTestBuildContext.Open);
                first = 1;
            }

            for (int i = first; i < spots.Length; i++)
            {
                var foot = new Vector3(spots[i].x, spots[i].y, spots[i].z);
                float height = spots[i].w;
                float yaw = 37f + 71f * i;
                if (outcrops.Count > 0)
                {
                    EnvironmentKit.Piece piece = outcrops[i % outcrops.Count];
                    _ctx.AppendPiece(piece, EnvironmentKit.Stand(piece, foot, yaw, height, footprints[i]), stone, Seg, "L2", "Ledge", true, LookTestBatchSet.Group.Ground, LookTestBuildContext.Open);
                }
                else
                {
                    rocks.Append(LookTestMeshFactory.Boulder(_rng, 1f), Matrix4x4.TRS(foot + Vector3.up * height * 0.5f, Quaternion.Euler(0f, yaw, 0f), new Vector3(footprints[i] * 2f, height, footprints[i] * 2f)), LookTestBuildContext.Open);
                }
            }
        }

        private void Forest()
        {
            LookTestMeshAccumulator wood = B.Get(Seg, "L2", "Wood", _ctx.Bark, true, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator crowns = B.Get(Seg, "L3", "Crowns", _ctx.Leaves, false, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator palms = _plants.Fronds != null ? B.Get(Seg, "L3", "Palms", _plants.Fronds, false, LookTestBatchSet.Group.Trees) : null;
            int placed = 0;
            for (int attempt = 0; attempt < _h.ForestTrees * 4 && placed < _h.ForestTrees; attempt++)
            {
                bool left = _rng.Chance(0.55f);
                float x = left ? _rng.NextFloat(-200f, -22f) : _rng.NextFloat(40f, 200f);
                float z = _rng.NextFloat(10f, 290f);
                float y = Height(x, z);
                float minDistance = x < 0f ? _h.ForestMinDistanceM * 2.2f : _h.ForestMinDistanceM;
                if (y < _h.BasinWaterY + 1.5f || Vector3.Distance(new Vector3(x, y, z), _eye) < minDistance)
                {
                    continue;
                }

                float distance = Vector3.Distance(new Vector3(x, y, z), _eye);
                float height = _rng.NextFloat(14f, 30f) * Mathf.Lerp(1f, 1.4f, distance / 300f);
                // Keep the low sun visible (keyframe: it bursts through a gap in the left foliage).
                if (BlocksSun(new Vector3(x, y + height * 0.6f, z), height * 0.7f))
                {
                    continue;
                }
                var foot = new Vector3(x, y - 0.5f, z);
                if (palms != null && _rng.Chance(0.1f))
                {
                    wood.Append(LookTestMeshFactory.Trunk(_rng, height, 0.35f + height * 0.012f, 2f, 7, 8), Matrix4x4.Translate(foot), LookTestBuildContext.Open);
                    HeroBasinPlants.PalmCrown(palms, _rng, foot + Vector3.up * height, height * 0.32f, Foliage(foot.y + height, height * 0.3f));
                }
                else
                {
                    wood.Append(LookTestMeshFactory.Trunk(_rng, height * 0.7f, 0.5f + height * 0.015f, 2f, 8, 8), Matrix4x4.Translate(foot), LookTestBuildContext.Open);
                    if (!KitCrown(foot + Vector3.up * height * 0.45f, height * 0.7f))
                    {
                        crowns.Append(LookTestMeshFactory.Crown(_rng, height, distance < 90f ? 70 : 45, height * 0.5f), Matrix4x4.Translate(foot), Foliage(foot.y + height * 0.5f, height * 0.5f));
                    }
                }

                placed++;
            }

            // Canopy wall segments (FP_Canopy_Clump) on the banks, front turned to the camera.
            List<EnvironmentKit.Piece> walls = _ctx.Kit.Of(EnvironmentRole.PlantClump).FindAll(p => p.Name.IndexOf("canopy", System.StringComparison.OrdinalIgnoreCase) >= 0 && p.Material != null);
            Vector4[] wallSpots = _h.CanopyWalls;
            for (int i = 0; walls.Count > 0 && wallSpots != null && i < wallSpots.Length; i++)
            {
                Vector4 w = wallSpots[i];
                var foot = new Vector3(w.x, Height(w.x, w.y) - 0.5f, w.y);
                Vector3 toEye = _eye - foot;
                float yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg + w.z;
                _ctx.AppendPiece(walls[0], Matrix4x4.TRS(foot, Quaternion.Euler(0f, yaw, 0f), Vector3.one * w.w), null, Seg, "L3", "Canopy walls", false, LookTestBatchSet.Group.Trees, HeroBasinPlants.Open);
            }

            // Canopy mass under the crowns: lumpy forest so the hills never read as bare ground.
            LookTestMeshAccumulator mass = B.Get(Seg, "L4", "Canopy mass", _ctx.FarCanopy, false, LookTestBatchSet.Group.Trees);
            for (int i = 0; i < 220; i++)
            {
                bool left = _rng.Chance(0.55f);
                float x = left ? _rng.NextFloat(-230f, -30f) : _rng.NextFloat(48f, 230f);
                float z = _rng.NextFloat(20f, 300f);
                float y = Height(x, z);
                // Smooth lumps read as blobs up close: only where crowns hide them or distance softens them.
                if (y < _h.BasinWaterY + 1f || Vector3.Distance(new Vector3(x, y, z), _eye) < _h.ForestMinDistanceM * 3f)
                {
                    continue;
                }

                float size = _rng.NextFloat(9f, 20f);
                mass.Append(LookTestMeshFactory.Boulder(_rng, 1f, 1), Matrix4x4.TRS(new Vector3(x, y - size * 0.15f, z), Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f), new Vector3(size, size * 0.75f, size)), LookTestBuildContext.Open);
            }
        }

        /// <summary>True when a crown (centre, radius) would cover the sun as seen from the camera.</summary>
        private bool BlocksSun(Vector3 centre, float radius)
        {
            Vector3 toSun = -_h.SunLightDirection.normalized;
            Vector3 v = centre - _eye;
            float along = Vector3.Dot(v, toSun);
            if (along <= 0f)
            {
                return false;
            }

            // Clear a cone of about 7 degrees around the sun plus the crown's own radius.
            float off = (v - toSun * along).magnitude;
            return off < radius + along * 0.12f;
        }

        private void Foreground()
        {
            // Left: a big tree, its crown silhouetted against the low sun (frames the top-left corner).
            LookTestMeshAccumulator wood = B.Get(Seg, "L2", "Wood", _ctx.Bark, true, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator crowns = B.Get(Seg, "L3", "Hero tree crowns", _ctx.Leaves, true, LookTestBatchSet.Group.Trees);
            Vector4[] trees = _h.HeroTrees;
            for (int i = 0; trees != null && i < trees.Length; i++)
            {
                Vector4 t = trees[i];
                var foot = new Vector3(t.x, Height(t.x, t.y) - 0.5f, t.y);
                wood.Append(LookTestMeshFactory.Trunk(_rng, t.z, 1.2f + 0.1f * i, 2f, 14, 18), Matrix4x4.Translate(foot), LookTestBuildContext.Open);
                if (!KitCrown(foot + Vector3.up * t.z * 0.55f, t.z * 0.75f))
                {
                    crowns.Append(LookTestMeshFactory.Crown(_rng, t.z, Mathf.RoundToInt(15f * t.w), t.w), Matrix4x4.Translate(foot), Foliage(foot.y + t.z * 0.65f, t.z * 0.4f));
                }
            }

            FramePlant("frameleft", _h.FrameLeft);
            FramePlant("frameright", _h.FrameRight);

            if (_plants.Broadleaf != null)
            {
                LookTestMeshAccumulator broad = B.Get(Seg, "L1", "Broadleaf", _plants.Broadleaf, true, LookTestBatchSet.Group.Plants);
                Vector3[] spots = { new Vector3(-2.8f, 0f, -2.4f), new Vector3(-4.6f, 0f, 0.2f), new Vector3(-3.4f, 0f, 2.6f), new Vector3(-6.5f, 0f, 3.5f), new Vector3(4.8f, 0f, -2.6f), new Vector3(-1.6f, 0f, -3.6f) };
                float[] sizes = { 1.5f, 1.9f, 1.4f, 2.2f, 1.6f, 1.1f };
                for (int i = 0; i < spots.Length; i++)
                {
                    Vector3 p = spots[i];
                    p.y = Height(p.x, p.z) - 0.05f;
                    if (!KitPlant("broadleaf", p, _rng.NextFloat(0f, 360f), sizes[i] * 0.9f))
                    {
                        HeroBasinPlants.Clump(broad, _rng, HeroBasinPlants.Broadleaf, p, _rng.NextFloat(0f, 360f), 7, new Vector2(sizes[i] * 0.7f, sizes[i]), new Vector2(20f, 65f), 0.25f, HeroBasinPlants.Open);
                    }
                }

                // A big leaf hanging into the top-right corner.
                Mesh hanging = HeroBasinPlants.Card(HeroBasinPlants.Broadleaf[1], 1.6f, -55f, 0.15f, 0.15f);
                broad.Append(hanging, Matrix4x4.TRS(new Vector3(5.2f, 4.6f, 0.6f), Quaternion.Euler(0f, -140f, 25f), Vector3.one), HeroBasinPlants.Open);
            }

            if (_plants.Bellcap != null)
            {
                LookTestMeshAccumulator bells = B.Get(Seg, "L1", "Bellcaps", _plants.Bellcap, false, LookTestBatchSet.Group.Plants);
                Vector3[] spots = { new Vector3(3.6f, 0f, -1.6f), new Vector3(4.4f, 0f, 0.2f), new Vector3(-3.8f, 0f, -0.9f) };
                for (int i = 0; i < spots.Length; i++)
                {
                    Vector3 p = spots[i];
                    p.y = Height(p.x, p.z) - 0.05f;
                    if (!KitPlant("bellcap", p, _rng.NextFloat(0f, 360f), _rng.NextFloat(1.0f, 1.3f)))
                    {
                        HeroBasinPlants.Stalks(bells, _rng, HeroBasinPlants.Bellcaps, p, 3, 0.35f, new Vector2(1.2f, 1.9f), HeroBasinPlants.Open);
                        HeroBasinPlants.Stalks(bells, _rng, HeroBasinPlants.Blades, p, 5, 0.5f, new Vector2(0.8f, 1.4f), HeroBasinPlants.Open);
                    }
                }
            }

            if (_plants.Fronds != null)
            {
                LookTestMeshAccumulator fronds = B.Get(Seg, "L1", "Fronds", _plants.Fronds, false, LookTestBatchSet.Group.Plants);
                for (int i = 0; i < 40; i++)
                {
                    float x = _rng.Chance(0.5f) ? _rng.NextFloat(-16f, -1.2f) : _rng.NextFloat(1.2f, 9f);
                    float z = _rng.NextFloat(-7f, 12f);
                    float y = Height(x, z);
                    // Keep the camera's view of Pista and the ledge clear: no fern between the lens and her.
                    bool inFront = z < 3f && Mathf.Abs(x - _eye.x) < 4.5f;
                    if (y < -3f || inFront)
                    {
                        continue;
                    }

                    if (!KitPlant("fern", new Vector3(x, y - 0.05f, z), _rng.NextFloat(0f, 360f), _rng.NextFloat(0.8f, 1.4f)))
                    {
                        HeroBasinPlants.Clump(fronds, _rng, HeroBasinPlants.Fronds, new Vector3(x, y - 0.05f, z), _rng.NextFloat(0f, 360f), 8, new Vector2(0.9f, 1.8f), new Vector2(25f, 70f), 0.35f, HeroBasinPlants.Open);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- Light shafts and mist

        private void Shafts()
        {
            Vector3 toSun = -_h.SunLightDirection.normalized;
            LookTestMeshAccumulator target = B.Get(Seg, "W", "Shafts", _ctx.LightShaft, false, LookTestBatchSet.Group.Water);
            Vector4[] shafts = _h.Shafts;
            for (int i = 0; i < shafts.Length; i++)
            {
                var ground = new Vector3(shafts[i].x, shafts[i].y, shafts[i].z);
                float length = shafts[i].w;
                target.AppendCard(ground + toSun * length * 0.5f, -toSun, _rng.NextFloat(_h.ShaftWidthM.x, _h.ShaftWidthM.y), length, _rng.NextFloat(0f, 10f));
            }
        }

        private void Mist()
        {
            LookTestMeshAccumulator mist = B.Get(Seg, "W", "Mist", _ctx.MistCard, false, LookTestBatchSet.Group.Water);
            float water = _h.BasinWaterY;
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(_rng.NextFloat(-60f, 90f), water + _rng.NextFloat(3f, 8f), _rng.NextFloat(70f, 260f));
                mist.AppendCard(p, Vector3.up, _rng.NextFloat(30f, 70f), _rng.NextFloat(8f, 16f), _rng.NextFloat(0f, 10f));
            }

            // Spray clouds around the tall fall's base and the arch feet.
            HeroFall[] falls = _h.Falls;
            for (int i = 1; i < falls.Length; i++)
            {
                Vector3 foot = new Vector3(falls[i].Top.x, falls[i].FootY, falls[i].Top.z);
                LookTestLandmarks.Billows(mist, _rng, foot + Vector3.up * falls[i].WidthM * 0.4f, 3, falls[i].WidthM, new Vector2(falls[i].WidthM * 1.6f, falls[i].WidthM * 2.4f), new Vector2(falls[i].WidthM * 0.6f, falls[i].WidthM));
            }

            // Low mist around the arch feet: kept under the sun (tall billows there washed out the sky and hid the sun).
            LookTestLandmarks.Billows(mist, _rng, _h.ArchFootA + Vector3.up * 10f, 3, 16f, new Vector2(40f, 60f), new Vector2(12f, 20f));
            LookTestLandmarks.Billows(mist, _rng, _h.ArchFootB + Vector3.up * 10f, 3, 16f, new Vector2(40f, 60f), new Vector2(12f, 20f));
        }

        private static LookTestMeshAccumulator.Painter Foliage(float baseY, float height)
        {
            return (world, local, source) =>
            {
                float w = Mathf.Clamp01((world.y - baseY) / Mathf.Max(0.1f, height));
                return new Color(w * w * 0.4f, 0f, 0f, source.a);
            };
        }
    }
}
