using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Gray-box stand-in for Pista: a 1.65 m capsule with a nose (facing) and a backpack block. It leans into
    /// steering, tucks in the air, lies back when sliding, squashes on landing, wobbles on a stumble and topples on
    /// a crash, so every movement state reads at a glance. Built from primitives; no allocation per frame.
    /// </summary>
    public sealed class CapsuleRunnerAvatar : RunnerAvatar
    {
        public const float BodyHeight = 1.65f;

        private Transform _pivot;
        private Transform _body;
        private Renderer[] _renderers;
        private float _lean;
        private float _slideBlend;
        private float _tuck;
        private float _landSquash;
        private float _deathTilt;
        private float _bobPhase;
        private float _swim;
        private float _swimPitch;

        public static CapsuleRunnerAvatar Create(Transform parent, Material body, Material accent)
        {
            var root = new GameObject("PistaStandIn");
            root.transform.SetParent(parent, false);
            CapsuleRunnerAvatar avatar = root.AddComponent<CapsuleRunnerAvatar>();
            avatar.Build(body, accent);
            return avatar;
        }

        public override void Apply(in RunnerVisualState state, float frameSeconds)
        {
            float dt = Mathf.Max(0f, frameSeconds);
            float lateral = state.VLatMax > 0f ? Mathf.Clamp(state.VLat / state.VLatMax, -1f, 1f) : 0f;
            _lean = Damp(_lean, -lateral * 14f, 0.06f, dt);
            _slideBlend = Damp(_slideBlend, state.Sliding ? 1f : 0f, 0.04f, dt);
            _tuck = Damp(_tuck, state.Grounded ? 0f : 1f, 0.05f, dt);
            _landSquash = Damp(_landSquash, 0f, 0.08f, dt);
            _deathTilt = Damp(_deathTilt, state.Dead && state.Cause != DeathCause.Fall ? 1f : 0f, 0.12f, dt);
            if (state.Grounded && !state.Sliding && !state.Dead)
            {
                _bobPhase += dt * Mathf.Max(4f, state.Speed) * 0.9f;
            }

            // Water: the capsule lies forward (dives pitch head-down) and its centre sits on the body line (spec 103 §4).
            bool water = state.Mode == MoveMode.Swim || state.Mode == MoveMode.DeepDive;
            _swim = Damp(_swim, water && !state.Leaping ? 1f : 0f, 0.06f, dt);
            float swimPitch = state.Mode == MoveMode.DeepDive ? 95f : state.Dive == DivePhase.Down ? 120f : state.Dive == DivePhase.Under ? 90f : state.Dive == DivePhase.Up ? 55f : 78f;
            _swimPitch = Damp(_swimPitch, swimPitch, 0.06f, dt);
            Vector3 position = state.Position - new Vector3(0f, _swim * BodyHeight * 0.5f, 0f);
            transform.SetPositionAndRotation(position, state.Facing);

            float stumble = state.SinceStumble < 0.35f ? Mathf.Sin(state.SinceStumble / 0.35f * Mathf.PI) : 0f;
            float pitch = Mathf.Lerp(8f, -70f, _slideBlend) + (stumble * 18f) + (_deathTilt * 80f) + (_tuck * 12f);
            pitch = Mathf.Lerp(pitch, _swimPitch, _swim) + (state.Mode == MoveMode.Swing ? -0.35f * state.SwingDeg : 0f);
            float roll = _lean + (state.SinceDodge < 0.2f ? -state.DodgeDirection * 10f * (1f - (state.SinceDodge / 0.2f)) : 0f);
            _pivot.localRotation = Quaternion.Euler(pitch, 0f, roll);

            float bob = state.Grounded && !state.Sliding ? Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.06f : 0f;
            float squashY = 1f - (_landSquash * 0.25f) - (_tuck * 0.12f);
            float squashXZ = 1f + (_landSquash * 0.15f);
            _pivot.localPosition = new Vector3(0f, bob + (_slideBlend * 0.15f) + (_swim * BodyHeight * 0.5f), 0f);
            _body.localScale = new Vector3(0.5f * squashXZ, BodyHeight * 0.5f * squashY, 0.5f * squashXZ);
            _body.localPosition = new Vector3(0f, BodyHeight * 0.5f * squashY, 0f);
        }

        public override void OnRunEvent(in RunEvent runEvent)
        {
            if (runEvent.Type == RunEventType.Land)
            {
                _landSquash = Mathf.Clamp01(0.4f + (runEvent.Value * 0.3f));
            }
        }

        public override void SetVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }

        public override void ResetPose()
        {
            _lean = 0f;
            _slideBlend = 0f;
            _tuck = 0f;
            _landSquash = 0f;
            _deathTilt = 0f;
            _swim = 0f;
            SetVisible(true);
        }

        private void Build(Material body, Material accent)
        {
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(transform, false);

            _body = CreatePart(PrimitiveType.Capsule, "Body", _pivot, body);
            _body.localScale = new Vector3(0.5f, BodyHeight * 0.5f, 0.5f);
            _body.localPosition = new Vector3(0f, BodyHeight * 0.5f, 0f);

            Transform nose = CreatePart(PrimitiveType.Cube, "Nose", _body, accent);
            nose.localScale = new Vector3(0.35f, 0.12f, 0.5f);
            nose.localPosition = new Vector3(0f, 0.55f, 0.45f);

            Transform pack = CreatePart(PrimitiveType.Cube, "Pack", _body, accent);
            pack.localScale = new Vector3(0.7f, 0.35f, 0.4f);
            pack.localPosition = new Vector3(0f, 0.15f, -0.55f);

            _renderers = GetComponentsInChildren<Renderer>();
        }

        private static Transform CreatePart(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            ViewUtil.DestroySafe(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return part.transform;
        }

        private static float Damp(float current, float target, float halfLife, float dt)
        {
            if (halfLife <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(target, current, Mathf.Pow(0.5f, dt / halfLife));
        }
    }
}
