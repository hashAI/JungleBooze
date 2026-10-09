using System;
using System.Globalization;
using System.Text;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Analytics
{
    /// <summary>
    /// Turns run events into GDD §22 analytics events (AC-103-50): <c>traversal_result</c> (swim_dive, swim_leap,
    /// vine_good, vine_perfect, beam_gap), <c>creature_found</c>, <c>secret_found</c>, <c>power_up</c>,
    /// <c>ability_unlocked</c>, plus run_started / run_ended / death / revive / route_selected. Recording is a
    /// fixed ring (no allocation per tick); <see cref="Flush"/> serializes to JSON lines for an
    /// <see cref="IAnalyticsSink"/> at the results screen (allocates there). On-device only: there is no upload.
    /// </summary>
    public sealed class AnalyticsRecorder
    {
        private static readonly string[] TraversalNames = { "jump", "slide", "dodge", "swim_dive", "swim_leap", "vine_good", "vine_perfect", "beam_gap" };
        private static readonly string[] CauseNames = { "none", "health", "crash", "fall" };
        private static readonly string[] RouteNames = { "main", "safe", "risky", "secret" };

        private readonly ExpeditionContent _content;
        private readonly AnalyticsEvent[] _events;
        private int _count;
        private int _runId;

        public AnalyticsRecorder(ExpeditionContent content, int capacity = 512)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _events = new AnalyticsEvent[Math.Max(16, capacity)];
        }

        /// <summary>Build string, e.g. Application.version (set by the composition root).</summary>
        public string Build { get; set; } = "dev";

        /// <summary>Random per launch, never tied to the person (GDD §22).</summary>
        public string SessionId { get; set; } = "local";

        public int Count => _count;

        /// <summary>Events dropped because the ring was full before a flush.</summary>
        public int Dropped { get; private set; }

        public AnalyticsEvent this[int index] => _events[index];

        public int CountOf(AnalyticsEventType type)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                n += _events[i].Type == type ? 1 : 0;
            }

            return n;
        }

        public void BeginRun(int runIndex, ulong seed, int abilities, float skill, bool landscape)
        {
            _runId = runIndex;
            Add(new AnalyticsEvent { Type = AnalyticsEventType.RunStarted, Int = runIndex, Text = landscape ? "landscape" : "portrait", Value = skill, Text2 = null, Flag = abilities != 0 });
            _lastSeed = seed;
            _lastAbilities = abilities;
        }

        private ulong _lastSeed;
        private int _lastAbilities;

        /// <summary>Feed every run event of the session (after each step).</summary>
        public void OnRunEvent(in RunEvent e, ExpeditionSession session)
        {
            long ms = e.Tick * 1000L / 60L;
            switch (e.Type)
            {
                case RunEventType.TraversalResult:
                    if (e.Reason >= (byte)TraversalKind.SwimDive)
                    {
                        Add(new AnalyticsEvent { Type = AnalyticsEventType.TraversalResult, TimeMs = ms, Text = TraversalNames[Math.Min(e.Reason, (byte)(TraversalNames.Length - 1))], Flag = e.Value > 0.5f, Int = e.Id });
                    }

                    break;

                case RunEventType.Discovery:
                {
                    if (e.Id < 0 || e.Id >= _content.Discoveries.Count)
                    {
                        break;
                    }

                    DiscoveryEntry entry = _content.Discoveries[e.Id];
                    AnalyticsEventType type = entry.Category == DiscoveryCategory.Creature ? AnalyticsEventType.CreatureFound : entry.Secret ? AnalyticsEventType.SecretFound : AnalyticsEventType.RunStarted;
                    if (type != AnalyticsEventType.RunStarted)
                    {
                        Add(new AnalyticsEvent { Type = type, TimeMs = ms, Text = entry.Id, Text2 = session.Stats.CurrentChunk, Flag = e.Reason == 1 });
                    }

                    break;
                }

                case RunEventType.PowerUp:
                    Add(new AnalyticsEvent { Type = AnalyticsEventType.PowerUp, TimeMs = ms, Text = PowerUpName((PowerUpKind)e.Reason), Text2 = "picked" });
                    break;

                case RunEventType.ShieldConsumed:
                    Add(new AnalyticsEvent { Type = AnalyticsEventType.PowerUp, TimeMs = ms, Text = "shield", Text2 = "used" });
                    break;

                case RunEventType.ShieldExpired:
                    Add(new AnalyticsEvent { Type = AnalyticsEventType.PowerUp, TimeMs = ms, Text = "shield", Text2 = "expired" });
                    break;

                case RunEventType.RouteChosen:
                    if (session.Path.IsLive(e.Id))
                    {
                        Add(new AnalyticsEvent { Type = AnalyticsEventType.RouteSelected, TimeMs = ms, Text = RouteNames[Math.Min(e.Reason, (byte)3)], Text2 = session.Path.Chunk(e.Id).Chunk.Id });
                    }

                    break;

                case RunEventType.Died:
                    Add(new AnalyticsEvent
                    {
                        Type = AnalyticsEventType.Death,
                        TimeMs = ms,
                        Text = CauseNames[Math.Min(e.Reason, (byte)3)],
                        Text2 = session.Stats.CurrentChunk,
                        Int = e.Id,
                        Value = session.Simulation.State.Distance,
                    });
                    break;
            }
        }

        public void RecordRunEnded(RunStats stats, float seconds, int revives)
        {
            Add(new AnalyticsEvent { Type = AnalyticsEventType.RunEnded, TimeMs = (long)(seconds * 1000f), Value = stats.Distance, Int = stats.TotalCoins, Text = CauseNames[Math.Min((int)stats.Cause, 3)], Text2 = stats.DeathLabel, Flag = revives > 0 });
        }

        public void RecordAbilityUnlocked(AbilityDefinition ability, int runIndex)
        {
            Add(new AnalyticsEvent { Type = AnalyticsEventType.AbilityUnlocked, Text = ability.Name, Int = ability.CostCoins, Value = runIndex });
        }

        public void RecordRevive(bool used, int cost, int index, long timeMs)
        {
            Add(new AnalyticsEvent { Type = used ? AnalyticsEventType.ReviveUsed : AnalyticsEventType.ReviveOffered, TimeMs = timeMs, Int = cost, Value = index });
        }

        /// <summary>Serializes and clears (results time; allocates).</summary>
        public int Flush(IAnalyticsSink sink)
        {
            int n = _count;
            if (sink != null)
            {
                for (int i = 0; i < _count; i++)
                {
                    sink.Write(ToJson(_events[i]));
                }
            }

            _count = 0;
            return n;
        }

        public void Clear()
        {
            _count = 0;
            Dropped = 0;
        }

        /// <summary>One JSON line with the GDD §22 property names (plus build, session_id, run_id, t_ms).</summary>
        public string ToJson(in AnalyticsEvent e)
        {
            var b = new StringBuilder(160);
            b.Append("{\"event\":\"").Append(Name(e.Type)).Append('"');
            Field(b, "build", Build);
            Field(b, "session_id", SessionId);
            b.Append(",\"run_id\":").Append(e.RunId.ToString(CultureInfo.InvariantCulture));
            b.Append(",\"t_ms\":").Append(e.TimeMs.ToString(CultureInfo.InvariantCulture));
            switch (e.Type)
            {
                case AnalyticsEventType.TraversalResult:
                    Field(b, "type", e.Text);
                    Bool(b, "success", e.Flag);
                    b.Append(",\"obstacle_id\":").Append(e.Int.ToString(CultureInfo.InvariantCulture));
                    break;
                case AnalyticsEventType.CreatureFound:
                case AnalyticsEventType.SecretFound:
                    Field(b, "entry_id", e.Text);
                    Bool(b, "first_time", e.Flag);
                    Field(b, "chunk_id", e.Text2);
                    break;
                case AnalyticsEventType.PowerUp:
                    Field(b, "id", e.Text);
                    Field(b, "action", e.Text2);
                    break;
                case AnalyticsEventType.AbilityUnlocked:
                    Field(b, "id", e.Text);
                    b.Append(",\"cost\":").Append(e.Int.ToString(CultureInfo.InvariantCulture));
                    b.Append(",\"run_index\":").Append(((int)e.Value).ToString(CultureInfo.InvariantCulture));
                    break;
                case AnalyticsEventType.RunStarted:
                    b.Append(",\"run_index\":").Append(e.Int.ToString(CultureInfo.InvariantCulture));
                    b.Append(",\"seed\":").Append(_lastSeed.ToString(CultureInfo.InvariantCulture));
                    b.Append(",\"abilities_mask\":").Append(_lastAbilities.ToString(CultureInfo.InvariantCulture));
                    b.Append(",\"skill_S\":").Append(e.Value.ToString("0.###", CultureInfo.InvariantCulture));
                    Field(b, "orientation", e.Text);
                    break;
                case AnalyticsEventType.RunEnded:
                    b.Append(",\"distance_m\":").Append(((int)e.Value).ToString(CultureInfo.InvariantCulture));
                    b.Append(",\"duration_s\":").Append((e.TimeMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture));
                    b.Append(",\"coins\":").Append(e.Int.ToString(CultureInfo.InvariantCulture));
                    Field(b, "cause", e.Text);
                    break;
                case AnalyticsEventType.Death:
                    Field(b, "cause", e.Text);
                    b.Append(",\"obstacle_id\":").Append(e.Int.ToString(CultureInfo.InvariantCulture));
                    Field(b, "chunk_id", e.Text2);
                    b.Append(",\"distance_m\":").Append(((int)e.Value).ToString(CultureInfo.InvariantCulture));
                    break;
                case AnalyticsEventType.ReviveOffered:
                case AnalyticsEventType.ReviveUsed:
                    b.Append(",\"cost\":").Append(e.Int.ToString(CultureInfo.InvariantCulture));
                    b.Append(",\"index\":").Append(((int)e.Value).ToString(CultureInfo.InvariantCulture));
                    break;
                case AnalyticsEventType.RouteSelected:
                    Field(b, "chunk_id", e.Text2);
                    Field(b, "route", e.Text);
                    break;
            }

            return b.Append('}').ToString();
        }

        public static string Name(AnalyticsEventType type)
        {
            switch (type)
            {
                case AnalyticsEventType.RunStarted: return "run_started";
                case AnalyticsEventType.RunEnded: return "run_ended";
                case AnalyticsEventType.Death: return "death";
                case AnalyticsEventType.TraversalResult: return "traversal_result";
                case AnalyticsEventType.CreatureFound: return "creature_found";
                case AnalyticsEventType.SecretFound: return "secret_found";
                case AnalyticsEventType.PowerUp: return "power_up";
                case AnalyticsEventType.AbilityUnlocked: return "ability_unlocked";
                case AnalyticsEventType.ReviveOffered: return "revive_offered";
                case AnalyticsEventType.ReviveUsed: return "revive_used";
                default: return "route_selected";
            }
        }

        private static string PowerUpName(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield: return "shield";
                case PowerUpKind.Magnet: return "magnet";
                case PowerUpKind.ExplorerVision: return "explorer_vision";
                default: return "none";
            }
        }

        private void Add(AnalyticsEvent e)
        {
            if (_count == _events.Length)
            {
                Dropped++;
                return;
            }

            e.RunId = _runId;
            _events[_count++] = e;
        }

        private static void Field(StringBuilder b, string name, string value)
        {
            b.Append(",\"").Append(name).Append("\":\"");
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == '"' || c == '\\')
                    {
                        b.Append('\\');
                    }

                    b.Append(c < ' ' ? ' ' : c);
                }
            }

            b.Append('"');
        }

        private static void Bool(StringBuilder b, string name, bool value)
        {
            b.Append(",\"").Append(name).Append("\":").Append(value ? "true" : "false");
        }
    }
}
