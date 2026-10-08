using System.Collections.Generic;
using JungleBooze.App.LookTest;
using JungleBooze.Editor.Setup;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Creates and assigns <c>URP-Realistic</c> (ADR 0004): Forward, HDR (32-bit), MSAA, main-light shadows
    /// (resolution, distance, cascades and soft shadows from <see cref="LookTestConfigAsset"/>), no additional
    /// lights, no depth or opaque texture, HDR color grading with a 32 LUT, SRP Batcher on. The First Playable asset
    /// <c>URP-Mobile</c> stays untouched; <see cref="RestoreMobile"/> switches back to it. Values are written through
    /// serialized fields (the same approach as ProjectBootstrap); a field missing in another URP version is reported
    /// and skipped. Also turns on Player > Frame Timing Stats so the look-test overlay can show CPU and GPU time.
    /// </summary>
    public static class LookTestPipelineSetup
    {
        public const string PipelinePath = ProjectBootstrap.RenderingFolder + "/URP-Realistic.asset";
        public const string RendererPath = ProjectBootstrap.RenderingFolder + "/URP-Realistic-Renderer.asset";

        /// <summary>Creates (or updates) URP-Realistic and makes it the active pipeline. Returns notes for the log.</summary>
        public static List<string> ApplyRealistic(LookTestConfigAsset config)
        {
            var notes = new List<string>();
            LookTestAssets.EnsureFolder(ProjectBootstrap.RenderingFolder);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            var rendererSo = new SerializedObject(rendererData);
            SetInt(rendererSo, "m_RenderingMode", 0, notes); // Forward.
            SerializedProperty postProcess = rendererSo.FindProperty("postProcessData") ?? rendererSo.FindProperty("m_PostProcessData");
            if (postProcess != null && postProcess.objectReferenceValue == null)
            {
                ScriptableObject data = ProjectBootstrap.FindUrpPostProcessData();
                if (data != null)
                {
                    postProcess.objectReferenceValue = data;
                }
                else
                {
                    notes.Add("URP post-processing data not found: post-processing will not render.");
                }
            }

            rendererSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }

            var so = new SerializedObject(asset);
            SetBool(so, "m_SupportsHDR", config.Hdr, notes);
            SetInt(so, "m_HDRColorBufferPrecision", 0, notes); // 32 bits (R11G11B10): half the bandwidth of 64.
            SetInt(so, "m_MSAA", config.MsaaSamples, notes);
            SetFloat(so, "m_RenderScale", 1f, notes); // Device builds set the scale at run time (LookTestRoot).
            SetBool(so, "m_RequireDepthTexture", false, notes);
            SetBool(so, "m_RequireOpaqueTexture", false, notes);
            SetInt(so, "m_MainLightRenderingMode", 1, notes); // Per pixel.
            SetBool(so, "m_MainLightShadowsSupported", true, notes);
            SetInt(so, "m_MainLightShadowmapResolution", config.ShadowResolution, notes);
            SetInt(so, "m_AdditionalLightsRenderingMode", 0, notes); // Disabled.
            SetFloat(so, "m_ShadowDistance", config.ShadowDistanceM, notes);
            SetInt(so, "m_ShadowCascadeCount", config.ShadowCascades, notes);
            SetFloat(so, "m_ShadowDepthBias", 1f, notes);
            SetFloat(so, "m_ShadowNormalBias", 1f, notes);
            SetBool(so, "m_SoftShadowsSupported", config.SoftShadows, notes);
            SetInt(so, "m_ColorGradingMode", 1, notes); // High dynamic range grading (needed for ACES on HDR).
            SetInt(so, "m_ColorGradingLutSize", 32, notes);
            SetBool(so, "m_UseSRPBatcher", true, notes);
            SetBool(so, "m_SupportsDynamicBatching", false, notes);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            if (GraphicsSettings.defaultRenderPipeline != asset)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
                notes.Add("Graphics: render pipeline set to URP-Realistic (JungleBooze > Look Test > Restore URP-Mobile switches back).");
            }

            if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != asset)
            {
                QualitySettings.renderPipeline = asset;
                notes.Add("Quality level override set to URP-Realistic.");
            }

            if (!PlayerSettings.enableFrameTimingStats)
            {
                PlayerSettings.enableFrameTimingStats = true;
                notes.Add("Player: Frame Timing Stats on (CPU/GPU times in the overlay).");
            }

            AssetDatabase.SaveAssets();
            return notes;
        }

        /// <summary>Switches the project back to the First Playable pipeline asset.</summary>
        public static void RestoreMobile()
        {
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectBootstrap.PipelineAssetPath);
            if (mobile == null)
            {
                Debug.LogWarning("[JungleBooze look test] " + ProjectBootstrap.PipelineAssetPath + " not found. Run JungleBooze > Setup > Run Project Setup.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = mobile;
            if (QualitySettings.renderPipeline != null)
            {
                QualitySettings.renderPipeline = mobile;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[JungleBooze look test] Render pipeline set back to URP-Mobile.");
        }

        private static void SetBool(SerializedObject so, string name, bool value, List<string> notes)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || p.propertyType != SerializedPropertyType.Boolean)
            {
                notes.Add("URP setting '" + name + "' not found; default kept.");
                return;
            }

            p.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value, List<string> notes)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || (p.propertyType != SerializedPropertyType.Integer && p.propertyType != SerializedPropertyType.Enum))
            {
                notes.Add("URP setting '" + name + "' not found; default kept.");
                return;
            }

            p.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value, List<string> notes)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || p.propertyType != SerializedPropertyType.Float)
            {
                notes.Add("URP setting '" + name + "' not found; default kept.");
                return;
            }

            p.floatValue = value;
        }
    }
}
