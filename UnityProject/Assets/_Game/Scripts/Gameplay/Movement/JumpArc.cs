namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Apex and airtime of a jump on flat ground with the simulation's own integrator.</summary>
    public readonly struct JumpArc
    {
        private const int MaxTicks = 600;

        public JumpArc(float apex, float airtime, int airTicks)
        {
            Apex = apex;
            Airtime = airtime;
            AirTicks = airTicks;
        }

        public float Apex { get; }

        public float Airtime { get; }

        public int AirTicks { get; }

        public static JumpArc Measure(JumpSlideConfig config, float dt)
        {
            float y = 0f;
            float vy = config.JumpVelocity;
            float apex = 0f;
            int ticks = 0;
            while (ticks < MaxTicks)
            {
                VerticalMotion.Integrate(config, ref y, ref vy, false, dt);
                ticks++;
                if (y > apex)
                {
                    apex = y;
                }

                if (y <= 0f && vy < 0f)
                {
                    break;
                }
            }

            return new JumpArc(apex, ticks * dt, ticks);
        }
    }
}
