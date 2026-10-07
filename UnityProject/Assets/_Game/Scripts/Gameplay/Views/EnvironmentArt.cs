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

        // Jungle wall segments (tools/blender/build_jungle_kit.py): pre-merged 12 m verge dressing, one draw call each.
        public const string WallA = "Jungle_WallA";
        public const string WallB = "Jungle_WallB";
        public const string WallC = "Jungle_WallC";

        /// <summary>The wall segment variants, in hash order.</summary>
        public static readonly string[] JungleWalls = { WallA, WallB, WallC };

        // Textures without a model (loaded by name + "_basecolor").
        public const string TrailTexture = "Ground_Trail";
        public const string FloorTexture = "Ground_JungleFloor";

        /// <summary>Shared atlas of every model whose name starts with <see cref="AtlasPrefix"/>.</summary>
        public const string JungleAtlas = "Jungle_Atlas";

        private const string AtlasPrefix = "Jungle_";

        /// <summary>Every prefab name above, in one list (the start-of-run asset report walks it).</summary>
        public static readonly string[] AllNames =
        {
            LowBarrier, HighBarrier, FullBlock, Boulder, ThornPatch, StrikeColumn, Coin, Magnet, Shield, Boost,
            PathTile, RavineEdge, VineBranch, Signpost, TreeA, TreeB, Bush, WallA, WallB, WallC,
        };

        /// <summary>Every texture-only name above (the asset report walks it too).</summary>
        public static readonly string[] AllTextureNames = { TrailTexture, FloorTexture, JungleAtlas };

        private const string BaseColorSuffix = "_basecolor";

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static Material _template;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            MaterialCache.Clear();
            _template = null;
        }

        /// <summary>
        /// The model named <paramref name="prefabName"/> (the FBX in <c>Resources/EnvironmentArt</c>, or a prefab of the
        /// same name), or null when it is not in the project.
        /// </summary>
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

                // Attach resets the root rotation, so a model that relies on one (an FBX exported without baked axes)
                // would end up lying on its side. tools/blender/glb_to_fbx.py bakes the axes; this flags any that don't.
                if (prefab.transform.localRotation != Quaternion.identity)
                {
                    Debug.LogWarning("[JungleBooze] Environment art '" + prefabName + "' has a root rotation of "
                        + prefab.transform.localEulerAngles + "; re-export it with baked axes or it will lie on its side.");
                }
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

            Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Object.Destroy(cameras[i].gameObject);
            }

            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Object.Destroy(lights[i].gameObject);
            }

            string textureKey = TextureKeyFor(prefabName);
            EnsureTexturedMaterials(instance, ResourcesFolder + textureKey + BaseColorSuffix, textureKey);
            return instance.transform;
        }

        /// <summary>
        /// Base color texture name for a model: <c>Jungle_*</c> models share <see cref="JungleAtlas"/>, every other
        /// model has its own <c>&lt;name&gt;_basecolor</c>. The editor import remap uses the same rule.
        /// </summary>
        public static string TextureKeyFor(string prefabName)
        {
            return prefabName.StartsWith(AtlasPrefix, System.StringComparison.Ordinal) ? JungleAtlas : prefabName;
        }

        /// <summary>The texture <c>&lt;name&gt;_basecolor</c> from <c>Resources/EnvironmentArt</c>, or null.</summary>
        public static Texture2D LoadTexture(string name)
        {
            return Resources.Load<Texture2D>(ResourcesFolder + name + BaseColorSuffix);
        }

        /// <summary>
        /// A new lit material (clone of the active pipeline's primitive material; no shader lookup by name) with
        /// <paramref name="texture"/> (may be null) and <paramref name="color"/>, matte, GPU instancing on.
        /// The caller owns and destroys it. Setup-time only.
        /// </summary>
        public static Material CreateLitMaterial(string name, Texture2D texture, Color color)
        {
            Material template = GetTemplate();
            if (template == null)
            {
                return null;
            }

            var material = new Material(template) { name = name, color = color, enableInstancing = true };
            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                }
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }

            return material;
        }

        private static Material GetTemplate()
        {
            if (_template == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _template = probe.GetComponent<MeshRenderer>().sharedMaterial;
                Object.Destroy(probe);
            }

            return _template;
        }

        /// <summary>
        /// Safety net for models whose imported material lost its texture (or the editor remap has not run): builds one
        /// shared material per model from <c>&lt;name&gt;_basecolor.png</c>, cloned from the active pipeline's primitive
        /// material (no shader lookup by name). Models that already carry a textured material are left alone.
        /// </summary>
        public static void EnsureTexturedMaterials(GameObject instance, string textureResourcePath, string materialKey)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                Material current = r.sharedMaterial;
                if (current != null && current.mainTexture != null)
                {
                    continue;
                }

                Material fixedMaterial = GetRuntimeMaterial(materialKey, textureResourcePath);
                if (fixedMaterial == null)
                {
                    continue;
                }

                int count = Mathf.Max(1, r.sharedMaterials.Length);
                var materials = new Material[count];
                for (int m = 0; m < count; m++)
                {
                    materials[m] = fixedMaterial;
                }

                r.sharedMaterials = materials;
            }
        }

        private static Material GetRuntimeMaterial(string materialKey, string textureResourcePath)
        {
            if (MaterialCache.TryGetValue(materialKey, out Material cached) && cached != null)
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(textureResourcePath);
            if (texture == null)
            {
                return null;
            }

            Material material = CreateLitMaterial("EnvArt_" + materialKey, texture, Color.white);
            if (material == null)
            {
                return null;
            }

            MaterialCache[materialKey] = material;
            return material;
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
