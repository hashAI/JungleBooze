using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Everything an expedition run reads besides movement tuning: the chunk catalog, the Expedition 1 script, the
    /// director, pickup and results tuning, journal entries and abilities. Built once by the composition root from the
    /// ScriptableObjects (or by tests in code); the run deep-copies the tuning it needs.
    /// </summary>
    public sealed class ExpeditionContent
    {
        public ExpeditionContent(
            IList<ChunkDefinition> chunks,
            ExpeditionScript script,
            WorldDirectorConfig director,
            PickupConfig pickups,
            ResultsConfig results,
            IList<DiscoveryEntry> discoveries,
            IList<AbilityDefinition> abilities,
            SailbackConfig sailback = null)
        {
            Library = new ChunkLibrary(chunks ?? throw new ArgumentNullException(nameof(chunks)));
            Script = script ?? throw new ArgumentNullException(nameof(script));
            Director = (director ?? throw new ArgumentNullException(nameof(director))).Clone();
            Pickups = (pickups ?? throw new ArgumentNullException(nameof(pickups))).Clone();
            Results = (results ?? throw new ArgumentNullException(nameof(results))).Clone();
            Discoveries = new List<DiscoveryEntry>(discoveries ?? throw new ArgumentNullException(nameof(discoveries)));
            var ordered = new List<AbilityDefinition>(abilities ?? throw new ArgumentNullException(nameof(abilities)));
            ordered.Sort((a, b) => a.Order.CompareTo(b.Order));
            Abilities = ordered;
            Sailback = (sailback ?? new SailbackConfig()).Clone();
        }

        /// <summary>The slice creature (spec 103 §7).</summary>
        public SailbackConfig Sailback { get; }

        public ChunkLibrary Library { get; }

        public ExpeditionScript Script { get; }

        public WorldDirectorConfig Director { get; }

        public PickupConfig Pickups { get; }

        public ResultsConfig Results { get; }

        public IReadOnlyList<DiscoveryEntry> Discoveries { get; }

        /// <summary>Abilities in unlock order.</summary>
        public IReadOnlyList<AbilityDefinition> Abilities { get; }

        public int FindDiscovery(string id)
        {
            for (int i = 0; i < Discoveries.Count; i++)
            {
                if (Discoveries[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public AbilityDefinition FindAbility(AbilityFlags ability)
        {
            for (int i = 0; i < Abilities.Count; i++)
            {
                if (Abilities[i].Ability == ability)
                {
                    return Abilities[i];
                }
            }

            return null;
        }

        /// <summary>The next ability in order the player doesn't own and that is playable in this build, or null.</summary>
        public AbilityDefinition NextAbility(AbilityFlags owned)
        {
            for (int i = 0; i < Abilities.Count; i++)
            {
                if (Abilities[i].Implemented && (owned & Abilities[i].Ability) == 0)
                {
                    return Abilities[i];
                }
            }

            return null;
        }
    }
}
