using System;
using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Tutorial;
using JungleBooze.Gameplay.Views;
using JungleBooze.Gameplay.Vine;
using JungleBooze.Services.Audio;
using JungleBooze.Services.Meta;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using JungleBooze.UI.Menus;
using JungleBooze.UI.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleBooze.App
{
    /// <summary>
    /// First Playable composition root (ADR 0003 decision 5). The Run scene is empty; after it loads, this builds
    /// the portrait follow camera, the key light, fog, the gray-box views, the HUD, the input adapter and the
    /// <see cref="GameSession"/>, and hands them to a <see cref="RunDriver"/>. Acts only when the active scene is
    /// named <see cref="RunSceneName"/>, so test scenes and later menu scenes are left alone.
    /// The game itself (Play mode, device) starts at the main menu with the local save from
    /// <c>Application.persistentDataPath</c>; <see cref="BootstrapIfRunScene"/> and the 3-argument
    /// <see cref="Build(Scene, RunConfigSet, ulong)"/> keep the test setup: Ready prompt and an in-memory save.
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
            Scene scene = SceneManager.GetActiveScene();
            if (!IsRunScene(scene))
            {
                return;
            }

            // The game: main menu first, real save on the device.
            PlayerSave save = PlayerSave.Load(new FileSaveStorage(Application.persistentDataPath));

            // GDD 14.4 / 19: count app sessions (the free continue belongs to session 1).
            save.BeginSession();
            Build(scene, RunConfigLoader.Load(), SeedFromClock(), save, true);
        }

        /// <summary>
        /// Builds the run in <paramref name="scene"/> if it is named "Run" (Ready prompt, in-memory save, for tests).
        /// Returns the driver, or null.
        /// </summary>
        public static RunDriver BootstrapIfRunScene(Scene scene)
        {
            if (!IsRunScene(scene))
            {
                return null;
            }

            return Build(scene, RunConfigLoader.Load(), SeedFromClock());
        }

        /// <summary>
        /// Builds every run object into <paramref name="scene"/> starting at the Ready prompt with an in-memory save.
        /// Tests call this with fixed configs and seed.
        /// </summary>
        public static RunDriver Build(Scene scene, RunConfigSet configs, ulong sessionSeed)
        {
            return Build(scene, configs, sessionSeed, PlayerSave.CreateInMemory(), false);
        }

        /// <summary>
        /// Builds every run object into <paramref name="scene"/>. <paramref name="save"/> holds best score, wallet
        /// and settings; <paramref name="startAtMenu"/>: true opens the main menu, false the Ready prompt.
        /// </summary>
        public static RunDriver Build(Scene scene, RunConfigSet configs, ulong sessionSeed, PlayerSave save, bool startAtMenu)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

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
            EnvironmentLookConfig look = RunConfigLoader.LoadEnvironmentLook();
            WorldTheme startTheme = WorldThemes.Get(WorldKind.Jungle, false);
            ApplyLightingAndFog(presentation, look, startTheme);

            // Spec 003 T3: the route every view places itself on. Straight (identity) unless RouteTuning.DebugRoute is on
            // or, in dev builds, F4 switched the next run to the curved debug route. Rebuilt from the run seed on every BeginRun.
            // Spec 003 T2: RouteTuning.UseGeneratedRoute (default off; dev menu JungleBooze > Route) swaps in the seeded RouteGenerator.
            RouteTuning routeTuning = RouteTuning.LoadOrDefault();
            var routeSource = new SelectableRouteSource { Curved = routeTuning.DebugRoute };
            var routeChunks = new SessionRouteChunkSource();
            bool generatedRoute = routeTuning.UseGeneratedRoute;
            var pathFrame = new PathFrame(routeTuning, generatedRoute ? (IRouteSource)new RouteGenerator(routeTuning, routeChunks) : routeSource);
            var pathFrameView = new PathFrameRunView(pathFrame, Debug.isDebugBuild && !generatedRoute ? routeSource : null);

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
            cameraView.SetFrame(pathFrame);
            cameraView.Init(camera, presentation);

            // Key light (style guide section 5, Jungle; angle and intensity from the look config). No realtime shadow
            // maps (ADR 0001): plants carry painted blob shadows in their meshes, HERO has her blob shadow.
            var lightObject = new GameObject("KeyLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localRotation = WorldThemeView.KeyRotation(startTheme, look);
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = startTheme.KeyLight;
            keyLight.intensity = look.KeyIntensity;
            keyLight.shadows = LightShadows.None;

            // Gradient sky and far canopy silhouettes around the camera (drawn first, unfogged).
            var skyObject = new GameObject("Sky");
            skyObject.transform.SetParent(root.transform, false);
            SkyView sky = skyObject.AddComponent<SkyView>();
            sky.Init(camera, look);

            // Ground, trail and jungle walls (gray-box tiles when the art is missing).
            var kit = new GrayBoxKit();
            var groundObject = new GameObject("Ground");
            groundObject.transform.SetParent(root.transform, false);
            GroundView ground = groundObject.AddComponent<GroundView>();
            ground.SetFrame(pathFrame);
            ground.Init(kit, configs.Runner, presentation.FogEndM, look);

            // Stage C2: ravines, obstacles and coins from the generated track.
            IRunWorldFactory worldFactory = CreateWorldFactory(configs);
            TrackRunSetup trackSetup = (worldFactory as TrackRunWorldFactory)?.Setup;
            var gapObject = new GameObject("Gaps");
            gapObject.transform.SetParent(root.transform, false);
            GapView gaps = gapObject.AddComponent<GapView>();
            gaps.SetFrame(pathFrame);
            gaps.Init(kit, configs.Runner, presentation.FogEndM);

            var obstacleObject = new GameObject("Obstacles");
            obstacleObject.transform.SetParent(root.transform, false);
            ObstacleView obstacles = obstacleObject.AddComponent<ObstacleView>();
            obstacles.SetFrame(pathFrame);
            obstacles.Init(kit, configs.Runner, presentation.FogEndM);

            // GDD 8.3: thorn patches and telegraphed lane strikes (ObstacleView skips those archetypes).
            var hazardObject = new GameObject("Hazards");
            hazardObject.transform.SetParent(root.transform, false);
            HazardView hazardView = hazardObject.AddComponent<HazardView>();
            hazardView.SetFrame(pathFrame);
            hazardView.Init(kit, configs.Runner, presentation.FogEndM);

            // GDD 7: vines (rope, glow, release ring, Perfect feedback).
            var vineObject = new GameObject("Vines");
            vineObject.transform.SetParent(root.transform, false);
            VineView vineView = vineObject.AddComponent<VineView>();
            vineView.SetFrame(pathFrame);
            vineView.Init(
                kit,
                configs.Runner,
                trackSetup != null ? trackSetup.Vines : VineConfig.CreateDefault(),
                presentation.FogEndM,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            var coinObject = new GameObject("Coins");
            coinObject.transform.SetParent(root.transform, false);
            CoinView coinView = coinObject.AddComponent<CoinView>();
            coinView.SetFrame(pathFrame);
            coinView.Init(kit, trackSetup != null ? trackSetup.Track.MaxActiveCoins : CoinViewFallbackPool, presentation.FogEndM);

            // GDD 10: pickup icons, shield bubble, magnet rings and speed lines.
            var powerUpObject = new GameObject("PowerUps");
            powerUpObject.transform.SetParent(root.transform, false);
            PowerUpView powerUpView = powerUpObject.AddComponent<PowerUpView>();
            powerUpView.SetFrame(pathFrame);
            powerUpView.Init(kit, configs.Runner, presentation, presentation.FogEndM);

            // GDD 9: world themes (palette, fog, ground and obstacle tint) and the gateway frames.
            var worldObject = new GameObject("Worlds");
            worldObject.transform.SetParent(root.transform, false);
            WorldThemeView worldView = worldObject.AddComponent<WorldThemeView>();
            worldView.SetFrame(pathFrame);
            worldView.Init(
                camera,
                keyLight,
                ground,
                obstacles,
                kit,
                configs.Runner,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                presentation.FogEndM,
                sky,
                look);

            var runnerObject = new GameObject("Pista");
            runnerObject.transform.SetParent(root.transform, false);
            RunnerView runnerView = runnerObject.AddComponent<RunnerView>();
            runnerView.SetFrame(pathFrame);
            runnerView.Init(kit, configs.Runner, presentation);

            // GDD 15: the companion macaw (gray-box).
            CompanionConfig companionConfig = RunConfigLoader.LoadCompanion();
            var companionObject = new GameObject("Companion");
            companionObject.transform.SetParent(root.transform, false);
            CompanionView companionView = companionObject.AddComponent<CompanionView>();
            companionView.SetFrame(pathFrame);
            companionView.Init(kit, configs.Runner, companionConfig);

            // Input and session.
            var input = new PlayerInputAdapter(configs.Input, PlayerInputAdapter.PixelsPerPointForDpi(Screen.dpi));
            var session = new GameSession(
                configs.Runner,
                configs.SpeedCurve,
                input,
                worldFactory,
                presentation.ToSessionTimings(),
                sessionSeed,
                startAtMenu ? SessionPhase.Menu : SessionPhase.Ready);
            routeChunks.Bind(session);

            // GDD 15 companion (meter, Lift, call-outs) and GDD 14.4 Continue rules.
            session.RecordDistanceM = save.BestDistanceM;
            session.ConfigureCompanion(companionConfig);
            session.ContinueRules = RunConfigLoader.LoadContinueRules();

            RunDriver driver = root.AddComponent<RunDriver>();

            // HUD.
            var hudObject = new GameObject("Hud", typeof(RectTransform));
            hudObject.transform.SetParent(root.transform, false);
            HudView hud = hudObject.AddComponent<HudView>();
            // GDD 13: missions, daily reward and shop on the same save.
            var progress = new MetaProgress(save, RunConfigLoader.LoadMeta(), new SystemDayClock());
            var eventCounter = new RunEventCounter();
            hud.Build(driver, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), save, progress);
            var recorder = new RunResultRecorder(save);
            recorder.AttachMeta(progress, eventCounter);
            driver.AttachMeta(recorder, hud);
            driver.AttachProgress(progress);
            session.ContinuePolicy = driver;

            // Companion HUD (Lift meter, call-out bubble) and the Continue screen, each on its own canvas.
            var companionHudObject = new GameObject("CompanionHud", typeof(RectTransform));
            companionHudObject.transform.SetParent(root.transform, false);
            CompanionHudView companionHud = companionHudObject.AddComponent<CompanionHudView>();
            companionHud.Build(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), companionConfig, camera, companionView);

            var continueObject = new GameObject("ContinueScreen", typeof(RectTransform));
            continueObject.transform.SetParent(root.transform, false);
            ContinueView continueView = continueObject.AddComponent<ContinueView>();
            continueView.Build(driver, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), save);

            // GDD 12: first-run tutorial (hints, ghost hand, Skip once completed); the driver starts it on Play.
            var tutorial = new TutorialDirector();
            var tutorialObject = new GameObject("TutorialHud", typeof(RectTransform));
            tutorialObject.transform.SetParent(root.transform, false);
            TutorialView tutorialView = tutorialObject.AddComponent<TutorialView>();
            tutorialView.Build(tutorial, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), save.ReduceMotion);

            // Reduce Motion (owner decision, [ASSUMED] one switch): the SAVE value drives it, live. Off: vine slow-mo,
            // camera tilt / shake / FOV swing, shards and speed lines, and the tutorial hand. The shared config asset
            // is never written.
            Action applyReduceMotion = () =>
            {
                bool reduce = save.ReduceMotion;
                session.VineHangSlowdown = !reduce;
                cameraView.ReduceMotion = reduce;
                powerUpView.ReduceMotion = reduce;
                tutorialView.SetReduceMotion(reduce);
            };
            applyReduceMotion();
            save.SettingsChanged += applyReduceMotion;
            driver.AttachTutorial(tutorial);

            // Audio: pooled playback (Resources/RunAudioCatalog), volumes follow the save's settings.
            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(root.transform, false);
            AudioPlayback audio = audioObject.AddComponent<AudioPlayback>();
            audio.SetCatalog(Resources.Load<AudioCatalog>("RunAudioCatalog"));
            audio.Bind(save);
            audioObject.AddComponent<UiTapAudioBinder>().Init(audio);
            var audioView = new RunAudioView(audio);

            // Dev aid (editor and development builds): F3 draws every simulation hitbox, and each stumble or death logs
            // which obstacle it was and whether anything was drawn for it. Last in the list, so it sees this frame's pieces.
            HitboxDebugView hitboxDebug = null;
            if (Debug.isDebugBuild)
            {
                var hitboxObject = new GameObject("HitboxDebug");
                hitboxObject.transform.SetParent(root.transform, false);
                hitboxDebug = hitboxObject.AddComponent<HitboxDebugView>();
                hitboxDebug.SetFrame(pathFrame);
                hitboxDebug.Init(kit, configs.Runner, obstacles, hazardView);
            }

            var views = new System.Collections.Generic.List<IRunView>
            {
                pathFrameView, eventCounter, tutorial, audioView, ground, worldView, gaps, obstacles, hazardView, vineView, coinView, powerUpView, runnerView, companionView, cameraView, hud, companionHud, tutorialView, continueView,
            };
            if (hitboxDebug != null)
            {
                views.Add(hitboxDebug);
            }

            driver.Init(session, input, views.ToArray(), kit);

            // Performance audit #3 / #4: glyphs and pooled pieces are prepared behind the main menu, not at first use.
            var prewarm = root.AddComponent<RunPrewarm>();
            prewarm.Begin(
                session,
                camera,
                new[]
                {
                    ground.transform, gaps.transform, obstacles.transform, hazardView.transform, vineView.transform,
                    coinView.transform, powerUpView.transform, worldView.transform, runnerView.transform, companionView.transform,
                },
                worldView,
                vineView);

            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] Run started. Seed " + session.RunSeed + ". Config: " + configs.Source + ".");
            }

            if (Debug.isDebugBuild)
            {
                LogAssetReport();
            }

            return driver;
        }

        /// <summary>One Console line saying which art and audio assets were found, so a silent gray-box fallback is visible.</summary>
        private static void LogAssetReport()
        {
            bool pista = Resources.Load<GameObject>("Characters/Pista/Pista") != null;
            bool duko = Resources.Load<GameObject>("Characters/Duko/Duko") != null;
            bool coin = EnvironmentArt.Exists("Pickup_Coin");
            bool tree = EnvironmentArt.Exists("Foliage_TreeA");
            bool catalog = Resources.Load<AudioCatalog>("RunAudioCatalog") != null;
            Debug.Log("[JungleBooze] Assets found: Pista=" + pista + ", Duko=" + duko + ", Pickup_Coin=" + coin
                + ", Foliage_TreeA=" + tree + ", audio catalog=" + catalog
                + ". A False means that piece is using its gray-box fallback or is silent.");

            var report = new System.Text.StringBuilder("[JungleBooze] Environment art: ");
            for (int i = 0; i < EnvironmentArt.AllNames.Length; i++)
            {
                if (i > 0)
                {
                    report.Append(", ");
                }

                report.Append(EnvironmentArt.AllNames[i]).Append('=').Append(EnvironmentArt.Exists(EnvironmentArt.AllNames[i]));
            }

            report.Append(". Textures: ");
            for (int i = 0; i < EnvironmentArt.AllTextureNames.Length; i++)
            {
                if (i > 0)
                {
                    report.Append(", ");
                }

                string name = EnvironmentArt.AllTextureNames[i];
                report.Append(name).Append('=').Append(EnvironmentArt.LoadTexture(name) != null);
            }

            Debug.Log(report.Append('.').ToString());
        }

        /// <summary>
        /// THE SWAP POINT for the run world: the generated track world (spec 002) built from the default track
        /// configs plus vine sections (GDD 7), power-ups (GDD 10) and signature hazards (GDD 8.3). Vine, power-up
        /// and hazard tuning come from their Resources assets when those exist. [ASSUMED] The other track config
        /// assets are not loaded yet; the in-code start values are used.
        /// </summary>
        private static IRunWorldFactory CreateWorldFactory(RunConfigSet configs)
        {
            VineConfig vines = RunConfigLoader.LoadVines();
            PowerUpConfig powerUps = RunConfigLoader.LoadPowerUps();
            HazardConfig hazards = RunConfigLoader.LoadHazards();
            WorldScheduleConfig worlds = RunConfigLoader.LoadWorlds();
            return new TrackRunWorldFactory(TrackRunSetup.CreateDefault(vines, powerUps, hazards, worlds));
        }

        private static bool IsRunScene(Scene scene)
        {
            return scene.IsValid() && string.Equals(scene.name, RunSceneName, StringComparison.Ordinal);
        }

        /// <summary>Session seed from the wall clock. Seeds pick the run; the simulation itself never reads the clock.</summary>
        public static ulong SeedFromClock()
        {
            return (ulong)DateTime.UtcNow.Ticks;
        }

        private static void ApplyLightingAndFog(RunnerPresentationConfig presentation, EnvironmentLookConfig look, in WorldTheme theme)
        {
            WorldThemeView.ApplyAmbient(theme, look);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = theme.Fog;
            RenderSettings.fogStartDistance = presentation.FogStartM;
            RenderSettings.fogEndDistance = presentation.FogEndM;
        }
    }
}
