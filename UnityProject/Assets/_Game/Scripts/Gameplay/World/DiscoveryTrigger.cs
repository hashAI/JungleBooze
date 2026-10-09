using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A discovery trigger (spec 103 §7.3), chunk-local: fires when the runner crosses <see cref="S"/> alive with
    /// x in [XMin, XMax]. <see cref="Placeholder"/> marks stand-ins for Part B mechanics (the sailback is discovered
    /// by observing a creature, not by a line; the Sunken Arch needs the deep dive).
    /// </summary>
    [Serializable]
    public struct DiscoveryTrigger
    {
        public string EntryId;
        public float S;
        public float XMin;
        public float XMax;
        public bool Placeholder;

        /// <summary>Only active when the player owns this ability (e.g. the Sunken Arch needs Deep Breath).</summary>
        public AbilityFlags RequiredAbility;

        public string Note;
    }
}
