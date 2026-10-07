using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box stand-in for Pista: a map-cloth capsule body with skin head and dark hair, the brown "X" on her back
    /// (two thin crossed cubes, facing the camera), the teal sash band below it, a satchel at the hip, and a flat
    /// blob shadow that shrinks with height. Squashes while sliding, wobbles toward the blocked side on a lane bump
    /// (spec 001 10.8), bobs while running and tips over on death. Pivot at the feet. No allocations per frame.
    /// </summary>
    public sealed class RunnerView : MonoBehaviour, IRunView
    {
        // Gray-box proportions (not tuning; replaced by the real model).
        private const float BodyHeightM = 1.4f;
        private const float BodyRadiusM = 0.4f;
        private const float HeadDiameterM = 0.42f;
        private const float SquashSmoothSeconds = 0.05f;
        private const float SlideWidenFactor = 1.2f;
        private const float BobHeightM = 0.05f;
        private const float BobStrideM = 1.6f;
        private const float ShadowWidthM = 0.9f;
        private const float ShadowDepthM = 0.6f;
        private const float ShadowLiftM = 0.012f;
        private const float DeathTiltDeg = 75f;
        private const float StumbleSeconds = 0.35f;
        private const float StumbleHopM = 0.2f;
        private const float StumbleTiltDeg = 20f;

        private RunnerConfig _runnerConfig;
        private RunnerPresentationConfig _presentation;
        private Transform _model;
        private Transform _shadow;
        private float _squash = 1f;
        private float _wobbleLeft;
        private float _wobbleDir;
        private bool _dead;
        private float _stumbleLeft;

        /// <summary>Rendered position of HERO's feet (after interpolation, before wobble and bob).</summary>
        public Vector3 RenderedFeetPosition { get; private set; }

        /// <summary>Current vertical scale of the model (1 standing, &lt; 1 sliding).</summary>
        public float CurrentSquash => _squash;

        public void Init(GrayBoxKit kit, RunnerConfig runnerConfig, RunnerPresentationConfig presentation)
        {
            _runnerConfig = runnerConfig;
            _presentation = presentation;

            _model = new GameObject("PistaModel").transform;
            _model.SetParent(transform, false);

            float bodyDiameter = BodyRadiusM * 2f;
            kit.Create(
                PrimitiveType.Capsule,
                "Body",
                _model,
                StylePalette.PistaMapCloth,
                new Vector3(0f, BodyHeightM * 0.5f, 0f),
                new Vector3(bodyDiameter, BodyHeightM * 0.5f, bodyDiameter));

            kit.Create(
                PrimitiveType.Sphere,
                "Head",
                _model,
                StylePalette.PistaSkin,
                new Vector3(0f, BodyHeightM + HeadDiameterM * 0.35f, 0f),
                new Vector3(HeadDiameterM, HeadDiameterM, HeadDiameterM));

            kit.Create(
                PrimitiveType.Sphere,
                "Hair",
                _model,
                StylePalette.PistaHair,
                new Vector3(0f, BodyHeightM + HeadDiameterM * 0.5f, -0.04f),
                new Vector3(HeadDiameterM * 1.06f, HeadDiameterM * 0.7f, HeadDiameterM * 1.06f));

            // The back "X" (camera looks along +z, so the back faces -z).
            float backZ = -(BodyRadiusM + 0.005f);
            float xCenterY = BodyHeightM * 0.68f;
            Vector3 barScale = new Vector3(0.06f, 0.42f, 0.02f);
            Transform barA = kit.Create(PrimitiveType.Cube, "BackX_A", _model, StylePalette.PistaMapLines, new Vector3(0f, xCenterY, backZ), barScale);
            barA.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Transform barB = kit.Create(PrimitiveType.Cube, "BackX_B", _model, StylePalette.PistaMapLines, new Vector3(0f, xCenterY, backZ), barScale);
            barB.localRotation = Quaternion.Euler(0f, 0f, -45f);

            // Teal sash band below the X (owner decision: sash below the X).
            kit.Create(
                PrimitiveType.Cylinder,
                "Sash",
                _model,
                StylePalette.PistaTealSash,
                new Vector3(0f, BodyHeightM * 0.42f, 0f),
                new Vector3(bodyDiameter * 1.05f, 0.05f, bodyDiameter * 1.05f));

            // Satchel on the right hip with its saffron strap tab.
            kit.Create(
                PrimitiveType.Cube,
                "Satchel",
                _model,
                StylePalette.PistaSatchel,
                new Vector3(BodyRadiusM + 0.06f, BodyHeightM * 0.33f, 0f),
                new Vector3(0.12f, 0.24f, 0.3f));
            kit.Create(
                PrimitiveType.Cube,
                "SatchelStrap",
                _model,
                StylePalette.PistaSatchelStrap,
                new Vector3(BodyRadiusM + 0.04f, BodyHeightM * 0.5f, 0f),
                new Vector3(0.05f, 0.3f, 0.08f));

            _shadow = kit.Create(
                PrimitiveType.Cylinder,
                "BlobShadow",
                transform,
                StylePalette.BlobShadow,
                new Vector3(0f, ShadowLiftM, 0f),
                new Vector3(ShadowWidthM, 0.002f, ShadowDepthM));
        }

        public void BeginRun(GameSession session)
        {
            _squash = 1f;
            _wobbleLeft = 0f;
            _wobbleDir = 0f;
            _dead = false;
            _stumbleLeft = 0f;
            if (_model != null)
            {
                _model.localRotation = Quaternion.identity;
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            switch (e.Type)
            {
                case RunnerEventType.LaneBlocked:
                    _wobbleLeft = _presentation.LaneBumpWobbleMs / 1000f;
                    _wobbleDir = e.Dir;
                    break;

                case RunnerEventType.Stumbled:
                    _stumbleLeft = StumbleSeconds;
                    break;

                case RunnerEventType.Died:
                    _dead = true;
                    break;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_model == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerState current = runner.Current;
            RunnerInterpolation.Evaluate(runner.Previous, current, alpha, out float x, out float y, out double z);
            RenderedFeetPosition = new Vector3(x, y, (float)z);

            // Slide squash: hitbox ratio, smoothed a little in real time.
            float targetSquash = current.Locomotion == Locomotion.Sliding
                ? _runnerConfig.SlidingHeightM / _runnerConfig.StandingHeightM
                : 1f;
            if (realDeltaSeconds <= 0f)
            {
                _squash = targetSquash;
            }
            else
            {
                float k = 1f - Mathf.Exp(-realDeltaSeconds / SquashSmoothSeconds);
                _squash += (targetSquash - _squash) * k;
            }

            float widen = 1f + (SlideWidenFactor - 1f) * (1f - _squash) / (1f - _runnerConfig.SlidingHeightM / _runnerConfig.StandingHeightM);
            _model.localScale = new Vector3(widen, _squash, widen);

            // Lane bump wobble: a half-sine push toward the blocked side; the camera does not move.
            float wobble = 0f;
            float wobbleSeconds = _presentation.LaneBumpWobbleMs / 1000f;
            if (_wobbleLeft > 0f && wobbleSeconds > 0f)
            {
                float u = 1f - _wobbleLeft / wobbleSeconds;
                wobble = _wobbleDir * _presentation.LaneBumpWobbleM * Mathf.Sin(u * Mathf.PI);
                if (!session.InHitPause)
                {
                    _wobbleLeft -= realDeltaSeconds;
                }
            }

            // Run bob from distance (no clock needed, freezes with the simulation).
            float bob = 0f;
            if (current.Locomotion == Locomotion.Running)
            {
                bob = BobHeightM * Mathf.Abs(Mathf.Sin((float)(z % (BobStrideM * 2.0)) * Mathf.PI / BobStrideM));
            }

            // Stumble: a short hop and forward lurch (no tint: hazard red is banned on the hero).
            float stumble = 0f;
            if (_stumbleLeft > 0f)
            {
                stumble = Mathf.Sin((1f - _stumbleLeft / StumbleSeconds) * Mathf.PI);
                if (!session.InHitPause)
                {
                    _stumbleLeft -= realDeltaSeconds;
                }
            }

            transform.localPosition = new Vector3(x, 0f, (float)z);
            _model.localPosition = new Vector3(wobble, y + bob + StumbleHopM * stumble, 0f);
            _model.localRotation = _dead
                ? Quaternion.Euler(DeathTiltDeg, 0f, 0f)
                : Quaternion.Euler(StumbleTiltDeg * stumble, 0f, 0f);

            float apex = _runnerConfig.JumpApexHeightM > 0f ? _runnerConfig.JumpApexHeightM : 1f;
            float shadowScale = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(y / apex));
            _shadow.localScale = new Vector3(ShadowWidthM * shadowScale, 0.002f, ShadowDepthM * shadowScale);
            _shadow.gameObject.SetActive(y > -0.05f);
        }
    }
}
