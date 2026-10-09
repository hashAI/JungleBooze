using System.Collections;
using System.Collections.Generic;
using System.Text;
using JungleBooze.App.FeelTest;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// AC-101-44 with the real scene camera: on the feel course at vMax (16 m/s), every obstacle on Pista's route has
    /// at least half of its front face on screen and unoccluded 1.5 s before contact. Both camera profiles, the
    /// 19.5:9 and 16:9 aspects, and both fork branches (obstacles of the branch not taken are skipped). Occlusion is
    /// a raycast against colliders added to the gray-box course for the test.
    /// </summary>
    public sealed class FeelTestCameraPlayModeTests
    {
        private const float VMax = 16f;
        private const float Lead = 1.5f;
        private const int Columns = 5;
        private const int Rows = 3;
        private const float MinVisibleShare = 0.5f;

        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(2532, 1170),
            new Vector2Int(1920, 1080),
            new Vector2Int(1170, 2532),
            new Vector2Int(1080, 1920),
        };

        [UnityTest]
        public IEnumerator AC44_EveryObstacleOnScreenAndUnoccluded15sBeforeContact()
        {
            SceneManager.LoadScene("FeelTest");
            yield return null;
            yield return null;
            FeelTestRoot root = Object.FindFirstObjectByType<FeelTestRoot>();
            Assert.IsNotNull(root, "FeelTest scene with FeelTestRoot");
            AddColliders(root.CourseView.transform);
            root.SetDebugVisible(false);
            root.SetBotDriving(true);

            var failures = new List<string>();
            int checkedCount = 0;
            foreach (Vector2Int screen in Screens)
            {
                foreach (bool risky in new[] { false, true })
                {
                    checkedCount += Run(root, screen, risky, failures);
                }
            }

            Debug.Log("[JungleBooze] AC-101-44: " + checkedCount + " obstacle checks, " + failures.Count + " failures");
            foreach (string failure in failures)
            {
                Debug.Log("[JungleBooze] AC-101-44 failure: " + failure);
            }
            Assert.Greater(checkedCount, 100);
            CollectionAssert.IsEmpty(failures, "obstacles not visible 1.5 s before contact");
        }

        private static int Run(FeelTestRoot root, Vector2Int screen, bool risky, List<string> failures)
        {
            bool landscape = screen.x >= screen.y;
            Camera camera = root.Camera;
            root.SetBotPrefersRiskyBranch(risky);
            root.SetCameraProfile(landscape, true);
            camera.aspect = (float)screen.x / screen.y;
            root.Restart(new RunOptions { ForcedSpeed = VMax, SkipStartRamp = true });

            CoursePath course = root.RunCourse;
            RunnerSimulation sim = root.Simulation;
            var done = new bool[course.ObstacleCount];
            float halfDepth = sim.Config.Hitbox.RunDepth * 0.5f;
            int checks = 0;
            string tag = screen.x + "x" + screen.y + (risky ? " risky" : " safe");
            for (int guard = 0; guard < 60 * 90 && root.Session.Phase != RunPhase.Results && !sim.State.Finished; guard++)
            {
                root.StepTicks(1);
                Assert.IsFalse(sim.State.Dead, tag + ": the bot died at s = " + sim.State.S);
                if (root.Session.Phase != RunPhase.Running)
                {
                    continue;
                }

                for (int id = 0; id < course.ObstacleCount; id++)
                {
                    ObstacleBox box = course.GetObstacle(id);
                    if (done[id] || sim.State.S + halfDepth < box.SMin - (Lead * VMax))
                    {
                        continue;
                    }

                    done[id] = true;
                    if (OnOtherBranch(course, box, risky))
                    {
                        continue;
                    }

                    checks++;
                    string problem = Check(root, camera, course, id, box);
                    if (problem != null)
                    {
                        failures.Add(tag + ": " + course.GetObstacleLabel(id) + " " + problem);
                    }
                }
            }

            Assert.IsTrue(sim.State.Finished, tag + ": the run finished");
            return checks;
        }

        private static bool OnOtherBranch(CoursePath course, in ObstacleBox box, bool risky)
        {
            for (int i = 0; i < course.ForkCount; i++)
            {
                ForkPoint fork = course.GetFork(i);
                if (box.SMax <= fork.SFront || box.SMin >= fork.SMerge)
                {
                    continue;
                }

                int side = risky ? -fork.SafeSide : fork.SafeSide;
                float c = fork.DividerCenterX;
                float hw = fork.DividerHalfWidth;
                bool right = box.XMin >= c + hw - 0.01f;
                bool left = box.XMax <= c - hw + 0.01f;
                return (side > 0 && left) || (side < 0 && right);
            }

            return false;
        }

        private static string Check(FeelTestRoot root, Camera camera, CoursePath course, int id, in ObstacleBox box)
        {
            Transform world = root.CourseView.transform;
            Transform obstacle = root.CourseView.GetObstacleObject(id).transform;
            course.GetLateralBounds(box.SMin, Mathf.Clamp(box.CenterX, -3f, 3f), out float pathMin, out float pathMax);
            float x0 = Mathf.Max(box.XMin, pathMin);
            float x1 = Mathf.Min(box.XMax, pathMax);
            if (x1 <= x0)
            {
                x0 = box.XMin;
                x1 = box.XMax;
            }

            float y0 = box.YMin + 0.05f;
            float y1 = Mathf.Min(box.YMax, box.YMin + 2f) - 0.05f;
            Vector3 eye = camera.transform.position;
            int inFrustum = 0;
            int visible = 0;
            string occluder = null;
            for (int c = 0; c < Columns; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    float x = Mathf.Lerp(x0, x1, (c + 0.5f) / Columns);
                    float y = Mathf.Lerp(y0, y1, (r + 0.5f) / Rows);
                    Vector3 p = world.TransformPoint(new Vector3(x, y, box.SMin - 0.02f));
                    Vector3 v = camera.WorldToViewportPoint(p);
                    if (v.z <= camera.nearClipPlane || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f)
                    {
                        continue;
                    }

                    inFrustum++;
                    Vector3 d = p - eye;
                    float distance = d.magnitude;
                    if (Physics.Raycast(eye, d / distance, out RaycastHit hit, distance - 0.05f) && !hit.transform.IsChildOf(obstacle))
                    {
                        occluder = hit.transform.name + (hit.transform.parent != null ? " (" + hit.transform.parent.name + ")" : string.Empty);
                        continue;
                    }

                    visible++;
                }
            }

            int total = Columns * Rows;
            if (visible >= total * MinVisibleShare)
            {
                return null;
            }

            var b = new StringBuilder();
            b.Append(visible).Append('/').Append(total).Append(" visible, ").Append(inFrustum).Append(" in frustum");
            if (occluder != null)
            {
                b.Append(", occluded by ").Append(occluder);
            }

            return b.ToString();
        }

        private static void AddColliders(Transform course)
        {
            foreach (MeshFilter filter in course.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name == "Coin" || filter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            Physics.SyncTransforms();
        }
    }
}
