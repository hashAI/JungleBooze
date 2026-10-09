using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Characters
{
    /// <summary>
    /// Batch diagnostic: runs the Pista prefab at constant 10/13/16 m/s and logs how fast the planted foot moves in
    /// world space (foot skating) and the cadence. tools/ci/unity.sh method JungleBooze.Editor.Characters.PistaSkateProbe.Run -nographics
    /// </summary>
    public static class PistaSkateProbe
    {
        /// <summary>Foot-skate measurement: planted-foot world speed at constant run speeds (bone positions, no rendering).</summary>
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Prefab);
            var sb = new StringBuilder();
            foreach (float speed in new[] { 10f, 13f, 16f })
            {
                GameObject go = Object.Instantiate(prefab);
                var avatar = go.GetComponent<JungleBooze.Gameplay.Views.AnimatedRunnerAvatar>();
                avatar.ResetPose();
                Animator a = avatar.Animator;
                Transform[] feet = { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
                Vector3[] last = new Vector3[2];
                float z = 0f;
                int contact = 0;
                double skateSum = 0;
                float skateMax = 0f;
                int stepsCount = 0;
                bool[] down = new bool[2];
                const float dt = 1f / 60f;
                for (int i = 0; i < 600; i++)
                {
                    z += speed * dt;
                    var st = new JungleBooze.Gameplay.Views.RunnerVisualState { Position = new Vector3(0, 0, z), Facing = Quaternion.identity, Speed = speed, Grounded = true, VLatMax = 11f };
                    avatar.Apply(st, dt);
                    for (int f = 0; f < 2; f++)
                    {
                        Vector3 p = feet[f].position;
                        bool planted = p.y < 0.165f;
                        if (i > 120 && planted && down[f])
                        {
                            float v = Mathf.Abs(p.z - last[f].z) / dt;
                            skateSum += v;
                            skateMax = Mathf.Max(skateMax, v);
                            contact++;
                        }

                        if (i > 120 && planted && !down[f])
                        {
                            stepsCount++;
                        }

                        down[f] = planted;
                        last[f] = p;
                    }
                }

                sb.AppendFormat(CultureInfo.InvariantCulture, "speed {0}: planted-foot world speed mean {1:0.00} m/s, max {2:0.00} m/s over {3} contact frames; steps/s {4:0.0} (warp model {5:0.0})\n",
                    speed, contact > 0 ? skateSum / contact : 0, skateMax, contact, stepsCount / (479f / 60f),
                    JungleBooze.Gameplay.Animation.StrideWarp.StepsPerSecond(avatar.Model.Config, speed, avatar.Model.Output.LocoBlend));
                Object.DestroyImmediate(go);
            }

            Debug.Log("[JungleBooze] skate\n" + sb);
            EditorApplication.Exit(0);
        }
    }
}
