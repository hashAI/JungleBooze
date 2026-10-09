using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Edge-brush feedback (spec 101 §2.3 step 4): when Pista pushes into the path edge, a few leaves burst out of
    /// the hedge at her side. One pooled <see cref="ParticleSystem"/> (64 particles), emitted with
    /// <see cref="ParticleSystem.EmitParams"/>: no allocation per burst. No damage, no haptic.
    /// </summary>
    public sealed class EdgeBrushView : MonoBehaviour
    {
        private const int MaxParticles = 64;

        private static readonly Color LeafA = new Color(0.30f, 0.52f, 0.20f, 1f);
        private static readonly Color LeafB = new Color(0.47f, 0.62f, 0.25f, 1f);

        private ParticleSystem _particles;
        private uint _seed = 1u;

        public int Bursts { get; private set; }

        public ParticleSystem Particles => _particles;

        public void Build(Material leaf)
        {
            _particles = gameObject.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.gravityModifier = 0.45f;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;
            ParticleSystem.RotationOverLifetimeModule spin = _particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = leaf;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _particles.Play();
        }

        /// <summary>Leaves out of the hedge at the path edge beside Pista.</summary>
        /// <param name="edge">World position on the path edge at Pista's height 0.</param>
        /// <param name="side">−1 left, +1 right.</param>
        /// <param name="forwardSpeed">Pista's speed: leaves are knocked slightly forward with her.</param>
        public void Burst(Vector3 edge, int side, float forwardSpeed, int count)
        {
            if (_particles == null)
            {
                return;
            }

            Bursts++;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = edge + new Vector3(side * Next(0f, 0.15f), Next(0.15f, 0.5f), Next(-0.2f, 0.3f));
                p.velocity = new Vector3(side * Next(0.6f, 1.8f), Next(1.0f, 2.4f), forwardSpeed * Next(0.15f, 0.35f));
                p.startSize = Next(0.06f, 0.13f);
                p.rotation = Next(0f, 360f);
                p.startColor = Color.Lerp(LeafA, LeafB, Next(0f, 1f));
                p.startLifetime = Next(0.45f, 0.8f);
                _particles.Emit(p, 1);
            }
        }

        /// <summary>Tools outside play mode (video capture): particle systems don't advance on their own there.</summary>
        public void ManualTick(float dt)
        {
            if (_particles != null && dt > 0f)
            {
                _particles.Simulate(dt, true, false, false);
            }
        }

        public void Clear()
        {
            if (_particles != null)
            {
                _particles.Clear();
            }
        }

        // Presentation-only noise (xorshift), never the simulation's random streams.
        private float Next(float min, float max)
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return min + ((max - min) * ((_seed & 0xFFFFFF) / 16777216f));
        }
    }
}
