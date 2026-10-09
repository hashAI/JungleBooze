using JungleBooze.App.Audio;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Audio
{
    /// <summary>Run event → sound mapping (spec 103 §11), music levels, ambience layers and footsteps.</summary>
    public sealed class ExpeditionAudioMapTests
    {
        private static string Map(RunEventType type, byte reason = 0)
        {
            return ExpeditionAudioMap.CueFor(new RunEvent(type, 1, 0, reason, 0f), out _);
        }

        [Test]
        public void MovementAndHits()
        {
            Assert.AreEqual("sfx.jump", Map(RunEventType.Jump));
            Assert.AreEqual("sfx.slide", Map(RunEventType.SlideStart));
            Assert.AreEqual("sfx.dodge", Map(RunEventType.Dodge, 1));
            Assert.AreEqual("sfx.land.hard", Map(RunEventType.Land, (byte)LandingKind.Hard));
            ExpeditionAudioMap.CueFor(new RunEvent(RunEventType.Land, 1, 0, (byte)LandingKind.Light, 0.2f), out float gain);
            Assert.Less(gain, 1f, "light landings are quieter");
            Assert.AreEqual("sfx.crash", Map(RunEventType.Hit, (byte)HitKind.Crash));
            Assert.AreEqual("sfx.hit.thorns", Map(RunEventType.Hit, (byte)HitKind.Thorns));
            Assert.AreEqual("sfx.bump.water", Map(RunEventType.Hit, (byte)HitKind.Bump));
            Assert.AreEqual("sfx.hit.minor", Map(RunEventType.Hit, (byte)HitKind.Trip));
            Assert.AreEqual("sfx.fall", Map(RunEventType.Died, (byte)DeathCause.Fall));
            Assert.AreEqual("sfx.fail.crash", Map(RunEventType.Died, (byte)DeathCause.Crash));
            Assert.IsNull(Map(RunEventType.InputDropped));
            Assert.IsNull(Map(RunEventType.Coin), "coins are handled with the streak pitch");
        }

        [Test]
        public void TraversalAndPickups()
        {
            Assert.AreEqual("sfx.water.enter", Map(RunEventType.WaterEnter, 1));
            Assert.AreEqual("sfx.water.wadeIn", Map(RunEventType.WaterEnter, 0));
            Assert.AreEqual("sfx.dive", Map(RunEventType.Dive));
            Assert.AreEqual("sfx.leap", Map(RunEventType.Leap));
            Assert.AreEqual("sfx.splash", Map(RunEventType.Splash));
            Assert.AreEqual("sfx.deepDive", Map(RunEventType.DeepDiveStart));
            Assert.AreEqual("sfx.vine.grab", Map(RunEventType.VineGrab));
            Assert.AreEqual("sfx.release", Map(RunEventType.VineRelease));
            Assert.AreEqual("sfx.perfect", Map(RunEventType.PerfectRelease));
            Assert.AreEqual("sfx.beam.land", Map(RunEventType.BeamAssist));
            Assert.AreEqual("sfx.curtain.pass", Map(RunEventType.CurtainPass));
            Assert.AreEqual("sfx.crystal", Map(RunEventType.Crystal));
            Assert.AreEqual("sfx.shield.pickup", Map(RunEventType.PowerUp, (byte)PowerUpKind.Shield));
            Assert.AreEqual("sfx.powerup", Map(RunEventType.PowerUp, (byte)PowerUpKind.Magnet));
            Assert.AreEqual("sfx.shield.break", Map(RunEventType.ShieldConsumed));
            Assert.AreEqual("sfx.cleanLine", Map(RunEventType.CleanLine));
            Assert.AreEqual("sfx.revive", Map(RunEventType.Revived));
        }

        [Test]
        public void EveryMappedIdIsListed()
        {
            var all = new System.Collections.Generic.HashSet<string>(ExpeditionAudioMap.All);
            for (int t = 0; t <= (int)RunEventType.CurtainPass; t++)
            {
                for (int r = 0; r < 8; r++)
                {
                    string id = ExpeditionAudioMap.CueFor(new RunEvent((RunEventType)t, 0, 0, (byte)r, 0f), out _);
                    Assert.IsTrue(id == null || all.Contains(id), id);
                }
            }
        }

        [Test]
        public void CreaturesMusicAndAmbience()
        {
            Assert.AreEqual("sfx.sailback.chirp", ExpeditionAudioMap.CreatureCue(CreatureState.Alert, false));
            Assert.AreEqual("sfx.sailback.launch", ExpeditionAudioMap.CreatureCue(CreatureState.Launch, false));
            Assert.AreEqual("sfx.sailback.distant", ExpeditionAudioMap.CreatureCue(CreatureState.Glide, true));
            Assert.IsNull(ExpeditionAudioMap.CreatureCue(CreatureState.Glide, false));

            Assert.AreEqual(0, ExpeditionAudioMap.MusicLevel(ChunkCategory.Recovery, RouteType.Main));
            Assert.AreEqual(1, ExpeditionAudioMap.MusicLevel(ChunkCategory.Straight, RouteType.Main));
            Assert.AreEqual(2, ExpeditionAudioMap.MusicLevel(ChunkCategory.Branch, RouteType.Risky));
            Assert.AreEqual(3, ExpeditionAudioMap.MusicLevel(ChunkCategory.Challenge, RouteType.Safe));

            Assert.AreEqual("amb.canopy.wind", ExpeditionAudioMap.BedFor(EnvironmentSet.Canopy));
            Assert.AreEqual("amb.forest", ExpeditionAudioMap.BedFor(EnvironmentSet.River));
            Assert.AreEqual("amb.river", ExpeditionAudioMap.WaterLayerFor(EnvironmentSet.River, false));
            Assert.AreEqual("amb.falls.roar", ExpeditionAudioMap.WaterLayerFor(EnvironmentSet.Waterfall, false));
            Assert.AreEqual("amb.rapids", ExpeditionAudioMap.WaterLayerFor(EnvironmentSet.Forest, true));
            Assert.IsNull(ExpeditionAudioMap.WaterLayerFor(EnvironmentSet.Forest, false));
            Assert.AreEqual("mus.sting.secret", ExpeditionAudioMap.StingFor(new DiscoveryEntry { Secret = true }));
            Assert.AreEqual("mus.sting.discovery", ExpeditionAudioMap.StingFor(new DiscoveryEntry()));
        }

        [Test]
        public void FootstepsFollowSpeedAndSurface()
        {
            Assert.AreEqual(FootstepCadence.MinRate, FootstepCadence.StepRate(0f), 1e-6f);
            Assert.AreEqual(FootstepCadence.MaxRate, FootstepCadence.StepRate(40f), 1e-6f);
            Assert.Greater(FootstepCadence.StepRate(16f), FootstepCadence.StepRate(10f));

            var c = new FootstepCadence();
            int steps = 0;
            for (int i = 0; i < 600; i++)
            {
                if (c.Advance(1f / 60f, 10f, true))
                {
                    steps++;
                }
            }

            Assert.AreEqual(FootstepCadence.StepRate(10f) * 10f, steps, 1.01f, "10 s at 10 m/s");
            Assert.IsFalse(c.Advance(1f, 10f, false), "no steps in the air");
            Assert.IsFalse(c.Advance(1f, 0.5f, true), "no steps standing");

            Assert.AreEqual(FootstepSurface.Shallow, FootstepCadence.SurfaceFor(EnvironmentSet.Forest, false, true));
            Assert.AreEqual(FootstepSurface.Wood, FootstepCadence.SurfaceFor(EnvironmentSet.Forest, true, false));
            Assert.AreEqual(FootstepSurface.Moss, FootstepCadence.SurfaceFor(EnvironmentSet.River, false, false));
            Assert.AreEqual(FootstepSurface.Dirt, FootstepCadence.SurfaceFor(EnvironmentSet.Forest, false, false));
            Assert.AreEqual("sfx.step.wood", FootstepCadence.CueFor(FootstepSurface.Wood));
        }
    }
}
