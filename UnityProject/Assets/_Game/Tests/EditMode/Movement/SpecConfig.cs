using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 values (the defaults of the plain config classes).</summary>
    internal static class SpecConfig
    {
        public static MovementConfig Create()
        {
            return new MovementConfig(new RunSpeedConfig(), new LateralMovementConfig(), new JumpSlideConfig(), new HitboxConfig(), new HealthConfig(), new RunFlowConfig());
        }
    }
}
