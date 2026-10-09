using System.Collections.Generic;
using JungleBooze.App.HeroBasin;
using JungleBooze.Core;
using JungleBooze.Editor.LookTest;
using JungleBooze.Editor.Scenery;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.HeroBasin
{
    /// <summary>
    /// Painterly v4 cheap tricks (ADR 0011, <see cref="HeroCards"/>): the projection-matched arch matte card, the
    /// distant pillar impostors and the painted cascade sheets. Materials are made in the painterly material folder;
    /// meshes go into the usual merge batches (one draw per material). Editor only.
    /// </summary>
    internal sealed class HeroBasinCards
    {
        public const string CardShaderPath = LookTestMaterials.ShaderFolder + "/PaintedCard.shader";
        public const string SheetShaderPath = LookTestMaterials.ShaderFolder + "/PaintedSheet.shader";
        public const string EffectsFolder = EnvironmentAssetRules.Root + "Effects";
        private const int Seg = 0;

        private readonly LookTestBuildContext _ctx;
        private readonly HeroCards _c;
        private readonly string _materialFolder;

        public HeroBasinCards(LookTestBuildContext ctx, HeroCards cards, string materialFolder)
        {
            _ctx = ctx;
            _c = cards;
            _materialFolder = materialFolder;
            ArchMaterial = Card("PaintedCard_Arch", EnvironmentAssetRules.BackdropsFolder + "/" + cards.ArchTexture + ".png", cards.ArchExposure, cards.ArchFogShare);
            FallMaterial = cards.FallCard ? Card("PaintedCard_Fall", EnvironmentAssetRules.BackdropsFolder + "/" + cards.FallTexture + ".png", cards.FallExposure, cards.FallFogShare) : null;
            if (FallMaterial != null)
            {
                FallMaterial.SetFloat("_FlowSpeed", cards.FallFlow.x);
                FallMaterial.SetFloat("_FlowSlide", cards.FallFlow.y);
                EditorUtility.SetDirty(FallMaterial);
            }

            PillarMaterial = Card("PaintedCard_Pillars", EnvironmentAssetRules.BackdropsFolder + "/" + cards.PillarTexture + ".png", 1f, cards.PillarFogShare);
            SheetMaterial = cards.PaintedCascades ? Sheet("PaintedSheet_Cascades", EffectsFolder + "/" + cards.CascadeTexture + ".png") : null;
            FoamTexture = cards.PaintedFoam ? Texture(EffectsFolder + "/" + cards.FoamTexture + ".png", true) : null;
        }

        public Material ArchMaterial { get; }

        public Material PillarMaterial { get; }

        public Material FallMaterial { get; }

        /// <summary>True when the painted fall card replaces the 3D tall plunge.</summary>
        public bool HasFallCard => FallMaterial != null;

        public Material SheetMaterial { get; }

        public Texture2D FoamTexture { get; }

        /// <summary>World point where the tall fall leaves the crown (inside the card's window), set by <see cref="Arch"/>.</summary>
        public Vector3? Mouth { get; private set; }

        // ---------------------------------------------------------------- Arch card

        /// <summary>
        /// The arch matte card: a quad perpendicular to the landscape camera's forward axis at
        /// <see cref="HeroCards.ArchDistanceM"/>, covering <see cref="HeroCards.ArchScreenRect"/> of the F4 frame.
        /// </summary>
        public void Arch(Camera landscape)
        {
            if (!_c.ArchCard || ArchMaterial == null)
            {
                return;
            }

            _viewProjection = landscape.projectionMatrix * landscape.worldToCameraMatrix;
            Vector4 r = _c.ArchScreenRect;
            Vector3 tl = AtDepth(landscape, r.x, r.y, _c.ArchDistanceM);
            Vector3 tr = AtDepth(landscape, r.z, r.y, _c.ArchDistanceM);
            Vector3 bl = AtDepth(landscape, r.x, r.w, _c.ArchDistanceM);
            Vector3 br = AtDepth(landscape, r.z, r.w, _c.ArchDistanceM);
            _ctx.Batches.Get(Seg, "L2", "Arch card", ArchMaterial, false, LookTestBatchSet.Group.Ground)
                .Append(Quad("ArchCard", bl, br, tl, tr, new Vector4(0f, 0f, 1f, 1f), 1f), Matrix4x4.identity, null);
            Vector2 m = _c.ArchMouthUv;
            Vector3 onCard = Vector3.Lerp(Vector3.Lerp(bl, br, m.x), Vector3.Lerp(tl, tr, m.x), m.y);
            Mouth = onCard + landscape.transform.forward * _c.ArchMouthSetbackM;

            if (FallMaterial != null)
            {
                // The painted plunge and its cliff, framed by the arch window (F4_f), behind the arch card.
                Vector4 f = _c.FallScreenRect;
                Vector3 ftl = AtDepth(landscape, f.x, f.y, _c.FallDistanceM);
                Vector3 ftr = AtDepth(landscape, f.z, f.y, _c.FallDistanceM);
                Vector3 fbl = AtDepth(landscape, f.x, f.w, _c.FallDistanceM);
                Vector3 fbr = AtDepth(landscape, f.z, f.w, _c.FallDistanceM);
                _ctx.Batches.Get(Seg, "L4", "Fall card", FallMaterial, false, LookTestBatchSet.Group.Ground)
                    .Append(Quad("FallCard", fbl, fbr, ftl, ftr, new Vector4(0f, 0f, 1f, 1f), 1f), Matrix4x4.identity, null);
            }
        }

        private Matrix4x4? _viewProjection;

        /// <summary>True when <paramref name="world"/> is seen inside the arch window of the landscape frame.</summary>
        public bool InWindow(Vector3 world)
        {
            if (!_viewProjection.HasValue || !_c.ArchCard)
            {
                return false;
            }

            return InRect(_viewProjection.Value, world, _c.ArchWindowRect);
        }

        /// <summary>True when <paramref name="world"/> projects (in front of the camera) inside screen rect (u0, v0 top, u1, v1).</summary>
        public static bool InRect(Matrix4x4 viewProjection, Vector3 world, Vector4 rect)
        {
            Vector4 clip = viewProjection * new Vector4(world.x, world.y, world.z, 1f);
            if (clip.w <= 0f)
            {
                return false;
            }

            float u = clip.x / clip.w * 0.5f + 0.5f;
            float v = 0.5f - clip.y / clip.w * 0.5f;
            return u > rect.x && u < rect.z && v > rect.y && v < rect.w;
        }

        /// <summary>
        /// Painted sheets for a cascade <paramref name="totalWidth"/> wide and <paramref name="sheetHeight"/> tall: one
        /// sheet per ~3.2 heights of width (the painted rows are ~2.5:1, so no row is stretched past ~1.3x), 1 to 6.
        /// </summary>
        public static int CascadePieces(float totalWidth, float sheetHeight)
        {
            return Mathf.Clamp(Mathf.RoundToInt(totalWidth / Mathf.Max(0.01f, sheetHeight * 3.2f)), 1, 6);
        }

        /// <summary>World point seen at screen (u, v; v 0 = top) whose depth along the camera forward is <paramref name="depth"/>.</summary>
        public static Vector3 AtDepth(Camera camera, float u, float v, float depth)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(u, 1f - v, 0f));
            float along = Mathf.Max(1e-3f, Vector3.Dot(ray.direction, camera.transform.forward));
            // The ray starts on the near plane; measure the depth from the camera itself.
            return camera.transform.position + ray.direction * (depth / along);
        }

        // ---------------------------------------------------------------- Pillar impostors

        /// <summary>Camera-facing pillar impostors at their world spots, feet on the terrain (painted mist hides the cut).</summary>
        public int Pillars(System.Func<float, float, float> height, Vector3 eye)
        {
            if (PillarMaterial == null || _c.PillarCards == null)
            {
                return 0;
            }

            Texture tex = PillarMaterial.mainTexture;
            float texAspect = tex != null ? (float)tex.width / Mathf.Max(1, tex.height) : 1.5f;
            LookTestMeshAccumulator target = _ctx.Batches.Get(Seg, "L4", "Pillar impostors", PillarMaterial, false, LookTestBatchSet.Group.Ground);
            int made = 0;
            foreach (Vector4 p in _c.PillarCards)
            {
                int index = Mathf.Clamp((int)p.w, 0, _c.PillarRects.Length - 1);
                Vector4 rect = _c.PillarRects[index];
                float aspect = (rect.z - rect.x) / Mathf.Max(1e-4f, rect.w - rect.y) * texAspect;
                float h = p.z;
                float w = h * aspect;
                var foot = new Vector3(p.x, height(p.x, p.y) - 0.08f * h, p.y);
                Vector3 toEye = eye - foot;
                toEye.y = 0f;
                Vector3 right = Vector3.Cross(Vector3.up, toEye.normalized);
                Vector3 bl = foot - right * w * 0.5f;
                Vector3 br = foot + right * w * 0.5f;
                target.Append(Quad("PillarCard", bl, br, bl + Vector3.up * h, br + Vector3.up * h, rect, 1f), Matrix4x4.identity, null);
                made++;
            }

            return made;
        }

        // ---------------------------------------------------------------- Painted cascade sheets

        /// <summary>
        /// A painted cascade: one strip from just above the lip to below the foot (the painted spray sits on the
        /// water), facing the viewer, bowed a little toward it. Returns false when the fall is too tall for a sheet.
        /// </summary>
        public bool Cascade(IRandom rng, Vector3 top, float footY, float width, Vector3 viewer, float setback)
        {
            float fall = top.y - footY;
            if (SheetMaterial == null || fall > _c.CascadeMaxHeightM || fall <= 0.05f)
            {
                return false;
            }

            Vector3 toViewer = viewer - top;
            toViewer.y = 0f;
            toViewer.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toViewer);
            float below = Mathf.Max(0.35f, fall * _c.CascadeOverscan.y);
            float lipRise = Mathf.Max(0.15f, fall * 0.08f);
            // Never stretch a painted row more than ~1.3x across: wide falls become several sheets side by side,
            // each with its own row, flip and lip height (F4_f: a broken curtain, not one slab).
            float total = width * _c.CascadeOverscan.x;
            float sheetHeight = fall + below + lipRise;
            int pieces = CascadePieces(total, sheetHeight);
            float pieceWidth = total / pieces;
            for (int k = 0; k < pieces; k++)
            {
                Vector3 centre = top + side * ((k + 0.5f) / pieces - 0.5f) * total + Vector3.up * (pieces > 1 ? rng.NextFloat(-0.06f, 0.04f) * fall : 0f)
                    + toViewer * (pieces > 1 ? rng.NextFloat(-0.3f, 0.3f) : 0f);
                Sheet(rng, centre, footY, pieceWidth * rng.NextFloat(1.0f, 1.12f), toViewer, side, setback, below, lipRise);
            }

            _ctx.Falls.Add(new LookTestBuildContext.FallSpan { Top = top, Foot = new Vector3(top.x, footY, top.z) + toViewer * setback, Width = width });
            return true;
        }

        private void Sheet(IRandom rng, Vector3 top, float footY, float w, Vector3 toViewer, Vector3 side, float setback, float below, float lipRise)
        {
            Vector4 row = _c.CascadeRows[rng.NextInt(0, _c.CascadeRows.Length)];
            const int columns = 6;
            const int rows = 4;
            var positions = new Vector3[columns + 1, rows + 1];
            var frames = new LookTestMeshFactory.Frame[columns + 1, rows + 1];
            var uvs = new Vector2[columns + 1, rows + 1];
            var colors = new Color[columns + 1, rows + 1];
            var along = new List<Vector2>((columns + 1) * (rows + 1));
            float seed = rng.NextFloat(0f, 1f);
            float speed = rng.NextFloat(0f, 1f);
            bool flip = rng.NextFloat(0f, 1f) < 0.5f;
            for (int i = 0; i <= columns; i++)
            {
                float s = (float)i / columns;
                float bow = 1f - (2f * s - 1f) * (2f * s - 1f);
                for (int j = 0; j <= rows; j++)
                {
                    float t = (float)j / rows; // 0 = lip, 1 = below the foot
                    float y = Mathf.Lerp(top.y + lipRise, footY - below, t);
                    // The sheet leans out as it falls (setback), and bows toward the viewer in the middle.
                    Vector3 p = top + side * (s - 0.5f) * w + toViewer * (setback * t * t + bow * 0.06f * w);
                    p.y = y;
                    positions[i, j] = p;
                    frames[i, j] = new LookTestMeshFactory.Frame { Normal = toViewer, Tangent = side, Bitangent = Vector3.up };
                    float u = Mathf.Lerp(row.x, row.z, flip ? 1f - s : s);
                    uvs[i, j] = new Vector2(u, Mathf.Lerp(row.w, row.y, t));
                    colors[i, j] = new Color(seed, speed, 0f, 1f);
                }
            }

            Mesh mesh = LookTestMeshFactory.Grid("PaintedCascade", positions, frames, uvs, colors);
            // UV1.x = 0 at the lip .. 1 at the foot (the shader slides only the body of the painting).
            for (int i = 0; i <= columns; i++)
            {
                for (int j = 0; j <= rows; j++)
                {
                    along.Add(Vector2.zero);
                }
            }

            Vector2[] uv0 = mesh.uv;
            for (int k = 0; k < uv0.Length; k++)
            {
                along[k] = new Vector2(Mathf.InverseLerp(row.w, row.y, uv0[k].y), 0f);
            }

            mesh.SetUVs(1, along);
            _ctx.Batches.Get(Seg, "W", "Painted cascades", SheetMaterial, false, LookTestBatchSet.Group.Water).Append(mesh, Matrix4x4.identity, null);
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>A two-triangle card: corners bottom-left/right, top-left/right; UV rect (u0, v0, u1, v1).</summary>
        private static Mesh Quad(string name, Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector4 rect, float alpha)
        {
            var positions = new Vector3[2, 2] { { bl, tl }, { br, tr } };
            Vector3 normal = Vector3.Cross(tl - bl, br - bl).normalized;
            var frame = new LookTestMeshFactory.Frame { Normal = normal, Tangent = (br - bl).normalized, Bitangent = (tl - bl).normalized };
            var frames = new LookTestMeshFactory.Frame[2, 2] { { frame, frame }, { frame, frame } };
            var uvs = new Vector2[2, 2] { { new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.w) }, { new Vector2(rect.z, rect.y), new Vector2(rect.z, rect.w) } };
            var c = new Color(1f, 1f, 1f, alpha);
            var colors = new Color[2, 2] { { c, c }, { c, c } };
            return LookTestMeshFactory.Grid(name, positions, frames, uvs, colors);
        }

        private Material Card(string name, string texturePath, float exposure, float fog)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CardShaderPath);
            Texture2D texture = Texture(texturePath, false);
            if (shader == null || texture == null)
            {
                Debug.LogWarning("[JungleBooze hero basin] Painted card missing: " + (shader == null ? CardShaderPath : texturePath));
                return null;
            }

            var m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", texture);
            m.SetFloat("_Exposure", exposure);
            m.SetFloat("_FogAmount", fog);
            LookTestAssets.EnsureFolder(_materialFolder);
            return LookTestAssets.SaveOrReplace(m, _materialFolder + "/" + name + ".mat");
        }

        private Material Sheet(string name, string texturePath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SheetShaderPath);
            Texture2D texture = Texture(texturePath, false);
            if (shader == null || texture == null)
            {
                Debug.LogWarning("[JungleBooze hero basin] Painted sheet missing: " + (shader == null ? SheetShaderPath : texturePath));
                return null;
            }

            var m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", texture);
            LookTestAssets.EnsureFolder(_materialFolder);
            return LookTestAssets.SaveOrReplace(m, _materialFolder + "/" + name + ".mat");
        }

        /// <summary>Loads a painted texture and fixes its import (straight alpha, clamp, mips; ASTC on iOS).</summary>
        private static Texture2D Texture(string path, bool repeat)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");
            TextureWrapMode wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (importer.wrapMode != wrap || !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None || !importer.mipmapEnabled
                || !ios.overridden || ios.format != TextureImporterFormat.ASTC_6x6)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = wrap;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                ios.overridden = true;
                ios.maxTextureSize = 2048;
                ios.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(ios);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
