using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.App.Audio
{
    /// <summary>
    /// Run event → cue id (spec 103 §11 hook ids). Pure functions, so the mapping is testable without audio. Events
    /// that need context (coins, discoveries, creatures, music, ambience) are handled in <see cref="ExpeditionAudio"/>.
    /// </summary>
    public static class ExpeditionAudioMap
    {
        public const string Jump = "sfx.jump";
        public const string Land = "sfx.land";
        public const string LandHard = "sfx.land.hard";
        public const string Slide = "sfx.slide";
        public const string Dodge = "sfx.dodge";
        public const string EdgeBrush = "sfx.edgeBrush";
        public const string HitMinor = "sfx.hit.minor";
        public const string HitThorns = "sfx.hit.thorns";
        public const string Crash = "sfx.crash";
        public const string Fall = "sfx.fall";
        public const string FailCrash = "sfx.fail.crash";
        public const string GapWhoosh = "sfx.gap.whoosh";
        public const string WaterEnter = "sfx.water.enter";
        public const string WadeIn = "sfx.water.wadeIn";
        public const string WaterExit = "sfx.water.exit";
        public const string SwimStroke = "sfx.swim.stroke";
        public const string Dive = "sfx.dive";
        public const string Surface = "sfx.surface";
        public const string Leap = "sfx.leap";
        public const string Splash = "sfx.splash";
        public const string BumpWater = "sfx.bump.water";
        public const string DeepDive = "sfx.deepDive";
        public const string CurtainPass = "sfx.curtain.pass";
        public const string VineGrab = "sfx.vine.grab";
        public const string VineCreak = "sfx.vine.creak";
        public const string VineWindow = "sfx.vine.window";
        public const string Release = "sfx.release";
        public const string Perfect = "sfx.perfect";
        public const string BranchCreak = "sfx.branch.creak";
        public const string BeamLand = "sfx.beam.land";
        public const string Coin = "sfx.coin";
        public const string Crystal = "sfx.crystal";
        public const string CleanLine = "sfx.cleanLine";
        public const string ShieldPickup = "sfx.shield.pickup";
        public const string ShieldBreak = "sfx.shield.break";
        public const string ShieldExpire = "sfx.shield.expire";
        public const string PowerUp = "sfx.powerup";
        public const string Revive = "sfx.revive";
        public const string Discovery = "sfx.discovery";
        public const string SailbackChirp = "sfx.sailback.chirp";
        public const string SailbackFlare = "sfx.sailback.sailFlare";
        public const string SailbackLaunch = "sfx.sailback.launch";
        public const string SailbackDistant = "sfx.sailback.distant";
        public const string StepDirt = "sfx.step.dirt";
        public const string StepMoss = "sfx.step.moss";
        public const string StepWood = "sfx.step.wood";
        public const string StepShallow = "sfx.step.shallow";
        public const string AmbForest = "amb.forest";
        public const string AmbRiver = "amb.river";
        public const string AmbRapids = "amb.rapids";
        public const string AmbFalls = "amb.falls.roar";
        public const string AmbCanopy = "amb.canopy.wind";
        public const string AmbUnderwater = "amb.underwater";
        public const string AmbGrotto = "amb.grotto";
        public const string AmbDripEcho = "amb.secret.dripEcho";
        public const string CueRisky = "amb.cue.risky";
        public const string StingDiscovery = "mus.sting.discovery";
        public const string StingSecret = "mus.sting.secret";
        public const string StingUnlock = "mus.sting.unlock";
        public const string StingGameOver = "mus.sting.gameover";
        public const string RevealVista = "mus.reveal.vista";
        public const string UiTap = "ui.tap";
        public const string UiBack = "ui.back";
        public const string UiConfirm = "ui.confirm";
        public const string UiToggle = "ui.toggle";
        public const string UiPause = "ui.pause";
        public const string UiCountdown = "ui.countdown";
        public const string UiCountUp = "ui.countUp";
        public const string UiNewRecord = "ui.newRecord";
        public const string UiObjective = "ui.objective";
        public const string UiLearn = "ui.learn";
        public const string UiToast = "ui.toast";
        public const string UiDenied = "ui.denied";

        /// <summary>Every id the expedition uses (the catalog must contain all of them).</summary>
        public static readonly string[] All =
        {
            Jump, Land, LandHard, Slide, Dodge, EdgeBrush, HitMinor, HitThorns, Crash, Fall, FailCrash, GapWhoosh,
            WaterEnter, WadeIn, WaterExit, SwimStroke, Dive, Surface, Leap, Splash, BumpWater, DeepDive, CurtainPass,
            VineGrab, VineCreak, VineWindow, Release, Perfect, BranchCreak, BeamLand, Coin, Crystal, CleanLine,
            ShieldPickup, ShieldBreak, ShieldExpire, PowerUp, Revive, Discovery, SailbackChirp, SailbackFlare,
            SailbackLaunch, SailbackDistant, StepDirt, StepMoss, StepWood, StepShallow, AmbForest, AmbRiver, AmbRapids,
            AmbFalls, AmbCanopy, AmbUnderwater, AmbGrotto, AmbDripEcho, CueRisky, StingDiscovery, StingSecret,
            StingUnlock, StingGameOver, RevealVista, UiTap, UiBack, UiConfirm, UiToggle, UiPause, UiCountdown,
            UiCountUp, UiNewRecord, UiObjective, UiLearn, UiToast, UiDenied,
        };

        /// <summary>
        /// The one-shot for a run event that maps directly, or null. <paramref name="gain"/> is a volume scale
        /// (light landings play quieter).
        /// </summary>
        public static string CueFor(in RunEvent e, out float gain)
        {
            gain = 1f;
            switch (e.Type)
            {
                case RunEventType.Jump:
                    return Jump;
                case RunEventType.Land:
                    var kind = (LandingKind)e.Reason;
                    if (kind == LandingKind.Hard)
                    {
                        return LandHard;
                    }

                    gain = kind == LandingKind.Light ? 0.6f : 1f;
                    return Land;
                case RunEventType.SlideStart:
                    return Slide;
                case RunEventType.Dodge:
                    return Dodge;
                case RunEventType.Hit:
                    return HitCue((HitKind)e.Reason);
                case RunEventType.ShieldConsumed:
                    return ShieldBreak;
                case RunEventType.Died:
                    return (DeathCause)e.Reason == DeathCause.Fall ? Fall : FailCrash;
                case RunEventType.Revived:
                    return Revive;
                case RunEventType.ShieldExpired:
                    return ShieldExpire;
                case RunEventType.Crystal:
                    return Crystal;
                case RunEventType.PowerUp:
                    return (PowerUpKind)e.Reason == PowerUpKind.Shield ? ShieldPickup : PowerUp;
                case RunEventType.CleanLine:
                case RunEventType.PerfectSpan:
                    return CleanLine;
                case RunEventType.WaterEnter:
                    return e.Reason == 1 ? WaterEnter : WadeIn;
                case RunEventType.WaterExit:
                    return WaterExit;
                case RunEventType.Dive:
                    return Dive;
                case RunEventType.Surface:
                case RunEventType.DeepDiveEnd:
                    return Surface;
                case RunEventType.Leap:
                    return Leap;
                case RunEventType.Splash:
                    return Splash;
                case RunEventType.DeepDiveStart:
                    return DeepDive;
                case RunEventType.VineGrab:
                    return VineGrab;
                case RunEventType.VineRelease:
                    return Release;
                case RunEventType.PerfectRelease:
                    return Perfect;
                case RunEventType.BeamAssist:
                    return BeamLand;
                case RunEventType.CurtainPass:
                    return CurtainPass;
                default:
                    return null;
            }
        }

        public static string HitCue(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Crash:
                    return Crash;
                case HitKind.Thorns:
                    return HitThorns;
                case HitKind.Bump:
                    return BumpWater;
                case HitKind.None:
                    return null;
                default:
                    return HitMinor;
            }
        }

        /// <summary>Sailback state change → cue (Alert chirps and flares the sail, Launch, distant calls for ambient gliders).</summary>
        public static string CreatureCue(CreatureState state, bool ambient)
        {
            switch (state)
            {
                case CreatureState.Alert:
                    return SailbackChirp;
                case CreatureState.Launch:
                    return SailbackLaunch;
                case CreatureState.Glide:
                    return ambient ? SailbackDistant : null;
                default:
                    return null;
            }
        }

        /// <summary>Music intensity for a chunk (GDD §19): Recovery 0, Challenge 3 (danger), risky route 2, else 1.</summary>
        public static int MusicLevel(ChunkCategory category, RouteType route)
        {
            if (category == ChunkCategory.Recovery)
            {
                return 0;
            }

            if (category == ChunkCategory.Challenge)
            {
                return 3;
            }

            return route == RouteType.Risky ? 2 : 1;
        }

        /// <summary>Ambience bed for an environment set.</summary>
        public static string BedFor(EnvironmentSet set)
        {
            return set == EnvironmentSet.Canopy ? AmbCanopy : AmbForest;
        }

        /// <summary>Water layer: rapids while swimming, else by environment (river, falls), or none.</summary>
        public static string WaterLayerFor(EnvironmentSet set, bool swimming)
        {
            if (swimming)
            {
                return AmbRapids;
            }

            switch (set)
            {
                case EnvironmentSet.River:
                    return AmbRiver;
                case EnvironmentSet.Waterfall:
                    return AmbFalls;
                default:
                    return null;
            }
        }

        /// <summary>Discovery sting: secrets get the secret sting, everything else the discovery sting.</summary>
        public static string StingFor(DiscoveryEntry entry)
        {
            return entry != null && entry.Secret ? StingSecret : StingDiscovery;
        }
    }
}
