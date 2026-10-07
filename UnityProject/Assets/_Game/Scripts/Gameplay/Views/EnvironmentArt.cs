using System.Collections.Generic;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Optional real art for the gray-box views. Looks up a prefab by name in
    /// <c>Resources/EnvironmentArt/</c> (stored under <c>Art/Environment/Resources/EnvironmentArt</c>). When the prefab
    /// exists the views parent an instance to the gray-box piece they already scale and move, hide the primitive's
    /// renderer, and carry on; when it is missing nothing changes and the gray-box shows. Conventions for each
    /// prefab are in <c>Art/Environment/README.md</c>. Setup-time only (instantiates); never call per frame.
    /// </summary>
    public static class EnvironmentArt
    {
        private const string ResourcesFolder = "EnvironmentArt/";

        // Prefab names (one place, so views and the editor builder agree).
        public const string LowBarrier = "Obstacle_LowBarrier";
        public const string HighBarrier = "Obstacle_HighBarrier";
        public const string FullBlock = "Obstacle_FullBlock";
        public const string Boulder = "Obstacle_Boulder";
        public const string ThornPatch = "Hazard_ThornPatch";
        public const string StrikeColumn = "Hazard_StrikeColumn";
        public const string Coin = "Pickup_Coin";
        public const string Magnet = "Pickup_Magnet";
        public const string Shield = "Pickup_Shield";
        public const string Boost = "Pickup_Boost";
        public const string PathTile = "Ground_PathTile";
        public const string RavineEdge = "Ground_RavineEdge";
        public const string VineBranch = "Prop_VineBranch";
        public const string Signpost = "Prop_Signpost";
        public const string TreeA = "Foliage_TreeA";
        public const string TreeB = "Foliage_TreeB";
        public const string Bush = "Foliage_Bush";

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        /// <summary>The prefab named <paramref name="prefabName"/>, or null when it has not been imported yet.</summary>
        public static GameObject Load(string prefabName)
        {
            if (Cache.TryGetValue(prefabName, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcesFolder + prefabName);
            if (prefab != null)
            {
                Cache[prefabName] = prefab;
            }

            return prefab;
        }

        /// <summary>True when real art exists for <paramref name="prefabName"/>.</summary>
        public static bool Exists(string prefabName)
        {
            return Load(prefabName) != null;
        }

        /// <summary>
        /// Instantiates the prefab under <paramref name="parent"/> at identity local transform, with colliders removed
        /// (the simulation owns collision). Returns null when the prefab is missing.
        /// </summary>
        public static Transform Attach(Transform parent, string prefabName)
        {
            GameObject prefab = Load(prefabName);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.name = prefabName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.Destroy(colliders[i]);
            }

            return instance.transform;
        }

        /// <summary>Turns off the renderer of a gray-box primitive (its transform keeps driving the attached art).</summary>
        public static void HideRenderer(Transform grayBox)
        {
            if (grayBox == null)
            {
                return;
            }

            MeshRenderer meshRenderer = grayBox.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Swaps a whole gray-box group for the prefab: every renderer already under <paramref name="group"/> is
        /// disabled, then the prefab is attached. Returns the new instance, or null (group untouched) when missing.
        /// </summary>
        public static Transform ReplaceGroup(Transform group, string prefabName)
        {
            if (!Exists(prefabName))
            {
                return null;
            }

            MeshRenderer[] renderers = group.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            return Attach(group, prefabName);
        }

        /// <summary>Deterministic hash of a tile or slot index, for dressing variety without a random source.</summary>
        public static uint Hash(long index)
        {
            unchecked
            {
                ulong x = (ulong)index * 0x9E3779B97F4A7C15UL;
                x ^= x >> 29;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 32;
                return (uint)x;
            }
        }
    }
}
