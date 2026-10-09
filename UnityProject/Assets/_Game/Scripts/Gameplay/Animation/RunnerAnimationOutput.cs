namespace JungleBooze.Gameplay.Animation
{
    /// <summary>What the Animator adapter applies this frame (plain data, no allocation).</summary>
    public struct RunnerAnimationOutput
    {
        /// <summary>Base-layer state that should be playing.</summary>
        public RunnerAnimState State;

        /// <summary>True on the frame <see cref="State"/> (re)starts: cross-fade into it.</summary>
        public bool Changed;

        /// <summary>Cross-fade length (s) when <see cref="Changed"/>.</summary>
        public float Fade;

        /// <summary>Start position inside the destination clip (normalized) when <see cref="Changed"/>.</summary>
        public float StartNormalized;

        /// <summary>Locomotion blend: 0 = Run, 1 = Run_Alt.</summary>
        public float LocoBlend;

        /// <summary>Playback rate of the locomotion state.</summary>
        public float RunRate;

        /// <summary>Playback rate of the current one-shot state (jump, slide, stumble, …).</summary>
        public float StateRate;

        /// <summary>Body yaw toward the travel direction, degrees (+ = right).</summary>
        public float YawDeg;

        /// <summary>Bank, degrees (+ = lean to the right).</summary>
        public float RollDeg;

        /// <summary>Procedural forward lean of the spine while running, degrees.</summary>
        public float ForwardLeanDeg;

        /// <summary>0…1: how much the in-air foot anchor applies.</summary>
        public float AnchorWeight;

        /// <summary>Procedural whole-body pitch about the hips, degrees (+ = forward/head down): swim, dive, swing.</summary>
        public float BodyPitchDeg;

        /// <summary>0…1: how much the body sits in the water (swim posture offsets).</summary>
        public float SwimWeight;
    }
}
