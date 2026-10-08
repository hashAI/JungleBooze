namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Vertical integration shared by the simulation, the config validator and bots, so they always agree.
    /// Constant acceleration within a tick, integrated exactly (trapezoid on velocity): the arc does not depend on
    /// forward speed and matches the continuous-time apex/airtime of spec 101 §2.4 closely.
    /// </summary>
    public static class VerticalMotion
    {
        /// <summary>Gravity for the current vertical velocity (spec 101 §2.4).</summary>
        public static float Gravity(JumpSlideConfig config, float vy, bool fastFall)
        {
            if (fastFall)
            {
                return config.GDown * config.FastFallGravityFactor;
            }

            float g = vy > 0f ? config.GUp : config.GDown;
            if (vy < config.ApexHangThreshold && vy > -config.ApexHangThreshold)
            {
                g *= config.ApexHangFactor;
            }

            return g;
        }

        /// <summary>Advances height and vertical velocity by one step.</summary>
        public static void Integrate(JumpSlideConfig config, ref float y, ref float vy, bool fastFall, float dt)
        {
            float g = Gravity(config, vy, fastFall);
            float next = vy - (g * dt);
            if (next < -config.MaxFallSpeed)
            {
                next = -config.MaxFallSpeed;
            }

            y += (vy + next) * 0.5f * dt;
            vy = next;
        }
    }
}
