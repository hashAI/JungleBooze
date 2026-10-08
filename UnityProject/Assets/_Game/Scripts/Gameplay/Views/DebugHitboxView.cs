using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// F1 debug overlay geometry (spec 101 §3.4, AC-101-46): the runner's current hitbox, the lateral target xT,
    /// and every obstacle's forgiving hitbox (spec 101 §2.6). Built once; toggling only enables objects.
    /// </summary>
    public sealed class DebugHitboxView : MonoBehaviour
    {
        private RunnerSimulation _sim;
        private Transform _runnerBox;
        private Transform _target;
        private GameObject _obstacleRoot;
        private bool _visible;

        public bool Visible => _visible;

        public void Build(RunnerSimulation sim, CoursePath course, FeelTestPalette palette)
        {
            _sim = sim;
            _runnerBox = ViewUtil.Primitive(PrimitiveType.Cube, "RunnerHitbox", transform, palette.DebugRunner, false);
            _target = ViewUtil.Primitive(PrimitiveType.Cube, "LateralTarget", transform, palette.DebugTarget, false);
            _target.localScale = new Vector3(0.06f, 2.2f, 0.06f);
            _obstacleRoot = new GameObject("ObstacleHitboxes");
            _obstacleRoot.transform.SetParent(transform, false);
            for (int id = 0; id < course.ObstacleCount; id++)
            {
                ObstacleBox box = course.GetObstacle(id);
                sim.EffectiveBox(box, out float x0, out float x1, out float y0, out float y1);
                ViewUtil.Box("Hitbox " + id, _obstacleRoot.transform, palette.DebugHitbox, new Vector3(x0, y0, box.SMin), new Vector3(x1, Mathf.Min(y1, y0 + 4f), box.SMax), false);
            }

            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _runnerBox.gameObject.SetActive(visible);
            _target.gameObject.SetActive(visible);
            _obstacleRoot.SetActive(visible);
        }

        public void Sync(in RunnerState s)
        {
            if (!_visible)
            {
                return;
            }

            HitboxConfig hb = _sim.Config.Hitbox;
            float depth = s.Sliding ? hb.SlideDepth : hb.RunDepth;
            float height = s.Sliding ? hb.SlideHeight : s.Grounded ? hb.RunHeight : hb.AirHeight;
            _runnerBox.localPosition = new Vector3(s.X, s.Y + (height * 0.5f), s.S);
            _runnerBox.localScale = new Vector3(hb.Width, height, depth);
            _target.localPosition = new Vector3(s.XTarget, s.GroundY + 1.1f, s.S + 1.5f);
        }
    }
}
