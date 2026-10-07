using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The two template materials every runtime-built gray-box material is cloned from. They are real assets in
    /// <c>Assets/_Game/Resources/RuntimeMaterials</c> (URP Lit, opaque; URP Unlit, alpha blended), so their shaders are
    /// part of every player build. The material <see cref="GameObject.CreatePrimitive"/> assigns is NOT a safe template:
    /// in the editor it is the URP Lit material, but in a player <c>UniversalRenderPipelineAsset.defaultMaterial</c> is
    /// null, so the primitive gets the built-in Standard material, which a URP build does not contain (magenta).
    /// The primitive fallback below exists only so a missing asset degrades loudly instead of throwing.
    /// Setup-time only; no shader lookup by name.
    /// </summary>
    public static class RuntimeMaterialTemplates
    {
        public const string OpaquePath = "RuntimeMaterials/GrayBoxLit";
        public const string TransparentPath = "RuntimeMaterials/GrayBoxTransparent";

        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        private static Material _opaque;
        private static Material _transparent;
        private static Material _primitiveFallback;
        private static bool _fallbackLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _opaque = null;
            _transparent = null;
            _primitiveFallback = null;
            _fallbackLogged = false;
        }

        /// <summary>Opaque URP Lit template (smoothness 0). Never null in a project that has the asset.</summary>
        public static Material GetOpaqueTemplate()
        {
            if (_opaque == null)
            {
                _opaque = Resources.Load<Material>(OpaquePath);
            }

            return _opaque != null ? _opaque : GetFallback(OpaquePath);
        }

        /// <summary>Alpha-blended, no depth write template (hitbox overlay).</summary>
        public static Material GetTransparentTemplate()
        {
            if (_transparent == null)
            {
                _transparent = Resources.Load<Material>(TransparentPath);
            }

            return _transparent != null ? _transparent : GetFallback(TransparentPath);
        }

        private static Material GetFallback(string missingPath)
        {
            if (!_fallbackLogged)
            {
                _fallbackLogged = true;
                Debug.LogError("[JungleBooze] Missing Resources/" + missingPath + ".mat. Falling back to the primitive material, "
                    + "which renders PINK in a device build. Run JungleBooze > Setup > Create Runtime Materials and commit the assets.");
            }

            if (_primitiveFallback == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _primitiveFallback = probe.GetComponent<MeshRenderer>().sharedMaterial;
                if (Application.isPlaying)
                {
                    Object.Destroy(probe);
                }
                else
                {
                    Object.DestroyImmediate(probe);
                }
            }

            return _primitiveFallback;
        }

        /// <summary>
        /// One Console / Xcode line per distinct shader on the renderers under <paramref name="root"/> plus the template
        /// materials: shader name, whether <c>shader.isSupported</c>, renderer count. A suspect line (error shader, built-in
        /// Standard, unsupported) means that shader is missing from the build and draws magenta. Dev builds only; allocates.
        /// </summary>
        public static void LogShaderReport(Transform root)
        {
            var report = new StringBuilder("[JungleBooze] Shader report. Pipeline: ");
            report.Append(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name
                : "NONE (built-in)");
            report.Append(", device: ").Append(SystemInfo.graphicsDeviceType);
            report.Append(", color space: ").Append(QualitySettings.activeColorSpace);

            AppendMaterial(report, "\n  template opaque", Resources.Load<Material>(OpaquePath));
            AppendMaterial(report, "\n  template transparent", Resources.Load<Material>(TransparentPath));
            AppendMaterial(report, "\n  UI default (sky)", Canvas.GetDefaultCanvasMaterial());

            if (root != null)
            {
                var shaders = new List<Shader>();
                var counts = new List<int>();
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material[] materials = renderers[i].sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        Shader shader = materials[m] != null ? materials[m].shader : null;
                        int at = shaders.IndexOf(shader);
                        if (at < 0)
                        {
                            shaders.Add(shader);
                            counts.Add(1);
                        }
                        else
                        {
                            counts[at]++;
                        }
                    }
                }

                report.Append("\n  scene renderers: ").Append(renderers.Length);
                for (int i = 0; i < shaders.Count; i++)
                {
                    report.Append("\n    ");
                    AppendShader(report, shaders[i]);
                    report.Append(" x").Append(counts[i]);
                }
            }

            Debug.Log(report.ToString());
        }

        private static void AppendMaterial(StringBuilder report, string label, Material material)
        {
            report.Append(label).Append(": ");
            if (material == null)
            {
                report.Append("MISSING");
                return;
            }

            report.Append(material.name).Append(" -> ");
            AppendShader(report, material.shader);
        }

        private static void AppendShader(StringBuilder report, Shader shader)
        {
            if (shader == null)
            {
                report.Append("<null shader> SUSPECT");
                return;
            }

            bool supported = shader.isSupported;
            bool suspect = !supported
                || string.Equals(shader.name, ErrorShaderName, System.StringComparison.Ordinal)
                || string.Equals(shader.name, "Standard", System.StringComparison.Ordinal);
            report.Append(shader.name).Append(" isSupported=").Append(supported);
            if (suspect)
            {
                report.Append(" SUSPECT (draws magenta)");
            }
        }
    }
}
