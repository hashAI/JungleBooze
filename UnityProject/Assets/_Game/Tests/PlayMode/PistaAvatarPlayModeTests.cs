using System;
using System.Collections;
using JungleBooze.Gameplay.Animation;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// The rigged Pista in the player loop: the Animator is driven (no bind/T-pose), the jump arc keeps the lowest
    /// foot at the simulation's feet height, the ponytail moves, and Apply() allocates nothing after warm-up.
    /// </summary>
    public sealed class PistaAvatarPlayModeTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Characters/Pista.prefab";
        private const float Dt = 1f / 60f;

        private static AnimatedRunnerAvatar Spawn()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Missing " + PrefabPath + " (JungleBooze > Characters > Build Pista Prefab)");
            GameObject go = Object.Instantiate(prefab);
            var avatar = go.GetComponent<AnimatedRunnerAvatar>();
            avatar.Bind(new MovementConfig(new RunSpeedConfig(), new LateralMovementConfig(), new JumpSlideConfig(), new HitboxConfig(), new HealthConfig(), new RunFlowConfig()));
            avatar.ResetPose();
            return avatar;
#else
            Assert.Ignore("Editor only");
            return null;
#endif
        }

        private static RunnerVisualState Running(float z, float speed)
        {
            return new RunnerVisualState { Position = new Vector3(0f, 0f, z), Facing = Quaternion.identity, Speed = speed, Grounded = true, VLatMax = 11f };
        }

        [UnityTest]
        public IEnumerator RunsWithArmsDownAndFeetCycling()
        {
            AnimatedRunnerAvatar avatar = Spawn();
            Animator a = avatar.Animator;
            Assert.IsTrue(a.isHuman);
            Transform arm = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform fore = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            float minFoot = float.MaxValue;
            float maxFoot = float.MinValue;
            float z = 0f;
            for (int i = 0; i < 90; i++)
            {
                z += 12f * Dt;
                avatar.Apply(Running(z, 12f), Dt);
                if (i > 30)
                {
                    float y = foot.position.y;
                    minFoot = Mathf.Min(minFoot, y);
                    maxFoot = Mathf.Max(maxFoot, y);
                }

                yield return null;
            }

            Assert.AreEqual(RunnerAnimState.Locomotion, avatar.Model.State);
            Vector3 upper = (fore.position - arm.position).normalized;
            Assert.Less(Mathf.Abs(upper.x), 0.8f, "upper arm is not held out sideways (no T/A-pose flash)");
            Assert.Greater(maxFoot - minFoot, 0.25f, "legs cycle");
            Assert.Less(minFoot, 0.2f, "feet reach the ground");
            Object.Destroy(avatar.gameObject);
        }

        [UnityTest]
        public IEnumerator JumpKeepsTheLowestFootAtTheSimulationHeight()
        {
            AnimatedRunnerAvatar avatar = Spawn();
            Animator a = avatar.Animator;
            Transform left = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = a.GetBoneTransform(HumanBodyBones.RightFoot);
            float z = 0f;
            for (int i = 0; i < 40; i++)
            {
                z += 12f * Dt;
                avatar.Apply(Running(z, 12f), Dt);
                yield return null;
            }

            avatar.OnRunEvent(new RunEvent(RunEventType.Jump, 0, -1, 0, 0f));
            float worst = 0f;
            for (int i = 0; i < 36; i++)
            {
                z += 12f * Dt;
                float t = (i + 1) * Dt;
                float simY = Mathf.Max(0f, (9.33f * t) - (0.5f * 31.1f * t * t));
                RunnerVisualState air = Running(z, 12f);
                air.Grounded = false;
                air.Position = new Vector3(0f, simY, z);
                avatar.Apply(air, Dt);
                if (i >= 6 && i <= 24)
                {
                    float lowest = Mathf.Min(left.position.y, right.position.y) - simY;
                    worst = Mathf.Max(worst, Mathf.Abs(lowest - 0.13f));
                }

                yield return null;
            }

            Assert.Less(worst, 0.12f, "mid-air, the lowest ankle stays within 12 cm of the simulated feet (+ankle height)");
            Object.Destroy(avatar.gameObject);
        }

        [UnityTest]
        public IEnumerator PonytailSwingsAndApplyDoesNotAllocate()
        {
            AnimatedRunnerAvatar avatar = Spawn();
            Transform tail = FindDeep(avatar.transform, "Ponytail03");
            Transform head = avatar.Animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.IsNotNull(tail);
            float minLocal = float.MaxValue;
            float maxLocal = float.MinValue;
            float z = 0f;
            for (int i = 0; i < 120; i++)
            {
                z += 12f * Dt;
                RunnerVisualState s = Running(z, 12f);
                s.VLat = (i / 20) % 2 == 0 ? 11f : -11f;
                avatar.Apply(s, Dt);
                float local = head.InverseTransformPoint(tail.position).x;
                minLocal = Mathf.Min(minLocal, local);
                maxLocal = Mathf.Max(maxLocal, local);
                yield return null;
            }

            Assert.Greater(maxLocal - minLocal, 0.01f, "the ponytail moves relative to the head");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++)
            {
                z += 12f * Dt;
                avatar.Apply(Running(z, 12f), Dt);
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before, "no allocation per frame");
            Object.Destroy(avatar.gameObject);
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
    }
}
