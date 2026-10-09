using System;
using System.Collections.Generic;
using JungleBooze.Core.Feedback;
using JungleBooze.Core.Settings;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.Services.Audio;
using JungleBooze.UI.Common;
using UnityEngine;

namespace JungleBooze.App.Audio
{
    /// <summary>
    /// Expedition sound (presentation only; never touches the simulation): run events → SFX and paired haptics,
    /// footsteps on the run cycle, coin streak pitch, sailback calls, adaptive music by chunk and route (recovery /
    /// explore / risky / danger, discovery and secret stings, soft stop on death, results loop), and ambience by
    /// environment (forest or canopy bed, river / falls / rapids layer, underwater, grotto and the secret's drip echo).
    /// Created by <c>ExpeditionRoot</c>; fed from its event drain and frame tick.
    /// </summary>
    public sealed class ExpeditionAudio
    {
        public const string CatalogResource = "AudioCatalog";

        private const float BedFade = 2f;
        private const float OverlayFade = 0.6f;
        private const float UnderwaterCutoff = 900f;
        private const double VistaStingGuard = 2.0;

        private readonly AudioService _audio;
        private readonly ExpeditionSession _session;
        private readonly ExpeditionContent _content;
        private readonly Dictionary<string, int> _cues = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly FootstepCadence _steps = new FootstepCadence();
        private readonly FootstepCadence _strokes = new FootstepCadence();
        private readonly CoinStreak _coins = new CoinStreak();
        private readonly int[] _stepCues = new int[4];

        private EnvironmentSet _env = EnvironmentSet.Forest;
        private ChunkCategory _category = ChunkCategory.Straight;
        private RouteType _route = RouteType.Main;
        private bool _secretChunk;
        private bool _inGrotto;
        private bool _wasSwimming;
        private bool _wasSubmerged;
        private bool _resultsPlayed;
        private bool _paused;
        private bool _muffled;
        private double _vistaTime = double.NegativeInfinity;
        private int _lastSwingTick = -1;
        private string _bed;
        private string _water;
        private string _overlay;

        private readonly Func<double> _clock;

        private ExpeditionAudio(AudioService audio, ExpeditionSession session, ExpeditionContent content, Func<double> clock)
        {
            _audio = audio;
            _clock = clock;
            _session = session;
            _content = content;
            foreach (string id in ExpeditionAudioMap.All)
            {
                _cues[id] = audio.Cue(id);
            }

            _stepCues[(int)FootstepSurface.Dirt] = Cue(ExpeditionAudioMap.StepDirt);
            _stepCues[(int)FootstepSurface.Moss] = Cue(ExpeditionAudioMap.StepMoss);
            _stepCues[(int)FootstepSurface.Wood] = Cue(ExpeditionAudioMap.StepWood);
            _stepCues[(int)FootstepSurface.Shallow] = Cue(ExpeditionAudioMap.StepShallow);
        }

        public AudioService Service => _audio;

        /// <summary>The shared haptics gate (GDD §19 100 ms rule); the feedback router should use it too.</summary>
        public HapticGate Haptics => _audio.Haptics;

        /// <summary>
        /// Builds the audio under <paramref name="parent"/>. <paramref name="catalog"/> null = Resources/AudioCatalog;
        /// returns null when there is no catalog (the game runs silent).
        /// </summary>
        public static ExpeditionAudio Create(Transform parent, AudioCatalog catalog, ISettingsStore settings, IHaptics haptics, ExpeditionSession session, ExpeditionContent content, Func<double> clock = null)
        {
            clock = clock ?? (() => Time.realtimeSinceStartupAsDouble);
            catalog = catalog != null ? catalog : Resources.Load<AudioCatalog>(CatalogResource);
            if (catalog == null)
            {
                Debug.LogWarning("[JungleBooze] No AudioCatalog; running without audio.");
                return null;
            }

            AudioService service = AudioService.Create(parent, catalog, settings, haptics, clock);
            return new ExpeditionAudio(service, session, content, clock);
        }

        /// <summary>UI taps play <c>ui.tap</c> in addition to whatever is already hooked (call after the HUD is built).</summary>
        public void HookUi(UiPreferences preferences)
        {
            Action previous = UiFeedback.Tap;
            int tap = Cue(ExpeditionAudioMap.UiTap);
            UiFeedback.Tap = () =>
            {
                previous?.Invoke();
                _audio.Play(tap, 0f, 1f, false);
            };

            if (preferences != null)
            {
                preferences.Changed += _audio.ApplyVolumes;
            }
        }

        public int Cue(string id)
        {
            return id != null && _cues.TryGetValue(id, out int i) ? i : -1;
        }

        /// <summary>A new run: music (the intro only on the session's first run), ambience, counters.</summary>
        public void BeginRun(bool withIntro)
        {
            _steps.Reset();
            _strokes.Reset();
            _coins.Reset();
            _env = EnvironmentSet.Forest;
            _category = ChunkCategory.Straight;
            _route = RouteType.Main;
            _secretChunk = false;
            _inGrotto = false;
            _wasSwimming = false;
            _wasSubmerged = false;
            _resultsPlayed = false;
            _muffled = false;
            _vistaTime = double.NegativeInfinity;
            _lastSwingTick = -1;
            _audio.Sfx.StopAll();
            _audio.MusicStart(withIntro);
            _audio.MusicUnderwater(false, UnderwaterCutoff);
            _bed = _water = _overlay = null;
            _audio.Overlay.Set(null, 0f, OverlayFade);
            UpdateAmbience(false, false, true);
        }

        /// <summary>One simulation event (from the root's event drain, same frame as the tick that made it).</summary>
        public void OnRunEvent(in RunEvent e)
        {
            double now = _clock();
            string id = ExpeditionAudioMap.CueFor(e, out float gain);
            if (id != null)
            {
                _audio.Play(Cue(id), 0f, gain);
            }

            switch (e.Type)
            {
                case RunEventType.Jump:
                    if (_session.Path.IsCanopy(_session.Simulation.State.S))
                    {
                        _audio.Play(Cue(ExpeditionAudioMap.GapWhoosh), 0f, 0.8f);
                    }

                    break;
                case RunEventType.Land:
                case RunEventType.BeamAssist:
                    _steps.OnLanded();
                    break;
                case RunEventType.EdgeBrush:
                    _audio.Play(Cue(ExpeditionAudioMap.EdgeBrush), 0f, 0.7f);
                    break;
                case RunEventType.Coin:
                    _audio.Play(Cue(ExpeditionAudioMap.Coin), _coins.OnCoin(now));
                    break;
                case RunEventType.VineGrab:
                    _audio.Play(Cue(ExpeditionAudioMap.VineCreak), 0f, 0.8f);
                    _lastSwingTick = 0;
                    break;
                case RunEventType.Died:
                    _audio.MusicStopSoft();
                    _audio.PlaySting(Cue(ExpeditionAudioMap.StingGameOver));
                    break;
                case RunEventType.Revived:
                    _audio.MusicStart(false);
                    _audio.MusicLevel(ExpeditionAudioMap.MusicLevel(_category, _route));
                    break;
                case RunEventType.Discovery:
                    OnDiscovery(e);
                    break;
                case RunEventType.Vista:
                    _vistaTime = now;
                    _audio.PlaySting(Cue(ExpeditionAudioMap.RevealVista));
                    break;
                case RunEventType.CreatureState:
                    OnCreature(e);
                    break;
                case RunEventType.ChunkEntered:
                    OnChunkEntered(e.Id);
                    break;
                case RunEventType.RouteChosen:
                    OnRouteChosen((RouteType)e.Reason);
                    break;
            }
        }

        /// <summary>Per rendered frame (unscaled seconds).</summary>
        public void Tick(float frameSeconds, bool paused, bool hapticsEnabled)
        {
            _audio.Haptics.Enabled = hapticsEnabled;
            if (paused != _paused)
            {
                _paused = paused;
                _audio.SetPaused(paused);
                _audio.Play(Cue(paused ? ExpeditionAudioMap.UiPause : ExpeditionAudioMap.UiCountdown), 0f, 1f, false);
            }

            double now = _clock();
            if (_coins.Update(now))
            {
                _audio.Haptics.Play(CueHaptic.Light);
            }

            RunnerSimulation sim = _session.Simulation;
            ref readonly RunnerState s = ref sim.State;
            if (!paused && _session.Phase == RunPhase.Running && !s.Dead)
            {
                bool wading = s.Mode == MoveMode.Run && s.Grounded && sim.Traversal != null && sim.Traversal.TryGetWater(s.S, s.X, out float surface) && surface > s.Y;
                bool canopy = _session.Path.IsCanopy(s.S);
                bool running = s.Mode == MoveMode.Run && s.Grounded && !s.Sliding;
                if (_steps.Advance(frameSeconds, s.Speed, running))
                {
                    _audio.Play(_stepCues[(int)FootstepCadence.SurfaceFor(_env, canopy, wading)]);
                }

                if (s.Mode == MoveMode.Swim && !s.Submerged && _strokes.Advance(frameSeconds * 0.45f, s.Speed, true))
                {
                    _audio.Play(Cue(ExpeditionAudioMap.SwimStroke), 0f, 0.8f);
                }

                TickVine(sim);
            }

            bool swimming = s.Mode == MoveMode.Swim || s.Mode == MoveMode.DeepDive;
            bool submerged = s.Submerged;
            if (swimming != _wasSwimming || submerged != _wasSubmerged)
            {
                _wasSwimming = swimming;
                _wasSubmerged = submerged;
                UpdateAmbience(swimming, submerged, false);
            }

            if (_session.Phase == RunPhase.Results && !_resultsPlayed)
            {
                _resultsPlayed = true;
                _audio.MusicResults();
            }

            _audio.Tick(frameSeconds);
        }

        /// <summary>Results screen: a new record jingle.</summary>
        public void OnResults(bool newRecord)
        {
            if (newRecord)
            {
                _audio.Play(Cue(ExpeditionAudioMap.UiNewRecord));
            }
        }

        /// <summary>An ability was learned (spec 103 §9.4): learn sound + unlock sting.</summary>
        public void OnLearn()
        {
            _audio.Play(Cue(ExpeditionAudioMap.UiLearn));
            _audio.PlaySting(Cue(ExpeditionAudioMap.StingUnlock));
        }

        /// <summary>UI hooks for the HUD (count-up ticks, objective card, denied purchase, toast).</summary>
        public void PlayUi(string id)
        {
            _audio.Play(Cue(id));
        }

        public void Stop()
        {
            _audio.StopAll();
        }

        private void TickVine(RunnerSimulation sim)
        {
            int k = sim.SwingTick;
            if (k < 0)
            {
                _lastSwingTick = -1;
                return;
            }

            int perfect = sim.PerfectStartTick;
            if (_lastSwingTick >= 0 && _lastSwingTick < perfect && k >= perfect)
            {
                _audio.Play(Cue(ExpeditionAudioMap.VineWindow), 0f, 1f, false);
            }

            _lastSwingTick = k;
        }

        private void OnDiscovery(in RunEvent e)
        {
            if (e.Reason != 1)
            {
                return;
            }

            DiscoveryEntry entry = _content != null && e.Id >= 0 && e.Id < _content.Discoveries.Count ? _content.Discoveries[e.Id] : null;
            _audio.Play(Cue(ExpeditionAudioMap.Discovery), 0f, 1f, false);

            // A find at a vista (D-01) keeps the vista reveal as its music instead of cutting it with a sting.
            if (_clock() - _vistaTime > VistaStingGuard)
            {
                _audio.PlaySting(Cue(ExpeditionAudioMap.StingFor(entry)));
            }

            // Spec 103 §11: success on the secret (D-03) and the Deep Breath find (D-04, made under water), light on the others.
            bool big = (entry != null && entry.Secret) || _session.Simulation.State.Mode == MoveMode.DeepDive || _wasSubmerged;
            _audio.Haptics.Play(big ? CueHaptic.Success : CueHaptic.Light);
        }

        private void OnCreature(in RunEvent e)
        {
            if (e.Id < 0 || e.Id >= SailbackSystem.Capacity)
            {
                return;
            }

            ref readonly Sailback animal = ref _session.Creatures.Get(e.Id);
            var state = (CreatureState)e.Reason;
            string id = ExpeditionAudioMap.CreatureCue(state, animal.Ambient);
            if (id == null)
            {
                return;
            }

            // Flock members call one after another: the cue's cooldown and voice limit keep it to a few calls.
            float gain = animal.Ambient ? 0.8f : 1f;
            _audio.Play(Cue(id), animal.Member * 0.7f, gain);
            if (state == CreatureState.Alert)
            {
                _audio.Play(Cue(ExpeditionAudioMap.SailbackFlare), 0f, 0.9f);
            }
        }

        private void OnChunkEntered(int serial)
        {
            WorldPath path = _session.Path;
            if (!path.IsLive(serial))
            {
                return;
            }

            ref readonly PlacedChunk placed = ref path.Chunk(serial);
            ChunkDefinition def = placed.Chunk.Definition;
            _env = def.Environment;
            _category = def.Category;
            _route = RouteType.Main;
            _secretChunk = HasSecret(placed.Chunk);
            _inGrotto = false;
            if (def.Category == ChunkCategory.Branch)
            {
                _audio.Play(Cue(ExpeditionAudioMap.CueRisky), 0f, 0.8f);
            }

            _audio.MusicLevel(ExpeditionAudioMap.MusicLevel(_category, _route));
            UpdateAmbience(_wasSwimming, _wasSubmerged, false);
        }

        private void OnRouteChosen(RouteType route)
        {
            _route = route;
            _inGrotto = route == RouteType.Secret;
            _secretChunk = false;
            _audio.MusicLevel(ExpeditionAudioMap.MusicLevel(_category, _route));
            UpdateAmbience(_wasSwimming, _wasSubmerged, false);
        }

        private void UpdateAmbience(bool swimming, bool submerged, bool snap)
        {
            float fade = snap ? 0.5f : BedFade;
            string bed = ExpeditionAudioMap.BedFor(_env);
            if (bed != _bed)
            {
                _bed = bed;
                _audio.SetAmbience(_audio.Bed, Cue(bed), fade);
            }

            string water = ExpeditionAudioMap.WaterLayerFor(_env, swimming);
            if (water != _water)
            {
                _water = water;
                _audio.SetAmbience(_audio.Water, Cue(water), fade);
            }

            string overlay = submerged ? ExpeditionAudioMap.AmbUnderwater : _inGrotto ? ExpeditionAudioMap.AmbGrotto : _secretChunk ? ExpeditionAudioMap.AmbDripEcho : null;
            if (overlay != _overlay)
            {
                _overlay = overlay;
                _audio.SetAmbience(_audio.Overlay, Cue(overlay), OverlayFade);
            }

            bool muffled = submerged || _inGrotto;
            _audio.Bed.LowPass.enabled = submerged;
            _audio.Water.LowPass.enabled = submerged;
            _audio.Bed.LowPass.cutoffFrequency = UnderwaterCutoff;
            _audio.Water.LowPass.cutoffFrequency = UnderwaterCutoff;
            if (muffled != _muffled)
            {
                _muffled = muffled;
                _audio.MusicUnderwater(muffled, submerged ? UnderwaterCutoff : 2500f);
            }
        }

        private static bool HasSecret(ChunkRuntime chunk)
        {
            for (int i = 0; i < chunk.RouteCount; i++)
            {
                if (chunk.GetRoute(i).Type == RouteType.Secret)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
