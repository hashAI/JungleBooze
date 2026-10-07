using System.Collections;
using JungleBooze.App;
using JungleBooze.Gameplay.Session;
using JungleBooze.UI.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// The runtime bootstrap builds a playable run into an empty scene named "Run" and nothing anywhere else.
    /// Each test creates its own empty scene, so the project's Run.unity is not needed.
    /// </summary>
    public sealed class RunSceneBootstrapTests
    {
        private Scene _previous;
        private Scene _scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previous = SceneManager.GetActiveScene();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_previous.IsValid() && _previous.isLoaded)
            {
                SceneManager.SetActiveScene(_previous);
            }

            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }

            _scene = default;
        }

        [UnityTest]
        public IEnumerator SceneNamedRun_GetsCameraLightSessionAndHud()
        {
            _scene = SceneManager.CreateScene(RunSceneBootstrap.RunSceneName);
            SceneManager.SetActiveScene(_scene);

            RunDriver driver = RunSceneBootstrap.BootstrapIfRunScene(_scene);

            Assert.IsNotNull(driver, "Bootstrap must act in a scene named Run.");
            Assert.IsNotNull(driver.Session);
            Assert.AreEqual(_scene, driver.gameObject.scene);
            Assert.AreEqual(4, driver.ViewCount);

            Camera camera = FindInScene<Camera>(_scene);
            Assert.IsNotNull(camera, "A camera is created.");
            Assert.AreEqual("MainCamera", camera.tag);
            Assert.AreEqual(60f, camera.fieldOfView, 1e-3f);

            Light light = FindInScene<Light>(_scene);
            Assert.IsNotNull(light);
            Assert.AreEqual(LightType.Directional, light.type);

            Assert.IsNotNull(FindInScene<HudView>(_scene), "The HUD is created.");

            // The loop runs: frames advance the simulation and the camera follows HERO forward.
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            if (driver.Session.Phase == SessionPhase.Running)
            {
                Assert.Greater(driver.Session.Runner.NextTick, 0L);
                Assert.Greater(camera.transform.position.z, -6.5f + (float)driver.Session.DistanceM - 1f);
            }
        }

        [UnityTest]
        public IEnumerator OtherSceneName_IsLeftAlone()
        {
            _scene = SceneManager.CreateScene("NotRun");
            SceneManager.SetActiveScene(_scene);

            Assert.IsNull(RunSceneBootstrap.BootstrapIfRunScene(_scene));
            Assert.AreEqual(0, _scene.rootCount);
            yield return null;
        }

        internal static T FindInScene<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
