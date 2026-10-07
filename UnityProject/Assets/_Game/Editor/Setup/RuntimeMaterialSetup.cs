using System.IO;
using JungleBooze.Gameplay.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.Setup
{
    /// <summary>
    /// Keeps the two template materials the runtime gray-box views clone (<see cref="RuntimeMaterialTemplates"/>) valid:
    /// <c>Resources/RuntimeMaterials/GrayBoxLit.mat</c> (URP Lit, opaque, smoothness 0) and <c>GrayBoxTransparent.mat</c>
    /// (URP Unlit, alpha blend, no depth write). Both live under a Resources folder, so their shaders and variants are
    /// part of every player build (a runtime clone of GameObject.CreatePrimitive's material is magenta on device, because
    /// the player has no URP default material). The repo already holds hand-written copies; this creates them when they are
    /// missing and repairs any property that drifted. It is idempotent (writes nothing when everything is right) and
    /// is the only place that looks a shader up by name. [ASSUMED] The shaders are NOT added to Graphics > Always Included
    /// Shaders: the Resources references already include them, and "always included" would compile every variant of URP Lit.
    /// </summary>
    [InitializeOnLoad]
    public static class RuntimeMaterialSetup
    {
        public const string Folder = "Assets/_Game/Resources/RuntimeMaterials";

        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string LogPrefix = "[JungleBooze setup] ";

        static RuntimeMaterialSetup()
        {
            EditorApplication.delayCall += EnsureQuietly;
        }

        [MenuItem("JungleBooze/Setup/Create Runtime Materials", false, 21)]
        public static void CreateRuntimeMaterials()
        {
            int changed = Ensure();
            if (changed == 0)
            {
                Debug.Log(LogPrefix + "Runtime materials are already correct in " + Folder + ".");
            }
        }

        private static void EnsureQuietly()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Ensure();
        }

        /// <summary>Creates or repairs both materials. Returns how many assets were created or changed.</summary>
        private static int Ensure()
        {
            Shader lit = Shader.Find(LitShaderName);
            Shader unlit = Shader.Find(UnlitShaderName);
            if (lit == null || unlit == null)
            {
                Debug.LogWarning(LogPrefix + "URP shaders not found (is the Universal RP package installed and imported?). "
                    + "Runtime materials were not checked.");
                return 0;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            int changed = 0;
            changed += EnsureMaterial(Folder + "/GrayBoxLit.mat", lit, false);
            changed += EnsureMaterial(Folder + "/GrayBoxTransparent.mat", unlit, true);
            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return changed;
        }

        private static int EnsureMaterial(string path, Shader shader, bool transparent)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            bool changed = created;
            if (created)
            {
                material = new Material(shader);
            }

            if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }

            changed |= Configure(material, transparent);

            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
                Debug.Log(LogPrefix + "Created " + path + ".");
            }
            else if (changed)
            {
                EditorUtility.SetDirty(material);
                Debug.Log(LogPrefix + "Repaired " + path + ".");
            }

            return changed ? 1 : 0;
        }

        private static bool Configure(Material m, bool transparent)
        {
            bool changed = false;
            if (transparent)
            {
                changed |= SetFloat(m, "_Surface", 1f);
                changed |= SetFloat(m, "_Blend", 0f);
                changed |= SetFloat(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
                changed |= SetFloat(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                changed |= SetFloat(m, "_SrcBlendAlpha", (float)BlendMode.One);
                changed |= SetFloat(m, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                changed |= SetFloat(m, "_ZWrite", 0f);
                changed |= SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", true);
                changed |= SetTag(m, "RenderType", "Transparent");
                changed |= SetQueue(m, (int)RenderQueue.Transparent);
                if (m.GetShaderPassEnabled("DepthOnly"))
                {
                    m.SetShaderPassEnabled("DepthOnly", false);
                    changed = true;
                }
            }
            else
            {
                changed |= SetFloat(m, "_Surface", 0f);
                changed |= SetFloat(m, "_Blend", 0f);
                changed |= SetFloat(m, "_SrcBlend", (float)BlendMode.One);
                changed |= SetFloat(m, "_DstBlend", (float)BlendMode.Zero);
                changed |= SetFloat(m, "_ZWrite", 1f);
                changed |= SetFloat(m, "_Smoothness", 0f);
                changed |= SetFloat(m, "_Glossiness", 0f);
                changed |= SetFloat(m, "_Metallic", 0f);
                changed |= SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", false);
                changed |= SetKeyword(m, "_ALPHATEST_ON", false);
                changed |= SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
                changed |= SetTag(m, "RenderType", "Opaque");
                changed |= SetQueue(m, (int)RenderQueue.Geometry);
                changed |= SetColor(m, "_BaseColor", Color.white);
                changed |= SetColor(m, "_Color", Color.white);
            }

            if (!m.enableInstancing)
            {
                m.enableInstancing = true;
                changed = true;
            }

            return changed;
        }

        private static bool SetFloat(Material m, string property, float value)
        {
            if (!m.HasProperty(property) || Mathf.Approximately(m.GetFloat(property), value))
            {
                return false;
            }

            m.SetFloat(property, value);
            return true;
        }

        private static bool SetColor(Material m, string property, Color value)
        {
            if (!m.HasProperty(property) || m.GetColor(property) == value)
            {
                return false;
            }

            m.SetColor(property, value);
            return true;
        }

        private static bool SetKeyword(Material m, string keyword, bool enabled)
        {
            if (m.IsKeywordEnabled(keyword) == enabled)
            {
                return false;
            }

            if (enabled)
            {
                m.EnableKeyword(keyword);
            }
            else
            {
                m.DisableKeyword(keyword);
            }

            return true;
        }

        private static bool SetTag(Material m, string tag, string value)
        {
            if (m.GetTag(tag, false, string.Empty) == value)
            {
                return false;
            }

            m.SetOverrideTag(tag, value);
            return true;
        }

        private static bool SetQueue(Material m, int queue)
        {
            if (m.renderQueue == queue)
            {
                return false;
            }

            m.renderQueue = queue;
            return true;
        }
    }
}
