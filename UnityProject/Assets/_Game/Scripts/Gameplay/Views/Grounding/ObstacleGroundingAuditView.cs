using System.Text;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Development aid (editor and development builds only): F7 writes one Console line per obstacle in view with the
    /// numbers of the grounding audit (spec 005 3.2, G1, G6): the "ghost distance" between each lethal hitbox face and
    /// the nearest visible body surface, the embed depth below the ground and any float above it, measured on what is
    /// really drawn (models and gray-box alike) in the rig's own frame. A FAIL line names the failed check. The
    /// hitbox overlay on F3 stays the way to see the boxes; this tool gives the numbers. Allocates (it is a dev dump).
    /// </summary>
    public sealed class ObstacleGroundingAuditView : MonoBehaviour, IRunView
    {
        private const float InViewM = 100f;
        private const float BehindM = 6f;

        private ObstacleView _obstacles;
        private HazardView _hazards;
        private RunnerConfig _runnerConfig;
        private ObstacleGroundingTuning _tuning;
        private double _heroZ;

        public void Init(ObstacleView obstacles, HazardView hazards, RunnerConfig runnerConfig)
        {
            _obstacles = obstacles;
            _hazards = hazards;
            _runnerConfig = runnerConfig;
            _tuning = obstacles.Tuning;
        }

        public void BeginRun(GameSession session)
        {
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (!Debug.isDebugBuild || _obstacles == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double heroZ);
            _heroZ = heroZ;
            if (AuditPressed())
            {
                RunAudit();
            }
        }

        private static bool AuditPressed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f7Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.F7);
#else
            return false;
#endif
#else
            return false;
#endif
        }

        private void RunAudit()
        {
            int total = 0;
            int failed = 0;
            for (int i = 0; i < _obstacles.RigCount; i++)
            {
                AuditRig(_obstacles.GetRig(i), ref total, ref failed);
            }

            if (_hazards != null)
            {
                for (int i = 0; i < _hazards.RigCount; i++)
                {
                    AuditRig(_hazards.GetRig(i), ref total, ref failed);
                }
            }

            Debug.Log("[JungleBooze] Grounding audit (F7): " + total + " obstacle(s) in view, " + failed + " failed. Limits: ghost "
                + _tuning.GhostMaxStaticM.ToString("F2") + " m (mover " + _tuning.GhostMaxMoverM.ToString("F2") + " m), embed at least "
                + _tuning.EmbedMinM.ToString("F2") + " m, float at most " + _tuning.MaxFloatM.ToString("F2") + " m.");
        }

        private void AuditRig(ObstacleRig rig, ref int total, ref int failed)
        {
            if (!rig.Live || !rig.Root.activeInHierarchy)
            {
                return;
            }

            float distance = (float)(rig.CenterS - _heroZ);
            if (distance < -BehindM || distance > InViewM)
            {
                return;
            }

            // The hitbox in the rig's frame: the union of the lane boxes (movers: the box at the barrel's current x).
            float w = rig.Shape.WidthM;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            if (rig.Kind == ObstacleArchetype.Mover)
            {
                float bx = rig.BarrelRoot != null ? rig.BarrelRoot.localPosition.x : rig.StartX;
                minX = bx - (w * 0.5f);
                maxX = bx + (w * 0.5f);
            }
            else
            {
                for (int lane = 0; lane < LaneMasks.LaneCount; lane++)
                {
                    if (LaneMasks.Contains(rig.LaneMask, lane))
                    {
                        float cx = _runnerConfig.LaneCenterX(lane);
                        minX = Mathf.Min(minX, cx - (w * 0.5f));
                        maxX = Mathf.Max(maxX, cx + (w * 0.5f));
                    }
                }
            }

            if (minX > maxX)
            {
                return;
            }

            var hitMin = new Vector3(minX, rig.Shape.BottomM, -rig.DepthM * 0.5f);
            var hitMax = new Vector3(maxX, rig.Shape.TopM, rig.DepthM * 0.5f);
            bool has = MeasureBody(rig, out Vector3 visMin, out Vector3 visMax);
            float ghostMax = rig.Kind == ObstacleArchetype.Mover ? _tuning.GhostMaxMoverM : _tuning.GhostMaxStaticM;
            GroundingAuditResult result = GroundingAuditMath.Evaluate(hitMin, hitMax, visMin, visMax, has, ghostMax, _tuning.EmbedMinM, _tuning.MaxFloatM);

            total++;
            if (!result.Pass)
            {
                failed++;
            }

            var line = new StringBuilder("[JungleBooze] Grounding obstacle #");
            line.Append(rig.ObstacleId).Append(' ').Append(rig.Kind).Append(" skin ").Append(rig.Skin);
            line.Append(" lanes ").Append(rig.LaneMask).Append(" at ").Append(distance.ToString("F1")).Append(" m: ");
            line.Append("ghost front ").Append(result.GhostFrontM.ToString("F2"));
            line.Append(" top ").Append(result.GhostTopM.ToString("F2"));
            line.Append(" bottom ").Append(result.GhostBottomM.ToString("F2"));
            line.Append(" sides ").Append(result.GhostLeftM.ToString("F2")).Append('/').Append(result.GhostRightM.ToString("F2"));
            line.Append(" | embed ").Append(result.EmbedM.ToString("F2"));
            line.Append(" float ").Append(result.FloatM.ToString("F2"));
            line.Append(" proud front ").Append(result.ProudFrontM.ToString("F2"));
            line.Append(" | parts ").Append(rig.PartsUsed).Append(" | ");
            if (result.Pass)
            {
                line.Append("PASS");
                Debug.Log(line.ToString());
            }
            else
            {
                line.Append("FAIL ").Append(result.Failures);
                Debug.LogWarning(line.ToString());
            }
        }

        /// <summary>Bounds of every mesh under the rig's Body group, in the rig's local frame. False when there is none.</summary>
        private static bool MeasureBody(ObstacleRig rig, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            Transform space = rig.Root.transform;
            MeshFilter[] filters = rig.Body.GetComponentsInChildren<MeshFilter>(false);
            for (int f = 0; f < filters.Length; f++)
            {
                MeshFilter filter = filters[f];
                Mesh mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds mb = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(
                        mb.center.x + ((corner & 1) == 0 ? -mb.extents.x : mb.extents.x),
                        mb.center.y + ((corner & 2) == 0 ? -mb.extents.y : mb.extents.y),
                        mb.center.z + ((corner & 4) == 0 ? -mb.extents.z : mb.extents.z));
                    Vector3 p = space.InverseTransformPoint(filter.transform.TransformPoint(local));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    any = true;
                }
            }

            return any;
        }
    }
}
