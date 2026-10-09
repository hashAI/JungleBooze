using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.Characters
{
    /// <summary>
    /// Configures the rigged Pista import (IMPORT_NOTES.md in the Pista art folder): texture settings (sRGB off for
    /// data maps, ASTC per ADR 0004), the URP Lit material <c>M_Pista</c> remapped onto the FBX, the Humanoid avatar
    /// with an explicit Meshy bone map and a computed T-pose reference (the file's rest pose is an A-pose), and the
    /// clip list (names without <c>Armature|</c>, loop flags, in-place root settings, extra <c>Land_Run</c> sub-clip).
    /// Idempotent: run it again after the FBX changes.
    /// </summary>
    public static class PistaImportSetup
    {
        private const string LogPrefix = "[JungleBooze] Pista import: ";

        /// <summary>Meshy numbers the spine top-down: Spine02 is the lowest segment.</summary>
        private static readonly string[,] BoneMap =
        {
            { "Hips", "Hips" }, { "Spine", "Spine02" }, { "Chest", "Spine01" }, { "UpperChest", "Spine" },
            { "Neck", "neck" }, { "Head", "Head" },
            { "LeftShoulder", "LeftShoulder" }, { "LeftUpperArm", "LeftArm" }, { "LeftLowerArm", "LeftForeArm" }, { "LeftHand", "LeftHand" },
            { "RightShoulder", "RightShoulder" }, { "RightUpperArm", "RightArm" }, { "RightLowerArm", "RightForeArm" }, { "RightHand", "RightHand" },
            { "LeftUpperLeg", "LeftUpLeg" }, { "LeftLowerLeg", "LeftLeg" }, { "LeftFoot", "LeftFoot" }, { "LeftToes", "LeftToeBase" },
            { "RightUpperLeg", "RightUpLeg" }, { "RightLowerLeg", "RightLeg" }, { "RightFoot", "RightFoot" }, { "RightToes", "RightToeBase" },
        };

        /// <summary>Clip name, loop, keep the clip's vertical body motion (false = the simulation owns height).</summary>
        private static readonly (string Name, bool Loop, bool KeepHeight)[] Clips =
        {
            ("Idle", true, true), ("Idle_Alt", true, true), ("Run", true, true), ("Run_Alt", true, true),
            ("Strafe_Left", true, true), ("Strafe_Right", true, true), ("Jump", false, false), ("Jump_Standing", false, false),
            ("Fall", true, false), ("Land", false, true), ("Slide", false, true), ("Stumble", false, true),
            ("Death_Backward", false, true), ("Vine_Grab", false, true), ("Vine_Hang", true, true), ("Vine_Swing", false, true),
        };

        public const string LandRunClip = "Land_Run";
        private const int LandRunLastFrame = 20;

        [MenuItem("JungleBooze/Characters/Configure Pista Import", false, 30)]
        public static void ConfigureAll()
        {
            ConfigureTextures();
            Material material = EnsureMaterial();
            ConfigureModel(material);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureTextures()
        {
            Texture(PistaPaths.BaseColor, TextureImporterType.Default, true, TextureImporterFormat.ASTC_4x4);
            Texture(PistaPaths.Normal, TextureImporterType.NormalMap, false, TextureImporterFormat.ASTC_5x5);
            Texture(PistaPaths.MetallicSmoothness, TextureImporterType.Default, false, TextureImporterFormat.ASTC_6x6);
            Texture(PistaPaths.Occlusion, TextureImporterType.Default, false, TextureImporterFormat.ASTC_8x8);
        }

        private static void Texture(string path, TextureImporterType type, bool srgb, TextureImporterFormat iosFormat)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.alphaSource = path == PistaPaths.MetallicSmoothness ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.anisoLevel = 2;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone",
                overridden = true,
                maxTextureSize = 2048,
                format = iosFormat,
                compressionQuality = 50,
            });
            importer.SaveAndReimport();
        }

        public static Material EnsureMaterial()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PistaPaths.Material);
            if (material == null)
            {
                material = new Material(lit) { name = "M_Pista" };
                AssetDatabase.CreateAsset(material, PistaPaths.Material);
            }

            material.shader = lit;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PistaPaths.BaseColor));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PistaPaths.Normal));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PistaPaths.MetallicSmoothness));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_SmoothnessTextureChannel", 0f); // metallic alpha
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PistaPaths.Occlusion));
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void ConfigureModel(Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(PistaPaths.Model);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.maxBonesPerVertex = 4;
            importer.skinWeights = ModelImporterSkinWeights.Standard;

            // Procedural lean and the ponytail spring write bone transforms after the Animator evaluates, so the
            // hierarchy must stay real (27 bones, cheap).
            importer.optimizeGameObjects = false;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Pista"), material);

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = BuildHumanDescription();
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = 0.5f;
            importer.animationPositionError = 0.5f;
            importer.animationScaleError = 0.5f;
            importer.resampleCurves = true;
            importer.SaveAndReimport();

            importer.clipAnimations = BuildClips(importer.defaultClipAnimations);
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new InvalidOperationException("Humanoid avatar is not valid (" + (avatar == null ? "missing" : "valid " + avatar.isValid + ", human " + avatar.isHuman) + ")");
            }

            Debug.Log(LogPrefix + "Humanoid avatar valid, " + importer.clipAnimations.Length + " clips.");
        }

        private static HumanDescription BuildHumanDescription()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Model);
            GameObject instance = Object.Instantiate(source);
            instance.name = source.name;
            try
            {
                Transform root = instance.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                EnforceTPose(root);

                Transform[] all = instance.GetComponentsInChildren<Transform>(true);
                var skeleton = new SkeletonBone[all.Length];
                for (int i = 0; i < all.Length; i++)
                {
                    skeleton[i] = new SkeletonBone
                    {
                        name = all[i].name,
                        position = all[i].localPosition,
                        rotation = all[i].localRotation,
                        scale = all[i].localScale,
                    };
                }

                var human = new HumanBone[BoneMap.GetLength(0)];
                for (int i = 0; i < human.Length; i++)
                {
                    human[i] = new HumanBone { humanName = BoneMap[i, 0], boneName = BoneMap[i, 1] };
                    human[i].limit.useDefaultValues = true;
                }

                return new HumanDescription
                {
                    human = human,
                    skeleton = skeleton,
                    upperArmTwist = 0.5f,
                    lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f,
                    lowerLegTwist = 0.5f,
                    armStretch = 0.05f,
                    legStretch = 0.05f,
                    feetSpacing = 0f,
                    hasTranslationDoF = false,
                };
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Same idea as the avatar editor's "Enforce T-Pose": arms straight out to the sides, legs straight down,
        /// so muscle space is centred on a true T-pose and arm swings don't hit the muscle limits.
        /// </summary>
        public static void EnforceTPose(Transform root)
        {
            Transform Find(string name) => FindDeep(root, name);
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform arm = Find(side + "Arm");
                Transform fore = Find(side + "ForeArm");
                Transform hand = Find(side + "Hand");
                Transform shoulder = Find(side + "Shoulder");
                float sign = Mathf.Sign(arm.position.x - shoulder.parent.position.x);
                var outward = new Vector3(sign, 0f, 0f);
                Aim(arm, fore.position, outward);
                Aim(fore, hand.position, outward);

                Transform upLeg = Find(side + "UpLeg");
                Transform leg = Find(side + "Leg");
                Transform foot = Find(side + "Foot");
                Aim(upLeg, leg.position, Vector3.down);
                Aim(leg, foot.position, Vector3.down);
            }
        }

        private static void Aim(Transform bone, Vector3 childPosition, Vector3 direction)
        {
            Vector3 current = childPosition - bone.position;
            if (current.sqrMagnitude < 1e-8f)
            {
                return;
            }

            bone.rotation = Quaternion.FromToRotation(current.normalized, direction) * bone.rotation;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static ModelImporterClipAnimation[] BuildClips(ModelImporterClipAnimation[] takes)
        {
            var result = new List<ModelImporterClipAnimation>();
            ModelImporterClipAnimation land = null;
            foreach (ModelImporterClipAnimation take in takes)
            {
                string name = take.takeName;
                int bar = name.LastIndexOf('|');
                if (bar >= 0)
                {
                    name = name.Substring(bar + 1);
                }

                bool loop = false;
                bool keepHeight = true;
                foreach (var c in Clips)
                {
                    if (c.Name == name)
                    {
                        loop = c.Loop;
                        keepHeight = c.KeepHeight;
                    }
                }

                ModelImporterClipAnimation clip = Configure(take, name, loop, keepHeight);
                result.Add(clip);
                if (name == "Land")
                {
                    land = clip;
                }
            }

            if (land != null)
            {
                ModelImporterClipAnimation landRun = Configure(land, LandRunClip, false, true);
                landRun.takeName = land.takeName;
                landRun.firstFrame = land.firstFrame;
                landRun.lastFrame = Mathf.Min(land.lastFrame, land.firstFrame + LandRunLastFrame);
                result.Add(landRun);
            }

            return result.ToArray();
        }

        private static ModelImporterClipAnimation Configure(ModelImporterClipAnimation take, string name, bool loop, bool keepHeight)
        {
            return new ModelImporterClipAnimation
            {
                takeName = take.takeName,
                name = name,
                firstFrame = take.firstFrame,
                lastFrame = take.lastFrame,
                loopTime = loop,
                loopPose = false,
                cycleOffset = 0f,
                wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever,
                lockRootRotation = true,
                keepOriginalOrientation = true,
                lockRootHeightY = keepHeight,
                keepOriginalPositionY = true,
                heightFromFeet = false,
                // Loops are in place already (their sway stays); one-shots (slide, stumble, landing, death) drift
                // forward in the source, so their horizontal body motion is dropped: the simulation owns position.
                lockRootPositionXZ = loop,
                keepOriginalPositionXZ = true,
                mirror = false,
            };
        }
    }
}
