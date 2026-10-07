using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Development aid (editor and development builds only; the bootstrap does not add it to release builds).
    /// <list type="bullet">
    /// <item>F3 toggles an overlay that draws every simulation hitbox as a semi-transparent box: obstacle boxes (red),
    /// active lane strikes (orange) and strike telegraphs (faint yellow), ravine gaps (magenta slab), vine grab points
    /// (cyan) and HERO's own box (green). A red box with nothing drawn inside it is a view/simulation mismatch.</item>
    /// <item>One Console line per stumble or death naming the obstacle id, kind, lanes, z and whether a view piece with
    /// an enabled renderer was drawn for it ("NOT DRAWN" is logged as a warning).</item>
    /// </list>
    /// Pooled, no allocations per frame while the overlay is off, and none while it is on. Materials are clones of the
    /// primitive material the <see cref="GrayBoxKit"/> uses (no shader lookup by name).
    /// </summary>
    public sealed class HitboxDebugView : MonoBehaviour, IRunView
    {
        private const int Capacity = 96;
        private const float BehindM = 6f;
        private const float AheadM = 70f;
        private const float PathMarginM = 0.6f;
        private const float GapSlabHeightM = 0.12f;
        private const float VineMarkerM = 0.6f;

        private const int CatObstacle = 0;
        private const int CatStrikeActive = 1;
        private const int CatStrikeTelegraph = 2;
        private const int CatGap = 3;
        private const int CatVine = 4;
        private const int CatHero = 5;
        private const int CatCount = 6;

        private static readonly Color[] Colors =
        {
            new Color(1f, 0.1f, 0.1f, 0.40f),
            new Color(1f, 0.55f, 0.05f, 0.45f),
            new Color(1f, 0.95f, 0.1f, 0.12f),
            new Color(1f, 0.1f, 1f, 0.45f),
            new Color(0.1f, 0.95f, 1f, 0.55f),
            new Color(0.1f, 1f, 0.2f, 0.30f),
        };

        private RunnerConfig _runnerConfig;
        private ObstacleView _obstacleView;
        private HazardView _hazardView;
        private TrackSimulation _track;
        private Transform[] _boxes;
        private MeshRenderer[] _renderers;
        private int[] _categories;
        private Material[] _materials;
        private int _used;
        private int _shown;
        private bool _overlay;
        private float _pathHalfWidthM;

        private bool _pending;
        private RunnerEventType _pendingType;
        private int _pendingId;
        private ObstacleArchetype _pendingArchetype;
        private byte _pendingLane;
        private long _pendingTick;
        private short _pendingValue;

        /// <summary>True while the hitbox overlay is on.</summary>
        public bool OverlayEnabled => _overlay;

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, ObstacleView obstacleView, HazardView hazardView)
        {
            _runnerConfig = runnerConfig;
            _obstacleView = obstacleView;
            _hazardView = hazardView;
            _pathHalfWidthM = runnerConfig.LaneCount * runnerConfig.LaneWidthM * 0.5f + PathMarginM;
            _boxes = new Transform[Capacity];
            _renderers = new MeshRenderer[Capacity];
            _categories = new int[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                GameObject box = kit.Create(PrimitiveType.Cube, "DebugBox" + i, transform, Color.white);
                _boxes[i] = box.transform;
                _renderers[i] = box.GetComponent<MeshRenderer>();
                _categories[i] = -1;
                box.SetActive(false);
            }

            // Clone the kit's primitive material (matches the active pipeline) and make each clone see-through.
            Material template = _renderers[0].sharedMaterial;
            _materials = new Material[CatCount];
            for (int c = 0; c < CatCount; c++)
            {
                _materials[c] = CreateTransparent(template, Colors[c], c);
            }
        }

        private void OnDestroy()
        {
            if (_materials == null)
            {
                return;
            }

            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null)
                {
                    Destroy(_materials[i]);
                }
            }
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            _pending = false;
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            if (e.Type == RunnerEventType.Died || e.Type == RunnerEventType.Stumbled)
            {
                _pending = true;
                _pendingType = e.Type;
                _pendingId = e.EntityId;
                _pendingArchetype = (ObstacleArchetype)e.Archetype;
                _pendingLane = e.Lane;
                _pendingTick = e.Tick;
                _pendingValue = e.Value;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_boxes == null || !Debug.isDebugBuild)
            {
                return;
            }

            if (TogglePressed())
            {
                _overlay = !_overlay;
                Debug.Log("[JungleBooze] Hitbox overlay " + (_overlay ? "ON" : "OFF") + " (F3).");
            }

            _used = 0;
            if (_overlay && _track != null)
            {
                DrawOverlay(session);
            }

            for (int i = _used; i < _shown; i++)
            {
                _boxes[i].gameObject.SetActive(false);
            }

            _shown = _used;

            if (_pending)
            {
                _pending = false;
                LogPending();
            }
        }

        private static bool TogglePressed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f3Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.F3);
#else
            return false;
#endif
#else
            return false;
#endif
        }

        private void DrawOverlay(GameSession session)
        {
            RunnerSimulation runner = session.Runner;
            RunnerState hero = runner.Current;
            double minZ = hero.Z - BehindM;
            double maxZ = hero.Z + AheadM;
            ObstacleKitConfig kit = _track.Kit;

            // HERO's box exactly as the collision code sees it (current simulation state, not interpolated).
            float heroH = hero.HitboxHeight > 0f ? hero.HitboxHeight : runner.Config.StandingHeightM;
            Add(CatHero, hero.X, hero.Y + heroH * 0.5f, (float)hero.Z, runner.Config.PlayerHitboxWidthM, heroH, runner.Config.PlayerHitboxDepthM);

            int count = _track.ObstacleCount;
            for (int i = 0; i < count; i++)
            {
                ref readonly ObstacleInstance o = ref _track.GetObstacle(i);
                if (o.Z > maxZ)
                {
                    break;
                }

                if (o.BackZ < minZ && !(o.Archetype == ObstacleArchetype.Gap && o.Z + o.GapLengthM >= minZ))
                {
                    continue;
                }

                if (o.Archetype == ObstacleArchetype.Gap)
                {
                    DrawGap(in o);
                    continue;
                }

                ObstacleShape shape = kit.GetShape(o.Archetype);
                float height = shape.TopM - shape.BottomM;
                float cy = shape.BottomM + height * 0.5f;
                float depth = o.DepthM > 0f ? o.DepthM : shape.DepthM;
                float cz = (float)o.Z + depth * 0.5f;
                if (o.Archetype == ObstacleArchetype.Mover)
                {
                    Add(CatObstacle, o.MoverX, cy, cz, shape.WidthM, height, depth);
                    continue;
                }

                int category = CatObstacle;
                if (o.Archetype == ObstacleArchetype.LaneStrike)
                {
                    category = o.StrikePhase == LaneStrikePhase.Active ? CatStrikeActive : CatStrikeTelegraph;
                }

                for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
                {
                    if (LaneMasks.Contains(o.LaneMask, lane))
                    {
                        Add(category, _runnerConfig.LaneCenterX(lane), cy, cz, shape.WidthM, height, depth);
                    }
                }
            }

            float grabY = _track.VineConfig.GrabPointHeightM;
            int vines = _track.VineCount;
            for (int i = 0; i < vines; i++)
            {
                ref readonly VineInstance v = ref _track.GetVine(i);
                if (v.Z > maxZ)
                {
                    break;
                }

                if (v.Z >= minZ)
                {
                    Add(CatVine, _runnerConfig.LaneCenterX(v.Lane), grabY, (float)v.Z, VineMarkerM, VineMarkerM, VineMarkerM);
                }
            }
        }

        private void DrawGap(in ObstacleInstance o)
        {
            float laneHalf = _runnerConfig.LaneWidthM * 0.5f;
            int lo = LaneMasks.Lowest(o.LaneMask);
            int hi = LaneMasks.Highest(o.LaneMask);
            float x0 = lo == 0 ? -_pathHalfWidthM : _runnerConfig.LaneCenterX(lo) - laneHalf;
            float x1 = hi == LaneMasks.LaneCount - 1 ? _pathHalfWidthM : _runnerConfig.LaneCenterX(hi) + laneHalf;
            Add(CatGap, (x0 + x1) * 0.5f, GapSlabHeightM * 0.5f + 0.05f, (float)o.Z + o.GapLengthM * 0.5f, x1 - x0, GapSlabHeightM, o.GapLengthM);
        }

        private void Add(int category, float cx, float cy, float cz, float sx, float sy, float sz)
        {
            if (_used >= Capacity)
            {
                return;
            }

            Transform t = _boxes[_used];
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
            }

            t.localPosition = new Vector3(cx, cy, cz);
            t.localScale = new Vector3(sx, sy, sz);
            if (_categories[_used] != category)
            {
                _categories[_used] = category;
                _renderers[_used].sharedMaterial = _materials[category];
            }

            _used++;
        }

        private void LogPending()
        {
            string what = _pendingType == RunnerEventType.Died ? "DIED" : "STUMBLED";
            string cause = _pendingType == RunnerEventType.Died ? " cause=" + (DeathCause)_pendingValue : string.Empty;
            if (_pendingId == 0 || _track == null)
            {
                Debug.Log("[JungleBooze] " + what + " tick " + _pendingTick + cause + " lane " + _pendingLane
                    + ": no obstacle (fall or other cause), kind " + _pendingArchetype + ".");
                return;
            }

            int index = -1;
            int count = _track.ObstacleCount;
            for (int i = 0; i < count; i++)
            {
                if (_track.GetObstacle(i).Id == _pendingId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Debug.LogWarning("[JungleBooze] " + what + " tick " + _pendingTick + cause + ": obstacle id " + _pendingId + " kind "
                    + _pendingArchetype + " is no longer in the simulation ring (smashed or cleared the same frame).");
                return;
            }

            ref readonly ObstacleInstance o = ref _track.GetObstacle(index);
            bool drawn = (_obstacleView != null && _obstacleView.IsDrawn(o.Id)) || (_hazardView != null && _hazardView.IsDrawn(o.Id));
            string lanes = o.Archetype == ObstacleArchetype.Mover ? "start " + o.FromLane + " -> " + o.ToLane + " x=" + o.MoverX.ToString("F2") : "mask " + o.LaneMask + " (" + o.FromLane + ".." + o.ToLane + ")";
            string line = "[JungleBooze] " + what + " tick " + _pendingTick + cause + ": obstacle id " + o.Id + ", kind " + o.Archetype
                + ", lanes " + lanes + ", z " + o.Z.ToString("F2") + " depth " + o.DepthM.ToString("F2")
                + ", runner lane " + _pendingLane + ", visible renderer: " + (drawn ? "yes" : "NO (NOT DRAWN: view/simulation mismatch)");
            if (drawn)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogWarning(line);
            }
        }

        private static Material CreateTransparent(Material template, Color color, int category)
        {
            var material = new Material(template) { name = "DebugHitbox_" + category };
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.color = color;
            return material;
        }
    }
}
