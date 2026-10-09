using System.Collections.Generic;
using System.IO;
using JungleBooze.App.HeroBasin;
using JungleBooze.Core;
using JungleBooze.Editor.LookTest;
using JungleBooze.Editor.Scenery;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Dense set-dressing pass of the painterly hero basin (design/aurelia/HERO_BASIN_DRESSING.md s3). Runs after the
    /// world is built and before the batches are emitted: temporary mesh colliders are made from the merged batches
    /// (arch, rock, ground, water), then every item is placed by a ray from the landscape camera through a normalized
    /// screen point (u right, v down) with a fixed seed. Items are appended to the same merged batches (one draw per
    /// material), so the pass adds triangles but almost no draws. Sizes are screen fractions turned into metres at the
    /// hit distance (<see cref="HeroDressing"/>). Editor only; allocates.
    /// </summary>
    internal sealed class HeroBasinDressing
    {
        private const int Seg = 0;
        private const string Label = "Dressing";

        private readonly LookTestBuildContext _ctx;
        private readonly HeroBasinConfigAsset _h;
        private readonly HeroDressing _d;
        private readonly HeroDressingPass _pass;
        private readonly bool _portrait;
        private readonly HeroBasinWorld _world;
        private readonly Camera _camera;
        private readonly List<string> _report;
        private readonly IRandom _rng;
        private readonly Dictionary<Collider, Kind> _kinds = new Dictionary<Collider, Kind>();
        private readonly List<GameObject> _temp = new List<GameObject>();
        private readonly List<Mesh> _tempMeshes = new List<Mesh>();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private Vector3 _eye;

        /// <summary>What a temporary collider stands for.</summary>
        public enum Kind
        {
            None,
            Arch,
            Rock,
            Ground,
            Water,

            /// <summary>Smooth mossy outcrop mounds (they read as lawn): covered like ground.</summary>
            Mound,
        }

        public HeroBasinDressing(LookTestBuildContext ctx, HeroBasinConfigAsset h, HeroBasinWorld world, Camera camera, List<string> report)
            : this(ctx, h, world, camera, report, false)
        {
        }

        /// <param name="portrait">
        /// True: the second, foreground-only pass through the portrait camera (framing plants, undergrowth, ground
        /// sweep of <see cref="HeroDressing.Portrait"/>); the camera must already be posed for P1.
        /// </param>
        public HeroBasinDressing(LookTestBuildContext ctx, HeroBasinConfigAsset h, HeroBasinWorld world, Camera camera, List<string> report, bool portrait)
        {
            _ctx = ctx;
            _h = h;
            _d = h.Dressing;
            _world = world;
            _camera = camera;
            _report = report;
            _portrait = portrait;
            _pass = (portrait ? _d.Portrait : _d.Landscape) ?? (portrait ? HeroDressingPass.PortraitDefault() : HeroDressingPass.LandscapeDefault());
            _rng = new Pcg32Random((ulong)(uint)_d.Seed, portrait ? 98UL : 99UL);
        }

        private LookTestBatchSet B => _ctx.Batches;

        public void Build()
        {
            _eye = _camera.transform.position;
            MakeColliders();
            try
            {
                if (!_portrait)
                {
                    // v4: the painted arch card carries its own overgrowth and curtains.
                    if (_world.Cards?.Mouth == null)
                    {
                        ArchOvergrowth();
                        ArchCurtains();
                    }

                    Islands();
                    Rims();
                    Shelves();
                    Cobbles();
                    CascadeBrinks();
                }

                Framing();
                Undergrowth();
                if (!_portrait)
                {
                    CornerLeaves();
                    Canopy(_d.CanopyLeft, _d.CanopyLeftRegion, "canopy left");
                    Canopy(_d.CanopyRight, _d.CanopyRightRegion, "canopy right");
                }

                LawnSweep();
                if (!_portrait)
                {
                    GodRays();
                }
            }
            finally
            {
                for (int i = 0; i < _temp.Count; i++)
                {
                    Object.DestroyImmediate(_temp[i]);
                }

                for (int i = 0; i < _tempMeshes.Count; i++)
                {
                    Object.DestroyImmediate(_tempMeshes[i]);
                }
            }

            var text = new System.Text.StringBuilder((_portrait ? "Dressing, portrait pass (seed " : "Dressing (seed ") + _d.Seed + "):");
            foreach (KeyValuePair<string, int> pair in _counts)
            {
                text.Append(' ').Append(pair.Key).Append(' ').Append(pair.Value).Append(',');
            }

            _report.Add(text.ToString().TrimEnd(','));
        }

        // ---------------------------------------------------------------- Screen helpers (pure parts unit-tested)

        /// <summary>Metres covered by one full frame width at view depth <paramref name="depth"/>.</summary>
        public static float FrameWidthM(float verticalFovDeg, float aspect, float depth)
        {
            return 2f * Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad) * aspect * depth;
        }

        /// <summary>Distance between two screen points in frame-width units (v scaled by the frame's height/width).</summary>
        public static float ScreenDistanceU(Vector2 a, Vector2 b, float aspect)
        {
            float du = a.x - b.x;
            float dv = (a.y - b.y) / Mathf.Max(0.01f, aspect);
            return Mathf.Sqrt(du * du + dv * dv);
        }

        /// <summary>
        /// Stratified jittered samples in a screen region (uMin, vMin, uMax, vMax): a grid of about
        /// <paramref name="count"/> cells, one random point per cell, in seeded random order.
        /// </summary>
        public static List<Vector2> Stratified(IRandom rng, Vector4 region, int count, float aspect)
        {
            float w = Mathf.Max(0.001f, region.z - region.x);
            float hgt = Mathf.Max(0.001f, (region.w - region.y) / Mathf.Max(0.01f, aspect));
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(count * w / hgt)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
            var points = new List<Vector2>(cols * rows);
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    float u = Mathf.Lerp(region.x, region.z, (i + rng.NextFloat(0.1f, 0.9f)) / cols);
                    float v = Mathf.Lerp(region.y, region.w, (j + rng.NextFloat(0.1f, 0.9f)) / rows);
                    points.Add(new Vector2(u, v));
                }
            }

            for (int i = points.Count - 1; i > 0; i--)
            {
                int k = rng.NextInt(0, i + 1);
                Vector2 t = points[i];
                points[i] = points[k];
                points[k] = t;
            }

            return points;
        }

        private float Aspect => _camera.aspect;

        private float MetresPerU(Vector3 world)
        {
            float depth = Vector3.Dot(world - _eye, _camera.transform.forward);
            return FrameWidthM(_camera.fieldOfView, Aspect, Mathf.Max(0.3f, depth));
        }

        private float MetresPerV(Vector3 world)
        {
            return MetresPerU(world) / Aspect;
        }

        private Ray ScreenRay(Vector2 uv)
        {
            return _camera.ViewportPointToRay(new Vector3(uv.x, 1f - uv.y, 0f));
        }

        private Vector2 ToScreen(Vector3 world, out bool inFront)
        {
            Vector3 p = _camera.WorldToViewportPoint(world);
            inFront = p.z > 0.1f;
            return new Vector2(p.x, 1f - p.y);
        }

        private bool Cast(Vector2 uv, out RaycastHit hit, out Kind kind)
        {
            kind = Kind.None;
            if (!Physics.Raycast(ScreenRay(uv), out hit, 5000f))
            {
                return false;
            }

            kind = _kinds.TryGetValue(hit.collider, out Kind k) ? k : Kind.None;
            return true;
        }

        private bool Visible(Vector3 point, Kind expected)
        {
            Vector3 to = point - _eye;
            float distance = to.magnitude;
            if (!Physics.Raycast(_eye, to / distance, out RaycastHit hit, distance + 2f))
            {
                return true;
            }

            return hit.distance > distance - Mathf.Max(1.5f, distance * 0.02f) && _kinds.TryGetValue(hit.collider, out Kind k) && k == expected;
        }

        private void Count(string what, int n = 1)
        {
            _counts.TryGetValue(what, out int c);
            _counts[what] = c + n;
        }

        // ---------------------------------------------------------------- Colliders

        private void MakeColliders()
        {
            B.ForEach((segment, layer, label, material, acc) =>
            {
                if (segment != Seg)
                {
                    return;
                }

                Kind kind = KindOf(layer, label, material);
                if (kind == Kind.None)
                {
                    return;
                }

                Mesh mesh = acc.ToMesh("DressingCollider_" + label);
                _tempMeshes.Add(mesh);
                var go = new GameObject("DressingCollider " + label) { hideFlags = HideFlags.HideAndDontSave };
                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                _kinds[collider] = kind;
                _temp.Add(go);
            });
            Physics.SyncTransforms();
        }

        private Kind KindOf(string layer, string label, Material material)
        {
            if (material == _ctx.Pool && label == "Pools")
            {
                return Kind.Water;
            }

            if (label == "Ledge" && material != null && material.name.Contains("Outcrop"))
            {
                return Kind.Mound;
            }

            switch (label)
            {
                case "Terrain":
                    return Kind.Ground;
                case "Arch":
                case "Arch card":
                    return layer == "L2" ? Kind.Arch : Kind.None;
                case "Ledge":
                case "Stones":
                case "Travertine":
                case "Terraces":
                case "Terrace rims":
                case "Pillars":
                    return Kind.Rock;
                default:
                    return Kind.None;
            }
        }

        // ---------------------------------------------------------------- Kit helpers

        private List<EnvironmentKit.Piece> Kit(EnvironmentRole role, string nameContains)
        {
            List<EnvironmentKit.Piece> list = _ctx.Kit.Of(role);
            list.RemoveAll(p => (nameContains != null && p.Name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                || (p.Material == null && (p.Parts.Count == 0 || p.Parts[0].SlotMaterials == null)));
            return list;
        }

        /// <summary>Stands a kit piece at <paramref name="foot"/>, <paramref name="widthM"/> wide, turned to the camera.</summary>
        private void PlaceKit(EnvironmentKit.Piece piece, Vector3 foot, float widthM, float yawJitter, string layer, LookTestBatchSet.Group group)
        {
            Bounds b = piece.Bounds;
            float height = widthM * b.size.y / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            Vector3 toEye = _eye - foot;
            float yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg + _rng.NextFloat(-yawJitter, yawJitter);
            _ctx.AppendPiece(piece, EnvironmentKit.Stand(piece, foot, yaw, height, 0f), null, Seg, layer, Label, false, group, HeroBasinPlants.Open);
        }

        private LookTestMeshAccumulator Rocks => B.Get(Seg, "L0", Label + " rocks", _ctx.Boulder, false, LookTestBatchSet.Group.Rocks);

        /// <summary>A soft lozenge boulder <paramref name="widthM"/> wide (mossy dome from the boulder material).</summary>
        private void Boulder(Vector3 centre, float widthM, float flatten, int subdivisions)
        {
            Mesh mesh = LookTestMeshFactory.Boulder(_rng, 1f, subdivisions);
            float s = widthM / 2.4f;
            Rocks.Append(mesh, Matrix4x4.TRS(centre, Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f), new Vector3(s, s * flatten, s)), _ctx.OpenWet);
        }

        /// <summary>Shrub cap or fern on a rock top: painted crown cards far away, fern / palm-fern clumps near.</summary>
        private void Shrub(Vector3 foot, float widthM, string what, bool dome = false)
        {
            float distance = Vector3.Distance(foot, _eye);
            List<EnvironmentKit.Piece> pool = dome || distance > 25f ? Kit(EnvironmentRole.CanopyCrown, null) : Kit(EnvironmentRole.PlantClump, _rng.Chance(0.5f) ? "PalmFern" : "Fern_P");
            if (pool.Count == 0)
            {
                pool = Kit(EnvironmentRole.CanopyCrown, null);
            }

            if (pool.Count == 0)
            {
                return;
            }

            EnvironmentKit.Piece piece = pool[_rng.NextInt(0, pool.Count)];
            bool crown = piece.Role == EnvironmentRole.CanopyCrown;
            Bounds b = piece.Bounds;
            // Low domes, not little trees: crowns squashed to ~0.6 of their height (footprint kept).
            float height = widthM * b.size.y / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)) * (crown ? 0.62f : 1f);
            Vector3 at = foot - Vector3.up * height * (crown ? 0.2f : 0.05f);
            Vector3 toEye = _eye - at;
            float yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg + _rng.NextFloat(-30f, 30f);
            _ctx.AppendPiece(piece, EnvironmentKit.Stand(piece, at, yaw, height, widthM * 0.5f), null, Seg, crown ? "L3" : "L1", Label, false,
                crown ? LookTestBatchSet.Group.Trees : LookTestBatchSet.Group.Plants, HeroBasinPlants.Open);
            Count(what);
        }

        // ---------------------------------------------------------------- 1. Arch overgrowth

        private struct Spot
        {
            public Vector3 World;
            public Vector3 Normal;
            public Vector2 Screen;
        }

        private List<Spot> ArchSpots(System.Func<Vector3, bool> normalTest, int step)
        {
            var spots = new List<Spot>();
            EnvironmentKit.Piece piece = _ctx.Kit.Of(EnvironmentRole.HeroArch).Count > 0 ? _ctx.Kit.Of(EnvironmentRole.HeroArch)[0] : null;
            if (piece == null)
            {
                return spots;
            }

            List<Matrix4x4> placements = _world.ArchPlacements.Count > 0
                ? _world.ArchPlacements
                : new List<Matrix4x4> { EnvironmentKit.Span(piece, _h.ArchFootA, _h.ArchFootB, _h.ArchTopY, _h.ArchDepthScale) };
            foreach (Matrix4x4 placement in placements)
            {
                foreach (EnvironmentKit.Part part in piece.Parts)
                {
                    Matrix4x4 m = placement * part.Matrix;
                    Matrix4x4 nm = m.inverse.transpose;
                    Vector3[] vertices = part.Mesh.vertices;
                    Vector3[] normals = part.Mesh.normals;
                    for (int i = 0; i < vertices.Length && i < normals.Length; i += step)
                    {
                        Vector3 n = nm.MultiplyVector(normals[i]).normalized;
                        if (!normalTest(n))
                        {
                            continue;
                        }

                        Vector3 w = m.MultiplyPoint3x4(vertices[i]);
                        Vector2 s = ToScreen(w, out bool front);
                        if (!front || s.x < -0.02f || s.x > 1.02f || s.y < -0.02f || s.y > 1f || !Visible(w + n * 0.5f, Kind.Arch))
                        {
                            continue;
                        }

                        spots.Add(new Spot { World = w, Normal = n, Screen = s });
                    }
                }
            }

            return spots;
        }

        private void ArchOvergrowth()
        {
            List<EnvironmentKit.Piece> crowns = Kit(EnvironmentRole.CanopyCrown, null);
            if (crowns.Count == 0)
            {
                var why = new System.Text.StringBuilder();
                foreach (EnvironmentKit.Piece p in _ctx.Kit.Of(EnvironmentRole.CanopyCrown))
                {
                    why.Append(' ').Append(p.Name).Append(" material=").Append(p.Material != null ? p.Material.name : "null").Append(" albedo=")
                        .Append(p.Textures.Albedo != null ? p.Textures.Albedo.name : "null").Append(" parts=").Append(p.Parts.Count);
                }

                _report.Add("Dressing: no painted canopy crowns, arch overgrowth skipped." + why);
                return;
            }

            List<Spot> tops = ArchSpots(n => n.y > _d.ArchTopNormalY, 3);
            // Densest on the crown knot and the shoulders (top of frame): weight toward small v.
            var keys = new float[tops.Count];
            for (int i = 0; i < tops.Count; i++)
            {
                keys[i] = _rng.NextFloat(0f, 1f) * (0.45f + tops[i].Screen.y);
            }

            var order = new List<int>(tops.Count);
            for (int i = 0; i < tops.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => keys[a].CompareTo(keys[b]));
            var placed = new List<Vector2>();
            int large = Mathf.RoundToInt(_d.ArchClumps * 0.15f);
            int medium = Mathf.RoundToInt(_d.ArchClumps * 0.3f);
            foreach (int index in order)
            {
                if (placed.Count >= _d.ArchClumps)
                {
                    break;
                }

                Spot s = tops[index];
                int n = placed.Count;
                float sizeU = n < large ? _d.ArchClumpSizeU.x : n < large + medium ? _d.ArchClumpSizeU.y : _d.ArchClumpSizeU.z;
                bool tooClose = false;
                for (int k = 0; k < placed.Count && !tooClose; k++)
                {
                    tooClose = ScreenDistanceU(placed[k], s.Screen, Aspect) < Mathf.Max(_d.ArchClumpSpacingU, sizeU * 0.55f);
                }

                if (tooClose)
                {
                    continue;
                }

                float width = sizeU * MetresPerU(s.World) * _rng.NextFloat(0.85f, 1.15f);
                EnvironmentKit.Piece piece = crowns[_rng.NextInt(0, crowns.Count)];
                Bounds b = piece.Bounds;
                float height = width * b.size.y / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
                // Sunk into the strand so it reads as growth on the top, spilling over the edge.
                PlaceKit(piece, s.World - Vector3.up * height * 0.3f, width, 25f, "L3", LookTestBatchSet.Group.Trees);
                placed.Add(s.Screen);
            }

            Count("arch clumps", placed.Count);
            _report.Add("Dressing: arch top candidates " + tops.Count + ", clumps " + placed.Count + ".");
        }

        private void ArchCurtains()
        {
            List<EnvironmentKit.Piece> vines = _ctx.Kit.Of(EnvironmentRole.ArchVines);
            if (vines.Count == 0 || vines[0].Material == null)
            {
                return;
            }

            Material material = vines[0].Material;
            List<Rect> cells = AtlasCells(material.GetTexture("_BaseMap") as Texture2D, 0.02f);
            if (cells.Count == 0)
            {
                _report.Add("Dressing: vine atlas cells not found, curtains skipped.");
                return;
            }

            // Undersides and outward side faces: curtains hang from strand edges.
            List<Spot> spots = ArchSpots(n => n.y < 0.1f, 5);
            LookTestMeshAccumulator target = B.Get(Seg, "L1", "Arch vines", material, false, LookTestBatchSet.Group.Plants);
            var anchors = new List<Vector2>();
            int groups = Mathf.CeilToInt(_d.ArchCurtains / 3f);
            int tries = 0;
            while (anchors.Count < groups && tries++ < 4000 && spots.Count > 0)
            {
                Spot s = spots[_rng.NextInt(0, spots.Count)];
                // The tall fall keeps a cleared band.
                if (s.Screen.x > _d.FallClearU.x && s.Screen.x < _d.FallClearU.y)
                {
                    continue;
                }

                bool tooClose = false;
                for (int k = 0; k < anchors.Count && !tooClose; k++)
                {
                    tooClose = ScreenDistanceU(anchors[k], s.Screen, Aspect) < 0.06f;
                }

                if (tooClose)
                {
                    continue;
                }

                anchors.Add(s.Screen);
                Vector3 side = Vector3.Cross(Vector3.up, (_eye - s.World).normalized).normalized;
                float mpu = MetresPerU(s.World);
                for (int k = 0; k < 3 && anchors.Count * 3 - 3 + k < _d.ArchCurtains; k++)
                {
                    Rect cell = cells[_rng.NextInt(0, cells.Count)];
                    float length = _rng.NextFloat(_d.ArchCurtainLengthV.x, _d.ArchCurtainLengthV.y) * MetresPerV(s.World);
                    Vector3 top = s.World + side * _rng.NextFloat(-0.012f, 0.012f) * mpu + (_eye - s.World).normalized * 1.5f;
                    target.Append(Curtain(cell, length, top), Matrix4x4.identity, null);
                    Count("curtains");
                }
            }
        }

        /// <summary>A hanging card (atlas cell top at <paramref name="top"/>), facing the camera, slightly bowed toward it.</summary>
        private Mesh Curtain(Rect cell, float length, Vector3 top)
        {
            const int rows = 6;
            float width = length * cell.width / Mathf.Max(0.01f, cell.height);
            Vector3 toEye = _eye - top;
            toEye.y = 0f;
            toEye.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toEye).normalized;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var indices = new List<int>();
            for (int j = 0; j <= rows; j++)
            {
                float t = (float)j / rows;
                Vector3 c = top - Vector3.up * length * t + toEye * length * 0.08f * Mathf.Sin(Mathf.PI * t);
                float taper = Mathf.Lerp(1f, 0.7f, t);
                for (int i = 0; i < 2; i++)
                {
                    float s = i - 0.5f;
                    vertices.Add(c + side * s * width * taper);
                    normals.Add((toEye + Vector3.up * 0.4f).normalized);
                    uvs.Add(new Vector2(Mathf.Lerp(cell.xMin, cell.xMax, i), Mathf.Lerp(cell.yMax, cell.yMin, t)));
                    colors.Add(new Color(t * t * 0.6f, 0f, 0f, Mathf.Lerp(0.7f, 1f, t)));
                }

                if (j > 0)
                {
                    int a = (j - 1) * 2;
                    indices.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }

            var mesh = new Mesh { name = "Curtain" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateTangents();
            _tempMeshes.Add(mesh);
            return mesh;
        }

        /// <summary>Bounding boxes (UV) of the opaque islands of an atlas, from its source PNG (alpha above 0.5).</summary>
        public static List<Rect> AtlasCells(Texture2D atlas, float minAreaShare)
        {
            var cells = new List<Rect>();
            string path = atlas != null ? AssetDatabase.GetAssetPath(atlas) : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return cells;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(path));
            const int n = 128;
            var mask = new bool[n, n];
            Color32[] pixels = source.GetPixels32();
            int w = source.width;
            int hgt = source.height;
            for (int y = 0; y < hgt; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a > 127)
                    {
                        mask[x * n / w, y * n / hgt] = true;
                    }
                }
            }

            Object.DestroyImmediate(source);
            return Islands(mask, n, minAreaShare);
        }

        /// <summary>Connected islands of a square mask as UV rects (pure; unit-tested).</summary>
        public static List<Rect> Islands(bool[,] mask, int n, float minAreaShare)
        {
            var cells = new List<Rect>();
            var seen = new bool[n, n];
            var stack = new Stack<Vector2Int>();
            for (int sy = 0; sy < n; sy++)
            {
                for (int sx = 0; sx < n; sx++)
                {
                    if (!mask[sx, sy] || seen[sx, sy])
                    {
                        continue;
                    }

                    int minX = sx, maxX = sx, minY = sy, maxY = sy, area = 0;
                    stack.Push(new Vector2Int(sx, sy));
                    seen[sx, sy] = true;
                    while (stack.Count > 0)
                    {
                        Vector2Int p = stack.Pop();
                        area++;
                        minX = Mathf.Min(minX, p.x);
                        maxX = Mathf.Max(maxX, p.x);
                        minY = Mathf.Min(minY, p.y);
                        maxY = Mathf.Max(maxY, p.y);
                        for (int k = 0; k < 4; k++)
                        {
                            int x = p.x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                            int y = p.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            if (x >= 0 && y >= 0 && x < n && y < n && mask[x, y] && !seen[x, y])
                            {
                                seen[x, y] = true;
                                stack.Push(new Vector2Int(x, y));
                            }
                        }
                    }

                    float box = (maxX - minX + 1) * (maxY - minY + 1) / (float)(n * n);
                    if (box >= minAreaShare)
                    {
                        cells.Add(Rect.MinMaxRect(minX / (float)n, minY / (float)n, (maxX + 1) / (float)n, (maxY + 1) / (float)n));
                    }
                }
            }

            return cells;
        }

        // ---------------------------------------------------------------- 2. Rock islands

        private readonly List<Vector4> _islands = new List<Vector4>();

        private void Islands()
        {
            LookTestMeshAccumulator foam = B.Get(Seg, "W", "Pools", _ctx.Pool, false, LookTestBatchSet.Group.Water);
            List<Vector2> samples = Stratified(_rng, _d.IslandRegion, _d.Islands * 6, Aspect);
            int large = Mathf.RoundToInt(_d.Islands * 0.25f);
            int medium = Mathf.RoundToInt(_d.Islands * 0.4f);
            foreach (Vector2 uv in samples)
            {
                if (_islands.Count >= _d.Islands)
                {
                    break;
                }

                if (!Cast(uv, out RaycastHit hit, out Kind kind) || kind != Kind.Water)
                {
                    continue;
                }

                int n = _islands.Count;
                float sizeU = n < large ? _d.IslandSizeU.x : n < large + medium ? _d.IslandSizeU.y : _d.IslandSizeU.z;
                float width = sizeU * MetresPerU(hit.point) * _rng.NextFloat(0.8f, 1.2f);
                bool overlap = false;
                foreach (Vector4 other in _islands)
                {
                    overlap |= Vector2.Distance(new Vector2(other.x, other.z), new Vector2(hit.point.x, hit.point.z)) < (other.w + width) * 0.5f;
                }

                if (overlap)
                {
                    continue;
                }

                float water = hit.point.y;
                float flatten = _rng.NextFloat(0.75f, 1.0f);
                Boulder(new Vector3(hit.point.x, water - 0.12f * width * flatten, hit.point.z), width, flatten, width > 3f ? 2 : 1);
                // Two satellite stones break the island's outline.
                for (int k = 0; k < 2; k++)
                {
                    float a = _rng.NextFloat(0f, 6.283f);
                    float w2 = width * _rng.NextFloat(0.3f, 0.5f);
                    Vector3 p = hit.point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * width * 0.45f;
                    Boulder(new Vector3(p.x, water - 0.15f * w2, p.z), w2, _rng.NextFloat(0.5f, 0.7f), 1);
                }

                foam.Append(FoamRing(width * 0.42f, width * 0.85f), Matrix4x4.Translate(new Vector3(hit.point.x, water + 0.04f, hit.point.z)), null);
                if (_rng.Chance(_d.IslandShrubShare))
                {
                    float top = water + width * flatten * 0.17f;
                    // Moss / shrub domes, not ferns: the islands read as rock with growth on top (F4_f).
                    Shrub(new Vector3(hit.point.x, top, hit.point.z), width * _rng.NextFloat(0.45f, 0.7f), "island shrubs", true);
                }

                _islands.Add(new Vector4(hit.point.x, water, hit.point.z, width));
            }

            Count("islands", _islands.Count);
            Spillways(foam);
        }

        /// <summary>
        /// Spillways between neighbouring islands: a mossy rock sill rising a real step above the water, a short
        /// fall pouring over its front lip toward the camera into a foam pool, and a little upper pool on the sill
        /// (F4_f: water stepping down between the rock islands, not flat foam bands).
        /// </summary>
        private void Spillways(LookTestMeshAccumulator foam)
        {
            int made = 0;
            for (int i = 0; i < _islands.Count && made < _d.Spillways; i++)
            {
                for (int k = i + 1; k < _islands.Count && made < _d.Spillways; k++)
                {
                    Vector4 a = _islands[i];
                    Vector4 b = _islands[k];
                    float gap = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                    if (gap > (a.w + b.w) * 1.1f || Mathf.Abs(a.y - b.y) > 0.3f)
                    {
                        continue;
                    }

                    Vector3 mid = new Vector3((a.x + b.x) * 0.5f, a.y, (a.z + b.z) * 0.5f);
                    Vector3 toEye = _eye - mid;
                    toEye.y = 0f;
                    toEye.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, toEye);
                    float width = Mathf.Clamp(gap - (a.w + b.w) * 0.3f, 1.2f, 0.5f * (a.w + b.w));
                    float drop = Mathf.Max(0.8f, width * _d.SpillwayDropShare);
                    Drop(mid, side, toEye, width, drop, foam);
                    made++;
                }
            }

            Count("spillway drops", made);
        }

        private void Drop(Vector3 waterAt, Vector3 side, Vector3 toEye, float width, float drop, LookTestMeshAccumulator pools)
        {
            float water = waterAt.y;
            float sill = width * _d.SpillwaySillShare;
            // The sill: a wide flat rock behind the lip, its top a step above the water, and a cheek rock either side.
            Vector3 back = waterAt - toEye * sill * 0.55f;
            // Boulder half height = width * flatten / 2: the dome's crest sits at the step height.
            const float flatten = 0.35f;
            Boulder(new Vector3(back.x, water + drop - 0.5f * width * 1.5f * flatten, back.z), width * 1.5f, flatten, 2);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 cheek = waterAt + side * s * width * _rng.NextFloat(0.55f, 0.7f) - toEye * sill * 0.2f;
                float w = sill * _rng.NextFloat(0.8f, 1.1f);
                Boulder(new Vector3(cheek.x, water + drop * 0.6f - 0.2f * w, cheek.z), w, _rng.NextFloat(0.7f, 0.9f), 1);
                if (_rng.Chance(0.5f))
                {
                    Shrub(new Vector3(cheek.x, water + drop * 0.6f + 0.25f * w, cheek.z), w * 0.8f, "spillway shrubs", true);
                }
            }

            // Upper pool on the sill, then the fall over the lip toward the camera.
            pools.Append(LookTestMeshFactory.Pool(width * 0.5f, 0.6f, "SpillPool"), Matrix4x4.Translate(new Vector3(back.x, water + drop + 0.05f, back.z) + toEye * sill * 0.2f), null);
            Vector3 lip = waterAt + Vector3.up * (drop + 0.04f) - toEye * sill * 0.05f;
            _world.ShortFall(lip, water, width * 0.8f, Mathf.Clamp(drop * 0.35f, 0.3f, 2.5f), 2);
        }

        /// <summary>A flat foam ring on the water: R (foam) 1 at the inner radius fading to 0 outside, A fading out.</summary>
        private Mesh FoamRing(float inner, float outer)
        {
            const int sides = 28;
            const int rings = 3;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float t = (float)j / rings;
                float r = Mathf.Lerp(inner, outer, t);
                for (int k = 0; k <= sides; k++)
                {
                    float a = 2f * Mathf.PI * k / sides;
                    float wobble = 1f + 0.12f * Mathf.Sin(a * 3f + inner) + 0.06f * Mathf.Sin(a * 7f);
                    var p = new Vector3(Mathf.Cos(a) * r * wobble, 0f, Mathf.Sin(a) * r * wobble);
                    vertices.Add(p);
                    uvs.Add(new Vector2(p.x, p.z));
                    colors.Add(new Color(1f - LookTestMath.Smooth(0.1f, 0.85f, t), 0.15f, 0f, 1f - LookTestMath.Smooth(0.5f, 1f, t)));
                }
            }

            for (int j = 0; j < rings; j++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = j * (sides + 1) + k;
                    int b = a + sides + 1;
                    indices.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }

            return FlatMesh("FoamRing", vertices, uvs, colors, indices);
        }

        /// <summary>A tapered foam streak (local +z toward the camera), R high in the core.</summary>
        private Mesh FoamStreak(float width, float length)
        {
            const int rows = 6;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            for (int j = 0; j <= rows; j++)
            {
                float t = (float)j / rows;
                float half = width * 0.5f * Mathf.Lerp(0.8f, 1.3f, t);
                for (int i = 0; i < 3; i++)
                {
                    float s = i - 1f;
                    var p = new Vector3(s * half, 0f, (t - 0.3f) * length);
                    vertices.Add(p);
                    uvs.Add(new Vector2(p.x, p.z));
                    float core = 1f - Mathf.Abs(s);
                    colors.Add(new Color(Mathf.Clamp01(core * (1f - t * 0.8f) + 0.2f), 0.1f, 0f, (1f - Mathf.Abs(s) * 0.9f) * (1f - LookTestMath.Smooth(0.7f, 1f, t))));
                }

                if (j > 0)
                {
                    int a = (j - 1) * 3;
                    indices.AddRange(new[] { a, a + 3, a + 1, a + 1, a + 3, a + 4, a + 1, a + 4, a + 2, a + 2, a + 4, a + 5 });
                }
            }

            return FlatMesh("FoamStreak", vertices, uvs, colors, indices);
        }

        private Mesh FlatMesh(string name, List<Vector3> vertices, List<Vector2> uvs, List<Color> colors, List<int> indices)
        {
            // Wind each triangle counter-clockwise seen from above (Unity front face = clockwise from the viewer above).
            for (int i = 0; i < indices.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
                if (n.y < 0f)
                {
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                }
            }

            var normals = new List<Vector3>(vertices.Count);
            var tangents = new List<Vector4>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                normals.Add(Vector3.up);
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            _tempMeshes.Add(mesh);
            return mesh;
        }

        // ---------------------------------------------------------------- 3. Pool rims and the cascade shelf

        private void Rims()
        {
            int boulders = 0;
            foreach (Vector3[] lip in _world.Lips)
            {
                float carry = _rng.NextFloat(0f, 1f);
                for (int i = 0; i + 1 < lip.Length; i++)
                {
                    Vector3 a = lip[i];
                    Vector3 b = lip[i + 1];
                    float length = Vector3.Distance(a, b);
                    float steps = length * _d.RimBouldersPerM;
                    for (float t = carry; t < steps; t += 1f)
                    {
                        Vector3 p = Vector3.Lerp(a, b, t / Mathf.Max(0.001f, steps));
                        carry = t + 1f - steps;
                        if (InNotch(p))
                        {
                            continue;
                        }

                        float size = _rng.NextFloat(_d.RimBoulderSizeM.x, _d.RimBoulderSizeM.y);
                        Vector3 toEye = _eye - p;
                        toEye.y = 0f;
                        toEye.Normalize();
                        // Straddle the crest and lean over the drop face toward the camera: no straight rim line left.
                        Vector3 c = p + toEye * size * 0.25f + Vector3.up * (-0.15f * size + _rng.NextFloat(-0.15f, 0.1f));
                        Boulder(c, size, _rng.NextFloat(0.6f, 0.85f), Vector3.Distance(c, _eye) < 18f ? 2 : 1);
                        boulders++;
                        if (_rng.Chance(_d.RimPlantsPerBoulder))
                        {
                            Shrub(c - toEye * size * 0.35f + Vector3.up * size * 0.18f, size * _rng.NextFloat(0.9f, 1.5f), "rim plants");
                        }
                    }
                }
            }

            Count("rim boulders", boulders);
        }

        private bool InNotch(Vector3 p)
        {
            foreach (KeyValuePair<Vector3, Vector3> notch in _world.Notches)
            {
                Vector3 ab = notch.Value - notch.Key;
                float t = Mathf.Clamp01(Vector3.Dot(p - notch.Key, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                if (Vector3.Distance(p, notch.Key + ab * t) < 0.6f)
                {
                    return true;
                }
            }

            return false;
        }

        private void Shelves()
        {
            List<Vector4> shelves = _world.Shelves;
            if (shelves.Count == 0)
            {
                return;
            }

            int per = Mathf.Max(10, _d.ShelfBoulders / shelves.Count);
            int boulders = 0;
            foreach (Vector4 shelf in shelves)
            {
                var centre = new Vector3(shelf.x, shelf.y, shelf.z);
                for (int k = 0; k < per; k++)
                {
                    float a = (k + _rng.NextFloat(-0.3f, 0.3f)) / per * 2f * Mathf.PI;
                    float ring = k % 3 == 2 ? _rng.NextFloat(0.78f, 0.88f) : _rng.NextFloat(0.94f, 1.04f);
                    Vector3 rim = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * shelf.w * ring;
                    if (OnCascade(rim))
                    {
                        continue;
                    }

                    float size = shelf.w * _rng.NextFloat(0.16f, 0.3f);
                    Boulder(rim + Vector3.up * (-0.1f * size), size, _rng.NextFloat(0.55f, 0.8f), 1);
                    boulders++;
                    if (_rng.Chance(_d.ShelfShrubShare))
                    {
                        Shrub(rim + Vector3.up * size * 0.25f, size * _rng.NextFloat(1.0f, 1.6f), "shelf shrubs", true);
                    }
                }
            }

            Count("shelf boulders", boulders);
        }

        /// <summary>True when a rim point sits on a wide cascade's lip (keep the falls clear).</summary>
        private bool OnCascade(Vector3 rim)
        {
            foreach (Vector4 lip in _world.CascadeLips)
            {
                if (Vector2.Distance(new Vector2(rim.x, rim.z), new Vector2(lip.x, lip.z)) < lip.w * 0.42f)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- 4. Ledge cobbles and undergrowth

        /// <summary>Cobbled ledge under Pista: flat stones set into the ledge top (path stone, moss only in the cracks).</summary>
        private void Cobbles()
        {
            Vector3 pista = _h.PistaPosition;
            int cobbles = 0;
            for (int i = 0; i < _d.Cobbles * 4 && cobbles < _d.Cobbles; i++)
            {
                float a = _rng.NextFloat(0f, 6.283f);
                float r = Mathf.Sqrt(_rng.NextFloat(0.0f, 1f)) * _d.CobbleRadiusM;
                var p = new Vector3(pista.x + Mathf.Cos(a) * r, pista.y + 4f, pista.z + Mathf.Sin(a) * r);
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 10f) || hit.normal.y < 0.7f || hit.point.y < pista.y - 1.2f)
                {
                    continue;
                }

                Cobble(hit.point, hit.normal, _rng.NextFloat(_d.CobbleSizeM.x, _d.CobbleSizeM.y));
                cobbles++;
            }

            Count("cobbles", cobbles);
        }

        private void Cobble(Vector3 point, Vector3 normal, float size)
        {
            LookTestMeshAccumulator stones = B.Get(Seg, "L0", Label + " cobbles", _ctx.Rootstone, false, LookTestBatchSet.Group.Rocks);
            Mesh stone = LookTestMeshFactory.Boulder(_rng, 1f, 1);
            float s = size / 2.4f;
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f);
            var scale = new Vector3(s * _rng.NextFloat(0.8f, 1.2f), s * _rng.NextFloat(0.18f, 0.3f), s);
            stones.Append(stone, Matrix4x4.TRS(point + normal * 0.02f, tilt, scale), LookTestBuildContext.Open);
        }

        /// <summary>Plant clumps for the ground passes: low ferns, palm ferns and painted shrub clumps, mixed.</summary>
        private EnvironmentKit.Piece GroundPlant(float widthM, float distance)
        {
            List<EnvironmentKit.Piece> low = Kit(EnvironmentRole.PlantClump, "Fern_P");
            low.RemoveAll(p => p.Name.IndexOf("PalmFern", System.StringComparison.OrdinalIgnoreCase) >= 0);
            List<EnvironmentKit.Piece> tall = Kit(EnvironmentRole.PlantClump, "PalmFern");
            List<EnvironmentKit.Piece> shrub = Kit(EnvironmentRole.PlantClump, "Canopy_P");
            if (distance > 25f)
            {
                // Far ground: painted shrub domes (24 tris each) read the same as fern clumps at that size.
                List<EnvironmentKit.Piece> domes = Kit(EnvironmentRole.CanopyCrown, null);
                if (domes.Count > 0)
                {
                    return domes[_rng.NextInt(0, domes.Count)];
                }
            }

            float roll = _rng.NextFloat(0f, 1f);
            List<EnvironmentKit.Piece> pool = distance > 9f && widthM > 1.6f
                ? (roll < 0.5f ? tall : roll < 0.8f ? shrub : low)
                : (roll < 0.45f ? low : roll < 0.75f ? tall : shrub);
            if (pool.Count == 0)
            {
                pool = low.Count > 0 ? low : tall.Count > 0 ? tall : shrub;
            }

            return pool.Count > 0 ? pool[_rng.NextInt(0, pool.Count)] : null;
        }

        private bool InClearBand(Vector2 uv)
        {
            return uv.x > _pass.ClearBandU.x && uv.x < _pass.ClearBandU.y;
        }

        private bool NearPista(Vector2 uv, Vector2 pistaScreen, float halfWidthU)
        {
            return Mathf.Abs(uv.x - pistaScreen.x) < halfWidthU && uv.y > pistaScreen.y - 0.4f;
        }

        /// <summary>
        /// Undergrowth at the sides of the frame only (the centre band keeps the water in view past the ledge, F4_f):
        /// mixed species, sizes and turns so no two neighbours repeat.
        /// </summary>
        private void Undergrowth()
        {
            Vector3 pista = _h.PistaPosition;
            Vector2 pistaScreen = ToScreen(pista + Vector3.up * 0.9f, out _);
            var placed = new List<Vector2>();
            foreach (Vector2 uv in Stratified(_rng, _pass.UndergrowthRegion, _pass.UndergrowthClumps * 8, Aspect))
            {
                if (placed.Count >= _pass.UndergrowthClumps)
                {
                    break;
                }

                if (InClearBand(uv) || NearPista(uv, pistaScreen, 0.06f))
                {
                    continue;
                }

                if (!Cast(uv, out RaycastHit hit, out Kind kind) || (kind != Kind.Ground && kind != Kind.Rock && kind != Kind.Mound) || hit.normal.y < 0.35f
                    || hit.distance > _pass.MaxDistanceM)
                {
                    continue;
                }

                Vector2 flat = new Vector2(hit.point.x - pista.x, hit.point.z - pista.z);
                if (flat.magnitude < _d.PistaClearRadiusM || hit.distance < 0.8f)
                {
                    continue;
                }

                bool tooClose = false;
                foreach (Vector2 q in placed)
                {
                    tooClose |= ScreenDistanceU(q, uv, Aspect) < 0.04f;
                }

                if (tooClose)
                {
                    continue;
                }

                float width = Mathf.Clamp(_rng.NextFloat(_pass.UndergrowthSizeU.x, _pass.UndergrowthSizeU.y) * MetresPerU(hit.point), 0.5f, 6f);
                EnvironmentKit.Piece piece = GroundPlant(width, hit.distance);
                if (piece == null)
                {
                    return;
                }

                PlaceKit(piece, hit.point - Vector3.up * 0.08f, width, 180f, "L1", LookTestBatchSet.Group.Plants);
                placed.Add(uv);
            }

            Count("undergrowth", placed.Count);
            _undergrowth.AddRange(placed);
        }

        private readonly List<Vector2> _undergrowth = new List<Vector2>();
        private readonly List<Vector3> _sweep = new List<Vector3>();

        /// <summary>
        /// Last pass (0% lawn, map s2): a fine screen grid over the lower frame; wherever a ray still lands on bare
        /// terrain or a smooth outcrop mound with nothing near it on screen, the side bands get a plant clump and the
        /// centre band a low mossy rock cluster (keeps the view to the water open).
        /// </summary>
        private void LawnSweep()
        {
            if (_pass.SweepItems <= 0)
            {
                return;
            }

            Vector3 pista = _h.PistaPosition;
            Vector2 pistaScreen = ToScreen(pista + Vector3.up * 0.9f, out _);
            int plants = 0;
            int rocks = 0;
            foreach (Vector2 q in _undergrowth)
            {
                _sweep.Add(new Vector3(q.x, q.y, 0.012f));
            }

            // Nearest ground first (bottom of the frame): the lawn that matters is the mound in front of the lens.
            List<Vector2> samples = Stratified(_rng, new Vector4(0f, _pass.SweepTopV, 1f, 1f), 2600, Aspect);
            samples.Sort((a, b) => b.y.CompareTo(a.y));
            foreach (Vector2 uv in samples)
            {
                if (plants + rocks >= _pass.SweepItems)
                {
                    break;
                }

                if (NearPista(uv, pistaScreen, 0.05f))
                {
                    continue;
                }

                // Beyond the pass's plant distance (ground the landscape frame shares) only low rock goes down.
                if (!Cast(uv, out RaycastHit hit, out Kind kind) || (kind != Kind.Ground && kind != Kind.Mound) || hit.distance < 0.9f || hit.distance > 3f * _pass.MaxDistanceM)
                {
                    continue;
                }

                if (new Vector2(hit.point.x - pista.x, hit.point.z - pista.z).magnitude < _d.PistaClearRadiusM)
                {
                    // Under and around her feet: a flat path stone, never a plant.
                    if (kind == Kind.Ground && !_portrait)
                    {
                        Cobble(hit.point, hit.normal, _rng.NextFloat(_d.CobbleSizeM.x, _d.CobbleSizeM.y));
                        _sweep.Add(new Vector3(uv.x, uv.y, 0.01f));
                    }

                    continue;
                }

                bool covered = false;
                foreach (Vector3 q in _sweep)
                {
                    covered |= ScreenDistanceU(q, uv, Aspect) < q.z;
                }

                if (covered)
                {
                    continue;
                }

                float mpu = MetresPerU(hit.point);
                if (InClearBand(uv) || hit.distance > _pass.MaxDistanceM)
                {
                    // Low mossy rock: a flat stone with one or two smaller ones, hugging the mound.
                    float width = Mathf.Clamp(_rng.NextFloat(0.04f, 0.07f) * mpu, 0.6f, 9f);
                    Quaternion tilt = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    Boulder(hit.point - hit.normal * width * 0.08f, width, _rng.NextFloat(0.35f, 0.55f), hit.distance < 12f ? 2 : 1);
                    for (int k = 0; k < (hit.distance > 20f ? 0 : 2); k++)
                    {
                        Vector3 offset = tilt * new Vector3(_rng.NextFloat(-0.6f, 0.6f), 0f, _rng.NextFloat(-0.6f, 0.6f)) * width;
                        Boulder(hit.point + offset, width * _rng.NextFloat(0.35f, 0.55f), _rng.NextFloat(0.4f, 0.7f), 1);
                    }

                    _sweep.Add(new Vector3(uv.x, uv.y, Mathf.Max(0.012f, 0.42f * width / mpu)));
                    rocks++;
                }
                else
                {
                    bool near = hit.distance < 6f;
                    float width = Mathf.Clamp(_rng.NextFloat(_pass.SweepSizeU.x, _pass.SweepSizeU.y) * mpu, near ? 1.3f : 0.5f, 12f);
                    EnvironmentKit.Piece piece = GroundPlant(width, hit.distance);
                    if (piece == null)
                    {
                        continue;
                    }

                    PlaceKit(piece, hit.point - Vector3.up * 0.08f, width, 180f, "L1", LookTestBatchSet.Group.Plants);
                    // A clump hides ground within about a fifth of its screen width of its foot.
                    _sweep.Add(new Vector3(uv.x, uv.y, Mathf.Max(0.01f, 0.2f * width / mpu)));
                    plants++;
                }
            }

            Count("sweep plants", plants);
            Count("sweep rock clusters", rocks);
        }

        /// <summary>Rocks along the brink of each wide cascade: the lip breaks up between the strands (no straight edge).</summary>
        private void CascadeBrinks()
        {
            int made = 0;
            foreach (Vector4 lip in _world.CascadeLips)
            {
                var brink = new Vector3(lip.x, lip.y, lip.z);
                Vector3 toEye = _eye - brink;
                toEye.y = 0f;
                toEye.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, toEye);
                int count = Mathf.Max(4, Mathf.RoundToInt(lip.w / 2.2f));
                for (int k = 0; k <= count; k++)
                {
                    float t = k / (float)count - 0.5f + _rng.NextFloat(-0.03f, 0.03f);
                    float size = lip.w * _rng.NextFloat(0.06f, 0.1f);
                    Vector3 c = brink + side * t * lip.w * 1.05f - toEye * size * 0.3f + Vector3.up * (-0.05f * size);
                    Boulder(c, size, _rng.NextFloat(0.6f, 0.85f), 2);
                    made++;
                }
            }

            Count("cascade brink rocks", made);
        }

        // ---------------------------------------------------------------- 5. Framing plants

        /// <summary>Hero framing plants of the pass (a few large clumps, each species, size and turn set per plant).</summary>
        private void Framing()
        {
            HeroFramePlant[] frames = _pass.Frames ?? new HeroFramePlant[0];
            foreach (HeroFramePlant f in frames)
            {
                if (f.SizeU <= 0f || string.IsNullOrEmpty(f.Piece))
                {
                    continue;
                }

                List<EnvironmentKit.Piece> pieces = Kit(EnvironmentRole.PlantClump, f.Piece);
                Vector3 at;
                bool floating = f.DepthM > 0f;
                if (floating)
                {
                    Ray ray = ScreenRay(f.Uv);
                    at = ray.origin + ray.direction * f.DepthM;
                }
                else if (Cast(f.Uv, out RaycastHit hit, out Kind kind) && kind != Kind.None && kind != Kind.Arch && hit.distance <= _pass.MaxDistanceM)
                {
                    at = hit.point;
                }
                else
                {
                    at = Vector3.zero;
                    pieces.Clear();
                }

                if (pieces.Count == 0)
                {
                    _report.Add("Dressing: framing " + f.Piece + " not placed (no kit piece or no ground at " + f.Uv + ").");
                    continue;
                }

                EnvironmentKit.Piece piece = pieces[_rng.NextInt(0, pieces.Count)];
                Bounds b = piece.Bounds;
                float width = f.SizeU * MetresPerU(at);
                float height = width * b.size.y / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
                Vector3 toEye = _eye - at;
                float yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg + f.YawDeg + _rng.NextFloat(-10f, 10f);
                // On the ray: the clump's lower half hangs below the frame edge.
                Vector3 foot = floating ? at - Vector3.up * height * 0.55f : at - Vector3.up * 0.1f;
                _ctx.AppendPiece(piece, EnvironmentKit.Stand(piece, foot, yaw, height, 0f), null, Seg, "L1", Label, false,
                    LookTestBatchSet.Group.Plants, HeroBasinPlants.Open);
                Vector2 uv = ToScreen(at, out _);
                // Lens-near clumps on the ray stand in front of the ground, not on it: they don't count as cover.
                if (!floating)
                {
                    _sweep.Add(new Vector3(uv.x, uv.y, f.SizeU * 0.15f));
                }
                Count("framing " + f.Piece);
            }
        }

        /// <summary>Backlit leaves hanging into the top-left corner, leaving gaps over the sun.</summary>
        private void CornerLeaves()
        {
            List<EnvironmentKit.Piece> frame = Kit(EnvironmentRole.PlantClump, "FrameLeft_P");
            if (frame.Count == 0 || frame[0].Parts[0].SlotMaterials == null || frame[0].Parts[0].SlotMaterials[0] == null)
            {
                return;
            }

            Material leaf = frame[0].Parts[0].SlotMaterials[0];
            List<Rect> cells = AtlasCells(leaf.GetTexture("_BaseMap") as Texture2D, 0.02f);
            if (cells.Count == 0)
            {
                return;
            }

            Vector2 sun = ToScreen(_eye - _h.SunLightDirection.normalized * 1000f, out _);
            LookTestMeshAccumulator target = B.Get(Seg, "L1", Label + " corner", leaf, false, LookTestBatchSet.Group.Plants);
            int made = 0;
            for (int i = 0; i < _d.CornerLeaves * 6 && made < _d.CornerLeaves; i++)
            {
                var uv = new Vector2(_rng.NextFloat(-0.03f, 0.12f), _rng.NextFloat(-0.05f, 0.22f));
                if (ScreenDistanceU(uv, sun, Aspect) < 0.085f)
                {
                    continue;
                }

                float depth = _rng.NextFloat(2.5f, 5f);
                Ray ray = ScreenRay(uv);
                Vector3 p = ray.origin + ray.direction * depth;
                float length = _rng.NextFloat(0.045f, 0.08f) * FrameWidthM(_camera.fieldOfView, Aspect, depth);
                Rect cell = cells[_rng.NextInt(0, cells.Count)];
                // Stem up-left out of frame, blade hanging down-right toward the light.
                Mesh card = HeroBasinPlants.Card(cell, length, _rng.NextFloat(-70f, -35f), 0.12f, 0.12f);
                float yaw = Mathf.Atan2(_camera.transform.right.x, _camera.transform.right.z) * Mathf.Rad2Deg + _rng.NextFloat(-40f, 30f);
                Vector3 stem = p - Vector3.up * -length * 0.4f - _camera.transform.right * length * 0.3f;
                target.Append(card, Matrix4x4.TRS(stem, Quaternion.Euler(0f, yaw, _rng.NextFloat(-15f, 15f)), Vector3.one), HeroBasinPlants.Open);
                made++;
            }

            Count("corner leaves", made);
        }

        // ---------------------------------------------------------------- 6. Mid canopy puffs

        private void Canopy(int count, Vector4 region, string what)
        {
            List<EnvironmentKit.Piece> crowns = Kit(EnvironmentRole.CanopyCrown, null);
            if (crowns.Count == 0 || count <= 0)
            {
                return;
            }

            var placed = new List<Vector2>();
            foreach (Vector2 uv in Stratified(_rng, region, count * 3, Aspect))
            {
                if (placed.Count >= count)
                {
                    break;
                }

                if (!Cast(uv, out RaycastHit hit, out Kind kind) || (kind != Kind.Ground && kind != Kind.Rock) || hit.distance < 12f)
                {
                    continue;
                }

                bool tooClose = false;
                foreach (Vector2 q in placed)
                {
                    tooClose |= ScreenDistanceU(q, uv, Aspect) < 0.04f;
                }

                if (tooClose)
                {
                    continue;
                }

                float width = Mathf.Clamp(_rng.NextFloat(_d.CanopySizeU.x, _d.CanopySizeU.y) * MetresPerU(hit.point), 3f, 45f);
                EnvironmentKit.Piece piece = crowns[_rng.NextInt(0, crowns.Count)];
                Bounds b = piece.Bounds;
                float height = width * b.size.y / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
                if (_world.Cards != null && _world.Cards.InWindow(hit.point + Vector3.up * height * 0.75f))
                {
                    // v4: the arch window stays clear for the painted fall.
                    continue;
                }

                PlaceKit(piece, hit.point - Vector3.up * height * 0.25f, width, 25f, "L3", LookTestBatchSet.Group.Trees);
                placed.Add(uv);
            }

            Count(what, placed.Count);
        }

        // ---------------------------------------------------------------- 7. God rays

        private void GodRays()
        {
            Vector2[] ends = _d.GodRayEnds;
            if (ends == null || ends.Length == 0 || _ctx.LightShaft == null)
            {
                return;
            }

            // Screen-space fan: each card runs from its end point to the sun's ray at the same distance, so it lies
            // across the view (a card along the sun direction is seen end-on near the sun and the shader fades it).
            Vector3 toSun = -_h.SunLightDirection.normalized;
            LookTestMeshAccumulator target = B.Get(Seg, "W", "Shafts", _ctx.LightShaft, false, LookTestBatchSet.Group.Water);
            for (int i = 0; i < ends.Length; i++)
            {
                Ray ray = ScreenRay(ends[i]);
                float distance = _d.GodRayDistanceM * _rng.NextFloat(0.9f, 1.15f);
                Vector3 end = ray.origin + ray.direction * distance;
                Vector3 source = _eye + toSun * distance;
                Vector3 axis = source - end;
                float length = axis.magnitude * 1.1f;
                float width = _rng.NextFloat(_d.GodRayWidthU.x, _d.GodRayWidthU.y) * MetresPerU(end);
                target.AppendCard((end + source) * 0.5f, -axis.normalized, width, length, _rng.NextFloat(0f, 10f));
            }

            Count("god rays", ends.Length);
        }
    }
}
