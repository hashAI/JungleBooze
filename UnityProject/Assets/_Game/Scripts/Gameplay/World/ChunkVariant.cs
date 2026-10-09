using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Course;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// One layout of a chunk (spec 102 §2: obstacle variants; spec 103 §3 slice variants), all chunk-local
    /// (s = 0 at the entry seam). Widths and floors reuse the feel-course types: the default floor is flat at y 0,
    /// later floor patches win. A variant marked <see cref="ScriptOnly"/> is only used by the Expedition 1 script.
    /// </summary>
    [Serializable]
    public sealed class ChunkVariant
    {
        public string Name = "Default";
        public bool ScriptOnly;

        /// <summary>Overrides the chunk length (e.g. the 60 m <c>Short</c> start), m; 0 = the chunk's length.</summary>
        public float Length;

        /// <summary>
        /// Gray-box stand-in for a Part B traversal set piece (swim, vine, canopy): passable on foot, flagged in the
        /// scene and in reports.
        /// </summary>
        public bool Placeholder;

        public string PlaceholderNote = string.Empty;

        public List<CourseWidthKey> Widths = new List<CourseWidthKey>();
        public List<CourseFloorPatch> Floors = new List<CourseFloorPatch>();
        public List<ChunkDivider> Dividers = new List<ChunkDivider>();
        public List<ChunkRoute> Routes = new List<ChunkRoute>();
        public List<CourseObstacle> Obstacles = new List<CourseObstacle>();
        public List<CoinPattern> Coins = new List<CoinPattern>();
        public List<CrystalAnchor> Crystals = new List<CrystalAnchor>();
        public List<PowerUpSlot> PowerUps = new List<PowerUpSlot>();
        public List<DiscoveryTrigger> Discoveries = new List<DiscoveryTrigger>();
        public List<TraversalZone> Traversal = new List<TraversalZone>();
        public List<HelpMarker> Help = new List<HelpMarker>();
    }
}
