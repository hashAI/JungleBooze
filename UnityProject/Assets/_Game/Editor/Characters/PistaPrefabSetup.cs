using JungleBooze.Gameplay.Animation;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Views;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace JungleBooze.Editor.Characters
{
    /// <summary>
    /// Builds Pista's Animator Controller (one base-layer state per <see cref="RunnerAnimState"/>, no transitions:
    /// <see cref="RunnerAnimationModel"/> cross-fades from code so input → pose has no transition-condition lag) and
    /// the runner prefab (<see cref="AnimatedRunnerAvatar"/> on the root, the FBX model as child).
    /// </summary>
    public static class PistaPrefabSetup
    {
        /// <summary>Run's cycle is shifted so its left-foot contact lines up with Run_Alt's in the blend tree.</summary>
        public const float RunCycleOffset = 0.265f;

        [MenuItem("JungleBooze/Characters/Build Pista Prefab", false, 31)]
        public static GameObject BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model) == null || !AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model).isHuman)
            {
                PistaImportSetup.ConfigureAll();
            }

            RunnerAnimationConfigAsset config = EnsureConfig();
            config.Values.StanceSpeedTable = BuildStanceSpeedTable("Run", RunCycleOffset, 64);
            EditorUtility.SetDirty(config);
            AnimatorController controller = BuildController();
            return BuildPrefab(controller, config, PistaPaths.Prefab);
        }

        /// <summary>Batch: tools/ci/unity.sh method JungleBooze.Editor.Characters.PistaPrefabSetup.Batch -nographics</summary>
        public static void Batch()
        {
            int code = 0;
            try
            {
                PistaImportSetup.ConfigureAll();
                BuildAll();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[JungleBooze] Pista setup failed: " + e);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static RunnerAnimationConfigAsset EnsureConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RunnerAnimationConfigAsset>(PistaPaths.AnimationConfig);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RunnerAnimationConfigAsset>();
                asset.SetValues(new RunnerAnimationConfig());
                AssetDatabase.CreateAsset(asset, PistaPaths.AnimationConfig);
            }

            return asset;
        }

        /// <summary>
        /// Samples a run clip on the humanoid model and returns, per phase of the (cycle-offset) loop, how fast the
        /// planted foot moves backwards at 1.0x (m/s); −1 where neither foot is planted. Feeds <c>StrideWarp</c>.
        /// </summary>
        public static float[] BuildStanceSpeedTable(string clipName, float cycleOffset, int entries)
        {
            AnimationClip clip = Clip(clipName);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Model);
            GameObject go = Object.Instantiate(source);
            try
            {
                Animator animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = go.AddComponent<Animator>();
                }

                animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model);
                animator.applyRootMotion = false;
                Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                const int Fine = 4;
                int samples = entries * Fine;
                var y = new float[2, samples];
                var z = new float[2, samples];
                float minY = float.MaxValue;
                for (int i = 0; i < samples; i++)
                {
                    float phase = Mathf.Repeat((i / (float)samples) + cycleOffset, 1f);
                    clip.SampleAnimation(go, phase * clip.length);
                    for (int f = 0; f < 2; f++)
                    {
                        Vector3 p = go.transform.InverseTransformPoint(feet[f].position);
                        y[f, i] = p.y;
                        z[f, i] = p.z;
                        minY = Mathf.Min(minY, p.y);
                    }
                }

                float plantedBelow = minY + 0.05f;
                float dtPerSample = clip.length / samples;
                var table = new float[entries];
                for (int e = 0; e < entries; e++)
                {
                    float best = -1f;
                    for (int k = 0; k < Fine; k++)
                    {
                        int i = (e * Fine) + k;
                        int prev = (i - 1 + samples) % samples;
                        int next = (i + 1) % samples;
                        for (int f = 0; f < 2; f++)
                        {
                            if (y[f, i] > plantedBelow)
                            {
                                continue;
                            }

                            float backward = -(z[f, next] - z[f, prev]) / (2f * dtPerSample);
                            best = Mathf.Max(best, Mathf.Max(0f, backward));
                        }
                    }

                    table[e] = best;
                }

                return table;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// The Expedition's Pista (spec 103): same model and tuning, a controller with the traversal states too
        /// (Swim = Run, Dive = Fall, Grab = Vine_Grab, Hang = Vine_Hang; procedural body pitch on top). Written next to
        /// Pista.prefab; the shared prefab and controller are not touched.
        /// </summary>
        [MenuItem("JungleBooze/Characters/Build Pista Expedition Prefab", false, 32)]
        public static GameObject BuildExpeditionPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model) == null || !AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model).isHuman)
            {
                PistaImportSetup.ConfigureAll();
            }

            RunnerAnimationConfigAsset config = EnsureConfig();
            AnimatorController controller = BuildController(PistaPaths.ExpeditionController, true);
            return BuildPrefab(controller, config, PistaPaths.ExpeditionPrefab);
        }

        public static AnimatorController BuildController()
        {
            return BuildController(PistaPaths.Controller, false);
        }

        public static AnimatorController BuildController(string path, bool traversal)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(new AnimatorControllerParameter { name = "LocoBlend", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = "RunRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = "StateRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = false;
            controller.layers = layers;

            AnimatorState idle = AddState(machine, "Idle", Clip("Idle"), null, new Vector3(250f, 0f));
            BlendTree tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "LocoBlend",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.children = new[]
            {
                new ChildMotion { motion = Clip("Run"), threshold = 0f, timeScale = 1f, cycleOffset = RunCycleOffset },
                new ChildMotion { motion = Clip("Run_Alt"), threshold = 1f, timeScale = 1f, cycleOffset = 0f },
            };
            AddState(machine, "Locomotion", tree, "RunRate", new Vector3(250f, 80f));
            AddState(machine, "Jump", Clip("Jump"), "StateRate", new Vector3(500f, 0f));
            AddState(machine, "Fall", Clip("Fall"), "StateRate", new Vector3(500f, 80f));
            AddState(machine, "Slide", Clip("Slide"), "StateRate", new Vector3(500f, 160f));
            AddState(machine, "Stumble", Clip("Stumble"), "StateRate", new Vector3(250f, 160f));
            AddState(machine, "LandHard", Clip(PistaImportSetup.LandRunClip), "StateRate", new Vector3(500f, 240f));
            AddState(machine, "Death", Clip("Death_Backward"), "StateRate", new Vector3(250f, 240f));
            if (traversal)
            {
                AddState(machine, "Swim", Clip("Run"), "StateRate", new Vector3(750f, 0f));
                AddState(machine, "Dive", Clip("Fall"), "StateRate", new Vector3(750f, 80f));
                AddState(machine, "Grab", Clip("Vine_Grab"), "StateRate", new Vector3(750f, 160f));
                AddState(machine, "Hang", Clip("Vine_Hang"), "StateRate", new Vector3(750f, 240f));
            }

            machine.defaultState = idle;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion, string speedParameter, Vector3 position)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            state.iKOnFeet = false;
            if (speedParameter != null)
            {
                state.speedParameter = speedParameter;
                state.speedParameterActive = true;
            }

            return state;
        }

        private static AnimationClip Clip(string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(PistaPaths.Model))
            {
                if (o is AnimationClip clip && clip.name == name)
                {
                    return clip;
                }
            }

            throw new System.InvalidOperationException("Clip '" + name + "' not found in " + PistaPaths.Model + " (run Configure Pista Import).");
        }

        private static GameObject BuildPrefab(AnimatorController controller, RunnerAnimationConfigAsset config, string prefabPath)
        {
            if (!AssetDatabase.IsValidFolder(PistaPaths.PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Characters");
            }

            var root = new GameObject("Pista");
            try
            {
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Model);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                Animator animator = model.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = model.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.shadowCastingMode = ShadowCastingMode.On;
                    skin.receiveShadows = true;
                    skin.updateWhenOffscreen = false;
                    skin.skinnedMotionVectors = false;
                    skin.quality = SkinQuality.Bone4;
                }

                AnimatedRunnerAvatar avatar = root.AddComponent<AnimatedRunnerAvatar>();
                avatar.Configure(animator, model.transform, config);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("[JungleBooze] Pista prefab written: " + prefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
