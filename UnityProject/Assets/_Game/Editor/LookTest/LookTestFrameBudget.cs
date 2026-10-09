using JungleBooze.App.LookTest;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Measures one camera view against the budget (editor, CPU side): every enabled renderer whose bounds touch the
    /// view frustum counts one draw per submesh in the main view (the SRP Batcher keeps one draw per renderer and
    /// material). Shadow pass estimate: shadow-casting renderers whose bounds touch the frustum cut at the shadow
    /// distance, widened toward the sun (casters outside the view can throw shadows into it). The real counters come
    /// from the device run (ADR 0004 protocol); this catches regressions on every build.
    /// </summary>
    public static class LookTestFrameBudget
    {
        /// <summary>Skybox, post-processing and the final blit: draws not made by scene renderers.</summary>
        public const int ExtraDraws = 6;

        public static string Measure(Camera camera, LookTestConfigAsset config)
        {
            LookTestBudgetTally tally = Tally(camera, config);
            return tally.Format(config, ExtraDraws);
        }

        public static LookTestBudgetTally Tally(Camera camera, LookTestConfigAsset config)
        {
            Plane[] view = GeometryUtility.CalculateFrustumPlanes(camera);
            float far = camera.farClipPlane;
            camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 1f, config.ShadowDistanceM);
            Plane[] shadowView = GeometryUtility.CalculateFrustumPlanes(camera);
            camera.farClipPlane = far;
            Vector3 toSun = -config.SunLightDirection.normalized;

            var tally = new LookTestBudgetTally();
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                long tris = LookTestMeshFactory.TriangleCount(mesh);
                int submeshes = Mathf.Max(1, mesh.subMeshCount);
                Bounds bounds = renderer.bounds;
                if (GeometryUtility.TestPlanesAABB(view, bounds))
                {
                    tally.AddMain(renderer.name, submeshes, tris);
                }

                if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                {
                    continue;
                }

                // Sweep the bounds toward the sun by their height so off-screen casters are found.
                Bounds swept = bounds;
                swept.Encapsulate(bounds.center + toSun * (bounds.size.y + 5f));
                if (GeometryUtility.TestPlanesAABB(shadowView, swept))
                {
                    tally.AddShadow(submeshes, tris);
                }
            }

            return tally;
        }
    }
}
