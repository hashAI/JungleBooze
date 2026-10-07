using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Builds gray-box primitives with one shared material per color. Materials are clones of
    /// <see cref="RuntimeMaterialTemplates.GetOpaqueTemplate"/> (a URP Lit asset in Resources, so the shader is in every
    /// player build; the material CreatePrimitive assigns is magenta on device, and CreatePrimitive itself fails there, so
    /// meshes come from <see cref="PrimitiveMeshes"/>). Setup-time only (allocates).
    /// <see cref="Dispose"/> destroys the materials it made.
    /// </summary>
    public sealed class GrayBoxKit : IDisposable
    {
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Material> _materials = new List<Material>();

        /// <summary>Creates a primitive under <paramref name="parent"/> with no collider and no shadows.</summary>
        public GameObject Create(PrimitiveType type, string name, Transform parent, Color color)
        {
            // Procedural mesh, no collider (gameplay never uses physics, spec 001 section 2; the physics module is
            // stripped from the player build, so GameObject.CreatePrimitive cannot be used).
            GameObject go = PrimitiveMeshes.Create(type, name, parent, GetMaterial(color, RuntimeMaterialTemplates.GetOpaqueTemplate()));
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>Creates a primitive and sets its local position and scale.</summary>
        public Transform Create(
            PrimitiveType type, string name, Transform parent, Color color, Vector3 localPosition, Vector3 localScale)
        {
            Transform t = Create(type, name, parent, color).transform;
            t.localPosition = localPosition;
            t.localScale = localScale;
            return t;
        }

        /// <summary>Shared material of <paramref name="color"/>, cloned from <paramref name="template"/> on first use.</summary>
        public Material GetMaterial(Color color, Material template)
        {
            for (int i = 0; i < _colors.Count; i++)
            {
                if (_colors[i] == color)
                {
                    return _materials[i];
                }
            }

            if (template == null)
            {
                throw new InvalidOperationException("No template material.");
            }

            var material = new Material(template)
            {
                name = "GrayBox_" + ColorUtility.ToHtmlStringRGB(color),
                color = color,
                enableInstancing = true,
            };

            _colors.Add(color);
            _materials.Add(material);
            return material;
        }

        public void Dispose()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null)
                {
                    Object.Destroy(_materials[i]);
                }
            }

            _materials.Clear();
            _colors.Clear();
        }
    }
}
