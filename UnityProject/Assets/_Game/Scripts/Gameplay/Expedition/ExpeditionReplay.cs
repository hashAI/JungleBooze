using System;
using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Replay format 3 for expedition runs (ADR 0006 amendment): converts the run setup to and from the recording
    /// header, and plays a recording back through a session (inputs per session tick, revive markers before the
    /// tick's input). Allocates at run start only; <see cref="Play"/> does not allocate per tick.
    /// </summary>
    public static class ExpeditionReplay
    {
        /// <summary>The setup values a replay needs (the discovered ids are captured now, from the profile lookup).</summary>
        public static ReplaySetup ToReplaySetup(in ExpeditionRunSetup setup, ExpeditionContent content)
        {
            var discovered = new List<string>();
            if (setup.Discovered != null && content != null)
            {
                for (int i = 0; i < content.Discoveries.Count; i++)
                {
                    string id = content.Discoveries[i].Id;
                    if (setup.Discovered(id))
                    {
                        discovered.Add(id);
                    }
                }
            }

            return new ReplaySetup
            {
                FirstExpedition = setup.FirstExpedition,
                Owned = (int)setup.Owned,
                PendingShowcase = (int)setup.PendingShowcase,
                Skill = setup.Skill,
                ForcedSpeed = setup.ForcedSpeed,
                Discovered = discovered.ToArray(),
            };
        }

        /// <summary>The run setup stored in a recording. Throws if the recording has none.</summary>
        public static ExpeditionRunSetup ToRunSetup(InputRecording recording)
        {
            if (recording == null)
            {
                throw new ArgumentNullException(nameof(recording));
            }

            ReplaySetup r = recording.Setup ?? throw new InvalidOperationException("The replay has no run setup (format 3 expedition replays always do).");
            string[] ids = r.Discovered ?? Array.Empty<string>();
            return new ExpeditionRunSetup
            {
                FirstExpedition = r.FirstExpedition,
                Seed = recording.Seed,
                Owned = (AbilityFlags)r.Owned,
                PendingShowcase = (AbilityFlags)r.PendingShowcase,
                Skill = r.Skill,
                ForcedSpeed = r.ForcedSpeed,
                Discovered = id => Array.IndexOf(ids, id) >= 0,
            };
        }

        /// <summary>
        /// Begins the recorded run on <paramref name="session"/> and plays it until the results (or
        /// <paramref name="maxTicks"/>). Revive markers are applied with the cost the run paid
        /// (<see cref="ReviveRules.Cost"/>). Returns the number of session ticks stepped.
        /// </summary>
        public static long Play(ExpeditionSession session, InputRecording recording, long maxTicks = 60L * 60 * 30)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            InputRecording keep = session.Recording;
            session.Recording = null;
            session.BeginRun(ToRunSetup(recording));
            var input = new ReplayInputProvider(recording);
            int marker = 0;
            long steps = 0;
            ResultsConfig results = session.Content.Results;
            while (steps < maxTicks && session.Phase != RunPhase.Results)
            {
                long tick = session.Run.SessionTick;
                while (marker < recording.MarkerCount && recording.GetMarker(marker).Tick <= tick)
                {
                    ReplayMarker m = recording.GetMarker(marker++);
                    if (m.Kind == ReplayMarkerKind.Revive && m.Tick == tick)
                    {
                        session.Revive(ReviveRules.Cost(results, session.Stats.Revives));
                    }
                }

                session.Step(input.ReadInput(tick));
                session.Events.Clear();
                steps++;
            }

            session.Recording = keep;
            return steps;
        }
    }
}
