using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Characters
{
    /// <summary>
    /// Batch diagnostics for the Pista import: <see cref="Run"/> configures the import and logs clips/avatar;
    /// <see cref="Analyze"/> samples every clip and logs hips height and foot positions per frame (contact frames,
    /// natural ground speed). Used to pick the timing numbers in RunnerAnimationConfig.
    /// </summary>
    public static class PistaInspect
    {
        public static void Run()
        {
            int code = 0;
            try
            {
                PistaImportSetup.ConfigureAll();
                var sb = new StringBuilder();
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(PistaPaths.Model))
                {
                    if (o is AnimationClip c && !o.name.StartsWith("__preview"))
                    {
                        sb.Append("clip ").Append(c.name).Append(" len ").Append(c.length.ToString("0.000", CultureInfo.InvariantCulture)).Append(" loop ").Append(c.isLooping).Append(" human ").Append(c.isHumanMotion).AppendLine();
                    }

                    if (o is Avatar a)
                    {
                        sb.Append("avatar valid ").Append(a.isValid).Append(" human ").Append(a.isHuman).AppendLine();
                    }
                }

                Debug.Log("[JungleBooze] Pista inspect\n" + sb);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[JungleBooze] Pista inspect failed: " + e);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static void Analyze()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PistaPaths.Model);
            GameObject go = Object.Instantiate(source);
            Animator animator = go.GetComponent<Animator>();
            if (animator == null)
            {
                animator = go.AddComponent<Animator>();
            }

            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(PistaPaths.Model);
            animator.applyRootMotion = false;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            var sb = new StringBuilder();
            sb.AppendLine("rest: hips " + hips.position.ToString("F3") + " head " + head.position.ToString("F3") + " lfoot " + lf.position.ToString("F3"));
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(PistaPaths.Model))
            {
                if (!(o is AnimationClip clip) || clip.name.StartsWith("__preview"))
                {
                    continue;
                }

                int frames = Mathf.RoundToInt(clip.length * 30f);
                sb.Append("== ").Append(clip.name).Append(" frames ").Append(frames).AppendLine();
                for (int f = 0; f <= frames; f++)
                {
                    clip.SampleAnimation(go, f / 30f);
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0,3} hipsY {1:0.000} hipsZ {2:0.000} headY {3:0.000} L({4:0.000},{5:0.000},{6:0.000}) R({7:0.000},{8:0.000},{9:0.000}) toeY {10:0.000}/{11:0.000}\n",
                        f, hips.position.y, hips.position.z, head.position.y, lf.position.x, lf.position.y, lf.position.z, rf.position.x, rf.position.y, rf.position.z, lt.position.y, rt.position.y);
                }
            }

            Object.DestroyImmediate(go);
            string outPath = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("JB_OUT") ?? "/tmp/junglebooze-unity", "pista_clips.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[JungleBooze] Pista analysis written to " + outPath);
            EditorApplication.Exit(0);
        }
    }
}
