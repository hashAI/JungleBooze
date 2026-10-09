using System;
using System.Globalization;
using System.Text;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Run end and progression (spec 103 §9, GDD §12–13, §17): banks a run into the profile (wallet, best, journal,
    /// skill S, showcase bookkeeping), builds the results, picks the one next objective, and learns abilities.
    /// Plain C#; allocates (results time only).
    /// </summary>
    public static class ProgressionRules
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static AbilityFlags Owned(SaveData profile)
        {
            return (AbilityFlags)profile.abilities;
        }

        public static bool Owns(SaveData profile, AbilityFlags ability)
        {
            return (profile.abilities & (int)ability) != 0;
        }

        public static bool CanAfford(SaveData profile, AbilityDefinition ability)
        {
            return ability != null && profile.coins >= ability.CostCoins && profile.crystals >= ability.CostCrystals;
        }

        /// <summary>LEARN (AC-103-47): deducts the cost once, sets the ability and its Showcase flag. False if not allowed.</summary>
        public static bool TryLearn(SaveData profile, AbilityDefinition ability)
        {
            if (profile == null || ability == null || !ability.Implemented || Owns(profile, ability.Ability) || !CanAfford(profile, ability))
            {
                return false;
            }

            profile.coins -= ability.CostCoins;
            profile.crystals -= ability.CostCrystals;
            profile.abilities |= (int)ability.Ability;
            if (ability.Showcase)
            {
                profile.pendingShowcase |= (int)ability.Ability;
            }

            return true;
        }

        /// <summary>
        /// Banks a finished run into <paramref name="profile"/> and returns the results. <paramref name="showcased"/>
        /// are abilities whose Showcase chunk appeared this run (cleared from the pending set).
        /// </summary>
        public static RunResults ApplyRun(SaveData profile, RunStats stats, ExpeditionContent content, bool firstExpedition, AbilityFlags showcased)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            float previousBest = profile.bestDistance;
            bool hadBest = profile.runsCompleted > 0;
            var results = new RunResults
            {
                Distance = stats.Distance,
                FirstExpedition = firstExpedition,
                Coins = stats.Coins,
                CleanLineCoins = stats.CleanLineCoins,
                PerfectCoins = stats.PerfectCoins,
                DiscoveryCoins = stats.DiscoveryCoins,
                TotalCoins = stats.TotalCoins,
                Crystals = stats.TotalCrystals - stats.ReviveCrystals,
                DeathLabel = stats.DeathLabel,
            };

            // NEW RECORD only from run 2 and only when the best is beaten (AC-103-48).
            results.NewRecord = hadBest && !firstExpedition && stats.Distance > previousBest;
            profile.bestDistance = Math.Max(previousBest, stats.Distance);
            results.Best = profile.bestDistance;

            profile.coins += stats.TotalCoins;
            // Revive crystals come out of this run's crystals and the wallet (never below 0).
            profile.crystals = Math.Max(0, profile.crystals + stats.TotalCrystals - stats.ReviveCrystals);
            profile.totalDistance += (long)stats.Distance;
            profile.runsCompleted++;
            profile.pendingShowcase &= ~(int)showcased;
            profile.skill = DifficultyModel.UpdateSkill(content.Director, profile.skill, DifficultyModel.RunScore(content.Director, stats.Distance, stats.Hits, stats.TraversalAttempts, stats.TraversalSuccesses));

            for (int i = 0; i < stats.NewDiscoveryCount; i++)
            {
                DiscoveryEntry entry = content.Discoveries[stats.NewDiscovery(i)];
                if (profile.FindJournal(entry.Id) == null)
                {
                    profile.journal.Add(new JournalRecord { id = entry.Id, sightings = 1, firstRun = profile.runsCompleted });
                }

                results.NewDiscoveryNames.Add(entry.Name);
            }

            results.WalletCoins = profile.coins;
            results.WalletCrystals = profile.crystals;
            results.CategoryCounts = CategoryCounts(profile, content);
            results.Objective = ChooseObjective(profile, stats, content, previousBest, hadBest);
            return results;
        }

        /// <summary>GDD §17 priorities, first match wins (AC-103-46).</summary>
        public static NextObjective ChooseObjective(SaveData profile, RunStats stats, ExpeditionContent content, float previousBest, bool hadBest)
        {
            ResultsConfig cfg = content.Results;
            AbilityDefinition next = content.NextAbility(Owned(profile));

            // 1. Ability affordable now.
            if (next != null && CanAfford(profile, next))
            {
                return new NextObjective
                {
                    Kind = ObjectiveKind.AbilityReady,
                    Ability = next.Ability,
                    Title = next.Name + " ready · " + Fraction(Math.Min(profile.coins, next.CostCoins), next.CostCoins),
                    Detail = next.ObjectiveLine,
                    Progress = Math.Min(profile.coins, next.CostCoins),
                    Target = next.CostCoins,
                };
            }

            // 2. Within 10% of the best (and not beyond it).
            if (hadBest && previousBest > 0f && stats.Distance < previousBest && stats.Distance >= previousBest * (1f - cfg.NearBestFraction))
            {
                return new NextObjective
                {
                    Kind = ObjectiveKind.NearBest,
                    Title = Metres(stats.Distance) + " / " + Metres(previousBest),
                    Detail = "So close to your best",
                    Progress = (int)stats.Distance,
                    Target = (int)previousBest,
                };
            }

            // 3. Next ability funded ≥ 60% (≥ 50% right after run 1).
            if (next != null)
            {
                float funded = Funded(profile, next);
                float threshold = profile.runsCompleted <= 1 ? cfg.NextAbilityFundedFirstRun : cfg.NextAbilityFunded;
                if (funded >= threshold)
                {
                    return new NextObjective
                    {
                        Kind = ObjectiveKind.AbilityProgress,
                        Ability = next.Ability,
                        Title = next.Name + " " + Fraction(Math.Min(profile.coins, next.CostCoins), next.CostCoins),
                        Detail = next.Line,
                        Progress = Math.Min(profile.coins, next.CostCoins),
                        Target = next.CostCoins,
                    };
                }
            }

            // 4. An undiscovered, non-secret entry the run passed.
            for (int i = 0; i < content.Discoveries.Count && i < 32; i++)
            {
                DiscoveryEntry e = content.Discoveries[i];
                if (!e.Secret && (stats.MissedDiscoveries & (1 << i)) != 0 && !profile.IsDiscovered(e.Id))
                {
                    return new NextObjective { Kind = ObjectiveKind.UnseenEntry, Title = string.IsNullOrEmpty(e.MissedHint) ? "Something is out there" : e.MissedHint, Entry = i };
                }
            }

            // 5. Secrets: the hint of a missed secret, else the biome count.
            for (int i = 0; i < content.Discoveries.Count && i < 32; i++)
            {
                DiscoveryEntry e = content.Discoveries[i];
                if (e.Secret && (stats.MissedDiscoveries & (1 << i)) != 0 && !profile.IsDiscovered(e.Id))
                {
                    return new NextObjective { Kind = ObjectiveKind.Secrets, Title = string.IsNullOrEmpty(e.MissedHint) ? "A secret is hidden nearby" : e.MissedHint, Entry = i };
                }
            }

            int secrets = 0;
            int found = 0;
            for (int i = 0; i < content.Discoveries.Count; i++)
            {
                if (content.Discoveries[i].Secret)
                {
                    secrets++;
                    found += profile.IsDiscovered(content.Discoveries[i].Id) ? 1 : 0;
                }
            }

            if (secrets > 0 && found < secrets)
            {
                return new NextObjective { Kind = ObjectiveKind.Secrets, Title = "Secrets " + found.ToString(Invariant) + "/" + secrets.ToString(Invariant), Progress = found, Target = secrets };
            }

            return new NextObjective { Kind = ObjectiveKind.BeatBest, Title = "Beat your best: " + Metres(profile.bestDistance), Target = (int)profile.bestDistance };
        }

        public static float Funded(SaveData profile, AbilityDefinition ability)
        {
            float coins = ability.CostCoins > 0 ? Math.Min(1f, profile.coins / (float)ability.CostCoins) : 1f;
            float crystals = ability.CostCrystals > 0 ? Math.Min(1f, profile.crystals / (float)ability.CostCrystals) : 1f;
            return Math.Min(coins, crystals);
        }

        public static string CategoryCounts(SaveData profile, ExpeditionContent content)
        {
            var b = new StringBuilder();
            AppendCategory(b, profile, content, DiscoveryCategory.Creature, "Creatures", content.Results.CreatureTotal);
            AppendCategory(b, profile, content, DiscoveryCategory.Plant, "Plants", content.Results.PlantTotal);
            AppendCategory(b, profile, content, DiscoveryCategory.Location, "Locations", content.Results.LocationTotal);
            AppendCategory(b, profile, content, DiscoveryCategory.Mystery, "Mysteries", content.Results.MysteryTotal);
            return b.ToString();
        }

        public static string Metres(float metres)
        {
            return ((int)metres).ToString("N0", Invariant) + " m";
        }

        private static void AppendCategory(StringBuilder b, SaveData profile, ExpeditionContent content, DiscoveryCategory category, string label, int total)
        {
            int found = 0;
            for (int i = 0; i < content.Discoveries.Count; i++)
            {
                if (content.Discoveries[i].Category == category && profile.IsDiscovered(content.Discoveries[i].Id))
                {
                    found++;
                }
            }

            if (found == 0)
            {
                return;
            }

            if (b.Length > 0)
            {
                b.Append(" · ");
            }

            b.Append(label).Append(' ').Append(found.ToString(Invariant)).Append('/').Append(total.ToString(Invariant));
        }

        private static string Fraction(int value, int total)
        {
            return value.ToString(Invariant) + "/" + total.ToString(Invariant);
        }
    }
}
