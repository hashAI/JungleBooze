using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>Setup-time helpers for building gray-box views (work in Play and in editor batch tools).</summary>
    public static class ViewUtil
    {
        public static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>A primitive without a collider (gameplay never uses physics).</summary>
        public static Transform Primitive(PrimitiveType type, string name, Transform parent, Material material, bool castShadows)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroySafe(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A cube primitive filling an axis-aligned box given in world space (x lateral, y up, z = s).</summary>
        public static Transform Box(string name, Transform parent, Material material, Vector3 min, Vector3 max, bool castShadows)
        {
            Transform t = Primitive(PrimitiveType.Cube, name, parent, material, castShadows);
            t.localPosition = (min + max) * 0.5f;
            t.localScale = max - min;
            return t;
        }
    }
}
