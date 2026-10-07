using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box world themes (GDD 9, style guide section 5). Each frame it asks the <see cref="TrackSimulation"/> which
    /// world HERO is in, blends the previous and next theme over the gateway chunk, and applies the result: camera and
    /// fog color, ambient and key light, ground tiles and verges, obstacle bodies. It also stands a gray-box gateway
    /// (two posts, a lintel and a banner with the world name) at every world switch. No UI: a banner or music
    /// crossfade can subscribe to <see cref="SegmentChanged"/>. Setup allocates; <see cref="Render"/> only touches
    /// materials, lights and render settings, and only while the blend changes.
    /// </summary>
    public sealed class WorldThemeView : MonoBehaviour, IRunView
    {
        // The Run scene's key light (RunSceneBootstrap) sits at pitch 50 / yaw -30 for the Jungle theme (-25 / 25);
        // other themes move it by the same difference.
        private const float BaseKeyPitchDeg = 50f;
        private const float BaseKeyYawDeg = -30f;
        private const float JungleKeyYawDeg = -25f;
        private const float JungleKeyPitchDeg = 25f;
        private const float AmbientMix = 0.55f;

        // Gateway frame (gray-box, replaced by the real cave mouth, waterfall, rope bridge and temple gate).
        private const float PathMarginM = 0.6f;
        private const float PostWidthM = 0.7f;
        private const float PostHeightM = 5.5f;
        private const float LintelHeightM = 0.7f;
        private const float BoardHeightM = 1.2f;
        private const float BoardCenterYM = 4.6f;
        private const float KeepBehindM = 14f;
        private const float TextCharacterSize = 0.1f;
        private const int TextFontSize = 64;
        private const float TextMaxChars = 12f;

        private Camera _camera;
        private Light _keyLight;
        private GroundView _ground;
        private ObstacleView _obstacles;
        private TrackSimulation _track;
        private float _viewDistanceM;

        private int _segment = -1;
        private int _appliedFrom = -1;
        private int _appliedTo = -1;
        private float _appliedT = -1f;

        private Material _accentMaterial;
        private Transform _gate;
        private TextMesh _gateText;
        private string[] _worldNames;
        private int _gateSegment = -1;

        /// <summary>Raised when HERO enters a new world segment (not at the start of a run). Argument = segment index.</summary>
        public event Action<int> SegmentChanged;

        /// <summary>World segment HERO is in (-1 before the first frame of a run).</summary>
        public int CurrentSegment => _segment;

        /// <summary>World name for banners: "Jungle", "River", "Mountains" or "Ancient Ruins".</summary>
        public string GetWorldName(WorldKind kind)
        {
            return _worldNames[(int)kind];
        }

        public void Init(
            Camera camera,
            Light keyLight,
            GroundView ground,
            ObstacleView obstacles,
            GrayBoxKit kit,
            RunnerConfig runnerConfig,
            Font font,
            float viewDistanceM)
        {
            _camera = camera;
            _keyLight = keyLight;
            _ground = ground;
            _obstacles = obstacles;
            _viewDistanceM = viewDistanceM;

            _worldNames = new string[4];
            for (int i = 0; i < _worldNames.Length; i++)
            {
                _worldNames[i] = WorldSkinConfig.CreateFor((WorldKind)i).WorldName;
            }

            float halfWidth = runnerConfig.LaneCount * runnerConfig.LaneWidthM * 0.5f + PathMarginM;
            _gate = new GameObject("WorldGate").transform;
            _gate.SetParent(transform, false);

            Transform left = kit.Create(
                PrimitiveType.Cube, "PostLeft", _gate, StylePalette.DeepCanopyTeal,
                new Vector3(-halfWidth - PostWidthM * 0.5f, PostHeightM * 0.5f, 0f),
                new Vector3(PostWidthM, PostHeightM, PostWidthM));
            Transform right = kit.Create(
                PrimitiveType.Cube, "PostRight", _gate, StylePalette.DeepCanopyTeal,
                new Vector3(halfWidth + PostWidthM * 0.5f, PostHeightM * 0.5f, 0f),
                new Vector3(PostWidthM, PostHeightM, PostWidthM));
            Transform lintel = kit.Create(
                PrimitiveType.Cube, "Lintel", _gate, StylePalette.DeepCanopyTeal,
                new Vector3(0f, PostHeightM + LintelHeightM * 0.5f, 0f),
                new Vector3(2f * halfWidth + 2f * PostWidthM, LintelHeightM, PostWidthM));
            kit.Create(
                PrimitiveType.Cube, "Board", _gate, StylePalette.Ink,
                new Vector3(0f, BoardCenterYM, 0.1f),
                new Vector3(2f * halfWidth, BoardHeightM, 0.15f));

            // The three frame pieces share one material so the accent can change per world without touching the kit's colors.
            Material template = left.GetComponent<MeshRenderer>().sharedMaterial;
            _accentMaterial = new Material(template) { name = "World_GateAccent", color = StylePalette.DeepCanopyTeal };
            left.GetComponent<MeshRenderer>().sharedMaterial = _accentMaterial;
            right.GetComponent<MeshRenderer>().sharedMaterial = _accentMaterial;
            lintel.GetComponent<MeshRenderer>().sharedMaterial = _accentMaterial;

            if (font != null)
            {
                var textObject = new GameObject("GateName");
                textObject.transform.SetParent(_gate, false);
                textObject.transform.localPosition = new Vector3(0f, BoardCenterYM, -0.05f);
                MeshRenderer textRenderer = textObject.AddComponent<MeshRenderer>();
                _gateText = textObject.AddComponent<TextMesh>();
                _gateText.font = font;
                _gateText.fontSize = TextFontSize;
                _gateText.characterSize = TextCharacterSize;
                _gateText.anchor = TextAnchor.MiddleCenter;
                _gateText.alignment = TextAlignment.Center;
                _gateText.fontStyle = FontStyle.Bold;
                _gateText.color = StylePalette.Parchment;
                _gateText.text = string.Empty;
                textRenderer.sharedMaterial = font.material;
            }

            _gate.gameObject.SetActive(false);
        }

        public void BeginRun(GameSession session)
        {
            _track = (session.World as TrackRunWorld)?.Track;
            _segment = -1;
            _appliedFrom = -1;
            _appliedTo = -1;
            _appliedT = -1f;
            _gateSegment = -1;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_ground == null)
            {
                return;
            }

            if (_track == null || _track.Worlds == null)
            {
                if (_gate.gameObject.activeSelf)
                {
                    _gate.gameObject.SetActive(false);
                }

                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double z);
            WorldScheduleConfig worlds = _track.Worlds;
            int segment = _track.WorldSegmentAt(z);

            // Blend over the gateway chunk: from its start to its end, centred on the switch.
            double half = _track.GatewayLengthM * 0.5;
            int fromSegment = segment;
            int toSegment = segment;
            float t = 0f;
            if (half > 0.0)
            {
                if (_track.TryGetSegmentStartZ(segment + 1, out double nextZ) && z >= nextZ - half)
                {
                    toSegment = segment + 1;
                    t = (float)((z - (nextZ - half)) / (2.0 * half));
                }
                else if (segment >= 1 && _track.TryGetSegmentStartZ(segment, out double thisZ) && z < thisZ + half)
                {
                    fromSegment = segment - 1;
                    t = (float)((z - (thisZ - half)) / (2.0 * half));
                }
            }

            t = Mathf.Clamp01(t);
            if (fromSegment != _appliedFrom || toSegment != _appliedTo || t != _appliedT)
            {
                _appliedFrom = fromSegment;
                _appliedTo = toSegment;
                _appliedT = t;
                WorldTheme fromTheme = WorldThemes.Get(worlds.KindOfSegment(fromSegment), worlds.IsDuskSegment(fromSegment));
                WorldTheme theme = fromTheme;
                if (toSegment != fromSegment)
                {
                    WorldTheme toTheme = WorldThemes.Get(worlds.KindOfSegment(toSegment), worlds.IsDuskSegment(toSegment));
                    theme = WorldTheme.Lerp(fromTheme, toTheme, t);
                }

                Apply(theme);
            }

            if (segment != _segment)
            {
                bool wasInRun = _segment >= 0;
                _segment = segment;
                if (wasInRun)
                {
                    SegmentChanged?.Invoke(segment);
                }
            }

            RenderGate(worlds, segment, z);
        }

        private void Apply(in WorldTheme theme)
        {
            _ground.ApplyTheme(theme);
            _obstacles.ApplyTheme(theme);
            if (_camera != null)
            {
                _camera.backgroundColor = theme.Fog;
            }

            RenderSettings.fogColor = theme.Fog;
            RenderSettings.ambientLight = Color.Lerp(theme.ShadowTint, theme.SkyHorizon, AmbientMix);
            if (_keyLight != null)
            {
                _keyLight.color = theme.KeyLight;
                _keyLight.transform.localRotation = Quaternion.Euler(
                    BaseKeyPitchDeg + (theme.KeyPitchDeg - JungleKeyPitchDeg),
                    BaseKeyYawDeg + (theme.KeyYawDeg - JungleKeyYawDeg),
                    0f);
            }
        }

        /// <summary>
        /// One gateway frame, standing at the gateway HERO just passed (until it is behind the camera) or at the next
        /// one once it has been generated and is within view.
        /// </summary>
        private void RenderGate(WorldScheduleConfig worlds, int segment, double z)
        {
            double gateZ;
            int destination;
            if (segment >= 1 && _track.TryGetSegmentStartZ(segment, out double passedZ) && z < passedZ + KeepBehindM)
            {
                gateZ = passedZ;
                destination = segment;
            }
            else if (_track.TryGetSegmentStartZ(segment + 1, out double nextZ) && nextZ - z < _viewDistanceM)
            {
                gateZ = nextZ;
                destination = segment + 1;
            }
            else
            {
                if (_gate.gameObject.activeSelf)
                {
                    _gate.gameObject.SetActive(false);
                }

                return;
            }

            if (destination != _gateSegment)
            {
                _gateSegment = destination;
                WorldKind kind = worlds.KindOfSegment(destination);
                _accentMaterial.color = WorldThemes.Get(kind, worlds.IsDuskSegment(destination)).Accent;
                if (_gateText != null)
                {
                    string title = _worldNames[(int)kind].ToUpperInvariant();
                    _gateText.text = title;
                    float fit = Mathf.Min(1f, TextMaxChars / title.Length);
                    _gateText.transform.localScale = new Vector3(fit, fit, 1f);
                }
            }

            _gate.localPosition = new Vector3(0f, 0f, (float)gateZ);
            if (!_gate.gameObject.activeSelf)
            {
                _gate.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (_accentMaterial != null)
            {
                Destroy(_accentMaterial);
            }
        }
    }
}
