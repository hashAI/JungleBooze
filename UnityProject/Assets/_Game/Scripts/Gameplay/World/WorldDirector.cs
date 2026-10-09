using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Picks the next chunk (spec 102 §6.3, spec 103 §10): the Expedition 1 script first (run 1), the 60 m Short
    /// start on later runs, then filter → forced picks (Recovery cadence, mercy, Branch cadence, Showcase) → weights
    /// → seeded pick, with the fallback chain freshness → dimension mix → band ±1 → any Recovery. Repetition rules
    /// R1–R5 and the seam rule V11 are hard filters; only validated, non-script variants are used.
    /// Deterministic: same library + config + setup + hit ticks → same picks. Allocation-free after
    /// <see cref="BeginRun"/> (which forks the run's random streams).
    /// </summary>
    public sealed class WorldDirector
    {
        public const int HistoryCapacity = 16;
        public const int MaxRelaxLevel = 5;

        private readonly ChunkLibrary _library;
        private readonly WorldDirectorConfig _config;
        private readonly SpeedCurve _speed;
        private readonly bool _requireValidated;
        private readonly int[] _candidateDefs;
        private readonly float[] _candidateWeights;
        private readonly int[] _variantScratch;
        private readonly int[] _historyDef = new int[HistoryCapacity];
        private readonly int[] _historyEntry = new int[HistoryCapacity];
        private readonly long[] _hitTicks;
        private readonly int _mercyWindowTicks;

        private IRandom _track;
        private IRandom _variants;
        private IRandom _pickups;
        private IRandom _discovery;
        private DirectorRunSetup _setup;
        private int _historyCount;
        private int _historyStart;
        private int _serial;
        private int _scriptIndex;
        private bool _afterScriptRecovery;
        private int _sinceRecovery;
        private int _sinceFork;
        private int _nextForkDue;
        private int _forksInPhase;
        private int _forkPhase;
        private int _hitCount;
        private bool _mercyPending;
        private int _picksAfterStart;
        private float _lastExitMarginM;
        private float _lastFlowCrystalS;
        private float _nextPowerUpS;
        private float _skill;

        public WorldDirector(ChunkLibrary library, WorldDirectorConfig config, RunSpeedConfig speed, float stepSeconds, bool requireValidated)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _config = (config ?? throw new ArgumentNullException(nameof(config))).Clone();
            _speed = new SpeedCurve(speed ?? throw new ArgumentNullException(nameof(speed)));
            _requireValidated = requireValidated;
            _candidateDefs = new int[Math.Max(1, library.DefinitionCount)];
            _candidateWeights = new float[Math.Max(1, library.DefinitionCount)];
            _variantScratch = new int[Math.Max(1, library.EntryCount)];
            _hitTicks = new long[Math.Max(1, _config.MercyHits)];
            _mercyWindowTicks = (int)Math.Round(_config.MercyWindowSeconds / stepSeconds);
        }

        public ChunkLibrary Library => _library;

        public WorldDirectorConfig Config => _config;

        /// <summary>Picks made this run.</summary>
        public int Serial => _serial;

        /// <summary>True while Expedition 1 entries remain.</summary>
        public bool ScriptActive => _setup.Script != null && _scriptIndex < _setup.Script.Entries.Count;

        /// <summary>Abilities whose Showcase chunk was placed this run.</summary>
        public AbilityFlags ShowcasedAbilities { get; private set; }

        /// <summary>Picks that needed the fallback chain (relax level ≥ 1) this run.</summary>
        public int FallbackPicks { get; private set; }

        /// <summary>Picks that broke a repetition rule because nothing else was valid (content gap; should stay 0).</summary>
        public int EmergencyPicks { get; private set; }

        /// <summary>Picks where even the emergency level found nothing (review S6; logged by the scene, should stay 0).</summary>
        public int LastResortPicks { get; private set; }

        public float Skill => _skill;

        public int BandShift => DifficultyModel.BandShift(_config, _skill);

        public float CueIntensity => DifficultyModel.CueIntensity(_config, _skill);

        /// <summary>Starts a run: forks the random streams in the fixed order of spec 102 §6.3 step 4.</summary>
        public void BeginRun(in DirectorRunSetup setup)
        {
            _setup = setup;
            var root = new Pcg32Random(setup.Script != null ? setup.Script.Seed : setup.Seed);
            _track = root.Fork(RandomStreamIds.TrackGeneration);
            _variants = root.Fork(RandomStreamIds.ObstacleVariants);
            _pickups = root.Fork(RandomStreamIds.Pickups);
            _discovery = root.Fork(RandomStreamIds.Discovery);
            _skill = setup.Skill;
            _historyCount = 0;
            _historyStart = 0;
            _serial = 0;
            _scriptIndex = 0;
            _afterScriptRecovery = setup.Script != null;
            _sinceRecovery = 0;
            _sinceFork = 0;
            _forksInPhase = 0;
            _forkPhase = -1;
            _nextForkDue = 0;
            _hitCount = 0;
            _mercyPending = false;
            _picksAfterStart = 0;
            _lastExitMarginM = float.PositiveInfinity;
            _lastFlowCrystalS = 0f;
            _nextPowerUpS = _config.PowerUpSpacingMin;
            ShowcasedAbilities = AbilityFlags.None;
            FallbackPicks = 0;
            EmergencyPicks = 0;
            LastResortPicks = 0;
        }

        /// <summary>A health-losing hit happened on <paramref name="tick"/> (mercy rule).</summary>
        public void OnHit(long tick)
        {
            int n = _hitTicks.Length;
            for (int i = n - 1; i > 0; i--)
            {
                _hitTicks[i] = _hitTicks[i - 1];
            }

            _hitTicks[0] = tick;
            _hitCount++;
            if (_hitCount >= n && tick - _hitTicks[n - 1] <= _mercyWindowTicks)
            {
                _mercyPending = true;
            }
        }

        /// <summary>Phase at a run distance.</summary>
        public PhaseRule PhaseAt(float distance)
        {
            return _config.Phases[_config.PhaseIndexAt(distance)];
        }

        /// <summary>Plans the chunk that will start at run distance <paramref name="startS"/>.</summary>
        public ChunkPick PlanNext(float startS)
        {
            ChunkPick pick;
            if (ScriptActive)
            {
                pick = ScriptPick(startS);
            }
            else if (_serial == 0)
            {
                pick = StartPick();
            }
            else
            {
                pick = DirectedPick(startS);
            }

            pick.Serial = _serial;
            _serial++;
            Remember(pick);
            return pick;
        }

        private ChunkPick ScriptPick(float startS)
        {
            ExpeditionScriptEntry entry = _setup.Script.Entries[_scriptIndex++];
            int index = _library.Find(entry.ChunkId, entry.Variant);
            if (index < 0)
            {
                throw new InvalidOperationException("Expedition script chunk not in the library: " + entry.ChunkId + " / " + entry.Variant);
            }

            ChunkRuntime chunk = _library.GetEntry(index);
            _lastFlowCrystalS = startS;
            if (entry.PowerUp != PowerUpKind.None && entry.PowerUpSlot >= 0 && entry.PowerUpSlot < chunk.PowerUpSlotCount)
            {
                _nextPowerUpS = startS + chunk.GetPowerUpSlot(entry.PowerUpSlot).S + _config.PowerUpSpacingMin;
            }

            return new ChunkPick
            {
                Entry = index,
                Reason = PickReason.Script,
                Phase = PhaseAt(startS).Phase,
                EnabledRoutes = AllRoutes(chunk),
                Reward = chunk.Definition.Reward,
                CoinDensity = 1f,
                CrystalMask = 0,
                FlowCrystalCoin = -1,
                PowerUpSlot = entry.PowerUp != PowerUpKind.None ? entry.PowerUpSlot : -1,
                PowerUp = entry.PowerUp,
                CueIntensity = _setup.Script.CueIntensity,
            };
        }

        private ChunkPick StartPick()
        {
            int def = -1;
            for (int d = 0; d < _library.DefinitionCount; d++)
            {
                if (_library.GetDefinition(d).RunOpener)
                {
                    def = d;
                    break;
                }
            }

            if (def < 0)
            {
                throw new InvalidOperationException("No run-opener chunk in the library.");
            }

            ChunkDefinition definition = _library.GetDefinition(def);
            int variant = _setup.ShortStart ? Math.Max(0, definition.FindVariant("Short")) : 0;
            int index = _library.FirstEntryOf(def) + variant;
            ChunkRuntime chunk = _library.GetEntry(index);
            return Dress(chunk, PickReason.Start, PhaseAt(0f).Phase, 0f, 0);
        }

        private ChunkPick DirectedPick(float startS)
        {
            int phaseIndex = _config.PhaseIndexAt(startS);
            PhaseRule rule = _config.Phases[phaseIndex];
            if (phaseIndex != _forkPhase)
            {
                _forkPhase = phaseIndex;
                _forksInPhase = 0;
                _nextForkDue = _track.NextInt(rule.ForkEveryMin, Math.Max(rule.ForkEveryMin, rule.ForkEveryMax) + 1);
            }

            float speed = _speed.Evaluate(startS);
            int recoveryEvery = DifficultyModel.RecoveryEvery(_config, _skill, rule.RecoveryEvery);
            bool mercy = _mercyPending;
            bool forcedRecovery = mercy || _afterScriptRecovery || _sinceRecovery >= recoveryEvery;
            bool forkAllowed = rule.MaxForks < 0 || _forksInPhase < rule.MaxForks;
            bool forcedBranch = !forcedRecovery && forkAllowed && _sinceFork >= _nextForkDue;
            int band = BandShift;
            PickReason reason = mercy ? PickReason.Mercy : forcedRecovery ? PickReason.Recovery : forcedBranch ? PickReason.Branch : PickReason.Normal;

            // Showcase rule: a newly unlocked ability is shown within the first picks after the start chunk.
            AbilityFlags showcase = _setup.PendingShowcase & ~ShowcasedAbilities;
            if (!forcedRecovery && showcase != AbilityFlags.None && _picksAfterStart < _config.ShowcaseWindow)
            {
                int count = Collect(rule, speed, band, 0, ChunkCategory.Straight, false, showcase);
                if (count > 0)
                {
                    int def = _candidateDefs[0];
                    ShowcasedAbilities |= _library.GetDefinition(def).ShowcaseAbilities & showcase;
                    return Finish(def, rule, speed, PickReason.Showcase, 0, startS);
                }
            }

            ChunkCategory forced = forcedRecovery ? ChunkCategory.Recovery : ChunkCategory.Branch;
            bool forceCategory = forcedRecovery || forcedBranch;

            // Fallback chain (spec 102 §6.3 step 5): forced Branch → dimension mix → band ±1 → band ±3 → any
            // Recovery → (a forced Recovery that R1 blocks: the easiest chunk, Recovery stays pending) → emergency.
            for (int step = 0; step < 8; step++)
            {
                int level;
                bool useForced;
                ChunkCategory category = forced;
                switch (step)
                {
                    case 0:
                        level = 0;
                        useForced = forceCategory;
                        break;
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        level = step;
                        useForced = forcedRecovery;
                        break;
                    case 5:
                        level = 0;
                        useForced = true;
                        category = ChunkCategory.Recovery;
                        break;
                    case 6:
                        level = 4;
                        useForced = false;
                        break;
                    default:
                        level = MaxRelaxLevel;
                        useForced = false;
                        break;
                }

                int count = Collect(rule, speed, band, level, category, useForced, AbilityFlags.None);
                if (count == 0)
                {
                    continue;
                }

                bool emergency = level == MaxRelaxLevel;
                int chosen = emergency ? LeastRecent(count) : step == 6 && forcedRecovery ? Easiest(count) : Weighted(count);
                if (step > 0)
                {
                    FallbackPicks++;
                }

                if (emergency)
                {
                    EmergencyPicks++;
                }

                PickReason why = emergency ? PickReason.Emergency : step >= 5 ? PickReason.Fallback : reason;

                // Review S6: V11 is never ignored (Collect only admits definitions with a variant that passes it).
                return Finish(chosen, rule, speed, why, step, startS);
            }

            return LastResort(rule, speed, startS);
        }

        /// <summary>
        /// Review S6: nothing passed even the emergency level. Any pool Recovery chunk whose variant passes V11 (phase,
        /// band and repetition rules ignored); if none, any pool chunk passing V11; if the seam can't be met at all,
        /// the Recovery variant with the largest entry margin. Never throws mid-run unless the library has no pool
        /// chunk at all (a content error the catalog validation rejects).
        /// </summary>
        private ChunkPick LastResort(in PhaseRule rule, float speed, float startS)
        {
            LastResortPicks++;
            EmergencyPicks++;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int d = 0; d < _library.DefinitionCount; d++)
                {
                    ChunkDefinition def = _library.GetDefinition(d);
                    if (def.RunOpener || !def.PoolEnabled || (def.RequiredAbilities & ~_setup.Owned) != 0)
                    {
                        continue;
                    }

                    if (pass == 0 && def.Category != ChunkCategory.Recovery)
                    {
                        continue;
                    }

                    if (EligibleVariants(d, rule, speed, false) > 0)
                    {
                        return Finish(d, rule, speed, PickReason.Emergency, MaxRelaxLevel, startS);
                    }
                }
            }

            int best = -1;
            float bestMargin = float.NegativeInfinity;
            for (int d = 0; d < _library.DefinitionCount; d++)
            {
                ChunkDefinition def = _library.GetDefinition(d);
                if (def.RunOpener || !def.PoolEnabled || (def.RequiredAbilities & ~_setup.Owned) != 0)
                {
                    continue;
                }

                int first = _library.FirstEntryOf(d);
                for (int v = 0; v < def.Variants.Count; v++)
                {
                    ChunkRuntime entry = _library.GetEntry(first + v);
                    float margin = entry.EntryMarginM + (def.Category == ChunkCategory.Recovery ? 1000f : 0f);
                    if (_library.IsPoolVariant(entry, _requireValidated) && margin > bestMargin)
                    {
                        bestMargin = margin;
                        best = first + v;
                    }
                }
            }

            if (best < 0)
            {
                throw new InvalidOperationException("World Director: the library has no pool chunk (content error).");
            }

            ChunkRuntime chunk = _library.GetEntry(best);
            return Dress(chunk, PickReason.Emergency, rule.Phase, startS, MaxRelaxLevel);
        }

        private ChunkPick Finish(int def, in PhaseRule rule, float speed, PickReason reason, int level, float startS)
        {
            int variantCount = EligibleVariants(def, rule, speed, false);
            int entry = _variantScratch[variantCount > 1 ? _variants.NextInt(0, variantCount) : 0];
            ChunkRuntime chunk = _library.GetEntry(entry);
            ChunkPick pick = Dress(chunk, reason, rule.Phase, startS, level);
            if (reason == PickReason.Mercy || reason == PickReason.Recovery)
            {
                _mercyPending = false;
                _afterScriptRecovery = false;
            }

            if (chunk.Definition.Category == ChunkCategory.Recovery)
            {
                _mercyPending = false;
                _afterScriptRecovery = false;
            }

            return pick;
        }

        /// <summary>
        /// Fills <see cref="_candidateDefs"/> / <see cref="_candidateWeights"/> with definitions that have an
        /// eligible variant. Relax levels: 0 all rules; 1 forced Branch dropped (caller); 2 dimension mix off;
        /// 3 rating band ±1; 4 rating band ±3; 5 emergency (R1/R3 off, least recently used).
        /// </summary>
        private int Collect(in PhaseRule rule, float speed, int band, int level, ChunkCategory forced, bool useForced, AbilityFlags showcase)
        {
            int count = 0;
            int widen = level == 3 ? 1 : level == 4 ? 3 : 0;
            int bandMin = rule.RatingMin + band - widen;
            int bandMax = rule.RatingMax + band + widen;
            for (int d = 0; d < _library.DefinitionCount; d++)
            {
                ChunkDefinition def = _library.GetDefinition(d);
                if (def.RunOpener || !def.PoolEnabled)
                {
                    continue;
                }

                if (showcase != AbilityFlags.None)
                {
                    if ((def.ShowcaseAbilities & showcase) == 0)
                    {
                        continue;
                    }
                }
                else if (useForced && def.Category != forced)
                {
                    continue;
                }

                bool recovery = def.Category == ChunkCategory.Recovery;
                if (rule.Phase < def.PhaseMin || rule.Phase > def.PhaseMax)
                {
                    continue;
                }

                if ((def.RequiredAbilities & ~_setup.Owned) != 0)
                {
                    continue;
                }

                // Rating band (Recovery and Showcase picks are exempt: they are forced picks).
                if (!recovery && showcase == AbilityFlags.None && level < 5 && (def.RatingMax < bandMin || def.RatingMin > bandMax))
                {
                    continue;
                }

                if (!PassesRepetition(d, def, level))
                {
                    continue;
                }

                if (level < 2 && !PassesDimensionMix(def))
                {
                    continue;
                }

                if (EligibleVariants(d, rule, speed, false) == 0)
                {
                    continue;
                }

                _candidateDefs[count] = d;
                _candidateWeights[count] = Weight(d, def);
                count++;
            }

            return count;
        }

        private int EligibleVariants(int def, in PhaseRule rule, float speed, bool ignoreSeam)
        {
            int first = _library.FirstEntryOf(def);
            ChunkDefinition definition = _library.GetDefinition(def);
            int count = 0;
            for (int v = 0; v < definition.Variants.Count; v++)
            {
                ChunkRuntime entry = _library.GetEntry(first + v);
                if (!_library.IsPoolVariant(entry, _requireValidated))
                {
                    continue;
                }

                // V11: exit margin of the previous chunk + entry margin of this one ≥ the phase's action gap.
                if (!ignoreSeam && (_lastExitMarginM + entry.EntryMarginM) / speed < rule.MinActionGap)
                {
                    continue;
                }

                _variantScratch[count++] = first + v;
            }

            return count;
        }

        private bool PassesRepetition(int d, ChunkDefinition def, int level)
        {
            int n = _historyCount;
            if (n == 0)
            {
                return true;
            }

            ChunkDefinition last = HistoryDef(0);

            // R1 (not relaxed before the emergency level).
            if (level < 5)
            {
                int window = Math.Min(n, _config.NoRepeatWindow);
                for (int i = 0; i < window; i++)
                {
                    if (_historyDef[Ring(i)] == d)
                    {
                        return false;
                    }
                }
            }
            else if (_historyDef[Ring(0)] == d)
            {
                return false;
            }

            // R2: Recovery never twice; others at most MaxSameCategory in a row.
            if (def.Category == ChunkCategory.Recovery)
            {
                if (last.Category == ChunkCategory.Recovery)
                {
                    return false;
                }
            }
            else if (n >= _config.MaxSameCategory)
            {
                bool all = true;
                for (int i = 0; i < _config.MaxSameCategory; i++)
                {
                    if (HistoryDef(i).Category != def.Category)
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    return false;
                }
            }

            // R3: environment at most MaxSameEnvironment in a row unless a set piece.
            if (!def.SetPiece && level < 5 && n >= _config.MaxSameEnvironment)
            {
                bool all = true;
                for (int i = 0; i < _config.MaxSameEnvironment; i++)
                {
                    if (HistoryDef(i).Environment != def.Environment)
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    return false;
                }
            }

            return true;
        }

        private bool PassesDimensionMix(ChunkDefinition def)
        {
            int cap = _config.MaxConsecutiveHighDimension;
            if (_historyCount < cap)
            {
                return true;
            }

            bool reaction = def.Dimensions.Reaction >= 2;
            bool navigation = def.Dimensions.Navigation >= 2;
            for (int i = 0; i < cap; i++)
            {
                ChunkDimensions h = HistoryDef(i).Dimensions;
                reaction &= h.Reaction >= 2;
                navigation &= h.Navigation >= 2;
            }

            return !reaction && !navigation;
        }

        private float Weight(int d, ChunkDefinition def)
        {
            float w = def.BaseWeight > 0f ? def.BaseWeight : 1f;
            int window = Math.Min(_historyCount, _config.FreshnessWindow);
            for (int i = 0; i < window; i++)
            {
                if (_historyDef[Ring(i)] == d)
                {
                    w *= _config.FreshnessFactor;
                    break;
                }
            }

            if (_historyCount > 0)
            {
                ChunkDimensions last = HistoryDef(0).Dimensions;
                if ((last.Reaction >= 2 && def.Dimensions.Reaction < 2) || (last.Navigation >= 2 && def.Dimensions.Navigation < 2))
                {
                    w *= _config.DimensionFitFactor;
                }
            }

            if (_skill > _config.SkillHigh && (def.Category == ChunkCategory.Challenge || def.Category == ChunkCategory.Branch))
            {
                w *= _config.SkillBiasFactor;
            }
            else if (_skill < _config.SkillLow && (def.Category == ChunkCategory.Recovery || def.Category == ChunkCategory.Straight))
            {
                w *= _config.SkillBiasFactor;
            }

            return w;
        }

        private int Weighted(int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                total += _candidateWeights[i];
            }

            float r = _track.NextFloat() * total;
            for (int i = 0; i < count; i++)
            {
                r -= _candidateWeights[i];
                if (r < 0f)
                {
                    return _candidateDefs[i];
                }
            }

            return _candidateDefs[count - 1];
        }

        /// <summary>The lowest-rated candidate (deterministic; mercy when no Recovery can be placed).</summary>
        private int Easiest(int count)
        {
            int best = _candidateDefs[0];
            for (int i = 1; i < count; i++)
            {
                ChunkDefinition c = _library.GetDefinition(_candidateDefs[i]);
                ChunkDefinition b = _library.GetDefinition(best);
                if (c.RatingMin < b.RatingMin || (c.RatingMin == b.RatingMin && c.Rating < b.Rating))
                {
                    best = _candidateDefs[i];
                }
            }

            return best;
        }

        private int LeastRecent(int count)
        {
            int best = _candidateDefs[0];
            int bestAge = -1;
            for (int i = 0; i < count; i++)
            {
                int d = _candidateDefs[i];
                int age = _historyCount;
                for (int k = 0; k < _historyCount; k++)
                {
                    if (_historyDef[Ring(k)] == d)
                    {
                        age = k;
                        break;
                    }
                }

                if (age > bestAge)
                {
                    bestAge = age;
                    best = d;
                }
            }

            return best;
        }

        /// <summary>Rewards, crystals, power-up and cue intensity for a director pick (Pickups stream).</summary>
        private ChunkPick Dress(ChunkRuntime chunk, PickReason reason, DifficultyPhase phase, float startS, int level)
        {
            ChunkDefinition def = chunk.Definition;
            var pick = new ChunkPick
            {
                Entry = chunk.LibraryIndex,
                Reason = reason,
                Phase = phase,
                EnabledRoutes = AllRoutes(chunk),
                Reward = def.Reward,
                CoinDensity = def.Reward == RewardProfile.Low ? _config.LowCoinDensity : 1f,
                FlowCrystalCoin = -1,
                PowerUpSlot = -1,
                PowerUp = PowerUpKind.None,
                CueIntensity = CueIntensity,
                RelaxLevel = level,
            };

            float crystalChance = _config.RiskyCrystalChance * (def.Reward == RewardProfile.Rich ? _config.RichCrystalFactor : 1f);
            for (int i = 0; i < chunk.CrystalCount && i < 31; i++)
            {
                CrystalAnchor anchor = chunk.GetCrystal(i);
                if (anchor.Always)
                {
                    continue;
                }

                // Review N8: the risky-crystal roll applies to anchors on Risky branches only (spec 102 §7). The roll is
                // drawn for every optional anchor so the Pickups stream stays in step.
                bool roll = _pickups.NextFloat() < crystalChance;
                if (roll && chunk.RouteAt(anchor.S, anchor.X) == RouteType.Risky)
                {
                    pick.CrystalMask |= 1 << i;
                }
            }

            if (startS - _lastFlowCrystalS >= _config.FlowCrystalMinSpacing && _pickups.NextFloat() < chunk.Length / _config.FlowCrystalChanceLength)
            {
                for (int i = 0; i < chunk.CoinCount; i++)
                {
                    CoinPoint coin = chunk.GetCoin(i);
                    if (coin.S >= chunk.Length * 0.5f && chunk.RouteAt(coin.S, coin.X) <= RouteType.Safe)
                    {
                        pick.FlowCrystalCoin = i;
                        _lastFlowCrystalS = startS + coin.S;
                        break;
                    }
                }
            }

            if (chunk.PowerUpSlotCount > 0 && startS + chunk.Length >= _nextPowerUpS)
            {
                int slot = -1;
                bool preferRisky = _pickups.NextFloat() < _config.RiskyPowerUpPreference;
                for (int i = 0; i < chunk.PowerUpSlotCount; i++)
                {
                    PowerUpSlot p = chunk.GetPowerUpSlot(i);
                    if (startS + p.S < _nextPowerUpS)
                    {
                        continue;
                    }

                    if (slot < 0 || (preferRisky && p.OnRisky && !chunk.GetPowerUpSlot(slot).OnRisky))
                    {
                        slot = i;
                    }
                }

                if (slot >= 0)
                {
                    pick.PowerUpSlot = slot;
                    pick.PowerUp = PowerUpKind.Shield;
                    _nextPowerUpS = startS + chunk.GetPowerUpSlot(slot).S + _pickups.NextFloat(_config.PowerUpSpacingMin, _config.PowerUpSpacingMax);
                }
            }

            return pick;
        }

        private void Remember(in ChunkPick pick)
        {
            ChunkRuntime chunk = _library.GetEntry(pick.Entry);
            ChunkDefinition def = chunk.Definition;
            int slot = (_historyStart + _historyCount) % HistoryCapacity;
            if (_historyCount == HistoryCapacity)
            {
                _historyStart = (_historyStart + 1) % HistoryCapacity;
                slot = (_historyStart + HistoryCapacity - 1) % HistoryCapacity;
            }
            else
            {
                _historyCount++;
            }

            _historyDef[slot] = chunk.DefinitionIndex;
            _historyEntry[slot] = pick.Entry;
            _lastExitMarginM = chunk.ExitMarginM;

            if (pick.Reason != PickReason.Start && pick.Reason != PickReason.Script)
            {
                _picksAfterStart++;
            }
            else if (pick.Reason == PickReason.Start)
            {
                _picksAfterStart = 0;
            }

            if (def.Category == ChunkCategory.Recovery)
            {
                _sinceRecovery = 0;
            }
            else
            {
                _sinceRecovery++;
            }

            if (def.Category == ChunkCategory.Branch)
            {
                _sinceFork = 0;
                if (pick.Reason != PickReason.Script)
                {
                    _forksInPhase++;
                    if (_forkPhase >= 0)
                    {
                        PhaseRule rule = _config.Phases[_forkPhase];
                        _nextForkDue = _track.NextInt(rule.ForkEveryMin, Math.Max(rule.ForkEveryMin, rule.ForkEveryMax) + 1);
                    }
                }
            }
            else
            {
                _sinceFork++;
            }
        }

        /// <summary>History ring index of the i-th most recent pick (0 = last).</summary>
        private int Ring(int i)
        {
            return (_historyStart + _historyCount - 1 - i + HistoryCapacity) % HistoryCapacity;
        }

        private ChunkDefinition HistoryDef(int i)
        {
            return _library.GetDefinition(_historyDef[Ring(i)]);
        }

        /// <summary>Library entry of the i-th most recent pick (0 = last).</summary>
        public int RecentEntry(int i)
        {
            return i < _historyCount ? _historyEntry[Ring(i)] : -1;
        }

        public int HistoryCount => _historyCount;

        private static int AllRoutes(ChunkRuntime chunk)
        {
            int mask = 0;
            for (int i = 0; i < chunk.RouteCount && i < 31; i++)
            {
                if (!chunk.GetRoute(i).Locked)
                {
                    mask |= 1 << i;
                }
            }

            return mask;
        }

        /// <summary>Reserved stream for discovery slot assignment (spec 102 §6.3 step 4); unused until journal slots land.</summary>
        internal IRandom DiscoveryStream => _discovery;
    }
}
