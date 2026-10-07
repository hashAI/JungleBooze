using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The stateless scenery beside the path (spec 003 section 11, task T8): trees, thin trunks, bushes, ferns, rocks,
    /// roots, hanging vines and light shafts in 10 m cells, placed by <see cref="SceneryPlacer"/> from
    /// (run seed, Scenery stream, cell index, band), strictly outside the sight corridor, oriented through the
    /// <see cref="PathFrame"/> so they follow bends and slopes.
    /// <para>Drawing: no GameObjects per prop. Every piece is a matrix in a preallocated array of its model part; one
    /// <c>Graphics.RenderMeshInstanced</c> call per (part, LOD) draws all of them (about 25 draw calls for everything).
    /// Cells are placed once when they enter the window and the arrays are refilled only when HERO crosses into a new
    /// cell (about every 0.5 s), nearest first, under the hard caps of <see cref="SceneryBudget"/>. Per-frame work is
    /// the draw calls only: no allocation.</para>
    /// <para>LOD: pieces within <see cref="ScenerySettings.LodNearM"/> of HERO draw the real art (or the gray-box
    /// stand-in); farther ones draw a generated low-poly mesh fitted to the same bounds.</para>
    /// <para>Art slots (through <see cref="EnvironmentArt"/>): Foliage_TreeA/TreeB, Tree_Trunk, Foliage_Bush,
    /// Foliage_FernCluster, Prop_Rock, Prop_Root, Vine_Liana. A missing or invisible slot falls back to a generated
    /// gray-box shape. Light shafts are always generated.</para>
    /// </summary>
    public sealed class ScenerySystem : MonoBehaviour, IRunView
    {
        private static readonly string[] ArtNames =
        {
            EnvironmentArt.TreeA, EnvironmentArt.TreeB, EnvironmentArt.TreeTrunk, EnvironmentArt.Bush, EnvironmentArt.FernCluster,
            EnvironmentArt.Rock, EnvironmentArt.Root, EnvironmentArt.VineCreeper, null,
        };

        private static readonly SceneryShape[] Shapes =
        {
            SceneryShape.Tree, SceneryShape.Tree, SceneryShape.Trunk, SceneryShape.Blob, SceneryShape.Blob,
            SceneryShape.Blob, SceneryShape.Root, SceneryShape.Vine, SceneryShape.Shaft,
        };

        // Nominal bounds (centre, size) of the gray-box stand-ins; they match the sizes the placer assumes at scale 1.
        private static readonly Bounds[] NominalBounds =
        {
            new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(4.4f, 5f, 4.4f)),
            new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(4.4f, 5f, 4.4f)),
            new Bounds(new Vector3(0f, 18.7f, 0f), new Vector3(7f, 37.4f, 7f)),
            new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 1.2f, 1.8f)),
            new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1f, 1.4f)),
            new Bounds(new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1.4f)),
            new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.6f, 0.6f)),
            new Bounds(new Vector3(0f, -3f, 0f), new Vector3(0.5f, 6f, 0.5f)),
            new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0f)),
        };

        // Flat colours of the generated meshes (no hazard red; the style guide's bark, leaf and stone family).
        private static readonly Color Bark = new Color32(0x6B, 0x4A, 0x32, 0xFF);
        private static readonly Color Leaf = StylePalette.JungleGreen;
        private static readonly Color FernLeaf = new Color32(0x5F, 0xA8, 0x45, 0xFF);
        private static readonly Color Stone = new Color32(0x8A, 0x7B, 0x68, 0xFF);

        /// <summary>The scenery vines are dimmed so the one live grab vine stays the brightest strand (art plan section 1).</summary>
        private static readonly Color VineDim = new Color(0.95f, 0.97f, 0.95f, 1f);

        private static readonly Vector3 DrawBoundsSize = new Vector3(600f, 600f, 600f);

        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly Dictionary<Material, Material> _instanced = new Dictionary<Material, Material>();
        private readonly Dictionary<Color, Material> _flat = new Dictionary<Color, Material>();

        private PathFrame _frame;
        private IRouteChunkSource _worlds;
        private ScenerySettings _settings;
        private SceneryModelSet[] _sets;
        private SceneryPart[] _allParts;
        private Texture2D _shaftTexture;

        // Placed cells, a ring indexed by cell index modulo its size.
        private int _ringSize;
        private int _cellsBehind;
        private int _cellsAhead;
        private long[] _cellIndex;
        private int[] _cellCount;
        private SceneryPiece[][] _cellPieces;
        private Matrix4x4[][] _cellMatrices;

        private bool _ready;
        private bool _dirty;
        private long _heroCell = long.MinValue;
        private Matrix4x4 _parentMatrix;
        private Vector3 _origin;
        private Bounds _drawBounds;
        private SceneryBudget _budget;
        private int _drawCalls;

        /// <summary>True when the system is on and has something to draw.</summary>
        public bool IsActive => _ready;

        /// <summary>Triangles drawn by the last rebuild (all LODs).</summary>
        public int ActiveTriangles => _budget.Triangles;

        public int ActivePieces => _budget.Pieces;

        /// <summary>Pieces the caps refused in the last rebuild (the farthest ones).</summary>
        public int DroppedByBudget => _budget.Dropped;

        /// <summary>Draw calls issued by the last frame.</summary>
        public int ActiveDrawCalls => _drawCalls;

        /// <summary>Builds the models, meshes and arrays. <paramref name="worlds"/> may be null (always Jungle). Setup time only.</summary>
        public void Init(PathFrame frame, IRouteChunkSource worlds, ScenerySettings settings)
        {
            _frame = PathPlacement.OrIdentity(frame);
            _worlds = worlds;
            _settings = settings ?? ScenerySettings.LoadOrDefault();
            _ready = false;
            if (!_settings.Enabled)
            {
                return;
            }

            if (!BuildSets())
            {
                Debug.LogWarning("[JungleBooze] Scenery: no model could be built (missing material template?); the scenery is off.");
                return;
            }

            float length = Mathf.Max(1f, _settings.CellLengthM);
            _cellsBehind = Mathf.CeilToInt(_settings.WindowBehindM / length);
            _cellsAhead = Mathf.CeilToInt(_settings.WindowAheadM / length);
            _ringSize = _cellsBehind + _cellsAhead + 3;
            _cellIndex = new long[_ringSize];
            _cellCount = new int[_ringSize];
            _cellPieces = new SceneryPiece[_ringSize][];
            _cellMatrices = new Matrix4x4[_ringSize][];
            for (int i = 0; i < _ringSize; i++)
            {
                _cellPieces[i] = new SceneryPiece[_settings.MaxPiecesPerCell];
                _cellMatrices[i] = new Matrix4x4[_settings.MaxPiecesPerCell];
            }

            _parentMatrix = transform.localToWorldMatrix;
            _origin = _frame.OriginOffset;
            InvalidateCells();
            _ready = true;
            LogSummary();
        }

        public void BeginRun(GameSession session)
        {
            if (!_ready)
            {
                return;
            }

            InvalidateCells();
            _heroCell = long.MinValue;
            _origin = _frame.OriginOffset;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (!_ready)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double z);

            Vector3 origin = _frame.OriginOffset;
            if (origin != _origin)
            {
                // Floating origin moved the world: every stored matrix is stale.
                _origin = origin;
                InvalidateCells();
            }

            Matrix4x4 parent = transform.localToWorldMatrix;
            if (parent != _parentMatrix)
            {
                _parentMatrix = parent;
                _dirty = true;
            }

            long heroCell = (long)System.Math.Floor(z / _settings.CellLengthM);
            if (heroCell != _heroCell || _dirty)
            {
                _heroCell = heroCell;
                _dirty = false;
                Rebuild(heroCell);
            }

            _frame.Sample(z, out PathPose pose);
            _drawBounds = new Bounds(pose.Center, DrawBoundsSize);
            Draw();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null)
                {
                    Destroy(_materials[i]);
                }
            }

            for (int i = 0; i < _meshes.Count; i++)
            {
                if (_meshes[i] != null)
                {
                    Destroy(_meshes[i]);
                }
            }

            if (_shaftTexture != null)
            {
                Destroy(_shaftTexture);
            }
        }

        // ---- Cells ----

        private void InvalidateCells()
        {
            if (_cellIndex == null)
            {
                return;
            }

            for (int i = 0; i < _cellIndex.Length; i++)
            {
                _cellIndex[i] = long.MinValue;
                _cellCount[i] = 0;
            }

            _dirty = true;
        }

        private int SlotOf(long cell)
        {
            return (int)(((cell % _ringSize) + _ringSize) % _ringSize);
        }

        /// <summary>Places <paramref name="cell"/> if it is not placed yet. False when the route is not built far enough yet (retried next frame).</summary>
        private bool EnsureCell(long cell)
        {
            int slot = SlotOf(cell);
            if (_cellIndex[slot] == cell)
            {
                return true;
            }

            float length = _settings.CellLengthM;
            double start = cell * (double)length;
            double mid = start + (length * 0.5);
            if (!_frame.IsBuilt(start + length + 5.0))
            {
                return false;
            }

            _frame.Sample(mid, out PathPose pose);
            var context = new SceneryCellContext
            {
                Curvature = StrongestCurvature(start, length),
                Beat = pose.Beat,
                Layer = pose.Layer,
                WorldIndex = _worlds != null ? (int)_worlds.WorldKindAt(mid) : 0,
            };

            SceneryPiece[] pieces = _cellPieces[slot];
            Matrix4x4[] matrices = _cellMatrices[slot];
            int count = SceneryPlacer.PlaceCell(_frame.RunSeed, RandomStreamIds.Scenery, cell, in context, _settings, pieces);
            for (int i = 0; i < count; i++)
            {
                matrices[i] = BuildMatrix(in pieces[i]);
            }

            _cellCount[slot] = count;
            _cellIndex[slot] = cell;
            return true;
        }

        /// <summary>Signed curvature with the largest magnitude at five points from 5 m before the cell to 5 m after it.</summary>
        private float StrongestCurvature(double start, float length)
        {
            float best = 0f;
            for (int i = 0; i < 5; i++)
            {
                double s = start + (i == 0 ? -5.0 : (i == 1 ? 0.0 : (i == 2 ? length * 0.5 : (i == 3 ? length : length + 5.0))));
                _frame.Sample(s, out PathPose pose);
                if (Mathf.Abs(pose.Curvature) > Mathf.Abs(best))
                {
                    best = pose.Curvature;
                }
            }

            return best;
        }

        private Matrix4x4 BuildMatrix(in SceneryPiece piece)
        {
            _frame.Sample(piece.S, out PathPose pose);
            Vector3 forward = pose.Tangent;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            // Upright-ish: the frame's up blended toward the world up, so trees are planted, not tilted with the bank.
            Vector3 up = Vector3.Slerp(pose.Up, Vector3.up, _settings.UprightBlend);
            Quaternion frameRotation = Quaternion.LookRotation(forward.normalized, up);
            Vector3 position = PathPlacement.Point(pose, piece.X, -_settings.GroundDropM);
            position.y += piece.HeightM;
            Quaternion rotation = frameRotation
                * Quaternion.AngleAxis(piece.YawDeg, Vector3.up)
                * Quaternion.AngleAxis(piece.RollDeg, Vector3.forward);
            return Matrix4x4.TRS(position, rotation, new Vector3(piece.ScaleXz, piece.ScaleY, piece.ScaleXz));
        }

        // ---- Rebuild and draw ----

        private void Rebuild(long heroCell)
        {
            long first = heroCell - _cellsBehind;
            long last = heroCell + _cellsAhead;
            for (long c = first; c <= last; c++)
            {
                if (!EnsureCell(c))
                {
                    _dirty = true;
                }
            }

            for (int i = 0; i < _allParts.Length; i++)
            {
                _allParts[i].Count = 0;
            }

            _budget = default;
            double reference = (heroCell + 0.5) * _settings.CellLengthM;

            // Nearest first, so the caps drop the farthest pieces.
            for (long c = heroCell; c <= last; c++)
            {
                AddCell(c, reference);
            }

            for (long c = heroCell - 1; c >= first; c--)
            {
                AddCell(c, reference);
            }
        }

        private void AddCell(long cell, double reference)
        {
            int slot = SlotOf(cell);
            if (_cellIndex[slot] != cell)
            {
                return;
            }

            SceneryPiece[] pieces = _cellPieces[slot];
            Matrix4x4[] matrices = _cellMatrices[slot];
            int count = _cellCount[slot];
            for (int i = 0; i < count; i++)
            {
                SceneryModel model = pieces[i].Model;
                SceneryModelSet set = _sets[(int)model];
                if (set == null)
                {
                    continue;
                }

                bool near = System.Math.Abs(pieces[i].S - reference) <= _settings.LodNearM;
                if (!_budget.TryTake(model, near ? set.NearTriangles : set.FarTriangles, _settings))
                {
                    continue;
                }

                Matrix4x4 world = _parentMatrix * matrices[i];
                SceneryPart[] parts = near ? set.Near : set.Far;
                for (int p = 0; p < parts.Length; p++)
                {
                    SceneryPart part = parts[p];
                    if (part.Count < part.Instances.Length)
                    {
                        part.Instances[part.Count++] = part.LocalIsIdentity ? world : world * part.Local;
                    }
                }
            }
        }

        private void Draw()
        {
            int calls = 0;
            for (int i = 0; i < _allParts.Length; i++)
            {
                SceneryPart part = _allParts[i];
                if (part.Count == 0)
                {
                    continue;
                }

                part.Params.worldBounds = _drawBounds;
                Graphics.RenderMeshInstanced(part.Params, part.Mesh, part.SubMesh, part.Instances, part.Count);
                calls++;
            }

            _drawCalls = calls;
        }

        // ---- Setup: models ----

        private bool BuildSets()
        {
            _sets = new SceneryModelSet[ScenerySettings.ModelCount];
            var all = new List<SceneryPart>();
            for (int m = 0; m < ScenerySettings.ModelCount; m++)
            {
                _sets[m] = BuildSet(m, all);
            }

            _allParts = all.ToArray();
            return _allParts.Length > 0;
        }

        private SceneryModelSet BuildSet(int model, List<SceneryPart> all)
        {
            SceneryShape shape = Shapes[model];
            Bounds bounds = NominalBounds[model];
            int capacity = _settings.MaxActivePieces;
            var near = new List<SceneryPart>();
            bool art = ArtNames[model] != null
                && TryExtractArt(ArtNames[model], model == (int)SceneryModel.HangingVine, capacity, near, out bounds);
            if (!art)
            {
                bounds = NominalBounds[model];
            }

            Material[] flat = FlatMaterials(model);
            if (flat == null)
            {
                return null;
            }

            if (!art)
            {
                Mesh stand = SceneryMeshes.Build(shape, bounds, shape == SceneryShape.Shaft ? SceneryMeshes.DetailFar : SceneryMeshes.DetailNear);
                _meshes.Add(stand);
                for (int sub = 0; sub < stand.subMeshCount; sub++)
                {
                    near.Add(new SceneryPart(stand, sub, flat[sub], Matrix4x4.identity, capacity));
                }
            }

            SceneryPart[] nearParts = near.ToArray();
            all.AddRange(nearParts);
            if (shape == SceneryShape.Shaft)
            {
                return new SceneryModelSet(nearParts, nearParts);
            }

            Mesh far = SceneryMeshes.Build(shape, bounds, SceneryMeshes.DetailFar);
            _meshes.Add(far);
            var farParts = new SceneryPart[far.subMeshCount];
            for (int sub = 0; sub < farParts.Length; sub++)
            {
                farParts[sub] = new SceneryPart(far, sub, flat[sub], Matrix4x4.identity, capacity);
            }

            all.AddRange(farParts);
            return new SceneryModelSet(nearParts, farParts);
        }

        private Material[] FlatMaterials(int model)
        {
            Material[] result;
            switch ((SceneryModel)model)
            {
                case SceneryModel.TreeA:
                case SceneryModel.TreeB:
                case SceneryModel.GiantTrunk:
                    result = new[] { Flat(Bark), Flat(Leaf) };
                    break;
                case SceneryModel.Bush:
                    result = new[] { Flat(Leaf) };
                    break;
                case SceneryModel.Fern:
                    result = new[] { Flat(FernLeaf) };
                    break;
                case SceneryModel.Rock:
                    result = new[] { Flat(Stone) };
                    break;
                case SceneryModel.Root:
                    result = new[] { Flat(Bark) };
                    break;
                case SceneryModel.HangingVine:
                    result = new[] { Flat(StylePalette.VineRope) };
                    break;
                default:
                    result = new[] { ShaftMaterial() };
                    break;
            }

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == null)
                {
                    return null;
                }
            }

            return result;
        }

        private Material Flat(Color color)
        {
            if (_flat.TryGetValue(color, out Material cached))
            {
                return cached;
            }

            Material material = EnvironmentArt.CreateLitMaterial("Scenery_" + ColorUtility.ToHtmlStringRGB(color), null, color);
            if (material != null)
            {
                _materials.Add(material);
                _flat[color] = material;
            }

            return material;
        }

        private Material ShaftMaterial()
        {
            Material template = RuntimeMaterialTemplates.GetTransparentTemplate();
            if (template == null)
            {
                return null;
            }

            _shaftTexture = BuildShaftTexture();
            var material = new Material(template) { name = "Scenery_Shaft", enableInstancing = true };

            // Art plan section 4: sun gold at 12 to 18 percent, soft, additive. Properties are set only where the template has them.
            Color gold = StylePalette.SunGold;
            gold.a = 0.16f;
            material.color = gold;
            material.mainTexture = _shaftTexture;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", _shaftTexture);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.One);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            _materials.Add(material);
            return material;
        }

        /// <summary>Soft card: alpha fades to zero at the foot, the top and both sides.</summary>
        private static Texture2D BuildShaftTexture()
        {
            const int width = 8;
            const int height = 32;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float alpha = Mathf.Sin(Mathf.PI * v) * Mathf.Sin(Mathf.PI * u);
                    pixels[(y * width) + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Scenery_ShaftGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// Reads the meshes and materials of the art model <paramref name="artName"/> (instantiated once under an
        /// inactive holder, verified visible by <see cref="EnvironmentArt.Attach"/>, then destroyed) into parts.
        /// False when the model is missing or would not draw.
        /// </summary>
        private bool TryExtractArt(string artName, bool dim, int capacity, List<SceneryPart> parts, out Bounds bounds)
        {
            bounds = default;
            if (!EnvironmentArt.Exists(artName))
            {
                return false;
            }

            Transform holder = new GameObject("SceneryProbe_" + artName).transform;
            holder.SetParent(transform, false);
            holder.gameObject.SetActive(false);
            Transform art = EnvironmentArt.Attach(holder, artName);
            if (art == null)
            {
                Destroy(holder.gameObject);
                return false;
            }

            Matrix4x4 toHolder = holder.worldToLocalMatrix;
            MeshRenderer[] renderers = art.GetComponentsInChildren<MeshRenderer>(true);
            bool any = false;
            for (int r = 0; r < renderers.Length; r++)
            {
                MeshFilter filter = renderers[r].GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                Material[] materials = renderers[r].sharedMaterials;
                if (mesh == null || mesh.vertexCount == 0 || materials.Length == 0)
                {
                    continue;
                }

                Matrix4x4 local = toHolder * renderers[r].transform.localToWorldMatrix;
                bool added = false;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material source = materials[Mathf.Min(sub, materials.Length - 1)];
                    if (source == null)
                    {
                        continue;
                    }

                    parts.Add(new SceneryPart(mesh, sub, InstancedCopy(source, dim), local, capacity));
                    added = true;
                }

                if (added)
                {
                    Encapsulate(ref bounds, mesh.bounds, local, !any);
                    any = true;
                }
            }

            Destroy(holder.gameObject);
            if (!any)
            {
                parts.Clear();
            }

            return any;
        }

        private static void Encapsulate(ref Bounds total, Bounds meshBounds, Matrix4x4 local, bool first)
        {
            Vector3 c = meshBounds.center;
            Vector3 e = meshBounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                var p = new Vector3(
                    c.x + ((corner & 1) == 0 ? -e.x : e.x),
                    c.y + ((corner & 2) == 0 ? -e.y : e.y),
                    c.z + ((corner & 4) == 0 ? -e.z : e.z));
                Vector3 q = local.MultiplyPoint3x4(p);
                if (first && corner == 0)
                {
                    total = new Bounds(q, Vector3.zero);
                }
                else
                {
                    total.Encapsulate(q);
                }
            }
        }

        /// <summary>A GPU-instancing copy of an art material (RenderMeshInstanced needs it), shared between parts of one source.</summary>
        private Material InstancedCopy(Material source, bool dim)
        {
            if (!dim && _instanced.TryGetValue(source, out Material cached))
            {
                return cached;
            }

            var copy = new Material(source) { name = source.name + "_Scenery", enableInstancing = true };
            if (dim && copy.HasProperty("_BaseColor"))
            {
                copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * VineDim);
            }

            _materials.Add(copy);
            if (!dim)
            {
                _instanced[source] = copy;
            }

            return copy;
        }

        private void LogSummary()
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            var report = new System.Text.StringBuilder("[JungleBooze] Scenery on. Models (art/stand-in, near tris, far tris): ");
            for (int m = 0; m < ScenerySettings.ModelCount; m++)
            {
                SceneryModelSet set = _sets[m];
                report.Append((SceneryModel)m).Append('=');
                if (set == null)
                {
                    report.Append("none");
                }
                else
                {
                    bool art = ArtNames[m] != null && EnvironmentArt.Exists(ArtNames[m]);
                    report.Append(art ? "art " : "gray ").Append(set.NearTriangles).Append('/').Append(set.FarTriangles);
                }

                report.Append(m < ScenerySettings.ModelCount - 1 ? ", " : ".");
            }

            Debug.Log(report.ToString());
        }
    }
}
