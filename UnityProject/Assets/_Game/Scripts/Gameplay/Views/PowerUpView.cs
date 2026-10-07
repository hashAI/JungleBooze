using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box power-ups (GDD 10, style guide 7.3 and 10):
    /// <list type="bullet">
    /// <item>Pickups: 0.8 m icons with unique silhouettes (Magnet "U", Shield dome, Speed Boost double chevron) in
    /// their cool glow color, a glow disc with an ink rim behind, bobbing 0.15 m at 1 Hz and turning slowly.</item>
    /// <item>On Pista: Shield = a sky-blue bubble cage with highlight arcs (dimmed over a chasm: the shield does not
    /// save from falling, so it does not glow there), flickering in its last second; Magnet = cyan rings pulsing out
    /// from her feet (the pulled coins fly in by themselves); Speed Boost = magenta speed streaks rushing past
    /// (off with Reduce Motion).</item>
    /// <item>Shards: a smashed obstacle bursts into dark shards, a popped shield into sky-blue shards.</item>
    /// </list>
    /// Pooled; no allocations after <see cref="Init"/>.
    /// </summary>
    public sealed class PowerUpView : MonoBehaviour, IRunView
    {
        private const int PickupCapacity = 4;
        private const float BehindM = 4f;
        private const float IconSizeM = 0.8f;
        private const float BobM = 0.15f;
        private const float BobHz = 1f;
        private const float SpinDegPerS = 70f;

        private const int BubbleSegments = 18;
        private const float BubbleRadiusM = 1.0f;
        private const float BubbleCenterM = 0.95f;
        private const float BubbleSpinDegPerS = 40f;
        private const int MagnetSegments = 20;
        private const float MagnetPulseS = 0.8f;
        private const float MagnetMinRadiusM = 0.5f;
        private const float MagnetMaxRadiusM = 1.6f;
        private const int SpeedLineCount = 12;
        private const float SpeedLineLengthM = 2.6f;
        private const float SpeedLineCycleS = 0.35f;
        private const float SpeedLineSpanM = 14f;
        private const float FlickerHz = 8f;

        private const int ShardCapacity = 32;
        private const int ShardsPerBurst = 8;
        private const float ShardLifeS = 0.6f;
        private const float ShardGravity = 14f;
        private const int PendingCapacity = 8;

        private sealed class PickupSlot
        {
            public Transform Root;
            public Transform Icon;
            public GameObject Magnet;
            public GameObject Shield;
            public GameObject Boost;
            public PowerUpType Shown;
        }

        private RunnerConfig _runnerConfig;
        private bool _reduceMotion;
        private float _viewDistanceM;
        private PickupSlot[] _pickups;
        private int _pickupsShown;

        private Transform _bubble;
        private MeshRenderer[] _bubbleRenderers;
        private Material _shieldMaterial;
        private Material _shieldHighlightMaterial;
        private Material _shieldDimMaterial;
        private bool _bubbleDim;
        private Transform _magnetRingA;
        private Transform _magnetRingB;
        private Transform _speedLines;
        private Transform[] _lines;

        private Transform[] _shards;
        private Vector3[] _shardVelocity;
        private float[] _shardLife;
        private Material _shardHazardMaterial;
        private Material _shardShieldMaterial;
        private MeshRenderer[] _shardRenderers;
        private int _nextShard;

        private readonly byte[] _pendingLane = new byte[PendingCapacity];
        private readonly bool[] _pendingShield = new bool[PendingCapacity];
        private int _pendingCount;

        private float _time;
        private TrackRunWorld _world;
        private PathFrame _frame;

        /// <summary>
        /// Reduce Motion (from the save, live): no speed lines and no shard bursts. Starts from the config's value
        /// and is overridden by the bootstrap with the save's setting.
        /// </summary>
        public bool ReduceMotion
        {
            get => _reduceMotion;
            set => _reduceMotion = value;
        }

        /// <summary>The route things are placed on (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, RunnerPresentationConfig presentation, float viewDistanceM)
        {
            _frame = PathPlacement.OrIdentity(_frame);
            _runnerConfig = runnerConfig;
            _reduceMotion = presentation != null && presentation.ReduceMotion;
            _viewDistanceM = viewDistanceM;

            _pickups = new PickupSlot[PickupCapacity];
            for (int i = 0; i < PickupCapacity; i++)
            {
                _pickups[i] = CreatePickup(kit, i);
            }

            CreateBubble(kit);
            _magnetRingA = CreateRing(kit, "MagnetRingA", StylePalette.MagnetGlow);
            _magnetRingB = CreateRing(kit, "MagnetRingB", StylePalette.MagnetGlow);
            CreateSpeedLines(kit);
            CreateShards(kit);
        }

        public void BeginRun(GameSession session)
        {
            _world = session.World as TrackRunWorld;
            _pendingCount = 0;
            for (int i = 0; i < ShardCapacity; i++)
            {
                _shardLife[i] = 0f;
                _shards[i].gameObject.SetActive(false);
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (e.Type != RunnerEventType.ObstacleSmashed && e.Type != RunnerEventType.ShieldAbsorbed)
            {
                return;
            }

            if (_pendingCount < PendingCapacity)
            {
                _pendingLane[_pendingCount] = e.Lane;
                _pendingShield[_pendingCount] = e.Type == RunnerEventType.ShieldAbsorbed;
                _pendingCount++;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_pickups == null)
            {
                return;
            }

            if (session.Phase != SessionPhase.Paused)
            {
                _time += realDeltaSeconds;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out float heroX, out float heroY, out double heroZ);
            var hero = new Vector3(heroX, heroY, (float)heroZ);

            // The simulation position (hero) is only used for simulation queries; everything drawn goes through the route.
            _frame.Sample(heroZ, out PathPose pose);
            Vector3 heroWorld = PathPlacement.Point(pose, heroX, heroY);
            Quaternion rot = PathPlacement.Orientation(pose);

            PowerUpSystem powerUps = _world?.PowerUps;
            RenderPickups(powerUps, heroZ);
            bool dead = runner.Current.IsDead;
            RenderShield(powerUps, hero, heroWorld, rot, dead);
            RenderMagnet(powerUps, heroWorld, rot, dead);
            RenderSpeedLines(powerUps, heroWorld, rot, dead);
            SpawnPendingShards(heroWorld, rot, heroZ);
            UpdateShards(session.Phase == SessionPhase.Paused ? 0f : realDeltaSeconds);
        }

        // ---- Pickups ----

        private PickupSlot CreatePickup(GrayBoxKit kit, int index)
        {
            var slot = new PickupSlot();
            slot.Root = new GameObject("PowerUpPickup" + index).transform;
            slot.Root.SetParent(transform, false);

            // Glow disc with an ink rim behind the icon (faces the camera).
            Quaternion facing = Quaternion.Euler(90f, 0f, 0f);
            Transform rim = kit.Create(PrimitiveType.Cylinder, "InkRim", slot.Root, StylePalette.Ink, new Vector3(0f, 0f, 0.32f), new Vector3(1.12f, 0.01f, 1.12f));
            rim.localRotation = facing;

            slot.Icon = new GameObject("Icon").transform;
            slot.Icon.SetParent(slot.Root, false);

            // Magnet: a "U" (two posts and a bottom bar) with parchment pole tips.
            slot.Magnet = new GameObject("Magnet");
            slot.Magnet.transform.SetParent(slot.Icon, false);
            Transform m = slot.Magnet.transform;
            Transform halo = kit.Create(PrimitiveType.Cylinder, "Halo", m, StylePalette.MagnetGlow, new Vector3(0f, 0f, 0.3f), new Vector3(1.0f, 0.01f, 1.0f));
            halo.localRotation = facing;
            kit.Create(PrimitiveType.Cube, "PostL", m, StylePalette.MagnetGlow, new Vector3(-0.24f, 0.06f, 0f), new Vector3(0.18f, 0.5f, 0.18f));
            kit.Create(PrimitiveType.Cube, "PostR", m, StylePalette.MagnetGlow, new Vector3(0.24f, 0.06f, 0f), new Vector3(0.18f, 0.5f, 0.18f));
            kit.Create(PrimitiveType.Cube, "Bottom", m, StylePalette.MagnetGlow, new Vector3(0f, -0.22f, 0f), new Vector3(0.66f, 0.18f, 0.18f));
            kit.Create(PrimitiveType.Cube, "TipL", m, StylePalette.Parchment, new Vector3(-0.24f, 0.36f, 0f), new Vector3(0.19f, 0.12f, 0.19f));
            kit.Create(PrimitiveType.Cube, "TipR", m, StylePalette.Parchment, new Vector3(0.24f, 0.36f, 0f), new Vector3(0.19f, 0.12f, 0.19f));

            // Shield: a dome bubble on an ink base.
            slot.Shield = new GameObject("Shield");
            slot.Shield.transform.SetParent(slot.Icon, false);
            Transform s = slot.Shield.transform;
            halo = kit.Create(PrimitiveType.Cylinder, "Halo", s, StylePalette.ShieldGlow, new Vector3(0f, 0f, 0.3f), new Vector3(1.0f, 0.01f, 1.0f));
            halo.localRotation = facing;
            kit.Create(PrimitiveType.Sphere, "Dome", s, StylePalette.ShieldGlow, new Vector3(0f, -0.1f, 0f), new Vector3(0.7f, 0.62f, 0.7f));
            kit.Create(PrimitiveType.Cylinder, "Base", s, StylePalette.Ink, new Vector3(0f, -0.3f, 0f), new Vector3(0.78f, 0.05f, 0.78f));
            kit.Create(PrimitiveType.Cube, "Highlight", s, StylePalette.Parchment, new Vector3(-0.16f, 0.06f, -0.3f), new Vector3(0.08f, 0.2f, 0.04f));

            // Speed Boost: a double chevron pointing up (forward).
            slot.Boost = new GameObject("Boost");
            slot.Boost.transform.SetParent(slot.Icon, false);
            Transform b = slot.Boost.transform;
            halo = kit.Create(PrimitiveType.Cylinder, "Halo", b, StylePalette.BoostGlow, new Vector3(0f, 0f, 0.3f), new Vector3(1.0f, 0.01f, 1.0f));
            halo.localRotation = facing;
            for (int c = 0; c < 2; c++)
            {
                float y = -0.14f + c * 0.3f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform bar = kit.Create(PrimitiveType.Cube, "Chevron", b, StylePalette.BoostGlow, new Vector3(side * 0.13f, y, 0f), new Vector3(0.36f, 0.13f, 0.16f));
                    bar.localRotation = Quaternion.Euler(0f, 0f, side * -40f);
                }
            }

            // Real icons (about 1 m across, facing the camera, glow included) replace the gray-box groups.
            EnvironmentArt.ReplaceGroup(m, EnvironmentArt.Magnet);
            EnvironmentArt.ReplaceGroup(s, EnvironmentArt.Shield);
            EnvironmentArt.ReplaceGroup(b, EnvironmentArt.Boost);

            float scale = IconSizeM;
            slot.Icon.localScale = new Vector3(scale, scale, scale);
            rim.localScale = new Vector3(1.12f * scale, 0.01f, 1.12f * scale);
            rim.localPosition = new Vector3(0f, 0f, 0.32f * scale);
            slot.Magnet.SetActive(false);
            slot.Shield.SetActive(false);
            slot.Boost.SetActive(false);
            slot.Shown = PowerUpType.None;
            slot.Root.gameObject.SetActive(false);
            return slot;
        }

        private void RenderPickups(PowerUpSystem powerUps, double heroZ)
        {
            int used = 0;
            PowerUpPlacer placer = powerUps?.Placer;
            if (placer != null)
            {
                double minZ = heroZ - BehindM;
                double maxZ = heroZ + _viewDistanceM;
                float bob = Mathf.Sin(_time * BobHz * 2f * Mathf.PI) * BobM;
                Quaternion spin = Quaternion.Euler(0f, Mathf.Sin(_time * SpinDegPerS * Mathf.Deg2Rad) * 35f, 0f);
                int count = placer.Count;
                for (int i = 0; i < count && used < PickupCapacity; i++)
                {
                    ref readonly PowerUpPickup p = ref placer.GetPickup(i);
                    if (p.Z > maxZ)
                    {
                        break;
                    }

                    if (p.Collected || p.Z < minZ)
                    {
                        continue;
                    }

                    PickupSlot slot = _pickups[used++];
                    if (slot.Shown != p.Type)
                    {
                        slot.Shown = p.Type;
                        slot.Magnet.SetActive(p.Type == PowerUpType.Magnet);
                        slot.Shield.SetActive(p.Type == PowerUpType.Shield);
                        slot.Boost.SetActive(p.Type == PowerUpType.SpeedBoost);
                    }

                    _frame.Sample(p.Z, out PathPose pickupPose);
                    slot.Root.localPosition = PathPlacement.Point(pickupPose, p.X, p.Y + bob);
                    slot.Root.localRotation = PathPlacement.Orientation(pickupPose);
                    slot.Icon.localRotation = spin;
                    if (!slot.Root.gameObject.activeSelf)
                    {
                        slot.Root.gameObject.SetActive(true);
                    }
                }
            }

            for (int i = used; i < _pickupsShown; i++)
            {
                _pickups[i].Root.gameObject.SetActive(false);
            }

            _pickupsShown = used;
        }

        // ---- Shield bubble ----

        private void CreateBubble(GrayBoxKit kit)
        {
            _bubble = new GameObject("ShieldBubble").transform;
            _bubble.SetParent(transform, false);
            int total = BubbleSegments * 3;
            _bubbleRenderers = new MeshRenderer[total];
            int r = 0;
            for (int ring = 0; ring < 3; ring++)
            {
                for (int k = 0; k < BubbleSegments; k++)
                {
                    float a = 2f * Mathf.PI * k / BubbleSegments;
                    Vector3 pos;
                    Quaternion rot;
                    switch (ring)
                    {
                        case 0: // vertical ring facing the camera
                            pos = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * BubbleRadiusM;
                            rot = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                            break;
                        case 1: // vertical ring along the run direction
                            pos = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * BubbleRadiusM;
                            rot = Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f);
                            break;
                        default: // horizontal ring at the bubble's waist
                            pos = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * BubbleRadiusM;
                            rot = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                            break;
                    }

                    bool highlight = IsHighlight(ring, k);
                    Color color = highlight ? StylePalette.Parchment : StylePalette.ShieldGlow;
                    Transform seg = kit.Create(PrimitiveType.Cube, "Seg", _bubble, color, pos, new Vector3(0.07f, 0.34f, 0.07f));
                    seg.localRotation = rot;
                    _bubbleRenderers[r++] = seg.GetComponent<MeshRenderer>();
                }
            }

            _shieldMaterial = kit.GetMaterial(StylePalette.ShieldGlow, _bubbleRenderers[0].sharedMaterial);
            _shieldHighlightMaterial = kit.GetMaterial(StylePalette.Parchment, _bubbleRenderers[0].sharedMaterial);
            _shieldDimMaterial = kit.GetMaterial(StylePalette.ShieldDim, _bubbleRenderers[0].sharedMaterial);
            _bubble.gameObject.SetActive(false);
        }

        private static bool IsHighlight(int ring, int k)
        {
            // Two hard highlight arcs (style guide 10): on the front ring and the side ring.
            return (ring == 0 && k >= 3 && k <= 5) || (ring == 1 && k >= 4 && k <= 5);
        }

        private void RenderShield(PowerUpSystem powerUps, Vector3 hero, Vector3 heroWorld, Quaternion rot, bool dead)
        {
            bool on = powerUps != null && !dead && powerUps.IsActive(PowerUpType.Shield);
            if (on && powerUps.IsEnding(PowerUpType.Shield))
            {
                on = ((int)(_time * FlickerHz) & 1) == 0;
            }

            if (_bubble.gameObject.activeSelf != on)
            {
                _bubble.gameObject.SetActive(on);
            }

            if (!on)
            {
                return;
            }

            _bubble.localPosition = heroWorld + (rot * new Vector3(0f, BubbleCenterM, 0f));
            _bubble.localRotation = rot * Quaternion.Euler(0f, _time * BubbleSpinDegPerS, 0f);

            // GDD 10: the bubble does not glow over a chasm (the shield does not save from falling).
            TrackSimulation track = _world?.Track;
            float half = _runnerConfig.PlayerHitboxDepthM * 0.5f;
            bool overChasm = track != null && !track.HasGround(hero.x, hero.z - half, hero.z + half);
            if (overChasm != _bubbleDim)
            {
                _bubbleDim = overChasm;
                for (int i = 0; i < _bubbleRenderers.Length; i++)
                {
                    int ring = i / BubbleSegments;
                    int k = i % BubbleSegments;
                    bool highlight = IsHighlight(ring, k);
                    _bubbleRenderers[i].sharedMaterial = overChasm ? _shieldDimMaterial : (highlight ? _shieldHighlightMaterial : _shieldMaterial);
                }
            }
        }

        // ---- Magnet rings ----

        private Transform CreateRing(GrayBoxKit kit, string name, Color color)
        {
            Transform ring = new GameObject(name).transform;
            ring.SetParent(transform, false);
            for (int k = 0; k < MagnetSegments; k++)
            {
                float a = 2f * Mathf.PI * k / MagnetSegments;
                Transform seg = kit.Create(
                    PrimitiveType.Cube, "Seg", ring, color, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), new Vector3(0.22f, 0.03f, 0.06f));
                seg.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }

            ring.gameObject.SetActive(false);
            return ring;
        }

        private void RenderMagnet(PowerUpSystem powerUps, Vector3 heroWorld, Quaternion rot, bool dead)
        {
            bool on = powerUps != null && !dead && powerUps.IsActive(PowerUpType.Magnet);
            if (on && powerUps.IsEnding(PowerUpType.Magnet))
            {
                on = ((int)(_time * FlickerHz) & 1) == 0;
            }

            PlaceRing(_magnetRingA, on, heroWorld, rot, 0f);
            PlaceRing(_magnetRingB, on, heroWorld, rot, 0.5f);
        }

        private void PlaceRing(Transform ring, bool on, Vector3 heroWorld, Quaternion rot, float phaseOffset)
        {
            if (ring.gameObject.activeSelf != on)
            {
                ring.gameObject.SetActive(on);
            }

            if (!on)
            {
                return;
            }

            float phase = (_time / MagnetPulseS + phaseOffset) % 1f;
            float radius = Mathf.Lerp(MagnetMinRadiusM, MagnetMaxRadiusM, phase);
            ring.localPosition = heroWorld + (rot * new Vector3(0f, 0.06f + phase * 0.5f, 0f));
            ring.localRotation = rot;
            ring.localScale = new Vector3(radius, 1f, radius);
        }

        // ---- Speed lines ----

        private void CreateSpeedLines(GrayBoxKit kit)
        {
            _speedLines = new GameObject("SpeedLines").transform;
            _speedLines.SetParent(transform, false);
            _lines = new Transform[SpeedLineCount];
            for (int i = 0; i < SpeedLineCount; i++)
            {
                Color color = (i % 3) == 0 ? StylePalette.Parchment : StylePalette.BoostGlow;
                _lines[i] = kit.Create(PrimitiveType.Cube, "Line" + i, _speedLines, color, Vector3.zero, new Vector3(0.05f, 0.05f, SpeedLineLengthM));
            }

            _speedLines.gameObject.SetActive(false);
        }

        private void RenderSpeedLines(PowerUpSystem powerUps, Vector3 heroWorld, Quaternion rot, bool dead)
        {
            bool on = !_reduceMotion && powerUps != null && !dead && powerUps.BoostPhase != SpeedBoostPhase.None;
            if (_speedLines.gameObject.activeSelf != on)
            {
                _speedLines.gameObject.SetActive(on);
            }

            if (!on)
            {
                return;
            }

            bool slowing = powerUps.BoostPhase == SpeedBoostPhase.Slowdown;
            _speedLines.localPosition = heroWorld;
            _speedLines.localRotation = rot;
            for (int i = 0; i < SpeedLineCount; i++)
            {
                // Fixed lanes of streaks around Pista (golden-angle spread), each rushing back on its own phase.
                float angle = i * 2.39996f;
                float radius = 1.3f + (i % 4) * 0.45f;
                float x = Mathf.Cos(angle) * radius;
                float y = 1.1f + Mathf.Sin(angle) * radius * 0.7f;
                if (y < 0.15f)
                {
                    y = 0.15f + (i % 3) * 0.2f;
                }

                float phase = (_time / SpeedLineCycleS + i * 0.37f) % 1f;
                float z = SpeedLineSpanM * 0.6f - phase * SpeedLineSpanM;
                _lines[i].localPosition = new Vector3(x, y, z);
                bool visible = !slowing || (i % 2) == 0;
                if (_lines[i].gameObject.activeSelf != visible)
                {
                    _lines[i].gameObject.SetActive(visible);
                }
            }
        }

        // ---- Shards ----

        private void CreateShards(GrayBoxKit kit)
        {
            _shards = new Transform[ShardCapacity];
            _shardVelocity = new Vector3[ShardCapacity];
            _shardLife = new float[ShardCapacity];
            _shardRenderers = new MeshRenderer[ShardCapacity];
            for (int i = 0; i < ShardCapacity; i++)
            {
                _shards[i] = kit.Create(PrimitiveType.Cube, "Shard" + i, transform, StylePalette.HazardWood, Vector3.zero, new Vector3(0.22f, 0.22f, 0.05f));
                _shardRenderers[i] = _shards[i].GetComponent<MeshRenderer>();
                _shards[i].gameObject.SetActive(false);
            }

            _shardHazardMaterial = _shardRenderers[0].sharedMaterial;
            _shardShieldMaterial = kit.GetMaterial(StylePalette.ShieldGlow, _shardHazardMaterial);
        }

        private void SpawnPendingShards(Vector3 heroWorld, Quaternion rot, double heroZ)
        {
            if (_reduceMotion)
            {
                _pendingCount = 0;
                return;
            }

            for (int p = 0; p < _pendingCount; p++)
            {
                bool shield = _pendingShield[p];
                Vector3 origin;
                if (shield)
                {
                    origin = heroWorld + (rot * new Vector3(0f, BubbleCenterM, 0.6f));
                }
                else
                {
                    _frame.Sample(heroZ + 1.2, out PathPose strikePose);
                    origin = PathPlacement.Point(strikePose, _runnerConfig.LaneCenterX(_pendingLane[p]), 1.0f);
                }

                for (int k = 0; k < ShardsPerBurst; k++)
                {
                    int i = _nextShard;
                    _nextShard = (_nextShard + 1) % ShardCapacity;
                    float a = 2f * Mathf.PI * (k + 0.5f * p) / ShardsPerBurst;
                    _shardVelocity[i] = rot * new Vector3(Mathf.Cos(a) * 3.5f, 3f + Mathf.Sin(a) * 2.5f, 4f + (k % 3));
                    _shardLife[i] = ShardLifeS;
                    _shards[i].localPosition = origin;
                    _shards[i].localRotation = Quaternion.Euler(k * 37f, k * 53f, 0f);
                    _shardRenderers[i].sharedMaterial = shield ? _shardShieldMaterial : _shardHazardMaterial;
                    _shards[i].gameObject.SetActive(true);
                }
            }

            _pendingCount = 0;
        }

        private void UpdateShards(float dt)
        {
            for (int i = 0; i < ShardCapacity; i++)
            {
                if (_shardLife[i] <= 0f)
                {
                    continue;
                }

                _shardLife[i] -= dt;
                if (_shardLife[i] <= 0f)
                {
                    _shards[i].gameObject.SetActive(false);
                    continue;
                }

                _shardVelocity[i].y -= ShardGravity * dt;
                _shards[i].localPosition += _shardVelocity[i] * dt;
                _shards[i].Rotate(360f * dt, 220f * dt, 0f, Space.Self);
            }
        }
    }
}
