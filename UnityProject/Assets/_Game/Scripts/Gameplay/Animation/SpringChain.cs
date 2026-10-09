using System;
using UnityEngine;

namespace JungleBooze.Gameplay.Animation
{
    /// <summary>
    /// Cheap verlet spring chain for secondary motion (Pista's ponytail). Points are simulated in the character's
    /// local space; each point is pulled toward its animated position, keeps its bone length to its parent, gets
    /// gravity and the body's inertia, and never swings more than a maximum angle from the animated direction (keeps
    /// the hair out of the head and back). Fixed 60 Hz sub-steps make it frame-rate independent. Arrays are
    /// allocated once; <see cref="Step"/> allocates nothing.
    /// </summary>
    public sealed class SpringChain
    {
        private const float SubStep = 1f / 60f;
        private const int MaxSubSteps = 4;

        private readonly Vector3[] _pos;
        private readonly Vector3[] _prev;
        private readonly float[] _length;
        private bool _initialized;
        private float _accumulator;

        public SpringChain(int points)
        {
            if (points < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(points));
            }

            _pos = new Vector3[points];
            _prev = new Vector3[points];
            _length = new float[points];
        }

        public int Count => _pos.Length;

        public float Stiffness { get; set; } = 0.16f;

        public float Damping { get; set; } = 0.12f;

        public float Gravity { get; set; } = 6f;

        public float MaxAngleDeg { get; set; } = 55f;

        /// <summary>Simulated position of point <paramref name="i"/> (local space).</summary>
        public Vector3 this[int i] => _pos[i];

        /// <summary>Snap to the animated pose (new run, teleports).</summary>
        public void Reset()
        {
            _initialized = false;
            _accumulator = 0f;
        }

        /// <summary>
        /// Advances the chain.
        /// </summary>
        /// <param name="root">Animated position of the chain's root (the first bone's head).</param>
        /// <param name="targets">Animated positions of each point (bone tails), length <see cref="Count"/>.</param>
        /// <param name="down">Gravity direction in local space (unit).</param>
        /// <param name="inertialAcceleration">Fictitious acceleration from the body's motion (local space, m/s²).</param>
        /// <param name="dt">Frame time (s).</param>
        public void Step(Vector3 root, Vector3[] targets, Vector3 down, Vector3 inertialAcceleration, float dt)
        {
            if (!_initialized)
            {
                Vector3 parent = root;
                for (int i = 0; i < _pos.Length; i++)
                {
                    _pos[i] = targets[i];
                    _prev[i] = targets[i];
                    _length[i] = (targets[i] - parent).magnitude;
                    parent = targets[i];
                }

                _initialized = true;
                return;
            }

            _accumulator = Mathf.Min(_accumulator + Mathf.Max(0f, dt), SubStep * MaxSubSteps);
            float stiffness = Mathf.Clamp01(Stiffness);
            float keep = 1f - Mathf.Clamp01(Damping);
            Vector3 accel = (down * Gravity) + inertialAcceleration;
            float cosLimit = Mathf.Cos(MaxAngleDeg * Mathf.Deg2Rad);
            while (_accumulator >= SubStep)
            {
                _accumulator -= SubStep;
                Vector3 parent = root;
                Vector3 animatedParent = root;
                for (int i = 0; i < _pos.Length; i++)
                {
                    Vector3 p = _pos[i];
                    Vector3 velocity = (p - _prev[i]) * keep;
                    _prev[i] = p;
                    p += velocity + (accel * (SubStep * SubStep));
                    p += (targets[i] - p) * stiffness;

                    Vector3 dir = p - parent;
                    float len = dir.magnitude;
                    dir = len > 1e-6f ? dir / len : (targets[i] - animatedParent).normalized;

                    Vector3 rest = targets[i] - animatedParent;
                    float restLen = rest.magnitude;
                    if (restLen > 1e-6f)
                    {
                        rest /= restLen;
                        float cos = Vector3.Dot(dir, rest);
                        if (cos < cosLimit)
                        {
                            Vector3 axis = Vector3.Cross(rest, dir);
                            if (axis.sqrMagnitude < 1e-10f)
                            {
                                axis = Vector3.Cross(rest, Vector3.up);
                            }

                            dir = Quaternion.AngleAxis(MaxAngleDeg, axis.normalized) * rest;
                        }
                    }

                    p = parent + (dir * _length[i]);
                    _pos[i] = p;
                    parent = p;
                    animatedParent = targets[i];
                }
            }
        }
    }
}
