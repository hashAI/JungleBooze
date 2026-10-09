using System;
using System.IO;
using System.Text;
using JungleBooze.App.HeroBasin;
using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Batch entry points for the hero basin (run with tools/ci/unity.sh method, without -nographics for captures):
    ///   JungleBooze.Editor.HeroBasin.HeroBasinBatch.BuildScene
    ///   JungleBooze.Editor.HeroBasin.HeroBasinBatch.Capture        [-jbHeroOut /tmp/junglebooze-hero]
    ///   JungleBooze.Editor.HeroBasin.HeroBasinBatch.BuildAndCapture
    /// Capture writes F4_landscape.png (2532x1170), P1_portrait.png (1170x2532), F4_compare.jpg (keyframe |
    /// landscape | portrait at one height) and stats.txt (draws and triangles per budget layer, as the look test).
    /// </summary>
    public static class HeroBasinBatch
    {
        public const string KeyframePath = "design/aurelia/keyframes/F4_e_openai_medium.jpg";
        private const string LogPrefix = "[JungleBooze] ";
        private const string DefaultOut = "/tmp/junglebooze-hero";

        public static void BuildScene()
        {
            Run(() =>
            {
                HeroBasinBuilder.Build();
                return 0;
            });
        }

        public static void Capture()
        {
            Run(() => CaptureShots(Arg("-jbHeroOut") ?? DefaultOut));
        }

        public static void BuildAndCapture()
        {
            Run(() =>
            {
                HeroBasinBuilder.Build();
                return CaptureShots(Arg("-jbHeroOut") ?? DefaultOut);
            });
        }

        [MenuItem("JungleBooze/Hero Basin/Build Scene")]
        private static void BuildFromMenu()
        {
            HeroBasinBuilder.Build();
        }

        public static int CaptureShots(string outDir)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(LogPrefix + "No GPU device: run Unity without -nographics to capture.");
                return 2;
            }

            EditorSceneManager.OpenScene(HeroBasinBuilder.ScenePath, OpenSceneMode.Single);
            HeroBasinConfigAsset h = HeroBasinBuilder.EnsureConfig();
            var look = AssetDatabase.LoadAssetAtPath<LookTestConfigAsset>(HeroBasinBuilder.LookPath);
            Camera camera = Object.FindFirstObjectByType<Camera>();
            LookTestAtmosphere atmosphere = Object.FindFirstObjectByType<LookTestAtmosphere>();
            if (atmosphere != null)
            {
                atmosphere.Apply();
            }

            GameObject pista = GameObject.Find("Pista");
            if (pista != null)
            {
                HeroBasinBuilder.PoseIdle(pista, h.PistaIdleTimeS);
            }

            Directory.CreateDirectory(outDir);
            var stats = new StringBuilder();
            stats.AppendLine("Hero basin frame stats (editor, " + SystemInfo.graphicsDeviceName + "). Budget: 250 draws, 350,000 tris main view.");

            var landscapeSize = new Vector2Int(2532, 1170);
            HeroBasinBuilder.PoseCamera(camera, h, false, (float)landscapeSize.x / landscapeSize.y);
            Texture2D landscape = LookTestBatch.Render(camera, landscapeSize);
            File.WriteAllBytes(Path.Combine(outDir, "F4_landscape.png"), landscape.EncodeToPNG());
            stats.AppendLine().AppendLine("F4 landscape").Append(LookTestFrameBudget.Measure(camera, look));

            var portraitSize = new Vector2Int(1170, 2532);
            HeroBasinBuilder.PoseCamera(camera, h, true, (float)portraitSize.x / portraitSize.y);
            Texture2D portrait = LookTestBatch.Render(camera, portraitSize);
            File.WriteAllBytes(Path.Combine(outDir, "P1_portrait.png"), portrait.EncodeToPNG());
            stats.AppendLine().AppendLine("P1 portrait").Append(LookTestFrameBudget.Measure(camera, look));
            camera.ResetAspect();

            string keyframeFile = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", KeyframePath));
            Texture2D keyframe = null;
            if (File.Exists(keyframeFile))
            {
                keyframe = new Texture2D(2, 2, TextureFormat.RGB24, false);
                keyframe.LoadImage(File.ReadAllBytes(keyframeFile));
            }

            Texture2D compare = SideBySide(1170, 24, keyframe, landscape, portrait);
            File.WriteAllBytes(Path.Combine(outDir, "F4_compare.jpg"), compare.EncodeToJPG(90));
            File.WriteAllText(Path.Combine(outDir, "stats.txt"), stats.ToString());
            Debug.Log(LogPrefix + "Hero basin shots written to " + outDir + Environment.NewLine + stats);

            Object.DestroyImmediate(landscape);
            Object.DestroyImmediate(portrait);
            Object.DestroyImmediate(compare);
            if (keyframe != null)
            {
                Object.DestroyImmediate(keyframe);
            }

            return 0;
        }

        /// <summary>Images scaled to one height, left to right, with a dark gap between them.</summary>
        public static Texture2D SideBySide(int height, int gap, params Texture2D[] images)
        {
            int width = 0;
            int count = 0;
            foreach (Texture2D image in images)
            {
                if (image != null)
                {
                    width += Mathf.RoundToInt(height * (float)image.width / image.height) + (count > 0 ? gap : 0);
                    count++;
                }
            }

            var result = new Texture2D(Mathf.Max(1, width), height, TextureFormat.RGB24, false);
            var fill = new Color32[result.width * height];
            for (int i = 0; i < fill.Length; i++)
            {
                fill[i] = new Color32(24, 24, 26, 255);
            }

            result.SetPixels32(fill);
            int x = 0;
            foreach (Texture2D image in images)
            {
                if (image == null)
                {
                    continue;
                }

                int w = Mathf.RoundToInt(height * (float)image.width / image.height);
                RenderTexture rt = RenderTexture.GetTemporary(w, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(image, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                result.ReadPixels(new Rect(0, 0, w, height), x, 0);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                x += w + gap;
            }

            result.Apply();
            return result;
        }

        private static void Run(Func<int> action)
        {
            int code;
            try
            {
                code = action();
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Hero basin failed: " + exception);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
