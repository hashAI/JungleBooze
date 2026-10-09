using System.Collections.Generic;
using JungleBooze.Editor.LookTest;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.Perf
{
    /// <summary>
    /// Headless overdraw measurement of one camera view as an Apple tile-based GPU shades it (editor, any GPU):
    /// <list type="number">
    /// <item>Opaque draws write depth; each then counts the pixels where it is the front-most opaque surface
    ///   (ZTest Equal): what survives hidden-surface removal, about one shaded fragment per pixel.</item>
    /// <item>Alpha-tested draws, front to back: count every fragment that passes the depth test (the shader runs
    ///   before the discard, and alpha test interrupts hidden-surface removal), then write clipped depth.</item>
    /// <item>The sky counts the pixels nothing opaque or alpha-tested covers.</item>
    /// <item>Blended draws, in queue order back to front: count every fragment in front of the depth (no
    ///   hidden-surface removal for blending). Atmos Card quads are expanded toward the camera as in the shader.</item>
    /// </list>
    /// Every draw is read back on its own, so the result has fragments per draw and a per-pixel heat map.
    /// Approximations: vertex wind and per-pixel alpha of blended layers are ignored (a blended fragment costs the
    /// same whether it ends up visible or not, which is right for GPU time).
    /// </summary>
    public sealed class PerfOverdraw : System.IDisposable
    {
        private const string ShaderName = "Hidden/JungleBooze/Perf Overdraw";
        private const string AtmosShader = "JungleBooze/Atmos Card";
        private static readonly int CameraPosId = Shader.PropertyToID("_PerfCameraPos");

        private readonly Dictionary<Material, Material> _countMaterials = new Dictionary<Material, Material>();
        private readonly Shader _shader;

        public PerfOverdraw()
        {
            _shader = Shader.Find(ShaderName);
        }

        public bool Ready => _shader != null;

        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>Shaded fragments per pixel by category: index 0 opaque, 1 cutout, 2 sky, 3 blended.</summary>
        public float[][] Heat { get; private set; }

        public List<PerfDrawSample> Measure(Camera camera, int width, int height)
        {
            Width = width;
            Height = height;
            Heat = new[] { new float[width * height], new float[width * height], new float[width * height], new float[width * height] };
            List<PerfDrawSample> draws = Collect(camera);

            var color = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            var depth = new RenderTexture(width, height, 24, RenderTextureFormat.Depth);
            color.Create();
            depth.Create();
            var readback = new Texture2D(width, height, TextureFormat.RFloat, false, true);
            var cb = new CommandBuffer { name = "PerfOverdraw" };
            Matrix4x4 view = camera.worldToCameraMatrix;
            Matrix4x4 proj = camera.projectionMatrix;

            void Begin(bool clearDepth)
            {
                cb.Clear();
                cb.SetRenderTarget(new RenderTargetIdentifier(color), new RenderTargetIdentifier(depth));
                cb.ClearRenderTarget(clearDepth, true, Color.clear);
                cb.SetViewProjectionMatrices(view, proj);
                cb.SetGlobalVector(CameraPosId, camera.transform.position);
            }

            void Count(PerfDrawSample sample, int heatIndex)
            {
                Graphics.ExecuteCommandBuffer(cb);
                RenderTexture.active = color;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                RenderTexture.active = null;
                NativeArray<float> data = readback.GetRawTextureData<float>();
                double sum = 0.0;
                long covered = 0;
                float[] heat = Heat[heatIndex];
                for (int i = 0; i < data.Length; i++)
                {
                    float v = data[i];
                    if (v > 0f)
                    {
                        sum += v;
                        covered++;
                        heat[i] += v;
                    }
                }

                sample.Fragments = sum;
                sample.Covered = covered;
            }

            // 1. Opaque depth, then front-most opaque pixels per draw.
            Begin(true);
            Graphics.ExecuteCommandBuffer(cb);
            foreach (PerfDrawSample d in draws)
            {
                if (d.Category == PerfDrawSample.Opaque)
                {
                    cb.Clear();
                    cb.SetRenderTarget(new RenderTargetIdentifier(color), new RenderTargetIdentifier(depth));
                    cb.SetViewProjectionMatrices(view, proj);
                    cb.SetGlobalVector(CameraPosId, camera.transform.position);
                    cb.DrawRenderer(d.Renderer, CountMaterial(d.Material, false), d.Submesh, 0);
                    Graphics.ExecuteCommandBuffer(cb);
                }
            }

            foreach (PerfDrawSample d in draws)
            {
                if (d.Category == PerfDrawSample.Opaque)
                {
                    Begin(false);
                    cb.DrawRenderer(d.Renderer, CountMaterial(d.Material, false), d.Submesh, 2);
                    Count(d, 0);
                }
            }

            // 2. Alpha-tested, front to back: count, then write clipped depth.
            foreach (PerfDrawSample d in draws)
            {
                if (d.Category == PerfDrawSample.Cutout)
                {
                    Material m = CountMaterial(d.Material, true);
                    Begin(false);
                    cb.DrawRenderer(d.Renderer, m, d.Submesh, 1);
                    Count(d, 1);
                    Begin(false);
                    cb.DrawRenderer(d.Renderer, m, d.Submesh, 0);
                    Graphics.ExecuteCommandBuffer(cb);
                }
            }

            // 3. Sky: a far quad counted where nothing is in front.
            var sky = new PerfDrawSample(null, 0, null, PerfDrawSample.Sky, "L6", 0, camera.farClipPlane);
            Mesh quad = FarQuad(camera);
            Begin(false);
            cb.DrawMesh(quad, Matrix4x4.identity, CountMaterial(null, false), 0, 1);
            Count(sky, 2);
            Object.DestroyImmediate(quad);

            // 4. Blended, in queue order then back to front.
            foreach (PerfDrawSample d in draws)
            {
                if (d.Category == PerfDrawSample.Blended)
                {
                    Begin(false);
                    cb.DrawRenderer(d.Renderer, CountMaterial(d.Material, false), d.Submesh, 1);
                    Count(d, 3);
                }
            }

            draws.Add(sky);
            cb.Release();
            color.Release();
            depth.Release();
            Object.DestroyImmediate(color);
            Object.DestroyImmediate(depth);
            Object.DestroyImmediate(readback);
            return draws;
        }

        public void Dispose()
        {
            foreach (Material m in _countMaterials.Values)
            {
                Object.DestroyImmediate(m);
            }

            _countMaterials.Clear();
        }

        /// <summary>Visible draws (bounds in the frustum), sorted as URP submits them.</summary>
        public static List<PerfDrawSample> Collect(Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            Vector3 eye = camera.transform.position;
            var draws = new List<PerfDrawSample>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, r.bounds))
                {
                    continue;
                }

                Mesh mesh = MeshOf(r);
                if (mesh == null)
                {
                    continue;
                }

                Material[] materials = r.sharedMaterials;
                int slots = Mathf.Max(materials.Length, mesh.subMeshCount);
                for (int slot = 0; slot < slots && slot < materials.Length; slot++)
                {
                    Material m = materials[slot];
                    if (m == null)
                    {
                        continue;
                    }

                    // Extra material slots render the last submesh again (URP).
                    int sub = Mathf.Min(slot, mesh.subMeshCount - 1);
                    long tris = mesh.GetTopology(sub) == MeshTopology.Triangles ? (long)mesh.GetIndexCount(sub) / 3 : 0;
                    float distance = Vector3.Distance(eye, r.bounds.ClosestPoint(eye));
                    draws.Add(new PerfDrawSample(r, sub, m, CategoryOf(m), LookTestBatchSet.LayerOf(r.name), tris, distance));
                }
            }

            draws.Sort((a, b) =>
            {
                int qa = a.Material.renderQueue;
                int qb = b.Material.renderQueue;
                if (qa != qb)
                {
                    return qa.CompareTo(qb);
                }

                return a.Category == PerfDrawSample.Blended ? b.Distance.CompareTo(a.Distance) : a.Distance.CompareTo(b.Distance);
            });
            return draws;
        }

        public static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            MeshFilter filter = r.GetComponent<MeshFilter>();
            return r is MeshRenderer && filter != null ? filter.sharedMesh : null;
        }

        public static string CategoryOf(Material m)
        {
            int q = m.renderQueue;
            if (q >= 3000 || (q > 2500 && m.GetTag("RenderType", false) == "Transparent"))
            {
                return PerfDrawSample.Blended;
            }

            if (q >= 2450 || m.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                return PerfDrawSample.Cutout;
            }

            return PerfDrawSample.Opaque;
        }

        private Material CountMaterial(Material source, bool clip)
        {
            if (source != null && _countMaterials.TryGetValue(source, out Material cached))
            {
                return cached;
            }

            var m = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            float cull = 0f;
            if (source != null)
            {
                if (source.HasProperty("_Cull"))
                {
                    cull = source.GetFloat("_Cull");
                }
                else if (CategoryOf(source) == PerfDrawSample.Opaque)
                {
                    cull = (float)CullMode.Back;
                }

                bool card = source.shader != null && source.shader.name == AtmosShader;
                m.SetFloat("_Card", card ? 1f : 0f);
                m.SetFloat("_MistPull", card && source.IsKeywordEnabled("_MIST") ? 1f : 0f);
                if (clip)
                {
                    SetCoverage(source, m);
                }

                _countMaterials[source] = m;
            }

            m.SetFloat("_PerfCull", cull);
            return m;
        }

        private static void SetCoverage(Material source, Material m)
        {
            string texName = null;
            float channel = 0f;
            if (source.HasProperty("_AlphaFromBaseA") && source.HasProperty("_AlphaMap"))
            {
                bool fromBase = source.GetFloat("_AlphaFromBaseA") > 0.5f;
                texName = fromBase ? "_BaseMap" : "_AlphaMap";
                channel = fromBase ? 0f : 1f;
            }
            else if (source.HasProperty("_BaseMap"))
            {
                texName = "_BaseMap";
            }
            else if (source.HasProperty("_MainTex"))
            {
                texName = "_MainTex";
            }

            Texture tex = texName != null ? source.GetTexture(texName) : null;
            if (tex == null)
            {
                return;
            }

            string stName = source.HasProperty("_BaseMap") ? "_BaseMap" : texName;
            m.SetTexture("_CoverTex", tex);
            m.SetTextureScale("_CoverTex", source.GetTextureScale(stName));
            m.SetTextureOffset("_CoverTex", source.GetTextureOffset(stName));
            m.SetFloat("_CoverChannel", channel);
            m.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
            m.SetFloat("_Clip", 1f);
        }

        private static Mesh FarQuad(Camera camera)
        {
            float d = camera.farClipPlane * 0.995f;
            Transform t = camera.transform;
            float halfH = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * d * 1.1f;
            float halfW = halfH * camera.aspect * 1.1f;
            Vector3 c = t.position + t.forward * d;
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    c - t.right * halfW - t.up * halfH, c + t.right * halfW - t.up * halfH,
                    c + t.right * halfW + t.up * halfH, c - t.right * halfW + t.up * halfH,
                },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
