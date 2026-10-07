using System;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Views;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JungleBooze.App
{
    /// <summary>
    /// First Playable composition root (ADR 0003 decision 5). The Run scene is empty; after it loads, this builds
    /// the portrait follow camera, the key light, fog, the gray-box views, the HUD, the input adapter and the
    /// <see cref="GameSession"/>, and hands them to a <see cref="RunDriver"/>. Acts only when the active scene is
    /// named <see cref="RunSceneName"/>, so test scenes and later menu scenes are left alone.
    /// </summary>
    public static class RunSceneBootstrap
    {
        public const string RunSceneName = "Run";
        public const string RootName = "RunRoot";
        public const int TargetFrameRate = 60;

        /// <summary>Camera far plane in m: past the fog end so fogged geometry fades instead of clipping.</summary>
        private const float CameraFarClipM = 160f;

        /// <summary>Coin pool size when the world is not the track world.</summary>
        private const int CoinViewFallbackPool = 64;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterFirstSceneLoad()
        {
            BootstrapIfRunScene(SceneManager.GetActiveScene());
        }

        /// <summary>Builds the run in <paramref name="scene"/> if it is named "Run". Returns the driver, or null.</summary>
        public static RunDriver BootstrapIfRunScene(Scene scene)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, RunSceneName, StringComparison.Ordinal))
            {
                return null;
            }

            return Build(scene, RunConfigLoader.Load(), SeedFromClock());
        }

        /// <summary>Builds every run object into <paramref name="scene"/>. Tests call this with fixed configs and seed.</summary>
        public static RunDriver Build(Scene scene, RunConfigSet configs, ulong sessionSeed)
        {
            if (configs == null)
            {
                throw new ArgumentNullException(nameof(configs));
            }

            Application.targetFrameRate = TargetFrameRate;

            var root = new GameObject(RootName);
            if (scene.IsValid() && root.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            RunnerPresentationConfig presentation = configs.Presentation;
            ApplyLightingAndFog(presentation);

            // Camera (portrait follow camera, spec 001 section 10).
            var cameraObject = new GameObject("RunCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = StylePalette.JungleFog;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = CameraFarClipM;
            camera.allowHDR = false;
            cameraObject.AddComponent<AudioListener>();
            FollowCameraView cameraView = cameraObject.AddComponent<FollowCameraView>();
            cameraView.Init(camera, presentation);

            // Key light (style guide section 5, Jungle; no realtime shadows, ADR 0001).
            var lightObject = new GameObject("KeyLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = StylePalette.JungleKeyLight;
            keyLight.intensity = 1.1f;
            keyLight.shadows = LightShadows.None;

            // Gray-box world.
            var kit = new GrayBoxKit();
            var groundObject = new GameObject("Ground");
            groundObject.transform.SetParent(root.transform, false);
            GroundView ground = groundObject.AddComponent<GroundView>();
            ground.Init(kit, configs.Runner, presentation.FogEndM);

            // Stage C2: ravines, obstacles and coins from the generated track.
            IRunWorldFactory worldFactory = CreateWorldFactory(configs);
            TrackRunSetup trackSetup = (worldFactory as TrackRunWorldFactory)?.Setup;
            var gapObject = new GameObject("Gaps");
            gapObject.transform.SetParent(root.transform, false);
            GapView gaps = gapObject.AddComponent<GapView>();
            gaps.Init(kit, configs.Runner, presentation.FogEndM);

            var obstacleObject = new GameObject("Obstacles");
            obstacleObject.transform.SetParent(root.transform, false);
            ObstacleView obstacles = obstacleObject.AddComponent<ObstacleView>();
            obstacles.Init(kit, configs.Runner, presentation.FogEndM);

            var coinObject = new GameObject("Coins");
            coinObject.transform.SetParent(root.transform, false);
            CoinView coinView = coinObject.AddComponent<CoinView>();
            coinView.Init(kit, trackSetup != null ? trackSetup.Track.MaxActiveCoins : CoinViewFallbackPool, presentation.FogEndM);

            var runnerObject = new GameObject("Pista");
            runnerObject.transform.SetParent(root.transform, false);
            RunnerView runnerView = runnerObject.AddComponent<RunnerView>();
            runnerView.Init(kit, configs.Runner, presentation);

            // Input and session.
            var input = new PlayerInputAdapter(configs.Input, PlayerInputAdapter.PixelsPerPointForDpi(Screen.dpi));
            var session = new GameSession(
                configs.Runner,
                configs.SpeedCurve,
                input,
                worldFactory,
                presentation.ToSessionTimings(),
                sessionSeed);

            RunDriver driver = root.AddComponent<RunDriver>();

            // HUD.
            var hudObject = new GameObject("Hud", typeof(RectTransform));
            hudObject.transform.SetParent(root.transform, false);
            HudView hud = hudObject.AddComponent<HudView>();
            hud.Build(driver, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            driver.Init(session, input, new IRunView[] { ground, gaps, obstacles, coinView, runnerView, cameraView, hud }, kit);

            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] Run started. Seed " + session.RunSeed + ". Config: " + configs.Source + ".");
            }

            return driver;
        }

        /// <summary>
        /// THE SWAP POINT for the run world: the generated track world (spec 002) built from the default track
        /// configs. [ASSUMED] The track config assets are not loaded yet; the in-code start values are used.
        /// </summary>
        private static IRunWorldFactory CreateWorldFactory(RunConfigSet configs)
        {
            return new TrackRunWorldFactory();
        }

        /// <summary>Session seed from the wall clock. Seeds pick the run; the simulation itself never reads the clock.</summary>
        public static ulong SeedFromClock()
        {
            return (ulong)DateTime.UtcNow.Ticks;
        }

        private static void ApplyLightingAndFog(RunnerPresentationConfig presentation)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(StylePalette.JungleShadowTint, StylePalette.JungleSkyHorizon, 0.55f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = StylePalette.JungleFog;
            RenderSettings.fogStartDistance = presentation.FogStartM;
            RenderSettings.fogEndDistance = presentation.FogEndM;
        }
    }
}
