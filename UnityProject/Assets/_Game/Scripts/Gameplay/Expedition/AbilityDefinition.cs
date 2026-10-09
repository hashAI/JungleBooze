using System;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>An ability (GDD §13): what it opens, its price and its results-card copy.</summary>
    [Serializable]
    public sealed class AbilityDefinition
    {
        public AbilityFlags Ability = AbilityFlags.DeepBreath;
        public string Name = "Deep Breath";

        /// <summary>One line on the ability card.</summary>
        public string Line = "Dive deep at shimmering water to reach sunken passages";

        /// <summary>Results objective sub-line when affordable ("Dive into the Sunken Arch").</summary>
        public string ObjectiveLine = string.Empty;

        /// <summary>Order in the unlock sequence (1 = first).</summary>
        public int Order = 1;

        public int CostCoins = 150;
        public int CostCrystals;

        /// <summary>Playable in this build (others are listed for objectives later but can't be learned).</summary>
        public bool Implemented = true;

        /// <summary>Force a chunk that shows it in the first run after the unlock (spec 103 §10.2).</summary>
        public bool Showcase = true;

        // Deep Breath tuning (spec 103 §4.5; used by Part B's deep dive).
        public float DeepDiveDepth = -2.5f;
        public float DeepDiveTime = 2.4f;
    }
}
