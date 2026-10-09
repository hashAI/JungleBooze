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
            Terraces(rock);
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
                    colors[i, j] = new Color(1f - LookTestMath.Smooth(0.05f, 0.7f, depth), Mathf.Clamp01(depth / 3f), 0f, LookTestMath.Smooth(-0.4f, 0.15f, depth));
                }
            }

            Mesh surface = LookTestMeshFactory.Grid("BasinWater", positions, frames, uvs, colors);
            B.Get(Seg, "W", "Pools", _ctx.Pool, false, LookTestBatchSet.Group.Water).Append(surface, Matrix4x4.identity, null);
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

                // Vines and moss curtains hanging from the arch's underside (blade cards of the bellcap atlas).
                if (_plants.Bellcap != null)
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

                LookTestLandmarks.Fall(_ctx, _rng, Seg, "Falls", f.Top, f.FootY, f.WidthM, _eye, 1.5f,
                    f.IntoPool ? LookTestLandmarks.FallFoot.Pool : LookTestLandmarks.FallFoot.Valley, f.Layers);
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
                Vector4 l = _h.LedgePiece;
                _ctx.AppendPiece(ledges[0], Matrix4x4.TRS(new Vector3(l.x, l.y, l.z), Quaternion.Euler(0f, l.w, 0f), Vector3.one), stone, Seg, "L2", "Ledge", true, LookTestBatchSet.Group.Ground, LookTestBuildContext.Open);
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
                var foot = new Vector3(x, y - 0.5f, z);
                if (palms != null && _rng.Chance(0.1f))
                {
                    wood.Append(LookTestMeshFactory.Trunk(_rng, height, 0.35f + height * 0.012f, 2f, 7, 8), Matrix4x4.Translate(foot), LookTestBuildContext.Open);
                    HeroBasinPlants.PalmCrown(palms, _rng, foot + Vector3.up * height, height * 0.32f, Foliage(foot.y + height, height * 0.3f));
                }
                else
                {
                    wood.Append(LookTestMeshFactory.Trunk(_rng, height * 0.7f, 0.5f + height * 0.015f, 2f, 8, 8), Matrix4x4.Translate(foot), LookTestBuildContext.Open);
                    crowns.Append(LookTestMeshFactory.Crown(_rng, height, distance < 90f ? 70 : 45, height * 0.5f), Matrix4x4.Translate(foot), Foliage(foot.y + height * 0.5f, height * 0.5f));
                }

                placed++;
            }

            // Canopy mass under the crowns: lumpy forest so the hills never read as bare ground.
            LookTestMeshAccumulator mass = B.Get(Seg, "L4", "Canopy mass", _ctx.FarCanopy, false, LookTestBatchSet.Group.Trees);
            for (int i = 0; i < 220; i++)
            {
                bool left = _rng.Chance(0.55f);
                float x = left ? _rng.NextFloat(-230f, -30f) : _rng.NextFloat(48f, 230f);
                float z = _rng.NextFloat(20f, 300f);
                float y = Height(x, z);
                if (y < _h.BasinWaterY + 1f)
                {
                    continue;
                }

                float size = _rng.NextFloat(9f, 20f);
                mass.Append(LookTestMeshFactory.Boulder(_rng, 1f, 1), Matrix4x4.TRS(new Vector3(x, y - size * 0.15f, z), Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f), new Vector3(size, size * 0.75f, size)), LookTestBuildContext.Open);
            }
        }

        private void Foreground()
        {
            // Left: a big tree, its crown silhouetted against the low sun (frames the top-left corner).
            LookTestMeshAccumulator wood = B.Get(Seg, "L2", "Wood", _ctx.Bark, true, LookTestBatchSet.Group.Trees);
            LookTestMeshAccumulator crowns = B.Get(Seg, "L3", "Hero tree crowns", _ctx.Leaves, true, LookTestBatchSet.Group.Trees);
            var treeFoot = new Vector3(-19f, Height(-19f, 4f) - 0.5f, 4f);
            wood.Append(LookTestMeshFactory.Trunk(_rng, 26f, 1.2f, 2f, 14, 18), Matrix4x4.Translate(treeFoot), LookTestBuildContext.Open);
            crowns.Append(LookTestMeshFactory.Crown(_rng, 26f, 70, 5f), Matrix4x4.Translate(treeFoot + new Vector3(-3f, 5f, 0f)), Foliage(treeFoot.y + 20f, 10f));
            var tree2 = new Vector3(-30f, Height(-30f, 24f) - 0.5f, 24f);
            wood.Append(LookTestMeshFactory.Trunk(_rng, 30f, 1.3f, 2f, 14, 18), Matrix4x4.Translate(tree2), LookTestBuildContext.Open);
            crowns.Append(LookTestMeshFactory.Crown(_rng, 30f, 140, 9f), Matrix4x4.Translate(tree2), Foliage(tree2.y + 18f, 12f));

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
                    if (y < -3f)
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

            LookTestLandmarks.Billows(mist, _rng, _h.ArchFootA + Vector3.up * 20f, 3, 20f, new Vector2(60f, 90f), new Vector2(25f, 40f));
            LookTestLandmarks.Billows(mist, _rng, _h.ArchFootB + Vector3.up * 20f, 3, 20f, new Vector2(60f, 90f), new Vector2(25f, 40f));
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
