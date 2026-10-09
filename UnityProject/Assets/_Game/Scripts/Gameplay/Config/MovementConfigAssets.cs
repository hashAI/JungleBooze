using System;
using JungleBooze.Gameplay.Movement;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// The set of movement ScriptableObjects a scene uses. <see cref="Build"/> deep-copies them into the plain
    /// <see cref="MovementConfig"/> the simulation reads (ARCHITECTURE §8).
    /// </summary>
    [Serializable]
    public sealed class MovementConfigAssets
    {
        public RunSpeedConfigAsset Speed;
        public LateralMovementConfigAsset Lateral;
        public JumpSlideConfigAsset JumpSlide;
        public HitboxConfigAsset Hitbox;
        public HealthConfigAsset Health;
        public RunFlowConfigAsset Flow;

        /// <summary>Spec 103 traversal tuning; null = code defaults (the feel test has no traversal).</summary>
        public SwimConfigAsset Swim;
        public VineConfigAsset Vine;
        public CanopyConfigAsset Canopy;

        public bool IsComplete => Speed != null && Lateral != null && JumpSlide != null && Hitbox != null && Health != null && Flow != null;

        public MovementConfig Build()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Movement config assets are missing (run JungleBooze > Feel Test > Build Scene).");
            }

            return new MovementConfig(Speed.Values, Lateral.Values, JumpSlide.Values, Hitbox.Values, Health.Values, Flow.Values,
                Swim != null ? Swim.Values : null, Vine != null ? Vine.Values : null, Canopy != null ? Canopy.Values : null);
        }

        /// <summary>Stable 64-bit FNV-1a hash of the serialized values (replay header).</summary>
        public ulong ComputeHash()
        {
            ulong hash = 14695981039346656037UL;
            hash = Mix(hash, JsonUtility.ToJson(Speed.Values));
            hash = Mix(hash, JsonUtility.ToJson(Lateral.Values));
            hash = Mix(hash, JsonUtility.ToJson(JumpSlide.Values));
            hash = Mix(hash, JsonUtility.ToJson(Hitbox.Values));
            hash = Mix(hash, JsonUtility.ToJson(Health.Values));
            hash = Mix(hash, JsonUtility.ToJson(Flow.Values));
            if (Swim != null)
            {
                hash = Mix(hash, JsonUtility.ToJson(Swim.Values));
            }

            if (Vine != null)
            {
                hash = Mix(hash, JsonUtility.ToJson(Vine.Values));
            }

            if (Canopy != null)
            {
                hash = Mix(hash, JsonUtility.ToJson(Canopy.Values));
            }

            return hash;
        }

        public static ulong Mix(ulong hash, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}
