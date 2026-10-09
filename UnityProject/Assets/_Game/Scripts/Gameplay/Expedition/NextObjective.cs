using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>The single highlighted objective on the results screen (GDD §17).</summary>
    public sealed class NextObjective
    {
        public ObjectiveKind Kind;
        public string Title = string.Empty;
        public string Detail = string.Empty;
        public int Progress;
        public int Target;

        /// <summary>Ability the card opens (AbilityReady / AbilityProgress).</summary>
        public AbilityFlags Ability;

        /// <summary>Discovery entry index (UnseenEntry / Secrets), or −1.</summary>
        public int Entry = -1;
    }
}
