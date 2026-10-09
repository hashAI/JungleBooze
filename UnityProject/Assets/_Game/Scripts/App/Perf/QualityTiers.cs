using JungleBooze.Core.Perf;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// Run-time device-class switch (ADR 0010). Before the first scene loads it reads
    /// <c>Resources/QualityTiers</c>, caps the frame rate, picks High or Low for the device, selects that Unity quality
    /// level (which carries the tier's URP asset) and sets the render scale for the tier's internal pixel count.
    /// In the editor only the frame-rate cap is applied, so editor views and captures keep the reviewed High settings.
    /// </summary>
    public static class QualityTiers
    {
        public const string ResourcePath = "QualityTiers";

        public static DeviceTier Current { get; private set; } = DeviceTier.High;

        public static bool Applied { get; private set; }

        public static float RenderScale
        {
            get
            {
                var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                return asset != null ? asset.renderScale : 1f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Apply(Resources.Load<QualityTierConfigAsset>(ResourcePath), Application.isEditor);
        }

        /// <summary>Applies the tier for this device. <paramref name="editorMode"/> = frame cap only.</summary>
        public static void Apply(QualityTierConfigAsset config, bool editorMode)
        {
            if (config == null)
            {
                Application.targetFrameRate = 60;
                Debug.LogWarning("[JungleBooze] Resources/" + ResourcePath + " not found: quality tier not applied (60 fps cap only).");
                return;
            }

            Application.targetFrameRate = config.TargetFrameRate;
            if (editorMode)
            {
                return;
            }

            Current = config.Classify(SystemInfo.deviceModel, SystemInfo.systemMemorySize);
            string levelName = config.QualityLevelName(Current);
            int level = System.Array.IndexOf(QualitySettings.names, levelName);
            if (level >= 0 && level != QualitySettings.GetQualityLevel())
            {
                QualitySettings.SetQualityLevel(level, true);
            }
            else if (level < 0)
            {
                Debug.LogWarning("[JungleBooze] Quality level '" + levelName + "' not found; keeping " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ".");
            }

            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                asset.renderScale = DeviceTierRules.RenderScaleFor(Screen.width, Screen.height, config.TargetMegapixels(Current), config.MinRenderScale, config.MaxRenderScale);
            }

            Applied = true;
            Debug.Log("[JungleBooze] Quality tier " + Current + " (" + SystemInfo.deviceModel + ", " + SystemInfo.systemMemorySize + " MB): level " +
                      QualitySettings.names[QualitySettings.GetQualityLevel()] + ", render scale " + RenderScale.ToString("0.00") + " of " + Screen.width + "x" + Screen.height + ".");
        }
    }
}
