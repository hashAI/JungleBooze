using System;
using JungleBooze.Core;
using JungleBooze.Services.Persistence;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// Missions (GDD 13.2) and the daily challenge (GDD 13.3). Three missions are active at a time. Every banked run
    /// adds to them; a completed mission pays its coins right away. Completing all 3 pays the set chest, raises the
    /// score multiplier by 1 and makes the next set, all at that Results screen [ASSUMED: the set is replaced as a
    /// whole, not slot by slot]. Missions are picked with a seeded generator from the set number, so the same set
    /// number always gives the same missions. The daily challenge comes from the day number the same way.
    /// Changes are kept in the save and marked dirty; the caller writes the save.
    /// </summary>
    public sealed class MissionService
    {
        public const int SlotCount = 3;

        private const ulong SetSeedBase = 0x4D49535300000000UL;
        private const ulong SetStream = 0x4D4953u;
        private const ulong ChallengeSeedBase = 0x4348414C00000000UL;
        private const ulong ChallengeStream = 0x434841u;

        private readonly PlayerSave _save;
        private readonly MetaConfig _config;
        private readonly IDayClock _clock;

        public MissionService(PlayerSave save, MetaConfig config, IDayClock clock)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            EnsureMissions();
        }

        public int CompletedSets => _save.Data.completedMissionSets;

        /// <summary>The number of the set being worked on (1 for the first).</summary>
        public int SetNumber => _save.Data.completedMissionSets + 1;

        /// <summary>The permanent score multiplier: 1 + completed sets, up to the configured cap (GDD 13.1).</summary>
        public int ScoreMultiplier
        {
            get
            {
                long m = 1L + _save.Data.completedMissionSets;
                return (int)Math.Min(m, _config.MaxScoreMultiplier);
            }
        }

        /// <summary>The chest the current set pays when all 3 missions are done.</summary>
        public int CurrentSetReward => SetRewardFor(_save.Data.completedMissionSets);

        public MissionSlotData GetSlot(int index)
        {
            return _save.Data.missions[index];
        }

        /// <summary>Mission reward for the set with this 0-based index: 50 + 10 per set, capped.</summary>
        public int RewardFor(int setIndex)
        {
            long v = _config.MissionRewardBase + (long)_config.MissionRewardStep * Math.Max(0, setIndex);
            return (int)Math.Min(v, _config.MissionRewardCap);
        }

        /// <summary>Set chest for the set with this 0-based index: 200 + 50 per set number, capped.</summary>
        public int SetRewardFor(int setIndex)
        {
            long v = _config.SetRewardBase + (long)_config.SetRewardPerSet * Math.Max(0, setIndex);
            return (int)Math.Min(v, _config.SetRewardCap);
        }

        /// <summary>Makes the first set if the save has none (new save or a damaged set).</summary>
        public void EnsureMissions()
        {
            MissionSlotData[] slots = _save.Data.missions;
            if (slots != null && slots.Length == SlotCount)
            {
                return;
            }

            GenerateSet(_save.Data.completedMissionSets);
            _save.MarkDirty();
        }

        /// <summary>Today's challenge: one goal per day, the same for everyone, 300 coins.</summary>
        public DailyChallenge GetDailyChallenge()
        {
            int day = _clock.Today;
            MissionTemplate[] pool = _config.DailyChallenges;
            var rng = new Pcg32Random(ChallengeSeedBase + unchecked((ulong)(long)day), ChallengeStream);
            MissionTemplate t = pool[rng.NextInt(0, pool.Length)];
            SaveData d = _save.Data;
            bool done = d.dailyChallengeDay == day && d.dailyChallengeDone;
            return new DailyChallenge(t.Kind, t.Targets[0], _config.DailyChallengeReward, done);
        }

        /// <summary>
        /// Counts one finished run: progress for the 3 missions (completed ones pay right away), the set chest and
        /// next set, and the daily challenge. Does not write the save.
        /// </summary>
        public MetaRunOutcome ApplyRun(in RunStats stats)
        {
            EnsureMissions();
            SaveData d = _save.Data;
            var outcome = new MetaRunOutcome();
            bool allDone = true;
            for (int i = 0; i < SlotCount; i++)
            {
                MissionSlotData slot = d.missions[i];
                if (!slot.completed)
                {
                    int value = stats.ValueOf((MissionKind)slot.kind);
                    if (slot.perRun)
                    {
                        if (value > slot.progress)
                        {
                            slot.progress = value;
                        }
                    }
                    else
                    {
                        slot.progress = (int)Math.Min((long)slot.progress + Math.Max(0, value), int.MaxValue);
                    }

                    if (slot.progress >= slot.target)
                    {
                        slot.progress = slot.target;
                        slot.completed = true;
                        outcome.MissionsCompleted++;
                        outcome.MissionCoins += slot.reward;
                    }
                }

                if (!slot.completed)
                {
                    allDone = false;
                }
            }

            _save.AddCoins(outcome.MissionCoins);
            if (allDone)
            {
                outcome.SetCompleted = true;
                outcome.SetCoins = SetRewardFor(d.completedMissionSets);
                _save.AddCoins(outcome.SetCoins);
                if (d.completedMissionSets < int.MaxValue)
                {
                    d.completedMissionSets++;
                }

                outcome.NewScoreMultiplier = ScoreMultiplier;
                GenerateSet(d.completedMissionSets);
            }

            int today = _clock.Today;
            if (d.dailyChallengeDay != today)
            {
                d.dailyChallengeDay = today;
                d.dailyChallengeDone = false;
            }

            if (!d.dailyChallengeDone)
            {
                DailyChallenge challenge = GetDailyChallenge();
                if (stats.ValueOf(challenge.Kind) >= challenge.Target)
                {
                    d.dailyChallengeDone = true;
                    outcome.DailyChallengeCompleted = true;
                    outcome.DailyChallengeCoins = challenge.Reward;
                    _save.AddCoins(challenge.Reward);
                }
            }

            _save.MarkDirty();
            return outcome;
        }

        private void GenerateSet(int setIndex)
        {
            MissionTemplate[] pool = _config.Missions;
            int n = pool.Length;
            var rng = new Pcg32Random(SetSeedBase + unchecked((ulong)(long)setIndex), SetStream);
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }

            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            var slots = new MissionSlotData[SlotCount];
            int reward = RewardFor(setIndex);
            for (int s = 0; s < SlotCount; s++)
            {
                MissionTemplate t = pool[order[s % n]];
                int tier = Math.Min(Math.Max(0, setIndex), t.Targets.Length - 1);
                slots[s] = new MissionSlotData
                {
                    kind = (int)t.Kind,
                    perRun = t.PerRun,
                    target = Math.Max(1, t.Targets[tier]),
                    reward = reward,
                };
            }

            _save.Data.missions = slots;
        }
    }
}
