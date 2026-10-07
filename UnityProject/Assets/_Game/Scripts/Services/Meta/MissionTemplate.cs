using System;

namespace JungleBooze.Services.Meta
{
    /// <summary>
    /// One mission template (GDD 13.2): what is counted, whether it must happen in one run or adds up over runs,
    /// and the tiered targets. Later mission sets use higher tiers (set 1 uses tier 1, and so on, capped at the last).
    /// </summary>
    [Serializable]
    public sealed class MissionTemplate
    {
        public MissionKind Kind;

        /// <summary>True: the target must be reached in a single run. False: counts add up over runs.</summary>
        public bool PerRun;

        public int[] Targets = { 1 };

        public MissionTemplate()
        {
        }

        public MissionTemplate(MissionKind kind, bool perRun, params int[] targets)
        {
            Kind = kind;
            PerRun = perRun;
            Targets = targets;
        }
    }
}
