using JungleBooze.Gameplay.Track;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The run's sky (style guide section 5: gradient sky, fog matched at the horizon): a vertex-colored dome and two
    /// rings of far canopy silhouettes around the camera, drawn first (render queue 1000/1001, no depth write, no fog)
    /// so everything else draws over them. The fog color sits exactly at the horizon, then the sky grades to the
    /// world's horizon and top colors. Uses the built-in default UI material (unlit, vertex colors, always included in
    /// builds; no shader lookup by name). Two draw calls. <see cref="ApplyTheme"/> recolors in place (no allocations);
    /// <see cref="LateUpdate"/> keeps the meshes centred on the camera after the camera view has moved it.
    /// tools/blender/sky_shapes.py builds the same meshes for preview renders: keep the constants in sync.
    /// </summary>
    public sealed class SkyView : MonoBehaviour
    {
        // Art geometry (mirrored in tools/blender/sky_shapes.py), not gameplay tuning.
        private const float DomeRadiusM = 150f;
        private const int DomeSegments = 24;
        private const float LowMix = 0.35f;
        private const float HighMix = 0.75f;
        private const int RingSegments = 384;
        private const float RingBottomM = -30f;
        private const float RingMidToFog = 0.6f;
        private const int DomeQueue = 1000;
        private const int RingQueue = 1001;

        private static readonly float[] DomeRingElevationDeg = { -40f, 0f, 3f, 9f, 18f, 30f, 90f };

        // Color per dome ring: 0 fog, 1 horizon, 2 low (horizon toward top), 3 high, 4 top.
        private static readonly int[] DomeRingColor = { 0, 0, 1, 2, 3, 4, 4 };

        private static readonly int[] RingFrequencies = { 5, 13, 23, 37 };
        private static readonly float[] RingRadiusM = { 135f, 118f };
        private static readonly float[] RingBaseM = { 4f, 0.5f };
        private static readonly float[] FarRingAmplitudesM = { 3f, 2f, 1.6f, 1.1f };
        private static readonly float[] NearRingAmplitudesM = { 2.4f, 1.6f, 1.4f, 1f };
        private static readonly float[] RingPhase = { 0.7f, 2.9f };

        private Transform _camera;
        private EnvironmentLookConfig _look;
        private Mesh _dome;
        private Mesh _rings;
        private Color[] _domeColors;
        private Color[] _ringColors;
        private readonly Color[] _palette = new Color[5];
        private Material _domeMaterial;
        private Material _ringMaterial;
        private bool _linear;

        /// <summary>Builds the meshes and materials (setup only; allocates) and applies the Jungle theme.</summary>
        public void Init(Camera targetCamera, EnvironmentLookConfig look)
        {
            _camera = targetCamera != null ? targetCamera.transform : null;
            _look = look ?? EnvironmentLookConfig.CreateDefault();
            _linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            Material ui = Canvas.GetDefaultCanvasMaterial();
            _domeMaterial = CreateMaterial(ui, "Sky_Dome", DomeQueue);
            _ringMaterial = CreateMaterial(ui, "Sky_FarCanopy", RingQueue);

            _dome = BuildDome();
            _rings = BuildRings();
            CreateRenderer("SkyDome", _dome, _domeMaterial);
            CreateRenderer("FarCanopy", _rings, _ringMaterial);

            ApplyTheme(WorldThemes.Get(WorldKind.Jungle, false));
            LateUpdate();
        }

        /// <summary>Recolors the dome and the far canopy for a world theme (or a blend of two). Allocation free.</summary>
        public void ApplyTheme(in WorldTheme theme)
        {
            if (_dome == null)
            {
                return;
            }

            _palette[0] = theme.Fog;
            _palette[1] = theme.SkyHorizon;
            _palette[2] = Color.Lerp(theme.SkyHorizon, theme.SkyTop, LowMix);
            _palette[3] = Color.Lerp(theme.SkyHorizon, theme.SkyTop, HighMix);
            _palette[4] = theme.SkyTop;

            int v = 0;
            for (int r = 0; r < DomeRingElevationDeg.Length; r++)
            {
                Color c = ToVertexColor(_palette[DomeRingColor[r]]);
                for (int s = 0; s < DomeSegments; s++)
                {
                    _domeColors[v++] = c;
                }
            }

            _dome.colors = _domeColors;

            v = 0;
            for (int ring = 0; ring < RingRadiusM.Length; ring++)
            {
                float haze = ring == 0 ? _look.FarRingHaze : _look.NearRingHaze;
                Color top = Color.Lerp(theme.ShadowTint, theme.Fog, haze);
                Color topV = ToVertexColor(top);
                Color midV = ToVertexColor(Color.Lerp(top, theme.Fog, RingMidToFog));
                Color fogV = ToVertexColor(theme.Fog);
                for (int s = 0; s < RingSegments; s++)
                {
                    _ringColors[v++] = topV;
                    _ringColors[v++] = midV;
                    _ringColors[v++] = fogV;
                }
            }

            _rings.colors = _ringColors;
        }

        private void LateUpdate()
        {
            if (_camera != null)
            {
                transform.SetPositionAndRotation(_camera.position, Quaternion.identity);
            }
        }

        private void OnDestroy()
        {
            DestroyObject(_dome);
            DestroyObject(_rings);
            DestroyObject(_domeMaterial);
            DestroyObject(_ringMaterial);
        }

        private static void DestroyObject(Object o)
        {
            if (o != null)
            {
                Destroy(o);
            }
        }

        /// <summary>The UI shader writes vertex colors straight to the target, so convert sRGB palette colors to linear.</summary>
        private Color ToVertexColor(Color srgb)
        {
            Color c = _linear ? srgb.linear : srgb;
            c.a = 1f;
            return c;
        }

        private static Material CreateMaterial(Material ui, string name, int queue)
        {
            var material = new Material(ui) { name = name, renderQueue = queue };
            if (material.HasProperty("_UIVertexColorAlwaysGammaSpace"))
            {
                material.SetFloat("_UIVertexColorAlwaysGammaSpace", 0f);
            }

            // UI text sets this per draw for alpha-only font textures; the sky samples the default white texture.
            material.SetVector("_TextureSampleAdd", Vector4.zero);
            return material;
        }

        private void CreateRenderer(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.allowOcclusionWhenDynamic = false;
        }

        private Mesh BuildDome()
        {
            int rings = DomeRingElevationDeg.Length;
            var vertices = new Vector3[rings * DomeSegments];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(rings - 1) * DomeSegments * 6];
            int t = 0;
            for (int r = 0; r < rings; r++)
            {
                float e = DomeRingElevationDeg[r] * Mathf.Deg2Rad;
                for (int s = 0; s < DomeSegments; s++)
                {
                    float a = 2f * Mathf.PI * s / DomeSegments;
                    vertices[r * DomeSegments + s] = new Vector3(
                        Mathf.Cos(e) * Mathf.Sin(a) * DomeRadiusM,
                        Mathf.Sin(e) * DomeRadiusM,
                        Mathf.Cos(e) * Mathf.Cos(a) * DomeRadiusM);
                }
            }

            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < DomeSegments; s++)
                {
                    int a = r * DomeSegments + s;
                    int b = r * DomeSegments + (s + 1) % DomeSegments;
                    t = Quad(triangles, t, a, b, b + DomeSegments, a + DomeSegments);
                }
            }

            _domeColors = new Color[vertices.Length];
            return CreateMesh("SkyDome", vertices, uvs, triangles, _domeColors);
        }

        private Mesh BuildRings()
        {
            int count = RingRadiusM.Length;
            var vertices = new Vector3[count * RingSegments * 3];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[count * RingSegments * 12];
            int v = 0;
            int t = 0;
            for (int ring = 0; ring < count; ring++)
            {
                float[] amplitudes = ring == 0 ? FarRingAmplitudesM : NearRingAmplitudesM;
                int start = v;
                for (int s = 0; s < RingSegments; s++)
                {
                    float a = 2f * Mathf.PI * s / RingSegments;
                    float x = Mathf.Sin(a) * RingRadiusM[ring];
                    float z = Mathf.Cos(a) * RingRadiusM[ring];
                    float h = RingBaseM[ring];
                    for (int f = 0; f < RingFrequencies.Length; f++)
                    {
                        h += amplitudes[f] * Mathf.Abs(Mathf.Sin(RingFrequencies[f] * (a + RingPhase[ring])));
                    }

                    vertices[v++] = new Vector3(x, h, z);
                    vertices[v++] = new Vector3(x, 0f, z);
                    vertices[v++] = new Vector3(x, RingBottomM, z);
                }

                for (int s = 0; s < RingSegments; s++)
                {
                    int a = start + s * 3;
                    int b = start + ((s + 1) % RingSegments) * 3;
                    t = Quad(triangles, t, a, b, b + 1, a + 1);
                    t = Quad(triangles, t, a + 1, b + 1, b + 2, a + 2);
                }
            }

            _ringColors = new Color[vertices.Length];
            return CreateMesh("FarCanopy", vertices, uvs, triangles, _ringColors);
        }

        private static int Quad(int[] triangles, int t, int a, int b, int c, int d)
        {
            triangles[t++] = a;
            triangles[t++] = b;
            triangles[t++] = c;
            triangles[t++] = a;
            triangles[t++] = c;
            triangles[t++] = d;
            return t;
        }

        private static Mesh CreateMesh(string name, Vector3[] vertices, Vector2[] uvs, int[] triangles, Color[] colors)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            return mesh;
        }
    }
}
